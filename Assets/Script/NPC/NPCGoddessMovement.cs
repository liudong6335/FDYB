/* 
 * ============================================================
 *  NPCGoddessMovement  -  NPC 移动与区域导航（任务区域版）
 * ============================================================
 *
 * 【功能】
 *   管理 NPC 基于任务区域的行为状态机：
 *   - Idle → NavigateToArea → Searching → ReadyToCollect → InTransit
 *   - 支持急救覆写目标（OverrideTarget）、暂停、卡死检测、障碍回避
 *
 * 【挂载对象】
 *   NPC 对象（与 NPCGoddess 同对象）
 *
 * 【外部调用】
 *   UpdateMovement()   - 每帧调用，传入行为参数
 *   Start/StopWalking  - 控制移动开关
 *   OverrideTarget()   - 急救系统设置临时目标
 *   IsPaused / SetPause / ClearPause - 暂停控制
 *
 * 【状态机】
 *   Idle              - 待命，无目标区域
 *   NavigateToArea    - 导航至当前任务区域入口
 *   Searching         - 在区域内，朝随机刷出点移动
 *   ReadyToCollect    - 到达刷出点，等待采集系统完成
 *   InTransit         - 采集完成，正在前往下一区域
 *   AllComplete       - 所有区域已完成
 */
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public enum NPCQuestState
{
    Idle,
    NavigateToArea,
    Searching,
    ReadyToCollect,
    InTransit,
    AllComplete
}

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
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Speed")]
    [SerializeField] private float minSpeed = 1f;
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float speedChangeIntervalMin = 4f;
    [SerializeField] private float speedChangeIntervalMax = 8f;
    [SerializeField] private float speedSmoothTime = 0.8f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private float obstacleCheckDistance = 3f;
    [SerializeField] private float obstacleCheckRadius = 0.5f;
    [SerializeField] private float obstacleAvoidStrength = 2f;
    [SerializeField] private LayerMask obstacleLayers = -1;

    [Header("Stuck Detection")]
    [SerializeField] private float stuckCheckInterval = 3f;
    [SerializeField] private float stuckThreshold = 0.3f;

    [Header("Search Behavior")]
    [SerializeField] private float searchArriveDistance = 1.5f;
    [SerializeField] private float areaArriveDistance = 5f;

    // State
    private NPCQuestState currentState = NPCQuestState.Idle;
    private bool isWalking;
    private bool hasArrived;
    private bool isReadyToCollect;

    // Speed
    private float targetMoveSpeed;
    private float currentMoveSpeed;
    private float speedSmoothVelocity;
    private float nextSpeedChangeTime;

    // Navigation
    private Vector3 currentTargetPosition;
    private Vector3 searchTargetPosition;
    private bool hasSearchTarget;

    // Pause
    private float pauseTimer;

    // Override target (set by behavior/rescue)
    private Vector3? overrideTarget;

    // Stuck
    private Vector3 stuckCheckPosition;
    private float stuckCheckTimer;

    // Animation
    private bool movingForAnim;
    private Vector3 lastMoveDirection;

    // Events
    public System.Action OnReadyToCollect;

    // ========================================
    // Public Properties
    // ========================================

    public bool HasArrived { get { return hasArrived; } }
    public bool IsWalking { get { return isWalking; } set { isWalking = value; } }
    public float CurrentMoveSpeed { get { return currentMoveSpeed; } }
    public bool UseNavMesh { get { return navMeshReady && navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh; } }
    public bool IsPaused { get { return pauseTimer > 0f; } }
    public bool MovingForAnim { get { return movingForAnim; } }
    public NPCQuestState CurrentState { get { return currentState; } }
    public bool IsReadyToCollect { get { return isReadyToCollect; } }

    // ========================================
    // Unity Lifecycle
    // ========================================

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
                navAgent.stoppingDistance = 0.5f;
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

        // Auto-add QuestCollectionController if not present
        if (GetComponent<QuestCollectionController>() == null)
            gameObject.AddComponent<QuestCollectionController>();
    }

    private void Start()
    {
        currentMoveSpeed = Random.Range(minSpeed, maxSpeed);
        targetMoveSpeed = currentMoveSpeed;
        stuckCheckPosition = transform.position;
        stuckCheckTimer = stuckCheckInterval;
        currentState = NPCQuestState.NavigateToArea;
        isWalking = true;
    }

    // ============================================
    // Public API
    // ============================================

    public void SetPause(float duration)
    {
        pauseTimer = Mathf.Max(pauseTimer, duration);
    }

    public void ClearPause()
    {
        pauseTimer = 0f;
    }

    public void OverrideTarget(Vector3 target)
    {
        overrideTarget = target;
    }

    public void ClearOverrideTarget()
    {
        overrideTarget = null;
    }

    public void StopWalking()
    {
        isWalking = false;
    }

    /// <summary>Signal that collection is complete and NPC should resume moving.</summary>
    public void NotifyCollectionComplete()
    {
        isReadyToCollect = false;
        if (QuestManager.Instance != null)
            QuestManager.Instance.CompleteArea();
        SetQuestState(NPCQuestState.InTransit);
    }

    /// <summary>Directly set the quest state (used by external systems).</summary>
    public void SetQuestState(NPCQuestState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
    }

    /// <summary>Estimate remaining journey time (simplified for area system).</summary>
    public float EstimateJourneyTime()
    {
        if (QuestManager.Instance == null) return -1f;
        int remaining = QuestManager.Instance.TotalAreaCount - QuestManager.Instance.CurrentAreaIndex;
        return remaining * 60f; // rough estimate: 60s per area
    }

    // ============================================
    // Per-frame update - called from NPCGoddess
    // ============================================

    public void UpdateMovement(bool isHealing, bool isEvading, bool isRescuing, bool isWaiting,
        Transform nearestThreat, float evadeSpeedMultiplier, float evadeStrength,
        float rescueDetourOffset)
    {
        // Update state machine
        if (!npc.IsDead && !hasArrived && isWalking)
        {
            UpdateQuestState();
        }

        // Stop NavMeshAgent when paused/healing/dead/arrived
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
                // Determine target position
                Vector3 targetPos = GetCurrentTarget();

                if (overrideTarget.HasValue)
                    targetPos = overrideTarget.Value;

                // Move toward target
                MoveToward(targetPos, isEvading, isRescuing, nearestThreat, evadeSpeedMultiplier, evadeStrength);
            }
        }

        // Stuck detection
        UpdateStuckCheck();

        // Animation flags
        UpdateAnimationFlags(isHealing, isEvading, isRescuing, isWaiting);
    }

    // ============================================
    // Quest State Machine
    // ============================================

    private void UpdateQuestState()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;

        switch (currentState)
        {
            case NPCQuestState.Idle:
                if (qm.CurrentArea != null)
                    currentState = NPCQuestState.NavigateToArea;
                break;

            case NPCQuestState.NavigateToArea:
                // NPC always navigates to the area entry point first
                if (qm.CurrentArea != null)
                {
                    Vector3 entryPos = qm.CurrentArea.GetEntryPosition();
                    float sqrDistToEntry = transform.position.SqrDistanceXZ(entryPos);
                    // Only transition to Searching when reaching the entry point
                    if (sqrDistToEntry <= searchArriveDistance * searchArriveDistance)
                    {
                        qm.EnterCurrentArea();
                        if (qm.CurrentArea != null && qm.CurrentAreaIndex > 0)
                        {
                            currentState = NPCQuestState.Searching;
                            hasSearchTarget = false;
                            Debug.Log($"[NPCGoddessMovement] Entering quest area (requires item): {qm.CurrentArea.AreaId}");
                        }
                        else
                        {
                            qm.CompleteArea();
                            currentState = NPCQuestState.InTransit;
                        }
                    }
                }
                else
                {
                    currentState = NPCQuestState.AllComplete;
                }
                break;

            case NPCQuestState.Searching:
                if (!hasSearchTarget && qm.CurrentArea != null)
                {
                    // Pick a random spawn point within the area
                    searchTargetPosition = qm.GetCurrentAreaSpawnPoint();
                    hasSearchTarget = true;

                    // Spawn quest item visual at the search point
                    var collector = GetComponent<QuestCollectionController>();
                    if (collector != null)
                        collector.SpawnQuestItemAt(searchTargetPosition);
                }

                // Check if arrived at search target
                if (hasSearchTarget)
                {
                    float sqrDist = transform.position.SqrDistanceXZ(searchTargetPosition);
                    if (sqrDist <= searchArriveDistance * searchArriveDistance)
                    {
                        // Arrived at search position - ready to collect
                        currentState = NPCQuestState.ReadyToCollect;
                        isReadyToCollect = true;
                        Debug.Log("[NPCGoddessMovement] Ready to collect at spawn point");
                        OnReadyToCollect?.Invoke();
                    }
                }
                break;

            case NPCQuestState.ReadyToCollect:
                // Wait for collection system to call NotifyCollectionComplete()
                // NPC stops moving in this state
                break;

            case NPCQuestState.InTransit:
                // Navigate to next area entry point
                if (qm.CurrentArea != null)
                {
                    Vector3 entryPos = qm.CurrentArea.GetEntryPosition();
                    float sqrDistToEntry = transform.position.SqrDistanceXZ(entryPos);
                    if (sqrDistToEntry <= searchArriveDistance * searchArriveDistance)
                    {
                        qm.EnterCurrentArea();
                        if (qm.CurrentAreaIndex > 0)
                        {
                            currentState = NPCQuestState.Searching;
                            hasSearchTarget = false;
                            Debug.Log($"[NPCGoddessMovement] Entering quest area: {qm.CurrentArea.AreaId}");
                        }
                        else
                        {
                            qm.CompleteArea();
                            currentState = NPCQuestState.InTransit;
                        }
                    }
                }
                else
                {
                    currentState = NPCQuestState.AllComplete;
                    Debug.Log("[NPCGoddessMovement] All quest areas complete!");
                }
                break;

            case NPCQuestState.AllComplete:
                hasArrived = true;
                isWalking = false;
                health.EnableRegen = false;
                break;
        }
    }

    // ============================================
    // Target Determination
    // ============================================

    private Vector3 GetCurrentTarget()
    {
        var qm = QuestManager.Instance;

        switch (currentState)
        {
            case NPCQuestState.NavigateToArea:
                if (qm != null && qm.CurrentArea != null)
                    return qm.CurrentArea.GetEntryPosition();
                break;

            case NPCQuestState.Searching:
                if (hasSearchTarget)
                    return searchTargetPosition;
                break;

            case NPCQuestState.InTransit:
                if (qm != null)
                {
                    var nextTarget = qm.GetNextTargetPosition();
                    if (nextTarget.HasValue)
                        return nextTarget.Value;
                }
                break;

            case NPCQuestState.ReadyToCollect:
                // Stay in place during collection
                return transform.position;
        }

        return transform.position;
    }

    // ============================================
    // Movement
    // ============================================

    private void MoveToward(Vector3 targetPos, bool isEvading, bool isRescuing,
        Transform nearestThreat, float evadeSpeedMultiplier, float evadeStrength)
    {
        if (currentState == NPCQuestState.ReadyToCollect)
        {
            // Don't move during collection
            if (UseNavMesh && navAgent.hasPath) navAgent.ResetPath();
            return;
        }

        float currentSpeed = currentMoveSpeed * (slowEffect != null ? slowEffect.SpeedMultiplier : 1f);
        if (isEvading || isRescuing) currentSpeed *= evadeSpeedMultiplier;

        // Flee from monster when evading
        Vector3 moveTarget = targetPos;
        if (isEvading && nearestThreat != null)
        {
            Vector3 toThreat = (nearestThreat.position - transform.position).normalized;
            toThreat.y = 0f;
            Vector3 perp = Vector3.Cross(Vector3.up, transform.forward).normalized;
            if (Vector3.Dot(perp, toThreat) > 0f) perp = -perp;
            float dist = Vector3.Distance(transform.position, nearestThreat.position);
            float weight = Mathf.Clamp01(evadeStrength * (1f - dist / 8f));
            moveTarget = moveTarget + perp * 5f * weight;
        }

        if (UseNavMesh)
        {
            navAgent.speed = currentSpeed;
            navAgent.SetDestination(moveTarget);

            Vector3 desiredVel = navAgent.pathPending ? Vector3.zero : navAgent.desiredVelocity;
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
            Vector3 toTarget = moveTarget - transform.position;
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
    }

    // ============================================
    // Speed Variation
    // ============================================

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

    // ============================================
    // Obstacle Avoidance
    // ============================================

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

    // ============================================
    // Stuck Detection
    // ============================================

    private void UpdateStuckCheck()
    {
        stuckCheckTimer -= Time.deltaTime;
        if (stuckCheckTimer <= 0f)
        {
            float moved = Vector3.Distance(transform.position, stuckCheckPosition);
            if (moved < stuckThreshold && pauseTimer <= 0f && currentState != NPCQuestState.ReadyToCollect)
            {
                // If stuck during search, pick a new search target
                if (currentState == NPCQuestState.Searching)
                {
                    hasSearchTarget = false;
                    Debug.Log("[NPCGoddessMovement] Stuck while searching, picking new target");
                }
                // Small pause to let the NavMesh catch up
                pauseTimer = 1f;
            }
            stuckCheckPosition = transform.position;
            stuckCheckTimer = stuckCheckInterval;
        }
    }

    // ============================================
    // Animation
    // ============================================

    private void UpdateAnimationFlags(bool isHealing, bool isEvading, bool isRescuing, bool isWaiting)
    {
        movingForAnim = isWalking && !npc.IsDead && !hasArrived && !isHealing && pauseTimer <= 0f
            && currentState != NPCQuestState.ReadyToCollect;
    }

    // ============================================
    // Gizmos
    // ============================================

    private void OnDrawGizmosSelected()
    {
        if (npc == null || npc.IsDead || currentState == NPCQuestState.Idle || currentState == NPCQuestState.AllComplete)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentTargetPosition, 0.4f);
            Gizmos.DrawLine(transform.position, currentTargetPosition);
        }

        if (currentState == NPCQuestState.Searching && hasSearchTarget)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(searchTargetPosition, 0.3f);
            Gizmos.DrawLine(transform.position, searchTargetPosition);
        }

        Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 fwd = transform.forward * obstacleCheckDistance;
        Gizmos.DrawWireSphere(origin + fwd, obstacleCheckRadius);
    }
}
