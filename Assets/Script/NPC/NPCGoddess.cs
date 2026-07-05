/*
 * ============================================================
 *  NPCGoddess  -  NPC女神（被护送目标）
 * ============================================================
 *
 * 【功能】
 *   玩家需要护送的女神NPC。会沿着路径（WaypointPath）行走，
 *   途中可以给玩家和自己加血，会躲避障碍物。
 *   到达终点后触发事件（怪物死亡倒计时）。
 *
 * 【挂载对象】
 *   场景中的 NPC 女神对象（带有 Animator）
 *
 * 【可调节参数】
 *   （属性）
 *   maxHealth                - 最大血量
 *   healthRegen              - 每秒回血
 *
 *   （移动）
 *   waypointPath             - 行走路径（拖入 WaypointPath 对象）
 *   rotationSpeed            - 转向速度
 *   minSpeed / maxSpeed       - 最小/最大行走速度
 *   speedSmoothTime          - 速度变化平滑时间
 *
 *   （路径随机浮动）
 *   waypointFloatMin/Max     - 到达路径点时的随机偏移范围
 *
 *   （暂停行为）
 *   pausesPerMinute          - 每分钟平均停顿次数
 *   pauseDurationMin/Max      - 每次停顿的时长范围
 *
 *   （总耗时）
 *   targetTimeMin/Max         - 预计到达终点的耗时范围（秒）
 *
 *   （障碍物躲避）
 *   obstacleCheckDistance    - 前方检测距离
 *   obstacleCheckRadius      - 检测半径
 *   obstacleAvoidStrength    - 躲避强度
 *
 *   （治疗技能）
 *   healAmount               - 每次治疗量（自己+附近玩家）
 *   healCooldown             - 治疗冷却时间
 *   healCastTime             - 施法时间
 *   healRange                - 治疗范围
 *   selfHealThreshold        - 自己血量低于此值开始治疗
 *   playerHealThreshold      - 玩家血量低于此值治疗玩家
 *
 *   （受伤减速）
 *   damageSlowMultiplier     - 受伤后移动速度倍率
 *   damageSlowDuration       - 减速持续时间
 *
 *   （卡住检测）
 *   stuckCheckInterval       - 检测间隔
 *   stuckThreshold           - 卡住判定阈值
 *
 * 【说明】
 *   - 用 CharacterController 移动
 *   - 自动在路径点之间行走，有随机速度和停顿
 *   - 血量低时自动给自己和附近玩家加血
 *   - 到达终点后触发 OnArrived 事件
 */
using UnityEngine;

[RequireComponent(typeof(Health))]
public class NPCGoddess : MonoBehaviour, IHealthProvider, IDamageable
{
    #region Serialized Fields
    [Header("Stats")]
    [SerializeField] private float maxHealth = 600f;
    [SerializeField] private float healthRegen = 1f;

    [Header("Movement")]
    [SerializeField] private WaypointPath waypointPath;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Speed (min / max)")]
    [SerializeField] private float minSpeed = 1f;
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float speedChangeIntervalMin = 4f;
    [SerializeField] private float speedChangeIntervalMax = 8f;
    [SerializeField] private float speedSmoothTime = 0.8f;

    [Header("Waypoint Float")]
    [SerializeField] private float waypointFloatMin = 0f;
    [SerializeField] private float waypointFloatMax = 5f;

    [Header("Pauses")]
    [SerializeField] private float pausesPerMinute = 2.5f;
    [SerializeField] private float pauseDurationMin = 2f;
    [SerializeField] private float pauseDurationMax = 4f;
    [SerializeField] private float minDistBetweenPauses = 15f;

    [Header("Journey Time Target (seconds)")]
    [SerializeField] private float targetTimeMin = 180f;
    [SerializeField] private float targetTimeMax = 210f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private float obstacleCheckDistance = 3f;
    [SerializeField] private float obstacleCheckRadius = 0.5f;
    [SerializeField] private float obstacleAvoidStrength = 2f;
    [SerializeField] private LayerMask obstacleLayers = -1;

    [Header("Heal Skill")]
    [SerializeField] private float healAmount = 200f;
    [SerializeField] private float healCooldown = 15f;
    [SerializeField] private float healCastTime = 3f;
    [SerializeField] private float healRange = 5f;
    [SerializeField] private float selfHealThreshold = 400f;
    [SerializeField] private float playerHealThreshold = 600f;

    [Header("Damage")]
    [SerializeField] private float damageSlowMultiplier = 0.4f;
    [SerializeField] private float damageSlowDuration = 2f;

