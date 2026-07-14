using UnityEngine;
using UnityEngine.AI;
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

    [Header("Behavior")]
    [SerializeField] private float aggroRange = 10f;
    [SerializeField] private float disengageDistance = 10f;

    [Header("Target Tracking")]
    [SerializeField] private float targetUpdateInterval = 2f;
    [SerializeField] private float exactTrackingRange = 15f;

    [Header("Patrol")]
    [SerializeField] private float patrolRadius = 8f;
    [SerializeField] private float patrolWaitTime = 3f;

        [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string attackParam = "Attack";
    [SerializeField] private string deathParam = "Death";
    [SerializeField] private string hitParam = "Hit";
    private int isMovingHash;
    private int attackHash;
    private int deathHash;
    private int hitHash;

    [Header("References")]
    [SerializeField] private UGUIFloatingHealthBar healthBar;

    // Core components
    private Health health;
    private NavMeshAgent navAgent;
    private CharacterController cc;
    private DamageSlowEffect slowEffect;
    private NPCGoddess targetNPC;
    private DemonMinionBrain brain;
    private DemonMinionLifecycle lifecycle;

    // Level / combat state
    private int currentLevel = 1;
    private float currentDamage;
    private Transform currentTarget;
    private float nextAttackTime;
    private float attackLockTimer;

    // Target selection state
    private Transform attackerTransform;
    private float sqrAttackRange;
    private float sqrAggroRange;
    private float sqrDisengageDistance;
    private float targetUpdateTimer;
    private Vector3 lastTargetPosition;

    // Patrol state
    private Vector3 patrolTarget;
    private float patrolWaitTimer;
    private bool isWaitingAtPatrol;

    // Lifecycle state
    private bool isDead;
    private bool initialized;
    private Vector3 spawnPosition;

    // Static registry
    private static readonly List<DemonMinion> allDemons = new List<DemonMinion>();
    private static int aliveCount;
    public static IReadOnlyList<DemonMinion> AllDemons => allDemons;
    public static int AliveCount { get { return aliveCount; } }
    public static int MaxCount = 4;

    // ============================================
    // Public Properties
    // ============================================
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
    public bool IsDead { get { return isDead; } }
    public Vector3 SpawnPosition { get { return spawnPosition; } set { spawnPosition = value; } }
    public NPCGoddess TargetNPC { get { return targetNPC; } }
    public bool UseNavMesh { get { return navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh; } }
    public bool IsDormant { get { return lifecycle != null && lifecycle.IsDormant; } }

    public System.Action<DemonMinion> OnDeath;
    public event System.Action<float> OnHealthChanged;

    // ============================================
    // Unity Lifecycle
    // ============================================

    private void Awake()
    {
        health = GetComponent<Health>();
        if (health == null) health = gameObject.AddComponent<Health>();

        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.speed = moveSpeed;
            navAgent.angularSpeed = rotationSpeed * 100f;
            navAgent.stoppingDistance = attackRange * 0.5f;
            navAgent.radius = 0.35f;
            navAgent.height = 1.8f;
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
        }

        cc = GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = gameObject.AddComponent<CharacterController>();
            cc.radius = 0.35f; cc.height = 1.8f; cc.center = new Vector3(0, 0.9f, 0);
        }
        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null) capsule.isTrigger = true;

        slowEffect = GetComponent<DamageSlowEffect>();

        sqrAttackRange = attackRange * attackRange;
        sqrAggroRange = aggroRange * aggroRange;
        sqrDisengageDistance = disengageDistance * disengageDistance;

        if (healthBar == null) healthBar = GetComponentInChildren<UGUIFloatingHealthBar>();
        if (healthBar != null) healthBar.SetProvider(this);

        patrolTarget = transform.position;
        isMovingHash = Animator.StringToHash(isMovingParam);
        attackHash = Animator.StringToHash(attackParam);
        deathHash = Animator.StringToHash(deathParam);
        hitHash = Animator.StringToHash(hitParam);

        allDemons.Add(this);
        brain = GetComponent<DemonMinionBrain>();
        lifecycle = GetComponent<DemonMinionLifecycle>();
    }

    private void OnValidate()
    {
        sqrAttackRange = attackRange * attackRange;
        sqrAggroRange = aggroRange * aggroRange;
        sqrDisengageDistance = disengageDistance * disengageDistance;
    }

    public void Initialize(int level, NPCGoddess npc)
    {
        currentLevel = Mathf.Clamp(level, 1, levelHealth.Length);
        currentDamage = levelDamage[currentLevel - 1];
        targetNPC = npc;
        isDead = false;

        if (health == null) health = GetComponent<Health>();
        health.SetMaxHealth(levelHealth[currentLevel - 1]);
        health.ResetToFull();
        health.RegenPerSecond = levelRegen[currentLevel - 1];
        health.RegenDelayAfterDamage = levelRegenDelay[currentLevel - 1];
        health.DisableRegenInCombat = levelRegenCombatDisable[currentLevel - 1];
        health.EnableRegen = true;

        health.onDeath.RemoveListener(OnHealthDepleted);
        health.onDeath.AddListener(OnHealthDepleted);
        health.onHealthChanged.RemoveListener(OnHealthChangedNotify);
        health.onHealthChanged.AddListener(OnHealthChangedNotify);

        if (animator == null) { animator = GetComponent<Animator>(); if (animator != null) animator.enabled = true; }
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
    }

    private void Start()
    {
        if (!initialized && !isDead)
        {
            if (targetNPC == null)
                targetNPC = FindFirstObjectByType<NPCGoddess>();
            if (targetNPC != null)
            {
                Initialize(currentLevel, targetNPC);
                MarkFirstInitialized();
            }
        }
    }

    private void MarkFirstInitialized()
    {
        if (!initialized)
        {
            initialized = true;
            aliveCount++;
            spawnPosition = transform.position;
        }
    }

    private void OnDestroy()
    {
        if (!isDead && initialized) aliveCount--;
        allDemons.Remove(this);
    }

    // ============================================
    // AliveCount management (called by Lifecycle)
    // ============================================

    public void NotifyDead()
    {
        if (!isDead) { isDead = true; aliveCount--; }
    }

    public void NotifyAlive()
    {
        if (isDead) { isDead = false; aliveCount++; }
        attackerTransform = null;
        currentTarget = null;
    }
    // ============================================
    // Lifecycle forwarding (for external callers like GameManager)
    // ============================================

    public void SetDormant() { if (lifecycle != null) lifecycle.SetDormant(); }
    public void Activate(int level, NPCGoddess npc) { if (lifecycle != null) lifecycle.Activate(level, npc); }
    public void Revive(int newLevel, NPCGoddess npc) { if (lifecycle != null) lifecycle.Revive(newLevel, npc); }

    // ============================================
    // Brain API �� called by DemonMinionBrain
    // ============================================

    public void SetTarget(Transform t) { currentTarget = t; }
    public void ClearTarget() { currentTarget = null; }

    // ============================================
    // Per-Frame Update
    // ============================================

    private void Update()
    {
        if (attackLockTimer > 0f) attackLockTimer -= Time.deltaTime;
        if (targetUpdateTimer > 0f) targetUpdateTimer -= Time.deltaTime;

        if (isDead)
        {
            UpdateAnimation();
            return;
        }

        if (targetNPC == null || targetNPC.IsDead) return;

        // Brain decides target
        if (brain != null) brain.DetermineTarget();

        // Execute movement / combat
        if (currentTarget != null)
        {
            float sqrDist = transform.position.SqrDistanceXZ(currentTarget.position);
            if (sqrDist <= sqrAttackRange)
            {
                LookAt(currentTarget);
                if (UseNavMesh) navAgent.isStopped = true;
                if (Time.time >= nextAttackTime) Attack();
            }
            else
            {
                if (attackLockTimer <= 0f) MoveToward(GetTargetMovePosition(currentTarget));
            }
        }
        else
        {
            if (targetNPC.HasArrived)
            {
                UpdatePatrol();
            }
            else
            {
                float sqrDistToNPC = transform.position.SqrDistanceXZ(targetNPC.transform.position);
                if (sqrDistToNPC > sqrAttackRange)
                    if (attackLockTimer <= 0f) MoveToward(GetTargetMovePosition(targetNPC.transform));
            }
        }

        UpdateAnimation();
    }

    // ============================================
    // Target selection helpers
    // ============================================

    public void ClearAttacker() { attackerTransform = null; }
    public void RegisterAttacker(Transform attacker) { attackerTransform = attacker; }

    private void ClearAttackerInternal()
    {
        attackerTransform = null;
        currentTarget = null;
    }

    private Vector3 GetTargetMovePosition(Transform target)
    {
        if (target == null) return transform.position;
        float sqrDist = transform.position.SqrDistanceXZ(target.position);
        float sqrExact = exactTrackingRange * exactTrackingRange;
        if (sqrDist <= sqrExact)
        {
            targetUpdateTimer = 0f;
            lastTargetPosition = target.position;
        }
        else if (targetUpdateTimer <= 0f)
        {
            lastTargetPosition = target.position;
            targetUpdateTimer = targetUpdateInterval;
        }
        return lastTargetPosition;
    }

    // ============================================
    // Movement
    // ============================================

    private void MoveToward(Vector3 targetPos)
    {
        float slowMul = slowEffect != null ? slowEffect.SpeedMultiplier : 1f;
        if (UseNavMesh)
        {
            navAgent.speed = moveSpeed * slowMul;
            navAgent.isStopped = false;
            navAgent.SetDestination(targetPos);

            Vector3 desiredVel = navAgent.pathPending ? Vector3.zero : navAgent.desiredVelocity;
            Vector3 moveDelta = desiredVel * Time.deltaTime;
            moveDelta.y = cc.isGrounded ? -0.1f : moveDelta.y - 9.81f * Time.deltaTime;
            cc.Move(moveDelta);

            navAgent.nextPosition = transform.position;

            MovementUtility.FaceDirection(transform, targetPos - transform.position, rotationSpeed, Time.deltaTime);
            return;
        }
        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0f;
        MovementUtility.FaceDirection(transform, toTarget, rotationSpeed, Time.deltaTime);
        Vector3 newPos = Vector3.MoveTowards(transform.position,
            new Vector3(targetPos.x, transform.position.y, targetPos.z), moveSpeed * slowMul * Time.deltaTime);
        Vector3 dd = newPos - transform.position;
        dd.y = cc.isGrounded ? -0.1f : dd.y - 9.81f * Time.deltaTime;
        cc.Move(dd);
    }

    private void LookAt(Transform target)
    {
        if (target == null) return;
        MovementUtility.FaceDirection(transform, target.position - transform.position, rotationSpeed, Time.deltaTime);
    }

    // ============================================
    // Combat
    // ============================================

    private void Attack()
    {
        nextAttackTime = Time.time + attackCooldown;
        attackLockTimer = attackLockTime;
        if (animator != null) { animator.ResetTrigger(attackHash); animator.SetTrigger(attackHash); }
        if (health != null) health.SetInCombat(true);
        if (currentTarget == null) return;

        float sqrDist = transform.position.SqrDistanceXZ(currentTarget.position);
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
        if (animator != null) { animator.ResetTrigger(hitHash); animator.SetTrigger(hitHash); }
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
        NotifyDead();

        if (animator != null)
        {
            animator.ResetTrigger(deathHash); animator.SetTrigger(deathHash);
            animator.SetBool(isMovingHash, false);
        }
        OnDeath?.Invoke(this);

        if (lifecycle != null) lifecycle.StartHideDeadBody();
    }

    // ============================================
    // Patrol
    // ============================================

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

    // ============================================
    // Animation
    // ============================================

    private void UpdateAnimation()
    {
        if (animator == null) return;
        if (isDead)
        {
            animator.SetBool(isMovingHash, false);
            return;
        }
        bool moving = false;
        if (currentTarget != null)
        {
            moving = transform.position.SqrDistanceXZ(currentTarget.position) > sqrAttackRange;
        }
        else if (targetNPC != null && !targetNPC.IsDead && !targetNPC.HasArrived)
        {
            moving = transform.position.SqrDistanceXZ(targetNPC.transform.position) > sqrAttackRange;
        }
        animator.SetBool(isMovingHash, moving);
    }

    // ============================================
    // Gizmos
    // ============================================

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
