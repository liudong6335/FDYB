/*
 * ============================================================
 *  NPCGoddess  -  NPCŮ�񣨱�����Ŀ�꣩
 * ============================================================
 *
 * �����ܡ�
 *   �����Ҫ���͵�Ů��NPC��������·����WaypointPath�����ߣ�
 *   ;�п��Ը���Һ��Լ���Ѫ�������ϰ��
 *   �����յ�󴥷��¼���������������ʱ����
 *
 * �����ض���
 *   �����е� NPC Ů����󣨴��� Animator��
 *
 * ���ɵ��ڲ�����
 *   �����ԣ�
 *   maxHealth                - ���Ѫ��
 *   healthRegen              - ÿ���Ѫ
 *
 *   ���ƶ���
 *   waypointPath             - ����·�������� WaypointPath ����
 *   rotationSpeed            - ת���ٶ�
 *   minSpeed / maxSpeed       - ��С/��������ٶ�
 *   speedSmoothTime          - �ٶȱ仯ƽ��ʱ��
 *
 *   ��·�����������
 *   waypointFloatMin/Max     - ����·����ʱ�����ƫ�Ʒ�Χ
 *
 *   ����ͣ��Ϊ��
 *   pausesPerMinute          - ÿ����ƽ��ͣ�ٴ���
 *   pauseDurationMin/Max      - ÿ��ͣ�ٵ�ʱ����Χ
 *
 *   ���ܺ�ʱ��
 *   targetTimeMin/Max         - Ԥ�Ƶ����յ�ĺ�ʱ��Χ���룩
 *
 *   ���ϰ����ܣ�
 *   obstacleCheckDistance    - ǰ��������
 *   obstacleCheckRadius      - ���뾶
 *   obstacleAvoidStrength    - ���ǿ��
 *
 *   �����Ƽ��ܣ�
 *   healAmount               - ÿ�����������Լ�+������ң�
 *   healCooldown             - ������ȴʱ��
 *   healCastTime             - ʩ��ʱ��
 *   healRange                - ���Ʒ�Χ
 *   selfHealThresholdPercent  - �Լ�Ѫ�����ڴ˰ٷֱ�(0~1)��ʼ����
 *   playerHealThresholdPercent - ���Ѫ�����ڴ˰ٷֱ�(0~1)�������
 *
 *   �����˼��٣�
 *   damageSlowMultiplier     - ���˺��ƶ��ٶȱ���
 *   damageSlowDuration       - ���ٳ���ʱ��
 *
 *   ����ס��⣩
 *   stuckCheckInterval       - �����
 *   stuckThreshold           - ��ס�ж���ֵ
 *
 * ��˵����
 *   - �� CharacterController �ƶ�
 *   - �Զ���·����֮�����ߣ�������ٶȺ�ͣ��
 *   - Ѫ����ʱ�Զ����Լ��͸�����Ҽ�Ѫ
 *   - �����յ�󴥷� OnArrived �¼�
 */