    [Header("Stuck Detection")]
    [SerializeField] private float stuckCheckInterval = 3f;
    [SerializeField] private float stuckThreshold = 0.3f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string moveSpeedParam = "MoveSpeed";
    [SerializeField] private string healParam = "Heal";
    [SerializeField] private string deathParam = "Death";
    #endregion

    #region Private State
    private Health health;
    private int currentWaypointIndex;
    private bool hasArrived;
    private bool isWalking;
    private float pauseTimer;
    private float nextHealTime;
    private bool isHealing;
    private float healStartTime;
    private Vector3 healPosition;
    private float damageSlowTimer;
    private Vector3 stuckCheckPosition;
    private float stuckCheckTimer;

    private float targetMoveSpeed;
    private float currentMoveSpeed;
    private float speedSmoothVelocity;
    private float nextSpeedChangeTime;
    private Vector3 currentWaypointFloatTarget;
    private float nextPauseDistanceTraveled;
    private float distanceSinceLastPause;
    private float pauseIntervalDistance;

    private CharacterController cc;
    private PlayerMove[] cachedPlayers;
    private float playerCacheTimer;
    #endregion

    public float CurrentHealth { get { return health != null ? health.CurrentHealth : 0f; } }
    public float MaxHealth { get { return health != null ? health.MaxHealth : maxHealth; } }
    public float HealthPercent { get { return health != null ? health.HealthPercent : 1f; } }
    public bool HasArrived { get { return hasArrived; } }
    public bool IsDead { get { return health != null && health.IsDead; } }
    public bool IsHealing { get { return isHealing; } }
    public bool IsWalking { get { return isWalking; } }
    public float HealCooldown { get { return healCooldown; } }
    public float NextHealTime { get { return nextHealTime; } }
    public float CurrentMoveSpeed { get { return currentMoveSpeed; } }

    #region Public Events / Properties
    public System.Action OnArrived;
    public event System.Action<float> OnHealthChanged;
    public System.Action OnDeath;
    public System.Action OnHealStart;
    public System.Action OnHealComplete;
    #endregion

