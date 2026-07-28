using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 挂到任意刷出点或物品预制体上，确保生成在可走的 NavMesh 表面。
/// Start() 时自动调整位置，避开障碍物。
/// 
/// 两种用法：
///   1. 挂到预制体 → 自动生效
///   2. SpawnPositionValidator.GetValidPosition(center, radius) → 代码调用
/// </summary>
public class SpawnPositionValidator : MonoBehaviour
{
    [Header("Validation")]
    [SerializeField] private float searchRadius = 2f;
    [SerializeField] private int maxAttempts = 5;
    [SerializeField] private bool validateOnStart = true;

    private void Start()
    {
        if (validateOnStart)
            SnapToValidPosition();
    }

    /// <summary>在 Start() 时将自身位置矫正到 NavMesh 上。</summary>
    public bool SnapToValidPosition()
    {
        Vector3 pos = GetValidPosition(transform.position, searchRadius, maxAttempts, searchRadius);
        if (pos != transform.position)
        {
            transform.position = pos;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 静态方法：在 center 周围 radius 范围内取一个 NavMesh 上的合法位置。
    /// 可用于代码动态生成物品。
    /// </summary>
    public static Vector3 GetValidPosition(Vector3 center, float radius, int maxAttempts = 5, float navMeshTolerance = 2f)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshTolerance, NavMesh.AllAreas))
                return hit.position;
        }

        // Final fallback: sample from center with larger tolerance
        if (NavMesh.SamplePosition(center, out NavMeshHit centerHit, navMeshTolerance * 2f, NavMesh.AllAreas))
            return centerHit.position;

        return center;
    }

    // ============================================
    // Editor Gizmo: 显示搜索范围
    // ============================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, searchRadius);
    }
}