using UnityEngine;
using UnityEngine.AI;

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

    [Header("Pauses")]
    [SerializeField] private float pausesPerMinute = 2.5f;
    [SerializeField] private float pauseDurationMin = 2f;
    [SerializeField] private float pauseDurationMax = 4f;
    [SerializeField] private float minDistBetweenPauses = 15f;

    [Header("Journey Time")]
    [SerializeField] private float walkTimeBase = 240f;
    [SerializeField] [Range(0f, 1f)] private float walkTimeRatio = 0.15f;

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
    [SerializeField] [Range(0f, 1f)] private float selfHealThresholdPercent = 0.6f;
    [SerializeField] [Range(0f, 1f)] private float playerHealThresholdPercent = 0.5f;

    [Header("Passive Heal Aura")]
    [SerializeField] private float passiveHealCooldown = 10f;
    [SerializeField] private float passiveHealDuration = 5f;
    [SerializeField] private float passiveHealRange = 5f;
    [SerializeField] private float passiveHealPerSecond = 30f;

   [Header("Damage")]

   [Header("Stuck Detection")]
    [SerializeField] private float stuckCheckInterval = 3f;
    [SerializeField] private float stuckThreshold = 0.3f;


    [Header("Zone Movement")]
    [SerializeField] private float zoneRadius = 8f;
    [SerializeField] private float deviationChance = 0.25f;
    [SerializeField] private float deviationCheckInterval = 5f;
    [SerializeField] private float deviationMaxDistance = 25f;
    [Header("Monster Evasion")]
    [SerializeField] private float dangerDetectRadius = 10f;
    [SerializeField] private float evadeTriggerDistance = 8f;
    [SerializeField] private float evadeSpeedMultiplier = 1.2f;
    [SerializeField] private float evadeStrength = 0.5f;
    [Header("Rescue Settings")]
    [SerializeField] private float rescueHpThreshold = 0.5f;
    [SerializeField] private float emergencyHpThreshold = 0.1f;
    [SerializeField] private float rescueDetectRange = 15f;
    [SerializeField] private float rescueArriveDistance = 3f;
    [SerializeField] private float rescueStayTime = 3f;
    [SerializeField] private float rescueDetourOffset = 5f;
    [SerializeField] private float rescueCooldownTime = 6f;
    [SerializeField] private float rescueForwardTime = 5f;
    [SerializeField] private float maxBacktrackDistance = 8f;

    [Header("Wait Settings")]
    [SerializeField] private float waitDurationMin = 3f;
    [SerializeField] private float waitDurationMax = 8f;
    [SerializeField] private float waitPatrolRadius = 5f;
    [SerializeField] private float waitNoTeammateTimeout = 3f;
    [SerializeField] private float waitCooldown = 10f;

    [Header("Behaviour AI")]
    [Tooltip("When true, BehaviourModelBase drives decisions; old hardcoded logic is skipped.")]
    [SerializeField] private bool useBehaviourAI = false;
    [System.NonSerialized] public string recommendedAction = "Walk";

    public bool IsBehaviourAIEnabled { get { return useBehaviourAI; } }

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string moveSpeedParam = "MoveSpeed";
    [SerializeField] private string healParam = "Heal";
    [SerializeField] private string deathParam = "Death";
    [SerializeField] private string hitParam = "Hit";
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
    private DamageSlowEffect slowEffect;
   private float nextPassiveHealTime;
    private float passiveHealActiveTimer;
    private bool isPassiveHealing;
    private Vector3 stuckCheckPosition;
    private float stuckCheckTimer;

    private float targetMoveSpeed;
    private float currentMoveSpeed;
    private float speedSmoothVelocity;
    private float nextSpeedChangeTime;
    private Vector3 currentMoveTarget;
    private bool isDeviating;
    private int deviatingToIndex;
    private float nextDeviationCheckTime;
    private float nextZoneDriftTime;
    private bool isEvading;
    private Transform nearestThreat;
    // Rescue & Wait state
    private bool isRescuing;
    private Transform rescueTarget;
    private float rescueStayTimer;
    private bool isWaiting;
    private Vector3 waitPatrolTarget;
    private float waitTimer;
    private float waitCooldownTimer;
    private float noTeammateTimer;
    private float rescueCooldownTimer;
    private float rescueForwardTimer;
    private float nextPauseDistanceTraveled;
    private float distanceSinceLastPause;
    private float pauseIntervalDistance;

    private CharacterController cc;
    private NavMeshAgent navAgent;
    private bool navMeshReady;
    
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
    public float HealRange { get { return healRange; } }
    /// <summary>Whether NavMeshAgent is available, enabled, and on a valid NavMesh.</summary>
    public bool UseNavMesh { get { return navMeshReady && navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh; } }
    public float CurrentMoveSpeed { get { return currentMoveSpeed; } }
    public int CurrentWaypointIndex { get { return currentWaypointIndex; } }
    public WaypointPath WaypointPath { get { return waypointPath; } }

    #region Public Events / Properties
    public System.Action OnArrived;
    public event System.Action<float> OnHealthChanged;
    public System.Action OnDeath;
    public System.Action OnHealStart;
    public System.Action OnHealComplete;
    #endregion

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent != null)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(transform.position, out navHit, 5f, NavMesh.AllAreas))
            {
                navAgent.Warp(navHit.position);
                navMeshReady = navAgent.isOnNavMesh;
            }
            if (navAgent.isOnNavMesh)
                navAgent.stoppingDistance = Mathf.Min(navAgent.stoppingDistance, 0.5f);
                navAgent.updatePosition = false;
                navAgent.updateRotation = false;
        }
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
        slowEffect = GetComponent<DamageSlowEffect>();
    }

    private void Start()
    {
        if (waypointPath == null) waypointPath = FindFirstObjectByType<WaypointPath>();
        if (waypointPath == null || waypointPath.WaypointCount == 0) return;

        CalibrateSpeed();
        currentWaypointIndex = 0;
        isWalking = true;
        currentMoveSpeed = targetMoveSpeed;
        RefreshZoneDrift();
        SetupPauseSchedule();
        stuckCheckPosition = transform.position;
        stuckCheckTimer = stuckCheckInterval;
    }

    private void CalibrateSpeed()
    {
        float totalDist = waypointPath.GetTotalLength();
        float targetTime = walkTimeBase * Random.Range(1f - walkTimeRatio, 1f + walkTimeRatio);

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

    private void RefreshZoneDrift()
    {
        if (waypointPath == null || waypointPath.WaypointCount == 0) return;
        Vector2 drift = Random.insideUnitCircle * zoneRadius * 0.5f;
        Vector3 zoneCenter = waypointPath.GetWaypoint(currentWaypointIndex);
        currentMoveTarget = zoneCenter + new Vector3(drift.x, 0f, drift.y);
        nextZoneDriftTime = Time.time + Random.Range(3f, 6f);
    }

    private bool TryStartDeviation()
    {
        if (waypointPath == null || waypointPath.WaypointCount < 2) return false;
        if (HasReachedEndOfPath()) return false;
        if (Random.value > deviationChance) return false;

        float progress = (float)currentWaypointIndex / Mathf.Max(waypointPath.WaypointCount - 1, 1);
        int interestIndex = waypointPath.PickWeightedInterest(
            currentWaypointIndex, transform.position, progress, 0.8f);

        // Only deviate to points ahead
        if (interestIndex <= currentWaypointIndex) return false;

        deviatingToIndex = interestIndex;
        currentMoveTarget = waypointPath.GetWaypoint(interestIndex);
        return true;
    }

    private bool HasReachedEndOfPath()
    {
        return waypointPath != null && waypointPath.HasReachedEnd(currentWaypointIndex);
    }

    private void Update()
    {
        // Stop NavMeshAgent when paused or healing
        if (UseNavMesh && (pauseTimer > 0f || isHealing || !isWalking || IsDead || hasArrived))
        {
            if (navAgent.hasPath) navAgent.ResetPath();
        }

        // Damage slow
        // Damage slow handled by DamageSlowEffect component

        // Heal decision: delegate to Behaviour AI or legacy logic
        if (!useBehaviourAI)
        {
            if (Time.time >= nextHealTime && !isHealing && !IsDead && !hasArrived)
                CheckAutoHeal();
        }
        else
        {
            // Behaviour AI drives heal decisions via recommendedAction
            if (!isHealing && !IsDead && !hasArrived && Time.time >= nextHealTime && recommendedAction == "Heal")
                StartHealCast();
        }

        // Heal casting update
        if (isHealing) UpdateHealCast();

        // Passive heal aura
        UpdatePassiveHealAura();

        // Speed variation
        UpdateSpeedVariation();

        // Movement
        if (isWalking && !IsDead && !hasArrived && !isHealing)
        {
            // Monster evasion
            UpdateMonsterEvasion();
            if (isEvading && pauseTimer > 0f)
                pauseTimer = 0f;
            // Behaviour AI: pause recommendation
            if (useBehaviourAI && pauseTimer <= 0f && (recommendedAction == "Pause" || recommendedAction == "Idle"))
                pauseTimer = Random.Range(2f, 4f);

            if (pauseTimer > 0f)
            {
                pauseTimer -= Time.deltaTime;
            }
            else
            {
        Vector3 target = Vector3.zero;
                // --- Target management: zone or deviation ---
                if (isDeviating)
                {
                    target = currentMoveTarget;
                    // Check if deviation is complete
                    float distToInterest = FlatDistanceTo(target);
                    float distFromZone = Vector3.Distance(transform.position, waypointPath.GetWaypoint(currentWaypointIndex));
                    if (distToInterest <= waypointPath.ArriveDistance || distFromZone > deviationMaxDistance)
                    {
                        isDeviating = false;
                        RefreshZoneDrift();
                        target = currentMoveTarget;
                    }
                }
                else
                {
                    // Periodically roll for deviation
                    if (Time.time >= nextDeviationCheckTime)
                    {
                        nextDeviationCheckTime = Time.time + deviationCheckInterval;
                        if (TryStartDeviation())
                        {
                            target = currentMoveTarget;
                            isDeviating = true;
                        }
                    }

                    if (!isDeviating)
                    {
                        // Within-zone drift refresh
                        if (Time.time >= nextZoneDriftTime)
                            RefreshZoneDrift();
                        target = currentMoveTarget;
                        // Flee from nearest monster when evading
                        if (isEvading && nearestThreat != null && !isRescuing && !isWaiting)
                        {
                            // Sidestep perpendicular to forward, away from monster
                            Vector3 toThreat = (nearestThreat.position - transform.position).normalized;
                            toThreat.y = 0f;
                            Vector3 perp = Vector3.Cross(Vector3.up, transform.forward).normalized;
                            if (Vector3.Dot(perp, toThreat) > 0f) perp = -perp;
                            float dist = Vector3.Distance(transform.position, nearestThreat.position);
                            float weight = Mathf.Clamp01(evadeStrength * (1f - dist / evadeTriggerDistance));
                            target = target + perp * 5f * weight;
                        }
                    }
                }
                // Override target for rescue/wait mode
                if (isRescuing && rescueTarget != null)
                    target = GetRescueTarget(rescueTarget.position);
                else if (isWaiting)
                {
                    if (FlatDistanceTo(waitPatrolTarget) <= 1f)
                        waitPatrolTarget = GetWaitPatrolTarget();
                    target = waitPatrolTarget;
                }
                float currentSpeed = currentMoveSpeed * (slowEffect != null ? slowEffect.SpeedMultiplier : 1f);
                if (isEvading || isRescuing) currentSpeed *= evadeSpeedMultiplier;

                if (UseNavMesh)
                {
                    navAgent.speed = currentSpeed;
                    navAgent.SetDestination(target);

                    // Wait for path calculation before reading desiredVelocity.
                    // Without this check, navAgent.desiredVelocity returns Vector3.zero
                    // on the first 1-2 frames after SetDestination.
                    Vector3 desiredVel;
                    if (navAgent.pathPending)
                    {
                        desiredVel = Vector3.zero;
                    }
                    else
                    {
                        desiredVel = navAgent.desiredVelocity;
                    }

                    Vector3 moveDelta = desiredVel * Time.deltaTime;
                    moveDelta.y = cc.isGrounded ? -0.1f : moveDelta.y - 9.81f * Time.deltaTime;
                    cc.Move(moveDelta);

                    // Sync agent position with actual transform after manual movement
                    navAgent.nextPosition = transform.position;

                    if (desiredVel.sqrMagnitude > 0.001f)
                        MovementUtility.FaceDirection(transform, new Vector3(desiredVel.x, 0f, desiredVel.z), rotationSpeed, Time.deltaTime);
                }
                else
                {
                    Vector3 toTarget = target - transform.position;
                    toTarget.y = 0f;

                    Vector3 avoid = GetObstacleAvoidance(toTarget);
                    Vector3 moveDir = (toTarget.normalized + avoid * obstacleAvoidStrength).normalized;

                    float speed = currentSpeed;
                    Vector3 nd = moveDir * speed * Time.deltaTime;
                    nd.y = cc.isGrounded ? -0.1f : nd.y - 9.81f * Time.deltaTime;
                    cc.Move(nd);

                    if (moveDir.sqrMagnitude > 0.001f)
                        MovementUtility.FaceDirection(transform, moveDir, rotationSpeed, Time.deltaTime);
                }


                if (!isRescuing && !isWaiting && !isDeviating && FlatDistanceTo(waypointPath.GetWaypoint(currentWaypointIndex)) <= zoneRadius)
                    OnWaypointReached();
                float movedThisFrame = currentSpeed * Time.deltaTime;
                if (UseNavMesh) movedThisFrame = Mathf.Max(navAgent.velocity.magnitude, 0.01f) * Time.deltaTime;
                distanceSinceLastPause += movedThisFrame;
                if (!useBehaviourAI && !isEvading && !isRescuing && !isWaiting) TrackPauseSchedule();
            }
        }

        if (!useBehaviourAI) UpdateStuckCheck();
        UpdateAnimation();
    }

    private void CheckAutoHeal()
    {
        if (isHealing || Time.time < nextHealTime) return;

        // Self-heal: own HP < 400
        if (HealthPercent < selfHealThresholdPercent)
        {
            StartHealCast();
            return;
        }

        // Heal player: own HP >= 400, find players with HP < 600
        if (HealthPercent >= selfHealThresholdPercent)
        {
            PlayerMove bestTarget = null;
            float bestDist = float.MaxValue;

            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p == null || p.CurrentHealth <= 0f) continue;
                if (p.HealthPercent >= playerHealThresholdPercent) continue;
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
        if (IsDead) return;
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
        RefreshZoneDrift();
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
                RefreshZoneDrift();
                pauseTimer = 1f;
            }
            stuckCheckPosition = transform.position;
            stuckCheckTimer = stuckCheckInterval;
        }
    }

    public void Heal(float amount) { health.Heal(amount); }

    public void TakeDamage(float dmg)
    {
        if (IsDead || hasArrived) return;
        health.TakeDamage(dmg);
        SafeSetTrigger(hitParam);
        // Slow handled by DamageSlowEffect component
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
        //Debug.Log("[NPCHeal] StartHealCast called");
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
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p == null || p.CurrentHealth <= 0f) continue;
                float dist = FlatDistanceTo(p.transform.position);
                if (dist <= healRange && p.CurrentHealth < p.EffectiveMaxHealth)
                {
                    // Heal the player too (always heals NPC + nearby players)
                    p.Heal(healAmount);
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
    private void UpdatePassiveHealAura()
    {
        if (IsDead || hasArrived) return;

        if (!isPassiveHealing)
        {
            if (Time.time >= nextPassiveHealTime)
            {
                isPassiveHealing = true;
                passiveHealActiveTimer = 0f;
            }
        }
        else
        {
            passiveHealActiveTimer += Time.deltaTime;

            // Heal nearby allies every frame (HP/s * deltaTime)
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p == null || p.CurrentHealth <= 0f) continue;
                float dist = FlatDistanceTo(p.transform.position);
                if (dist <= passiveHealRange && p.CurrentHealth < p.EffectiveMaxHealth)
                {
                    p.Heal(passiveHealPerSecond * Time.deltaTime);
                }
            }

            // Check duration expiry
            if (passiveHealActiveTimer >= passiveHealDuration)
            {
                isPassiveHealing = false;
            }
        }
    }
    
    /// <summary>Scan for nearby monsters and evade if too close.</summary>
    private void UpdateMonsterEvasion()
    {
        if (IsDead || hasArrived) { isEvading = false; return; }

        nearestThreat = null;
        float nearestSqr = dangerDetectRadius * dangerDetectRadius;

        foreach (var demon in DemonMinion.AllDemons)
        {
            if (demon == null || demon.IsDead) continue;
            // Only consider monsters in front of NPC
            Vector3 toMonster = demon.transform.position - transform.position;
            toMonster.y = 0f;
            if (Vector3.Dot(toMonster.normalized, transform.forward) < 0f) continue;
            float dx = transform.position.x - demon.transform.position.x;
            float dz = transform.position.z - demon.transform.position.z;
            float sqr = dx * dx + dz * dz;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearestThreat = demon.transform;
            }
        }

        if (nearestThreat != null)
        {
            float dist = Mathf.Sqrt(nearestSqr);
            isEvading = dist < evadeTriggerDistance;
        }
        else
        {
            isEvading = false;
        }
    }

    // ========== Rescue & Wait Methods ==========

    /// <summary>Find the teammate with lowest HP within detect range.</summary>
    private Transform FindRescueTarget()
    {
        Transform best = null;
        float lowestHP = rescueHpThreshold;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null || p.IsDead) continue;
            float hp = p.HealthPercent;
            if (hp >= rescueHpThreshold) continue;
            float dist = FlatDistanceTo(p.transform.position);
            bool emergency = hp < emergencyHpThreshold;
            bool inRange = dist <= rescueDetectRange;
            if ((inRange || emergency) && hp < lowestHP)
            {
                lowestHP = hp;
                best = p.transform;
            }
        }
        return best;
        // Skip rescue target if it's behind NPC and too far back
        if (best != null)
        {
            Vector3 toTarget = best.position - transform.position;
            toTarget.y = 0f;
            bool isBehind = Vector3.Dot(toTarget.normalized, transform.forward) < 0f;
            if (isBehind && toTarget.magnitude > maxBacktrackDistance)
                return null;
        }
    }

    /// <summary>Check if any teammate is within the given distance.</summary>
    private bool IsAnyTeammateWithin(float range)
    {
        if (IsRescuingTargetInRange()) return true;
        float sqr = range * range;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null || p.IsDead) continue;
            float dx = transform.position.x - p.transform.position.x;
            float dz = transform.position.z - p.transform.position.z;
            if (dx * dx + dz * dz < sqr) return true;
        }
        return false;
    }

    private bool IsRescuingTargetInRange()
    {
        return rescueTarget != null && FlatDistanceTo(rescueTarget.position) <= rescueDetectRange;
    }

    /// <summary>Get target position for rescue, detouring around monsters if needed.</summary>
    private Vector3 GetRescueTarget(Vector3 teammatePos)
    {
        Transform threat = null;
        float threatSqr = dangerDetectRadius * dangerDetectRadius;
        foreach (var demon in DemonMinion.AllDemons)
        {
            if (demon == null || demon.IsDead) continue;
            float dx = transform.position.x - demon.transform.position.x;
            float dz = transform.position.z - demon.transform.position.z;
            float sqr = dx * dx + dz * dz;
            if (sqr < threatSqr) { threatSqr = sqr; threat = demon.transform; }
        }
        if (threat != null)
        {
            Vector3 toT = (teammatePos - transform.position).normalized;
            Vector3 toM = (threat.position - transform.position).normalized;
            Vector3 perp = Vector3.Cross(Vector3.up, toT).normalized;
            float side = Vector3.Dot(perp, toM) > 0f ? -1f : 1f;
            return teammatePos + perp * side * rescueDetourOffset;
        }
        return teammatePos;
    }

    private Vector3 GetWaitPatrolTarget()
    {
        Vector2 rand = Random.insideUnitCircle * waitPatrolRadius;
        return transform.position + new Vector3(rand.x, 0f, rand.y);
    }

    /// <summary>Update the NPC state machine: Walking, Rescuing, or Waiting.</summary>
    private void UpdateNPCState()
    {
        if (IsDead || hasArrived || isHealing) return;

        // --- Rescue: find and go to lowest-HP teammate ---
        Transform target = null;
        if (rescueCooldownTimer <= 0f && rescueForwardTimer <= 0f)
        {
            target = FindRescueTarget();
        }
        if (target != null)
        {
            if (!isRescuing) rescueStayTimer = 0f;
            isRescuing = true;
            rescueTarget = target;
            noTeammateTimer = 0f;
            pauseTimer = 0f;
            return;
        }

        // --- Already rescuing? Check arrival ---
        if (isRescuing)
        {
            if (rescueTarget != null && FlatDistanceTo(rescueTarget.position) <= rescueArriveDistance)
            {
                rescueStayTimer += Time.deltaTime;
                if (rescueStayTimer >= rescueStayTime)
                {
                    isRescuing = false;
                    rescueTarget = null;
                    rescueStayTimer = 0f;
                    rescueCooldownTimer = rescueCooldownTime;
                    rescueForwardTimer = rescueForwardTime;
                    if (waypointPath != null)
                    {
                        int nearest = waypointPath.GetNearestWaypointIndex(transform.position);
                        int next = waypointPath.GetNextIndex(nearest);
                        currentWaypointIndex = next > nearest ? next : nearest;
                    }
                    RefreshZoneDrift();
                }
            }
            else if (rescueTarget == null || rescueTarget.GetComponent<PlayerMove>() == null || rescueTarget.GetComponent<PlayerMove>().IsDead)
            {
                isRescuing = false;
                rescueTarget = null;
                rescueStayTimer = 0f;
                rescueCooldownTimer = rescueCooldownTime;
                rescueForwardTimer = rescueForwardTime;
            }
            return; // still rescuing, skip wait check
        }

        // --- Wait logic: no teammate nearby for 3s ---
        if (!isRescuing)
        {
            if (isWaiting)
            {
                // Early exit if teammate returned
                if (IsAnyTeammateWithin(rescueDetectRange))
                {
                    waitTimer = 0f;
                }
                else
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f)
                {
                    isWaiting = false;
                    waitCooldownTimer = waitCooldown;
                }
            }
            else
            {
                bool nearby = IsAnyTeammateWithin(rescueDetectRange);
                if (!nearby)
                {
                    noTeammateTimer += Time.deltaTime;
                    if (noTeammateTimer >= waitNoTeammateTimeout && waitCooldownTimer <= 0f)
                    {
                        isWaiting = true;
                        waitTimer = Random.Range(waitDurationMin, waitDurationMax);
                        waitPatrolTarget = GetWaitPatrolTarget();
                        noTeammateTimer = 0f;
                    }
                }
                else
                {
                    noTeammateTimer = 0f;
                }
            }
        }

        if (waitCooldownTimer > 0f) waitCooldownTimer -= Time.deltaTime;
        if (rescueCooldownTimer > 0f) rescueCooldownTimer -= Time.deltaTime;
        if (rescueForwardTimer > 0f) rescueForwardTimer -= Time.deltaTime;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healRange);

        if (!hasArrived && !IsDead && waypointPath != null && waypointPath.WaypointCount > 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentMoveTarget, 0.4f);
            Gizmos.DrawLine(transform.position, currentMoveTarget);
        }

        Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 fwd = transform.forward * obstacleCheckDistance;
        Gizmos.DrawWireSphere(origin + fwd, obstacleCheckRadius);
    }
}




