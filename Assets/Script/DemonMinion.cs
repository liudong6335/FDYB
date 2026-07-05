using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Health))]
public class DemonMinion : MonoBehaviour, IHealthProvider, IDamageable
{
    [Header("Stats Per Level")]
    [SerializeField] private float[] levelHealth = { 400f, 500f, 600f };
    [SerializeField] private float[] levelDamage = { 25f, 40f, 80f };
    [SerializeField] private float[] levelRegen = { 3f, 5f, 8f };
    [SerializeField] private float[] levelRegenDelay = { 3f, 3f, 3f };
    [SerializeField] private bool[] levelRegenCombatDisable = { true, true, true };

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float attackRange = 5f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float attackLockTime = 0.6f;
    private float attackLockTimer;

    [Header("Behavior")]
    [SerializeField] private float aggroRange = 10f;
    [SerializeField] private float disengageDistance = 10f;
    [SerializeField] private float altarDeathDelay = 30f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string attackParam = "Attack";
    [SerializeField] private string deathParam = "Death";
    [SerializeField] private string hitParam = "Hit";
    [Header("References")]
    [SerializeField] private UGUIFloatingHealthBar healthBar;

    private Health health;
    private int currentLevel = 1;
    private float currentDamage;
    private NPCGoddess targetNPC;
    private Transform currentTarget;
    private float nextAttackTime;
    private bool isDead;
    private bool isRespawning;
    private bool isMovingInFrame;
    private bool initialized;
    private CharacterController cc;
    private Renderer[] cachedRenderers;
    private Transform attackerTransform;

    private PlayerMove[] cachedPlayers;
    private float playerCacheTimer;
    private static readonly float PlayerCacheInterval = 1f;

    // Squared distances for cheap comparisons
    private float sqrAttackRange;
    private float sqrAggroRange;
    private float sqrDisengageDistance;

    private static int aliveCount;
    public static int AliveCount { get { return aliveCount; } }
    public static int MaxCount = 4;

    public int Level { get { return currentLevel; } }
    public float CurrentHealth { get { return health != null ? health.CurrentHealth : 0f; } }
    public float HealthPercent { get { return health != null ? health.HealthPercent : 0f; } }
    public float MaxHealth { get { return health != null ? health.MaxHealth : 0f; } }
    public bool IsDead { get { return isDead; } }
    public Vector3 SpawnPosition { get; private set; }

    public System.Action<DemonMinion> OnDeath;
    public event System.Action<float> OnHealthChanged;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (health == null) health = gameObject.AddComponent<Health>();

