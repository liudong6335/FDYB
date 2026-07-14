using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NPCGoddess))]
public class NPCGoddessMovement : MonoBehaviour
{
    private NPCGoddess npc;
    private Health health;
    private CharacterController cc;
    private NavMeshAgent navAgent;
    private DamageSlowEffect slowEffect;
    private bool navMeshReady;

    [Header("Movement")]
    [SerializeField] private WaypointPath waypointPath;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Speed")]
    [SerializeField] private float minSpeed = 1f;
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float speedChangeIntervalMin = 4f;
    [SerializeField] private float speedChangeIntervalMax = 8f;
    [SerializeField] private float speedSmoothTime = 0.8f;

    [Header("Journey Time")]
    [SerializeField] private float walkTimeBase = 240f;
    [SerializeField][Range(0f, 1f)] private float walkTimeRatio = 0.15f;

    [Header("Pauses")]
    [SerializeField] private float pausesPerMinute = 2.5f;
    [SerializeField] private float pauseDurationMin = 2f;
    [SerializeField] private float pauseDurationMax = 4f;
    [SerializeField] private float minDistBetweenPauses = 15f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private float obstacleCheckDistance = 3f;
    [SerializeField] private float obstacleCheckRadius = 0.5f;
    [SerializeField] private float obstacleAvoidStrength = 2f;
    [SerializeField] private LayerMask obstacleLayers = -1;

    [Header("Stuck Detection")]
    [SerializeField] private float stuckCheckInterval = 3f;
    [SerializeField] private float stuckThreshold = 0.3f;

    [Header("Zone Movement")]
    [SerializeField] private float zoneRadius = 8f;
    [SerializeField] private float deviationChance = 0.25f;
    [SerializeField] private float deviationCheckInterval = 5f;
    [SerializeField] private float deviationMaxDistance = 25f;

    // Private state
    private int currentWaypointIndex;
    private bool hasArrived;
    private bool isWalking;
    private float pauseTimer;

    private float targetMoveSpeed;
    private float currentMoveSpeed;
    private float speedSmoothVelocity;
    private float nextSpeedChangeTime;
    private Vector3 currentMoveTarget;
    private bool isDeviating;
    private int deviatingToIndex;
    private float nextDeviationCheckTime;
    private float nextZoneDriftTime;

    private float nextPauseDistanceTraveled;
    private float distanceSinceLastPause;
    private float pauseIntervalDistance;

    private Vector3 stuckCheckPosition;
    private float stuckCheckTimer;

    // Override target (set by external systems like rescue/wait)
    private Vector3? overrideTarget;

    private Vector3 lastMoveDirection;

    // Public properties
    public bool HasArrived { get { return hasArrived; } }
    public bool IsWalking { get { return isWalking; } set { isWalking = value; } }
    public bool IsDeviating { get { return isDeviating; } }
    public float CurrentMoveSpeed { get { return currentMoveSpeed; } }
    public int CurrentWaypointIndex { get { return currentWaypointIndex; } }
    public WaypointPath WaypointPath { get { return waypointPath; } }
    public bool UseNavMesh { get { return navMeshReady && navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh; } }

    /// <summary>Whether movement is paused (not walking due to timer or external pause request).</summary>
    public bool IsPaused { get { return pauseTimer > 0f; } }

    // Events
    public System.Action OnArrived;

