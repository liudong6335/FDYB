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