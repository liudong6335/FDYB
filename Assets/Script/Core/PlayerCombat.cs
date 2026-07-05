using UnityEngine;
using System.Collections;

public class PlayerCombat : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float attackLockTime = 0.45f;
    [SerializeField] private float chaseMaxDistance = 30f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private GameObject attackProjectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private float projectileSpeed = 20f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string attackParameter = "Attack";

    private PlayerHealth playerHealth;
    private Transform attackTarget;
    private float nextAttackTime;
    private float attackLockUntil;

    // Squared distances
    private float sqrAttackRange;
    private float sqrChaseMaxDistance;

    public Transform AttackTarget { get { return attackTarget; } }
    public LayerMask EnemyLayerMask { get { return enemyLayer; } }
    public bool IsAttackLocked { get { return Time.time < attackLockUntil; } }
    public bool CanAttack { get { return Time.time >= nextAttackTime && Time.time >= attackLockUntil; } }
    public System.Action OnAttackTargetChanged;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        if (animator == null) animator = GetComponent<Animator>();
        if (projectileSpawnPoint == null) projectileSpawnPoint = transform;
    }

    private void Start()
    {
        // Default to Enemy layer if not configured in Inspector
        if (enemyLayer.value == 0) enemyLayer = LayerMask.GetMask("Enemy");

        sqrAttackRange = playerHealth.EffectiveAttackRange * playerHealth.EffectiveAttackRange;
        sqrChaseMaxDistance = chaseMaxDistance * chaseMaxDistance;
    }

    /// <summary>Try to engage a new attack target. Returns true if target is in range.</summary>
    public bool TryEngage(Transform target)
    {
        if (target == null) return false;
        SetTarget(target);
        float sqrDist = SqrDistanceTo(target.position);
        return sqrDist <= sqrAttackRange;
    }

    public void ClearTarget()
    {
        attackTarget = null;
        OnAttackTargetChanged?.Invoke();
    }

    /// <summary>Call every frame. Handles chase-distance check and triggers attacks.</summary>
    public void UpdateCombat()
    {
        if (attackTarget == null) return;

        var damageable = attackTarget.GetComponent<IDamageable>();
        if (damageable != null && damageable.IsDead)
        {
            ClearTarget();
            return;
        }

        float sqrDist = SqrDistanceTo(attackTarget.position);

        // Disengage if too far
        if (sqrDist > sqrChaseMaxDistance)
        {
            ClearTarget();
            return;
        }

        // In range: attack on cooldown
        if (sqrDist <= sqrAttackRange && CanAttack)
            TriggerAttack();
    }

    /// <summary>Check if at attack range (used by movement to stop).</summary>
    public bool IsInAttackRange(Transform target)
    {
        if (target == null) return false;
        return SqrDistanceTo(target.position) <= sqrAttackRange;
    }

    private void SetTarget(Transform target)
    {
        if (attackTarget == target) return;
        attackTarget = target;
        OnAttackTargetChanged?.Invoke();
    }

    private void TriggerAttack()
    {
        nextAttackTime = Time.time + attackCooldown;
        attackLockUntil = Time.time + attackLockTime;

        if (animator != null) { animator.ResetTrigger(attackParameter); animator.SetTrigger(attackParameter); }

        if (attackTarget == null) return;

        if (attackProjectilePrefab != null)
        {
            GameObject proj = Instantiate(attackProjectilePrefab, projectileSpawnPoint.position, Quaternion.identity);
            Projectile p = proj.GetComponent<Projectile>();
            if (p != null)
                p.Initialize(attackTarget.position, playerHealth.EffectiveDamage, enemyLayer);
            else
                StartCoroutine(MoveProjectile(proj, attackTarget.position));
        }
        else
        {
            Vector3 dir = (attackTarget.position - projectileSpawnPoint.position).normalized;
            if (Physics.Raycast(projectileSpawnPoint.position, dir, out RaycastHit hit,
                playerHealth.EffectiveAttackRange, enemyLayer))
                ApplyDamage(hit.collider);
        }
    }

    private void ApplyDamage(Collider target)
    {
        // Minion-specific aggro registration
        var minion = target.GetComponent<DemonMinion>();
        if (minion != null)
        {
            minion.TakeDamage(playerHealth.EffectiveDamage);
            minion.RegisterAttacker(transform);
            return;
        }
        // Generic damageable (players, NPC, etc.)
        var damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(playerHealth.EffectiveDamage);
            return;
        }
    }

    private IEnumerator MoveProjectile(GameObject proj, Vector3 targetPos)
    {
        Vector3 dir = (targetPos - proj.transform.position).normalized;
        float range = playerHealth.EffectiveAttackRange;
        float traveled = 0f;
        while (proj != null && traveled < range)
        {
            float step = projectileSpeed * Time.deltaTime;
            proj.transform.position += dir * step;
            traveled += step;
            yield return null;
        }
        if (proj != null) Destroy(proj);
    }

    private float SqrDistanceTo(Vector3 point)
    {
        float dx = transform.position.x - point.x;
        float dz = transform.position.z - point.z;
        return dx * dx + dz * dz;
    }
}
