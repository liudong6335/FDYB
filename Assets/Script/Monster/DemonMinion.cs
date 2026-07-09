/*
 * ============================================================
 *  DemonMinion  -  恶魔小怪
 * ============================================================
 *
 * 【功能】
 *   游戏中的敌人单位。会追踪并攻击玩家和NPC，
 *   可升级、可复活、可激活/休眠。
 *   由 GameManager 控制激活和复活。
 *
 * 【挂载对象】
 *   怪物预制体（在 Hierarchy > Monster 下作为子对象）
 *
 * 【可调节参数】
 *   （等级属性 - 按等级配置数组）
 *   levelHealth[]             - 各等级的血量 [400, 500, 600]
 *   levelDamage[]             - 各等级的伤害 [25, 40, 80]
 *   levelRegen[]              - 各等级的回血 [3, 5, 8]
 *
 *   （移动）
 *   moveSpeed                 - 移动速度
 *   rotationSpeed             - 转向速度
 *   attackRange               - 攻击距离
 *   attackCooldown            - 攻击冷却时间
 *
 *   （行为）
 *   aggroRange                - 警戒范围（进入此范围开始追击）
 *   disengageDistance         - 脱离战斗距离
 *   altarDeathDelay           - NPC到达后怪物延迟死亡的秒数
 *
 *   （动画）
 *   animator                  - 动画控制器
 *   isMovingParam / Attack / Death / Hit - 动画参数名
 *
 * 【说明】
 *   - 初始为休眠状态（不可见不可交互）
 *   - 由 GameManager 按时间间隔激活
 *   - 死亡后会升级复活
 *   - 会优先攻击攻击过自己的玩家
 *   - NPC到达终点后怪物会自动死亡
 */
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Patrol")]
    [SerializeField] private float patrolRadius = 8f;
    [SerializeField] private float patrolWaitTime = 3f;
    private Vector3 patrolTarget;
    private float patrolWaitTimer;
    private bool isWaitingAtPatrol;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string attackParam = "Attack";
    [SerializeField] private string deathParam = "Death";
    [SerializeField] private string hitParam = "Hit";
    [Header("References")]
    [SerializeField] private UGUIFloatingHealthBar healthBar;

    [Header("Behaviour AI")]
    [Tooltip("When true, BehaviourModelBase drives decisions; old state-machine AI is skipped.")]
    [SerializeField] private bool useBehaviourAI = false;

    private Health health;
    private NavMeshAgent navAgent;
    private int currentLevel = 1;
    private float currentDamage;
    private NPCGoddess targetNPC;
    private Transform currentTarget;
    private float nextAttackTime;
    private bool isDead;
    private bool isRespawning;
    private bool isDormant;
    private bool isMovingInFrame;
    private bool initialized;
    private CharacterController cc;
    
    /// <summary>Whether NavMeshAgent is available and on a valid NavMesh.</summary>
    public bool UseNavMesh { get { return navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh; } }

    private void SetupNavMeshAgent()
    {
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.speed = moveSpeed;
            navAgent.angularSpeed = rotationSpeed * 100f;
            navAgent.stoppingDistance = attackRange * 0.5f;
            navAgent.radius = 0.35f;
            navAgent.height = 1.8f;
        }
    }

    private Renderer[] cachedRenderers;
    private Transform attackerTransform;





    // Squared distances for cheap comparisons
    private float sqrAttackRange;
    private float sqrAggroRange;
    private float sqrDisengageDistance;

    private static int aliveCount;
    private static readonly List<DemonMinion> allDemons = new List<DemonMinion>();
    public static IReadOnlyList<DemonMinion> AllDemons => allDemons;
    public static int AliveCount { get { return aliveCount; } }
    public static int MaxCount = 4;

    public int Level { get { return currentLevel; } }
    public float CurrentHealth { get { return health != null ? health.CurrentHealth : 0f; } }
    public float HealthPercent { get { return health != null ? health.HealthPercent : 0f; } }
    public float MaxHealth { get { return health != null ? health.MaxHealth : 0f; } }
    public float AttackRange { get { return attackRange; } }
    public float SqAttackRange { get { return sqrAttackRange; } }
    public float SqAggroRange { get { return sqrAggroRange; } }
    public float SqDisengageDistance { get { return sqrDisengageDistance; } }
    public float MoveSpeed { get { return moveSpeed; } }
    public Transform AttackerTransform { get { return attackerTransform; } }
    public void SetBehaviourAI(bool enabled) { useBehaviourAI = enabled; }
    public bool IsBehaviourAIEnabled { get { return useBehaviourAI; } }
    /// <summary>Chase target set by Behaviour tick, consumed per-frame in Update().</summary>
    private Transform aiChaseTarget;
    public void SetChaseTarget(Transform t) { aiChaseTarget = t; }
    public void ClearChaseTarget() { aiChaseTarget = null; }
    public bool IsDead { get { return isDead; } }
    public Vector3 SpawnPosition { get; private set; }

    public System.Action<DemonMinion> OnDeath;
    public event System.Action<float> OnHealthChanged;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (health == null) health = gameObject.AddComponent<Health>();
        SetupNavMeshAgent();
        allDemons.Add(this);

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
        patrolTarget = transform.position;
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
        // Full animator reset on revive 閿?prevents invisible model after death
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
        allDemons.Remove(this);
    }


    private void Update()
    {
        if (attackLockTimer > 0f) attackLockTimer -= Time.deltaTime;
        if (isDead)
        {
            UpdateAnimation();
            return;
        }
        // Old state-machine AI runs only when behaviour AI is off
        if (!useBehaviourAI)
        {
        if (targetNPC == null || targetNPC.IsDead) return;

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
            if (targetNPC != null && targetNPC.HasArrived)
            {
                UpdatePatrol();
            }
            else
            {
                float sqrDistToNPC = SqrDistanceTo(targetNPC.transform.position);
                if (sqrDistToNPC > sqrAttackRange)
                    if (attackLockTimer <= 0f) MoveToward(targetNPC.transform.position);
            }
        }
        }

        // New AI: per-frame chase when target is set by Behaviour model
        if (useBehaviourAI)
        {
            if (aiChaseTarget != null)
            {
                currentTarget = aiChaseTarget;
            
            if (UseNavMesh)
            {
                float sqrDist = SqrDistanceTo(aiChaseTarget.position);
                if (sqrDist <= sqrAttackRange)
                {
                    navAgent.ResetPath();
                    LookAt(aiChaseTarget);
                    if (Time.time >= nextAttackTime) Attack();
                }
                else
                {
                    navAgent.SetDestination(aiChaseTarget.position);
                    MovementUtility.FaceDirection(transform, aiChaseTarget.position - transform.position, rotationSpeed, Time.deltaTime);
                }
            }
            else
            {
            float sqrDist = SqrDistanceTo(aiChaseTarget.position);
            if (sqrDist <= sqrAttackRange)
            {
                LookAt(aiChaseTarget);
                if (Time.time >= nextAttackTime) Attack();
            }
            else
            {
                if (attackLockTimer <= 0f) MoveToward(aiChaseTarget.position);
            }
            }
            }
            else
            {
                currentTarget = null;
            }
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


                {
                    foreach (var p in PlayerMove.AllPlayers)
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

            if (targetNPC != null && !targetNPC.HasArrived)
                SetTarget(targetNPC.transform);
            else if (targetNPC != null && currentTarget == targetNPC.transform)
                currentTarget = null;
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
        if (UseNavMesh)
        {
            navAgent.SetDestination(targetPos);
            MovementUtility.FaceDirection(transform, targetPos - transform.position, rotationSpeed, Time.deltaTime);
            return;
        }
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

    public void Heal(float amount) { if (health != null) health.Heal(amount); }

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

        // Play death animation
        if (animator != null)
        {
            animator.ResetTrigger(deathParam); animator.SetTrigger(deathParam);
            animator.SetBool(isMovingParam, false);
        }
        OnDeath?.Invoke(this);

        // Hide after 1 second (allow death animation to play), keep alive for revival
        if (!useBehaviourAI) StartCoroutine(HideDeadBody());





    }

    private System.Collections.IEnumerator HideDeadBody()
    {
        yield return new WaitForSeconds(1f);
        if (!isDead) yield break; // revived during wait, skip hiding

        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in renderers) if (r != null) r.enabled = false;
        foreach (var r in meshRenderers) if (r != null) r.enabled = false;

        if (healthBar == null) healthBar = GetComponentInChildren<UGUIFloatingHealthBar>();
        if (healthBar != null) healthBar.gameObject.SetActive(false);

        if (animator != null) animator.enabled = false;

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
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

    // --- Public action wrappers for Behaviour AI ---
    public void PerformChase(Transform target)
    {
        // Just sets the target — actual movement happens in Update() every frame
        SetChaseTarget(target);
    }
    public void PerformPatrol() { UpdatePatrol(); }
    public void PerformIdle() { ClearChaseTarget(); }

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
        if (!isDead)
        {
            OnHealthDepleted();
            Destroy(gameObject, 1f); // altar death always destroys
        }
    }

    /// <summary>
    /// Set this demon to dormant (inactive, invisible) at game start.
    /// </summary>
    public void SetDormant()
    {
        isDead = true;
        isDormant = true;
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
        isDormant = false;
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
        get { return isDormant; }
    }

    private void UpdatePatrol()
    {
        if (isWaitingAtPatrol)
        {
            patrolWaitTimer -= Time.deltaTime;
            if (patrolWaitTimer <= 0f)
            {
                isWaitingAtPatrol = false;
                Vector2 rand = Random.insideUnitCircle * patrolRadius;
                    patrolTarget = transform.position + new Vector3(rand.x, 0f, rand.y);
            }
            return;
        }

        float dist = Vector3.Distance(transform.position, patrolTarget);
        if (dist <= 1f)
        {
            isWaitingAtPatrol = true;
            patrolWaitTimer = patrolWaitTime + Random.Range(0f, 2f);
        }
        else
        {
            MoveToward(patrolTarget);
        }
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









