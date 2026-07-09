/*
 * ============================================================
 *  WaypointPath  -  路径点系统
 * ============================================================
 *
 * 【功能】
 *   定义一组路径点，供 NPCGoddess 沿着行走。
 *   支持循环路径和非循环路径。
 *   在场景视图中会绘制路径线方便编辑。
 *
 * 【挂载对象】
 *   场景中的空对象，作为路径点的容器
 *
 * 【可调节参数】
 *   waypoints       - 路径点列表（在 Inspector 中编辑坐标）
 *   lineColor       - 场景视图中的路径线颜色
 *   waypointRadius  - 路径点的显示半径
 *   loopPath        - 是否循环（走完回到起点）
 *   arriveDistance  - 判定到达路径点的距离
 *
 * 【说明】
 *   路径点是相对于当前物体的局部坐标，移动物体整体路径会跟着动
 */
using UnityEngine;
using System.Collections.Generic;

public class WaypointPath : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private Color lineColor = Color.cyan;
    [SerializeField] private float waypointRadius = 0.5f;
    [SerializeField] private bool loopPath = false;
    [SerializeField] private float arriveDistance = 1f;
    [SerializeField] private List<Vector3> waypoints = new List<Vector3>();

    [Header("Interest Points (Weighted Random)")]
    [Tooltip("Per-waypoint exploration weight (0~1). Higher = more likely to be picked.")]
    [SerializeField] private List<float> exploreWeights = new List<float>();
    [Tooltip("Minimum distance from current position to consider a waypoint as a candidate.")]
    [SerializeField] private float minInterestDist = 8f;

    public int WaypointCount { get { return waypoints.Count; } }
    public bool LoopPath { get { return loopPath; } }
    public float ArriveDistance { get { return arriveDistance; } }
    public float MinInterestDist { get { return minInterestDist; } }

    public float GetExploreWeight(int index)
    {
        if (index < 0 || index >= exploreWeights.Count) return 1f;
        return Mathf.Clamp01(exploreWeights[index]);
    }

    public Vector3 GetWaypoint(int index)
    {
        if (waypoints.Count == 0) return transform.position;
        index = Mathf.Clamp(index, 0, waypoints.Count - 1);
        return transform.TransformPoint(waypoints[index]);
    }

    public int GetNextIndex(int index)
    {
        int next = index + 1;
        if (loopPath && next >= waypoints.Count) return 0;
        return next;
    }

    public bool HasReachedEnd(int index)
    {
        return !loopPath && index >= waypoints.Count;
    }

    public int GetNearestWaypointIndex(Vector3 position)
    {
        if (waypoints.Count == 0) return 0;
        int nearest = 0;
        float minDist = float.MaxValue;
        for (int i = 0; i < waypoints.Count; i++)
        {
            float dist = Vector3.Distance(position, GetWaypoint(i));
            if (dist < minDist) { minDist = dist; nearest = i; }
        }
        return nearest;
    }

    public float GetTotalLength()
    {
        float total = 0f;
        for (int i = 0; i < waypoints.Count - 1; i++)
            total += Vector3.Distance(GetWaypoint(i), GetWaypoint(i + 1));
        return total;
    }

    /// <summary>Pick a weighted-random interest point as the next destination.</summary>
    /// <param name="currentIndex">Current waypoint index to exclude.</param>
    /// <param name="currentPosition">NPC current world position.</param>
    /// <param name="progress01">Journey progress 0~1 (furthest visited / total).</param>
    /// <param name="forwardBias">0~1, higher = more likely to pick points closer to the end.</param>
    public int PickWeightedInterest(int currentIndex, Vector3 currentPosition, float progress01, float forwardBias)
    {
        if (waypoints.Count == 0) return 0;
        var candidates = new System.Collections.Generic.List<(int index, float score)>();
        float totalProgress = (waypoints.Count > 1) ? 1f / (waypoints.Count - 1) : 1f;

        for (int i = 0; i < waypoints.Count; i++)
        {
            // Never pick current index
            if (i == currentIndex) continue;

            Vector3 wp = GetWaypoint(i);
            float dist = Vector3.Distance(currentPosition, wp);

            // Too close to current position
            if (dist < minInterestDist) continue;

            float weight = GetExploreWeight(i);
            // Base score from explore weight
            float score = weight * 0.6f;
            // Forward bias: prefer points further along the path
            float pointProgress = i * totalProgress;
            score += pointProgress * forwardBias * 0.4f;
            // Random jitter for variety
            score *= Random.Range(0.7f, 1.3f);

            candidates.Add((i, Mathf.Max(score, 0.01f)));
        }

        if (candidates.Count == 0)
        {
            // Fallback: just go to the next index
            int next = currentIndex + 1;
            if (next >= waypoints.Count) next = waypoints.Count - 1;
            return next;
        }

        // Weighted random selection
        float total = 0f;
        foreach (var c in candidates) total += c.score;
        float r = Random.value * total;
        float accum = 0f;
        int selected = candidates[0].index;
        foreach (var c in candidates)
        {
            accum += c.score;
            if (r <= accum) { selected = c.index; break; }
        }
        return selected;
    }

    private void OnDrawGizmos()
    {
        if (waypoints.Count == 0) return;
        Gizmos.color = lineColor;
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 wp = GetWaypoint(i);
            Gizmos.DrawWireSphere(wp, waypointRadius);
            if (i < waypoints.Count - 1)
            {
                Vector3 next = GetWaypoint(i + 1);
                Gizmos.DrawLine(wp, next);
            }
        }
    }
}