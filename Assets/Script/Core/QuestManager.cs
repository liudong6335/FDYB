/* 
 * ============================================================
 *  QuestManager  -  任务区域链管理器（单例）
 * ============================================================
 *
 * 【功能】
 *   管理游戏中的任务区域链：按顺序推进区域、追踪完成状态。
 *   提供区域进入/完成事件，供 NPC 行为、怪物系统、宝箱系统调用。
 *   所有区域完成后触发 OnAllAreasComplete 事件（可用于切换到 Boss 阶段）。
 *
 * 【挂载对象】
 *   场景中的单例对象，DontDestroyOnLoad
 *
 * 【外部调用】
 *   QuestManager.Instance.GetCurrentArea()    - 获取当前区域
 *   QuestManager.Instance.CompleteArea()      - 标记当前区域完成
 *   QuestManager.Instance.GetNextTargetPos()  - 获取 NPC 下一个目标位置
 *   QuestManager.Instance.IsAllAreasDone()    - 是否所有区域已完成
 *
 * 【事件】
 *   OnAreaEnter(QuestArea)     - NPC 进入新区域时触发
 *   OnAreaComplete(QuestArea)  - 区域完成时触发
 *   OnAllAreasComplete()       - 所有区域完成时触发
 */
using UnityEngine;
using System.Collections.Generic;

public class QuestManager : MonoBehaviour
{
    [Header("Area Chain")]
    [Tooltip("按顺序排列的任务区域列表，从左到右依次推进")]
    [SerializeField] private List<QuestArea> areaChain = new List<QuestArea>();

    private int currentAreaIndex;
    private bool allAreasComplete;
    private static QuestManager instance;

    // ========================================
    // Singleton
    // ========================================

    public static QuestManager Instance { get { return instance; } }

    private void Awake()
    {
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        currentAreaIndex = 0;
        allAreasComplete = false;
    }

    // ========================================
    // Events
    // ========================================

    public System.Action<QuestArea> OnAreaEnter;
    public System.Action<QuestArea> OnAreaComplete;
    public System.Action OnAllAreasComplete;

    // ========================================
    // Public Properties
    // ========================================

    public int TotalAreaCount { get { return areaChain.Count; } }
    public int CurrentAreaIndex { get { return currentAreaIndex; } }
    public bool AllAreasComplete { get { return allAreasComplete; } }

    /// <summary>Current quest area, or null if chain is empty or complete.</summary>
    public QuestArea CurrentArea
    {
        get
        {
            if (allAreasComplete || currentAreaIndex >= areaChain.Count) return null;
            return areaChain.Count > 0 ? areaChain[currentAreaIndex] : null;
        }
    }

    /// <summary>List of all quest areas (read-only for external query).</summary>
    public IReadOnlyList<QuestArea> AreaChain { get { return areaChain.AsReadOnly(); } }

    // ========================================
    // Public Methods
    // ========================================

    /// <summary>Signal that NPC has entered the current quest area.</summary>
    public void EnterCurrentArea()
    {
        var area = CurrentArea;
        if (area == null) return;

        area.IsCompleted = false;
        OnAreaEnter?.Invoke(area);
        Debug.Log($"[QuestManager] Entered area: {area.AreaId} (index {currentAreaIndex})");
    }

    /// <summary>Mark the current area as complete and advance to the next.</summary>
    public void CompleteArea()
    {
        var area = CurrentArea;
        if (area == null) return;

        area.IsCompleted = true;
        OnAreaComplete?.Invoke(area);
        Debug.Log($"[QuestManager] Completed area: {area.AreaId}");

        // Advance to next area
        currentAreaIndex++;

        // Check if all areas are done
        if (currentAreaIndex >= areaChain.Count)
        {
            allAreasComplete = true;
            OnAllAreasComplete?.Invoke();
            Debug.Log("[QuestManager] All quest areas completed!");
        }
    }

    /// <summary>
    /// Get the NPC's next target position.
    /// - If current area exists: navigate to area entry
    /// - If all areas done: return null (Boss phase handles it)
    /// </summary>
    public Vector3? GetNextTargetPosition()
    {
        var area = CurrentArea;
        if (area == null) return null;
        return area.GetEntryPosition();
    }

    /// <summary>
    /// Get a random spawn point in the current area for the quest item.
    /// </summary>
    public Vector3 GetCurrentAreaSpawnPoint()
    {
        var area = CurrentArea;
        if (area == null) return Vector3.zero;
        return area.GetRandomSpawnPoint();
    }

    /// <summary>Check if a position is within the current quest area.</summary>
    public bool IsWithinCurrentArea(Vector3 position)
    {
        var area = CurrentArea;
        if (area == null) return false;
        return area.IsWithinArea(position);
    }

    /// <summary>
    /// Get the midpoint between the current area and the next area.
    /// Used for rescue behavior during transit.
    /// </summary>
    public Vector3? GetMidpointToNextArea()
    {
        if (currentAreaIndex + 1 >= areaChain.Count) return null;

        QuestArea current = areaChain.Count > 0 ? areaChain[currentAreaIndex] : null;
        QuestArea next = areaChain[currentAreaIndex + 1];

        if (current == null || next == null) return null;

        return (current.Center + next.Center) * 0.5f;
    }

    /// <summary>
    /// Check if the NPC is currently in transit (between areas, not yet entered target).
    /// </summary>
    public bool IsInTransit(Vector3 npcPosition)
    {
        var area = CurrentArea;
        if (area == null) return false;
        return !area.IsWithinArea(npcPosition);
    }

    /// <summary>Reset the entire area chain (for restart).</summary>
    public void ResetChain()
    {
        currentAreaIndex = 0;
        allAreasComplete = false;
        foreach (var area in areaChain)
        {
            if (area != null) area.IsCompleted = false;
        }
        Debug.Log("[QuestManager] Area chain reset");
    }

    // ========================================
    // Gizmos (show area chain connections)
    // ========================================

    private void OnDrawGizmos()
    {
        if (areaChain.Count < 2) return;
        Gizmos.color = Color.white;
        for (int i = 0; i < areaChain.Count - 1; i++)
        {
            if (areaChain[i] != null && areaChain[i + 1] != null)
            {
                Vector3 from = areaChain[i].Center;
                Vector3 to = areaChain[i + 1].Center;
                Gizmos.DrawLine(from, to);
                // Arrow
                Vector3 dir = (to - from).normalized;
                Vector3 mid = (from + to) * 0.5f;
                Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;
                Gizmos.DrawLine(mid + dir * 0.5f + perp * 0.3f, mid + dir * 0.5f);
                Gizmos.DrawLine(mid + dir * 0.5f - perp * 0.3f, mid + dir * 0.5f);
            }
        }
    }
}
