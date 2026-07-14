using UnityEngine;

/// <summary>
/// NPC Ů���״̬��Ϊģ�� ���� ������ʲôʱ����ʲô����
///
/// ����ְ��3 ����Ϊ״̬����
///   1. ��ܹ��Monster Evasion��
///      ǰ���й���ƽ�ʱ��֪ͨ Movement ����ý�ɫ�������ܡ�
///      ���ܹ��������������ͣ����ȡ����ͣ��
///
///   2. ��Ԯ���ѣ�Rescue��
///      ������Ѫ��������ֵʱ���л�����Ԯ״̬��
///      ͨ�� OverrideTarget() �� Movement ������ѣ�
///      �����ͣ��һ��ʱ�䣬�ٻص�·����
///
///   3. �ȴ���Wait��
///      ���ж����뿪��ⷶΧ������ʱʱ���
///      ��С��Χ�����Ѳ�ߵȴ���ֱ�����ѷ��ء�
///
/// ÿ֡ UpdateBehavior() �� NPCGoddess.Update() ���ã�
/// ͨ�� Movement.OverrideTarget() / ClearOverrideTarget() / ClearPause()
/// ���Ӱ���ƶ�����ֱ�Ӳٿ� Transform��
/// </summary>
[RequireComponent(typeof(NPCGoddess), typeof(NPCGoddessMovement))]
public class NPCGoddessBehavior : MonoBehaviour
{
    private NPCGoddess npc;
    private NPCGoddessMovement movement;

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

    // ���״̬
    private bool isEvading;
    private Transform nearestThreat;

    // ��Ԯ��ȴ�״̬
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

    // �������� ���� �� Movement ����� UpdateMovement() �ж�ȡ
    public bool IsEvading { get { return isEvading; } }
    public bool IsRescuing { get { return isRescuing; } }
    public bool IsWaiting { get { return isWaiting; } }
    public Transform NearestThreat { get { return nearestThreat; } }
    public float EvadeSpeedMultiplier { get { return evadeSpeedMultiplier; } }
    public float EvadeStrength { get { return evadeStrength; } }
    public float RescueDetourOffset { get { return rescueDetourOffset; } }

    private void Awake()
    {
        npc = GetComponent<NPCGoddess>();
        movement = GetComponent<NPCGoddessMovement>();
    }

    // ============================================
    // ÿִ֡�� ���� �� NPCGoddess.Update() ����
    // ˳�򣺶�ܼ�� �� ״̬���л� �� �����ƶ�Ŀ��
    // ============================================

    public void UpdateBehavior()
    {
        // 1. ��ܼ�⣺ǰ���й������
        UpdateMonsterEvasion();
        if (isEvading && movement.IsPaused)
            movement.ClearPause();

        // 2. ״̬���л������� ? ��Ԯ ? �ȴ�
        //    UpdateNPCState ����״̬ת����
        // Note: UpdateNPCState transitions states. Movement code
        // reads IsRescuing/IsWaiting to override targets.
        UpdateNPCState();

        // 3. �����߽��д�� Movement
        //    ��Ԯ / �ȴ� �Ḳ������·��Ŀ��
        if (isRescuing && rescueTarget != null)
        {
            movement.OverrideTarget(GetRescueTarget(rescueTarget.position));
        }
        else if (isWaiting)
        {
            if (transform.position.DistanceXZ(waitPatrolTarget) <= 1f)
                waitPatrolTarget = GetWaitPatrolTarget();
            movement.OverrideTarget(waitPatrolTarget);
        }
        else
        {
            movement.ClearOverrideTarget();
        }
    }

    // ============================================
    // ������
    // ɨ��ǰ����forward ���򣩵Ĺ���������в���봥��������ʼ���ܡ�
    // ============================================

