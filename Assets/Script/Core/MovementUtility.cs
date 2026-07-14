/*
 * ============================================================
 *  MovementUtility  -  移动工具类（静态工具）
 * ============================================================
 *
 * 【功能】
 *   提供通用的移动辅助方法，目前只有面朝方向功能。
 *   供 DemonMinion、NPCGoddess、PlayerMove 调用，避免代码重复。
 *
 * 【方法】
 *   FaceDirection(transform, direction, rotationSpeed, deltaTime)
 *     - 让物体平滑转向指定方向（仅在XZ平面上旋转）
 */
using UnityEngine;

/// <summary>
/// Shared movement utilities to avoid code duplication.
/// </summary>
public static class MovementUtility
{
    /// <summary>
    /// Smoothly rotates a transform toward a direction on the XZ plane.
    /// </summary>
    public static void FaceDirection(Transform transform, Vector3 direction, float rotationSpeed, float deltaTime)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f) return;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction.normalized, Vector3.up),
            rotationSpeed * deltaTime);
    }
    /// <summary>
    /// Squared horizontal (XZ-plane) distance between two points (no sqrt).
    /// Use for comparisons to avoid expensive Mathf.Sqrt calls.
    /// </summary>
    public static float SqrDistanceXZ(this Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    /// <summary>
    /// Horizontal (XZ-plane) distance between two points (with sqrt).
    /// </summary>
    public static float DistanceXZ(this Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}