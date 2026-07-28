/*
 * ============================================================
 *  PlayerCombat  -  鐜╁鎴樻枟绯荤粺
 * ============================================================
 *
 * 銆愬姛鑳姐€?
 *   绠＄悊鐜╁鐨勬垬鏂楅€昏緫锛氶€夋嫨鐩爣銆佹敾鍑汇€佸彂灏勬姇灏勭墿銆?
 *   杩藉嚮/鑴辩鍒ゅ畾銆備笌 PlayerMove 鍜?PlayerHealth 閰嶅悎浣跨敤銆?
 *
 * 銆愭寕杞藉璞°€?
 *   鐜╁瀵硅薄锛堜笌 PlayerMove 鍦ㄥ悓涓€瀵硅薄锛?
 *
 * 銆愬彲璋冭妭鍙傛暟銆?
 *   attackCooldown         - 鏀诲嚮鍐峰嵈鏃堕棿锛堢锛?
 *   attackLockTime         - 鏀诲嚮閿佸畾鏃堕棿锛堟湡闂翠笉鑳界Щ鍔級
 *   chaseMaxDistance       - 鏈€澶ц拷鍑昏窛绂伙紙瓒呭嚭鍒欐斁寮冪洰鏍囷級
 *   enemyLayer             - 鏁屼汉鎵€鍦ㄧ殑灞傜骇
 *   attackProjectilePrefab - 鏀诲嚮鎶曞皠鐗╅鍒朵綋
 *   projectileSpawnPoint   - 鎶曞皠鐗╃敓鎴愪綅缃?
 *   projectileSpeed        - 鎶曞皠鐗╅琛岄€熷害
 *
 * 銆愯鏄庛€?
 *   - 娌℃湁鎶曞皠鐗╅鍒朵綋鏃朵娇鐢ㄥ皠绾挎娴嬬洿鎺ラ€犳垚浼ゅ
 *   - 鏀诲嚮鎬墿锛圖emonMinion锛変細鑷姩娉ㄥ唽浠囨仺
 */
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
    private int attackParameterHash;

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
        attackParameterHash = Animator.StringToHash(attackParameter);
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
        float sqrDist = transform.position.SqrDistanceXZ(target.position);
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

        float sqrDist = transform.position.SqrDistanceXZ(attackTarget.position);

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
        return transform.position.SqrDistanceXZ(target.position) <= sqrAttackRange;
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

        if (animator != null) { animator.ResetTrigger(attackParameterHash); animator.SetTrigger(attackParameterHash); }

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
}