    private void UpdateMonsterEvasion()
    {
        if (npc.IsDead || npc.HasArrived) { isEvading = false; return; }

        nearestThreat = null;
        float nearestSqr = dangerDetectRadius * dangerDetectRadius;

        foreach (var demon in DemonMinion.AllDemons)
        {
            if (demon == null || demon.IsDead) continue;
            Vector3 toMonster = demon.transform.position - transform.position;
            toMonster.y = 0f;
            if (Vector3.Dot(toMonster.normalized, transform.forward) < 0f) continue;
            float sqr = transform.position.SqrDistanceXZ(demon.transform.position);
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

    // ============================================
    // ��Ԯ��ȴ� ���� ��̬״̬��
    // ============================================

    private Transform FindRescueTarget()
    {
        Transform best = null;
        float lowestHP = rescueHpThreshold;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null || p.IsDead) continue;
            float hp = p.HealthPercent;
            if (hp >= rescueHpThreshold) continue;
            float dist = transform.position.DistanceXZ(p.transform.position);
            bool emergency = hp < emergencyHpThreshold;
            bool inRange = dist <= rescueDetectRange;
            if ((inRange || emergency) && hp < lowestHP)
            {
                lowestHP = hp;
                best = p.transform;
            }
        }
        if (best != null)
        {
            Vector3 toTarget = best.position - transform.position;
            toTarget.y = 0f;
            bool isBehind = Vector3.Dot(toTarget.normalized, transform.forward) < 0f;
            if (isBehind && toTarget.magnitude > maxBacktrackDistance)
                return null;
        }
        return best;
    }

    /// <summary>��ⷶΧ���Ƿ����κζ��Ѵ��ڡ�</summary>
        private bool IsAnyTeammateWithin(float range)
    {
        if (IsRescuingTargetInRange()) return true;
        float sqr = range * range;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null || p.IsDead) continue;
            float sqrDist2 = transform.position.SqrDistanceXZ(p.transform.position);
            if (sqrDist2 < sqr) return true;
        }
        return false;
    }

    /// <summary>��ǰ��ԮĿ���Ƿ����ڼ�ⷶΧ�ڡ�</summary>
        private bool IsRescuingTargetInRange()
    {
        return rescueTarget != null && transform.position.DistanceXZ(rescueTarget.position) <= rescueDetectRange;
    }

    /// <summary>�����ԮĿ��λ�ã����й����ڸ������Ƶ����棬����ֱ�����ѡ�</summary>
        private Vector3 GetRescueTarget(Vector3 teammatePos)
    {
        Transform threat = null;
        float threatSqr = dangerDetectRadius * dangerDetectRadius;
        foreach (var demon in DemonMinion.AllDemons)
        {
            if (demon == null || demon.IsDead) continue;
            float sqr = transform.position.SqrDistanceXZ(demon.transform.position);
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

    /// <summary>���ɵȴ�ģʽ�е����Ѳ��Ŀ��㡣</summary>
        private Vector3 GetWaitPatrolTarget()
    {
        Vector2 rand = Random.insideUnitCircle * waitPatrolRadius;
        return transform.position + new Vector3(rand.x, 0f, rand.y);
    }

    /// <summary>״̬�����ģ����ֶ������ˡ���Ԯ�������뿪���ȴ�Ѳ�ߣ����ѷ��ء��������ߡ�</summary>
        private void UpdateNPCState()
    {
        if (npc.IsDead || npc.HasArrived || npc.IsHealing) return;

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
            movement.ClearPause();
            return;
        }

        if (isRescuing)
        {
            if (rescueTarget != null && transform.position.DistanceXZ(rescueTarget.position) <= rescueArriveDistance)
            {
                rescueStayTimer += Time.deltaTime;
                if (rescueStayTimer >= rescueStayTime)
                {
                    isRescuing = false;
                    rescueTarget = null;
                    rescueStayTimer = 0f;
                    rescueCooldownTimer = rescueCooldownTime;
                    rescueForwardTimer = rescueForwardTime;
                    if (movement.WaypointPath != null)
                    {
                        int nearest = movement.GetNearestWaypointIndex(transform.position);
                        int next = movement.WaypointPath.GetNextIndex(nearest);
                        movement.SetWaypointIndex(next > nearest ? next : nearest);
                    }
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
            return;
        }

        if (isWaiting)
        {
            if (IsAnyTeammateWithin(rescueDetectRange))
                waitTimer = 0f;
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

        if (waitCooldownTimer > 0f) waitCooldownTimer -= Time.deltaTime;
        if (rescueCooldownTimer > 0f) rescueCooldownTimer -= Time.deltaTime;
        if (rescueForwardTimer > 0f) rescueForwardTimer -= Time.deltaTime;
    }
}