    private void Awake()
    {
        health = GetComponent<Health>();
        health.SetMaxHealth(maxHealth);
        health.ResetToFull();
        health.RegenPerSecond = healthRegen;
        health.DisableRegenInCombat = false;
        health.RegenDelayAfterDamage = 0f;
        health.EnableRegen = true;
        health.onDeath.AddListener(OnHealthDepleted);
        health.onHealthChanged.AddListener(pct => OnHealthChanged?.Invoke(pct));
        if (animator == null) animator = GetComponent<Animator>();
        cc = GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = gameObject.AddComponent<CharacterController>();
            cc.radius = 0.35f; cc.height = 1.8f; cc.center = new Vector3(0, 0.9f, 0);
        }
        var capsule = GetComponent<CapsuleCollider>(); if (capsule != null) capsule.isTrigger = true;
    }

    private void Start()
    {
        if (waypointPath == null) waypointPath = FindFirstObjectByType<WaypointPath>();
        if (waypointPath == null || waypointPath.WaypointCount == 0) return;

        CalibrateSpeed();
        currentWaypointIndex = 0;
        isWalking = true;
        currentMoveSpeed = targetMoveSpeed;
        SetWaypointFloatTarget();
        SetupPauseSchedule();
        stuckCheckPosition = transform.position;
        stuckCheckTimer = stuckCheckInterval;
    }

    private void CalibrateSpeed()
    {
        float totalDist = waypointPath.GetTotalLength();
        float targetTime = Random.Range(targetTimeMin, targetTimeMax);

        float pausesTotal = targetTime / 60f * pausesPerMinute;
        float avgPauseDuration = (pauseDurationMin + pauseDurationMax) * 0.5f;
        float totalPauseTime = pausesTotal * avgPauseDuration;
        float walkTime = targetTime - totalPauseTime;
        walkTime = Mathf.Max(walkTime, 10f);

        float requiredSpeed = totalDist / walkTime;
        targetMoveSpeed = Mathf.Clamp(requiredSpeed, minSpeed, maxSpeed);
        currentMoveSpeed = targetMoveSpeed;

        pauseIntervalDistance = totalDist / Mathf.Max(pausesTotal, 1f);
        pauseIntervalDistance = Mathf.Max(pauseIntervalDistance, minDistBetweenPauses);
    }

    private void SetupPauseSchedule()
    {
        distanceSinceLastPause = 0f;
        nextPauseDistanceTraveled = pauseIntervalDistance * Random.Range(0.7f, 1.3f);
    }

    private void SetWaypointFloatTarget()
    {
        Vector3 wp = waypointPath.GetWaypoint(currentWaypointIndex);
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Random.Range(waypointFloatMin, waypointFloatMax);
        currentWaypointFloatTarget = wp + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private void Update()
    {
        // Cache players periodically
        playerCacheTimer -= Time.deltaTime;
        if (playerCacheTimer <= 0f)
        {
            cachedPlayers = FindObjectsByType<PlayerMove>(FindObjectsSortMode.None);
            playerCacheTimer = 1f;
        }

        // Damage slow
        if (damageSlowTimer > 0f) damageSlowTimer -= Time.deltaTime;

        // Check auto-heal
        if (Time.time >= nextHealTime && !isHealing && !IsDead && !hasArrived)
            CheckAutoHeal();

        // Heal casting update
        if (isHealing) UpdateHealCast();

        // Speed variation
        UpdateSpeedVariation();

        // Movement
        if (isWalking && !IsDead && !hasArrived && !isHealing)
        {
            if (pauseTimer > 0f)
            {
                pauseTimer -= Time.deltaTime;
            }
            else
            {
                Vector3 target = currentWaypointFloatTarget;
                Vector3 toTarget = target - transform.position;
                toTarget.y = 0f;

                Vector3 avoid = GetObstacleAvoidance(toTarget);
                Vector3 moveDir = (toTarget.normalized + avoid * obstacleAvoidStrength).normalized;

                bool isDamageSlowed = damageSlowTimer > 0f;
                float speed = isDamageSlowed ? currentMoveSpeed * damageSlowMultiplier : currentMoveSpeed;
                Vector3 nd = moveDir * speed * Time.deltaTime;
                nd.y = cc.isGrounded ? -0.1f : nd.y - 9.81f * Time.deltaTime;
                cc.Move(nd);

                if (moveDir.sqrMagnitude > 0.001f)
                    MovementUtility.FaceDirection(transform, moveDir, rotationSpeed, Time.deltaTime);

                float distToTarget = FlatDistanceTo(target);

                if (distToTarget <= waypointPath.ArriveDistance)
                    OnWaypointReached();

                float movedThisFrame = speed * Time.deltaTime;
                distanceSinceLastPause += movedThisFrame;
                TrackPauseSchedule();
            }
        }

        UpdateStuckCheck();
        UpdateAnimation();
    }

    private void CheckAutoHeal()
    {
        if (isHealing || Time.time < nextHealTime) return;

        // Self-heal: own HP < 400
        if (CurrentHealth < selfHealThreshold)
        {
            StartHealCast();
            return;
        }

        // Heal player: own HP >= 400, find players with HP < 600
        if (CurrentHealth >= selfHealThreshold && cachedPlayers != null)
        {
            PlayerMove bestTarget = null;
            float bestDist = float.MaxValue;

            foreach (var p in cachedPlayers)
            {
                if (p == null || p.CurrentHealth <= 0f) continue;
                if (p.CurrentHealth >= playerHealThreshold) continue;
                if (p.HealthPercent >= 1f) continue;

                float dist = FlatDistanceTo(p.transform.position);
                if (dist < bestDist && dist <= healRange)
                {
                    bestDist = dist;
                    bestTarget = p;
                }
            }

            if (bestTarget != null)
                StartHealCast();
        }
    }

    private void UpdateSpeedVariation()
    {
        if (Time.time >= nextSpeedChangeTime)
        {
            nextSpeedChangeTime = Time.time + Random.Range(speedChangeIntervalMin, speedChangeIntervalMax);
            targetMoveSpeed = Random.Range(minSpeed, maxSpeed);
        }
        currentMoveSpeed = Mathf.SmoothDamp(currentMoveSpeed, targetMoveSpeed,
            ref speedSmoothVelocity, speedSmoothTime);
    }

    private void TrackPauseSchedule()
    {
        if (pauseTimer > 0f) return;

        if (distanceSinceLastPause >= nextPauseDistanceTraveled)
        {
            pauseTimer = Random.Range(pauseDurationMin, pauseDurationMax);
            distanceSinceLastPause = 0f;
            nextPauseDistanceTraveled = pauseIntervalDistance * Random.Range(0.7f, 1.3f);
        }
    }

    private Vector3 GetObstacleAvoidance(Vector3 toTarget)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 forward = toTarget.normalized;

        if (Physics.SphereCast(origin, obstacleCheckRadius, forward, out RaycastHit hit,
            obstacleCheckDistance, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 avoidDir = right;
            if (Vector3.Dot(hit.normal, right) < 0f) avoidDir = -right;
            return avoidDir * (1f - hit.distance / obstacleCheckDistance);
        }
        return Vector3.zero;
    }

    private void OnWaypointReached()
    {
        if (waypointPath == null) return;
        if (TryArrive()) return;
        currentWaypointIndex = waypointPath.GetNextIndex(currentWaypointIndex);
        SetWaypointFloatTarget();
        pauseTimer = Random.Range(1f, 3f);
        distanceSinceLastPause = 0f;
        stuckCheckPosition = transform.position;
        stuckCheckTimer = stuckCheckInterval;
    }

    /// <summary>Check arrival and trigger end-of-path events. Returns true if arrived.</summary>
    private bool TryArrive()
    {
        if (!waypointPath.HasReachedEnd(currentWaypointIndex)) return false;
        hasArrived = true;
        isWalking = false;
        health.EnableRegen = false;
        OnArrived?.Invoke();
        return true;
    }

    private void UpdateStuckCheck()
    {
        stuckCheckTimer -= Time.deltaTime;
        if (stuckCheckTimer <= 0f)
        {
            float moved = Vector3.Distance(transform.position, stuckCheckPosition);
            if (moved < stuckThreshold && pauseTimer <= 0f)
            {
                currentWaypointIndex = waypointPath.GetNextIndex(currentWaypointIndex);
                if (TryArrive()) return;
                SetWaypointFloatTarget();
                pauseTimer = 1f;
            }
            stuckCheckPosition = transform.position;
            stuckCheckTimer = stuckCheckInterval;
        }
    }

    public void TakeDamage(float dmg)
    {
        if (IsDead || hasArrived) return;
        health.TakeDamage(dmg);
        damageSlowTimer = damageSlowDuration;
    }

    private void OnHealthDepleted()
    {
        isWalking = false;
        OnDeath?.Invoke();
        SafeSetTrigger(deathParam);
    }

    public bool RequestHeal(Vector3 playerPos)
    {
        if (IsDead || hasArrived || isHealing) return false;
        if (Time.time < nextHealTime) return false;
        if (health.IsFullHealth) return false;

        float dist = FlatDistanceTo(playerPos);
        if (dist > healRange) return false;

        StartHealCast();
        return true;
    }

    
    /// <summary>Safely set an animator trigger, ignoring missing parameters.</summary>
    private void SafeSetTrigger(string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return;
        foreach (var p in animator.parameters)
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Trigger)
                { animator.SetTrigger(paramName); return; }
    }

    private void StartHealCast()
    {
        isHealing = true;
        healStartTime = Time.time;
        healPosition = transform.position;
        SafeSetTrigger(healParam);
        OnHealStart?.Invoke();
    }

    private void UpdateHealCast()
    {
        if (Vector3.Distance(transform.position, healPosition) > 0.1f)
        {
            isHealing = false;
            return;
        }
        if (Time.time - healStartTime >= healCastTime)
            CompleteHeal();
    }

    private void CompleteHeal()
    {
        isHealing = false;
        nextHealTime = Time.time + healCooldown;
        health.Heal(healAmount);

        // Also heal nearby players
        if (cachedPlayers != null)
        {
            foreach (var p in cachedPlayers)
            {
                if (p == null || p.CurrentHealth <= 0f) continue;
                float dist = FlatDistanceTo(p.transform.position);
                if (dist <= healRange && p.CurrentHealth < p.EffectiveMaxHealth)
                {
                    // Heal the player too (always heals NPC + nearby players)
                    p.TakeDamage(-healAmount);
                }
            }
        }

        OnHealComplete?.Invoke();
    }

    public void StartWalking() { if (!hasArrived && !IsDead) isWalking = true; }
    public void StopWalking() { isWalking = false; }

    /// <summary>Horizontal (XZ-plane) distance to a point.</summary>
    private float FlatDistanceTo(Vector3 point)
    {
        float dx = transform.position.x - point.x;
        float dz = transform.position.z - point.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public float EstimateJourneyTime()
    {
        if (waypointPath == null || waypointPath.WaypointCount < 2) return -1f;
        float dist = waypointPath.GetTotalLength();
        float avgSpeed = (minSpeed + maxSpeed) * 0.5f;
        float totalPauses = dist / pauseIntervalDistance;
        float avgPause = (pauseDurationMin + pauseDurationMax) * 0.5f;
        return (dist / avgSpeed) + totalPauses * avgPause;
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;
        bool moving = isWalking && !IsDead && !hasArrived && !isHealing && pauseTimer <= 0f;
        animator.SetBool(isMovingParam, moving);
        animator.SetFloat(moveSpeedParam, moving ? currentMoveSpeed : 0f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healRange);

        if (!hasArrived && !IsDead && waypointPath != null && waypointPath.WaypointCount > 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentWaypointFloatTarget, 0.4f);
            Gizmos.DrawLine(transform.position, currentWaypointFloatTarget);
        }

        Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 fwd = transform.forward * obstacleCheckDistance;
        Gizmos.DrawWireSphere(origin + fwd, obstacleCheckRadius);
    }
}

