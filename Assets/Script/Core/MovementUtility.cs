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
}