        sqrAttackRange = attackRange * attackRange;
        sqrAggroRange = aggroRange * aggroRange;
        sqrDisengageDistance = disengageDistance * disengageDistance;
        cc = GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = gameObject.AddComponent<CharacterController>();
            cc.radius = 0.35f; cc.height = 1.8f; cc.center = new Vector3(0, 0.9f, 0);
        }
        var capsule = GetComponent<CapsuleCollider>(); if (capsule != null) capsule.isTrigger = true;
        cachedRenderers = GetComponentsInChildren<Renderer>();
        if (healthBar == null) healthBar = GetComponentInChildren<UGUIFloatingHealthBar>();
        if (healthBar != null) healthBar.SetProvider(this);
    }

    public void Initialize(int level, NPCGoddess npc)
    {
        currentLevel = Mathf.Clamp(level, 1, levelHealth.Length);
        currentDamage = levelDamage[currentLevel - 1];
        targetNPC = npc;
        isDead = false;
        if (!initialized) SpawnPosition = transform.position;

        if (health == null) health = GetComponent<Health>();
        health.SetMaxHealth(levelHealth[currentLevel - 1]);
        health.ResetToFull();
        health.RegenPerSecond = levelRegen[currentLevel - 1];
        health.RegenDelayAfterDamage = levelRegenDelay[currentLevel - 1];
        health.DisableRegenInCombat = levelRegenCombatDisable[currentLevel - 1];
        health.EnableRegen = true;

        // Prevent duplicate listener on re-initialize
        health.onDeath.RemoveListener(OnHealthDepleted);
        health.onDeath.AddListener(OnHealthDepleted);
        health.onHealthChanged.RemoveListener(OnHealthChangedNotify);
        health.onHealthChanged.AddListener(OnHealthChangedNotify);

        if (animator == null) { animator = GetComponent<Animator>(); if (animator != null) animator.enabled = true; }
        // Full animator reset on revive é”?prevents invisible model after death
        if (animator != null)
        {
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);
        }
        if (healthBar == null) healthBar = GetComponentInChildren<UGUIFloatingHealthBar>();
        if (healthBar != null) healthBar.SetProvider(this);

        float scale = 1f + (currentLevel - 1) * 0.15f;
        transform.localScale = Vector3.one * scale;

        if (!initialized)
        {
            initialized = true;
            aliveCount++;
        }

        // Reset caches
    }

    private void Start()
    {
        if (!initialized && !isDead)
        {
            if (targetNPC == null)
                targetNPC = FindFirstObjectByType<NPCGoddess>();
            if (targetNPC != null)
                Initialize(currentLevel, targetNPC);
        }
    }

    private void OnDestroy()
    {
        if (!isDead && initialized) aliveCount--;
    }

    private void Update()
    {
        if (attackLockTimer > 0f) attackLockTimer -= Time.deltaTime;
        if (isDead)
        {
            UpdateAnimation();
            return;
        }
        if (targetNPC == null || targetNPC.IsDead || targetNPC.HasArrived) return;

        // Periodic player cache refresh
        playerCacheTimer -= Time.deltaTime;
        if (playerCacheTimer <= 0f)
        {
            cachedPlayers = FindObjectsByType<PlayerMove>(FindObjectsSortMode.None);
            playerCacheTimer = PlayerCacheInterval;
        }

        DetermineTarget();
        if (currentTarget != null)
        {
            float sqrDist = SqrDistanceTo(currentTarget.position);
            if (sqrDist <= sqrAttackRange)
            {
                LookAt(currentTarget);
                if (Time.time >= nextAttackTime) Attack();
            }
            else
            {
                if (attackLockTimer <= 0f) MoveToward(currentTarget.position);
            }
        }
        else
        {
            float sqrDistToNPC = SqrDistanceTo(targetNPC.transform.position);
            if (sqrDistToNPC > sqrAttackRange)
                if (attackLockTimer <= 0f) MoveToward(targetNPC.transform.position);
        }

        UpdateAnimation();
    }

    private void DetermineTarget()
    {
        if (attackerTransform != null)
        {
            var attackerDamageable = attackerTransform.GetComponent<IDamageable>();
            if (attackerDamageable != null && attackerDamageable.IsDead)
                ClearAttackerInternal();
            else if (SqrDistanceTo(attackerTransform.position) > sqrDisengageDistance)
                ClearAttackerInternal();
            else
            {
                SetTarget(attackerTransform);
                return;
            }
        }

        if (targetNPC != null)
        {
            float sqrNpcDist = SqrDistanceTo(targetNPC.transform.position);
            if (sqrNpcDist > sqrDisengageDistance && attackerTransform == null)
            {
                PlayerMove nearest = null;
                float nearestSqrDist = float.MaxValue;

                if (cachedPlayers != null)
                {
                    foreach (var p in cachedPlayers)
                    {
                        if (p == null || p.CurrentHealth <= 0f) continue;
                        float d = SqrDistanceTo(p.transform.position);
                        if (d < nearestSqrDist && d <= sqrAggroRange)
                        {
                            nearestSqrDist = d;
                            nearest = p;
                        }
                    }
                }

                if (nearest != null)
                {
                    SetTarget(nearest.transform);
                    return;
                }
            }
        }

        if (targetNPC != null)
            SetTarget(targetNPC.transform);
    }

    private void SetTarget(Transform newTarget)
    {
        if (currentTarget == newTarget) return;
        currentTarget = newTarget;
    }

    private void ClearAttackerInternal()
    {
        attackerTransform = null;
        currentTarget = null;
    }

    private void MoveToward(Vector3 targetPos)
    {
        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0f;
        MovementUtility.FaceDirection(transform, toTarget, rotationSpeed, Time.deltaTime);
        Vector3 newPos = Vector3.MoveTowards(transform.position,
            new Vector3(targetPos.x, transform.position.y, targetPos.z), moveSpeed * Time.deltaTime);
        Vector3 dd = newPos - transform.position;
        dd.y = cc.isGrounded ? -0.1f : dd.y - 9.81f * Time.deltaTime;
        cc.Move(dd);
    }

    private void LookAt(Transform target)
    {
        if (target == null) return;
        MovementUtility.FaceDirection(transform, target.position - transform.position, rotationSpeed, Time.deltaTime);
    }

    private void Attack()
    {
        nextAttackTime = Time.time + attackCooldown;
        attackLockTimer = attackLockTime;
        if (animator != null) { animator.ResetTrigger(attackParam); animator.SetTrigger(attackParam); }
        if (health != null) health.SetInCombat(true);
        if (currentTarget == null) return;

        float sqrDist = SqrDistanceTo(currentTarget.position);
        float attackThreshold = (attackRange + 0.5f) * (attackRange + 0.5f);
        if (sqrDist <= attackThreshold)
        {
            var target = currentTarget.GetComponent<IDamageable>();
            if (target != null && !target.IsDead)
                target.TakeDamage(currentDamage);
        }
    }

    public void TakeDamage(float dmg)
    {
        if (isDead || health == null) return;
        if (animator != null) { animator.ResetTrigger(hitParam); animator.SetTrigger(hitParam); }
        health.TakeDamage(dmg);
        health.SetInCombat(true);
    }

    private void OnHealthChangedNotify(float pct)
    {
        OnHealthChanged?.Invoke(pct);
    }

    private void OnHealthDepleted()
    {
        if (isDead) return;
        isDead = true;
        aliveCount--;
        // Stay in place with idle animation é”?no Destroy
        if (animator != null)
        {
            animator.ResetTrigger(deathParam); animator.SetTrigger(deathParam);
            animator.SetBool(isMovingParam, false);
        }
        OnDeath?.Invoke(this);
    }

    /// <summary>Revive this demon in place with +1 level.</summary>
    public void Revive(int newLevel, NPCGoddess npc)
    {
        isRespawning = false;
        isDead = false;
        ClearAttackerInternal(); // Reset targeting -- start chasing NPC fresh
        // Re-enable all renderers (Skinned + regular) including inactive children
        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        var allRenderers = new Renderer[renderers.Length + meshRenderers.Length];
        renderers.CopyTo(allRenderers, 0);
        meshRenderers.CopyTo(allRenderers, renderers.Length);
        cachedRenderers = allRenderers;
        foreach (var r in cachedRenderers)
            if (r != null) r.enabled = true;
        // Re-enable health bar
        if (healthBar == null) healthBar = GetComponentInChildren<UGUIFloatingHealthBar>();
        if (healthBar != null) { healthBar.gameObject.SetActive(true); healthBar.SetProvider(this); }
        // Re-enable animator
        if (animator != null)
            animator.enabled = true;
        // Re-enable collider
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
        Initialize(newLevel, npc);
        aliveCount++; // Restore counter (Initialize skips it since already initialized)
    }

    public void RegisterAttacker(Transform attacker) { attackerTransform = attacker; }
    public void ClearAttacker() { attackerTransform = null; }

    /// <summary>
    /// Start a countdown until this minion dies (used when NPC reaches the altar).
    /// </summary>
    public void StartAltarDeathCountdown()
    {
        if (!isDead)
            StartCoroutine(AltarDeathDelay());
    }
 
    private IEnumerator AltarDeathDelay()
    {
        yield return new WaitForSeconds(altarDeathDelay);
        if (!isDead) OnHealthDepleted();
    }

    /// <summary>
    /// Set this demon to dormant (inactive, invisible) at game start.
    /// </summary>
    public void SetDormant()
    {
        isDead = true;
        // Disable all renderers so demon is invisible
        // Refresh all renderers (Skinned + regular) including inactive children
        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        var allRenderers = new Renderer[renderers.Length + meshRenderers.Length];
        
        renderers.CopyTo(allRenderers, 0);
        meshRenderers.CopyTo(allRenderers, renderers.Length);
        cachedRenderers = allRenderers;
        Debug.Log($"[DemonMinion.SetDormant] Found {cachedRenderers.Length} renderers on {gameObject.name}");
        foreach (var r in cachedRenderers)
            if (r != null) r.enabled = false;
        // Disable health bar
        // healthBar hides itself via LateUpdate when renderers are off
        if (healthBar == null) healthBar = GetComponentInChildren<UGUIFloatingHealthBar>();
        if (healthBar != null) healthBar.gameObject.SetActive(false);
        // Stop animation
        if (animator != null)
        {
            animator.SetBool(isMovingParam, false);
            animator.enabled = false;
        }
        // Disable collider so player cannot interact
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }
    /// <summary>
    /// Activate a dormant demon, making it visible and start chasing the NPC.
    /// </summary>
    public void Activate(int level, NPCGoddess npc)
    {
        if (!isDead || isRespawning) return;
        isDead = false;
        aliveCount++;
        // Refresh all renderers (Skinned + regular) including inactive children
        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        var allRenderers = new Renderer[renderers.Length + meshRenderers.Length];
        
        renderers.CopyTo(allRenderers, 0);
        meshRenderers.CopyTo(allRenderers, renderers.Length);
        cachedRenderers = allRenderers;
        Debug.Log($"[DemonMinion.Activate] Found {cachedRenderers.Length} renderers on {gameObject.name}");
        foreach (var r in cachedRenderers)
            if (r != null) r.enabled = true;
        // Re-enable health bar
        if (healthBar == null) healthBar = GetComponentInChildren<UGUIFloatingHealthBar>();
        if (healthBar != null) { healthBar.gameObject.SetActive(true); healthBar.SetProvider(this); }
        // Re-enable animator
        if (animator == null) animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = true;
        // Re-enable collider
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        Initialize(level, npc);
        SpawnPosition = transform.position;
    }

    public bool IsDormant
    {
        get { return isDead && animator != null && !animator.enabled; }
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;
        // Dead demons: stay in idle (no movement animation)
        if (isDead)
        {
            animator.SetBool(isMovingParam, false);
            return;
        }
        // Determine if demon is moving based on distance to current target
        bool moving = false;
        if (currentTarget != null)
        {
            moving = SqrDistanceTo(currentTarget.position) > sqrAttackRange;
        }
        else if (targetNPC != null && !targetNPC.IsDead && !targetNPC.HasArrived)
        {
            moving = SqrDistanceTo(targetNPC.transform.position) > sqrAttackRange;
        }
        animator.SetBool(isMovingParam, moving);
    }

    private float SqrDistanceTo(Vector3 point)
    {
        float dx = transform.position.x - point.x;
        float dz = transform.position.z - point.z;
        return dx * dx + dz * dz;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}