    private void Awake()
    {
        npc = GetComponent<NPCGoddess>();
        health = GetComponent<Health>();

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
            {
                navAgent.stoppingDistance = Mathf.Min(navAgent.stoppingDistance, 0.5f);
                navAgent.updatePosition = false;
                navAgent.updateRotation = false;
            }
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

    // ============================================
    // Public API
    // ============================================

    /// <summary>Pause walking for a set duration.</summary>
    public void SetPause(float duration)
    {
        pauseTimer = Mathf.Max(pauseTimer, duration);
    }

    /// <summary>Clear any active pause.</summary>
    public void ClearPause()
    {
        pauseTimer = 0f;
    }

    /// <summary>Override the movement target (used by rescue/wait/evasion).</summary>
    public void OverrideTarget(Vector3 target)
    {
        overrideTarget = target;
    }

    /// <summary>Clear the target override and return to normal waypoint movement.</summary>
    public void ClearOverrideTarget()
    {
        overrideTarget = null;
    }

    /// <summary>Horizontal (XZ-plane) distance to a point.</summary>

    /// <summary>Walk to the next waypoint. Returns false if arrived or cannot move.</summary>
    public bool StartWaypointWalk()
    {
        if (hasArrived || npc.IsDead) return false;
        if (waypointPath == null || waypointPath.WaypointCount == 0) return false;
        isWalking = true;
        return true;
    }

    public void StopWalking()
    {
        isWalking = false;
    }

    /// <summary>Estimate remaining journey time.</summary>
    public float EstimateJourneyTime()
    {
        if (waypointPath == null || waypointPath.WaypointCount < 2) return -1f;
        float dist = waypointPath.GetTotalLength();
        float avgSpeed = (minSpeed + maxSpeed) * 0.5f;
        float totalPauses = dist / pauseIntervalDistance;
        float avgPause = (pauseDurationMin + pauseDurationMax) * 0.5f;
        return (dist / avgSpeed) + totalPauses * avgPause;
    }

    // ============================================
    // Per-frame update - called from NPCGoddess
    // ============================================

    public void UpdateMovement(bool isHealing, bool isEvading, bool isRescuing, bool isWaiting,
        Transform nearestThreat, float evadeSpeedMultiplier, float evadeStrength,
        float rescueDetourOffset)
    {
        // Stop NavMeshAgent when paused or healing
        if (UseNavMesh && (pauseTimer > 0f || isHealing || !isWalking || npc.IsDead || hasArrived))
        {
            if (navAgent.hasPath) navAgent.ResetPath();
        }

        // Speed variation
        UpdateSpeedVariation();

        // Movement
        if (isWalking && !npc.IsDead && !hasArrived && !isHealing)
        {
            if (pauseTimer > 0f)
            {
                pauseTimer -= Time.deltaTime;
            }
            else
            {
                Vector3 target = Vector3.zero;
                // --- Use override target (set by behavior) or waypoint logic ---
                if (overrideTarget.HasValue)
                {
                    target = overrideTarget.Value;
                }
                // --- Target management: zone or deviation ---
                else if (isDeviating)
                {
                    target = currentMoveTarget;
                    float distToInterest = transform.position.DistanceXZ(target);
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
                        if (Time.time >= nextZoneDriftTime)
                            RefreshZoneDrift();
                        target = currentMoveTarget;

                        // Flee from nearest monster when evading
                        if (isEvading && nearestThreat != null)
                        {
                            Vector3 toThreat = (nearestThreat.position - transform.position).normalized;
                            toThreat.y = 0f;
                            Vector3 perp = Vector3.Cross(Vector3.up, transform.forward).normalized;
                            if (Vector3.Dot(perp, toThreat) > 0f) perp = -perp;
                            float dist = Vector3.Distance(transform.position, nearestThreat.position);
                            float weight = Mathf.Clamp01(evadeStrength * (1f - dist / (navMeshReady ? 8f : 8f))); // evadeTriggerDistance approximated
                            target = target + perp * 5f * weight;
                        }
                    }
                }

                float currentSpeed = currentMoveSpeed * (slowEffect != null ? slowEffect.SpeedMultiplier : 1f);
                if (isEvading || isRescuing) currentSpeed *= evadeSpeedMultiplier;

                if (UseNavMesh)
                {
                    navAgent.speed = currentSpeed;
                    navAgent.SetDestination(target);

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

                    navAgent.nextPosition = transform.position;

                    if (desiredVel.sqrMagnitude > 0.001f)
                    {
                        lastMoveDirection = new Vector3(desiredVel.x, 0f, desiredVel.z);
                        MovementUtility.FaceDirection(transform, lastMoveDirection, rotationSpeed, Time.deltaTime);
                    }
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
                    {
                        lastMoveDirection = moveDir;
                        MovementUtility.FaceDirection(transform, moveDir, rotationSpeed, Time.deltaTime);
                    }
                }

                // Waypoint arrival check (only during normal waypoint movement, not override/rescue/wait)
                if (!overrideTarget.HasValue && !isRescuing && !isWaiting && !isDeviating &&
                    transform.position.DistanceXZ(waypointPath.GetWaypoint(currentWaypointIndex)) <= zoneRadius)
                    OnWaypointReached();

                float movedThisFrame = currentSpeed * Time.deltaTime;
                if (UseNavMesh) movedThisFrame = Mathf.Max(navAgent.velocity.magnitude, 0.01f) * Time.deltaTime;
                distanceSinceLastPause += movedThisFrame;
                if (!isEvading && !isRescuing && !isWaiting) TrackPauseSchedule();
            }
        }

        // Stuck detection
        UpdateStuckCheck();

        // Update animation state on NPC (for NPCGoddess to read)
        UpdateAnimationFlags(isHealing, isEvading, isRescuing, isWaiting);
    }

    private bool movingForAnim;
    private void UpdateAnimationFlags(bool isHealing, bool isEvading, bool isRescuing, bool isWaiting)
    {
        movingForAnim = isWalking && !npc.IsDead && !hasArrived && !isHealing && pauseTimer <= 0f;
    }

    public bool MovingForAnim { get { return movingForAnim; } }

    // ============================================
    // Private movement methods
    // ============================================

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

        if (interestIndex <= currentWaypointIndex) return false;

        deviatingToIndex = interestIndex;
        currentMoveTarget = waypointPath.GetWaypoint(interestIndex);
        return true;
    }

    public bool HasReachedEndOfPath()
    {
        return waypointPath != null && waypointPath.HasReachedEnd(currentWaypointIndex);
    }

    private void UpdateSpeedVariation()
    {
        if (npc.IsDead) return;
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

    private bool TryArrive()
    {
        if (!waypointPath.HasReachedEnd(currentWaypointIndex)) return false;
        hasArrived = true;
        isWalking = false;
        health.EnableRegen = false;
        OnArrived?.Invoke();
        npc.NotifyArrived();
        return true;
    }

    /// <summary>Jump to a specific waypoint index (used by behavior after rescue).</summary>
    public void SetWaypointIndex(int index)
    {
        if (waypointPath == null) return;
        currentWaypointIndex = index;
        RefreshZoneDrift();
    }

    /// <summary>Get the nearest waypoint index to a position.</summary>
    public int GetNearestWaypointIndex(Vector3 pos)
    {
        if (waypointPath == null) return 0;
        return waypointPath.GetNearestWaypointIndex(pos);
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

    private void OnDrawGizmosSelected()
    {
        if (!hasArrived && !npc.IsDead && waypointPath != null && waypointPath.WaypointCount > 0)
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
