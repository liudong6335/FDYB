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

    public int WaypointCount { get { return waypoints.Count; } }
    public bool LoopPath { get { return loopPath; } }
    public float ArriveDistance { get { return arriveDistance; } }

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
