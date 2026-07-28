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
    [Header("Quest Item")]
    public GameObject questItemPrefab;

    [Header("Area Chain")]
    [Tooltip("按顺序排列的任务区域列表，从左到右依次推进")]
    [SerializeField] private List<QuestArea> areaChain = new List<QuestArea>();

    private int currentAreaIndex;
    private bool allAreasComplete;
    private GameObject currentItemInstance;
    private bool itemSpawned;
    private Vector3 currentItemPosition;
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

        // Activate area monsters
        var npc = FindFirstObjectByType<NPCGoddess>();
        if (npc != null) area.ActivateMonsters(npc);

        Debug.Log($"[QuestManager] Entered area: {area.AreaId} (index {currentAreaIndex})");
    }

    /// <summary>Mark the current area as complete and advance to the next.</summary>
    public void CompleteArea()
    {
        var area = CurrentArea;
        if (area == null) return;
        if (area.IsCompleted) return;

        area.IsCompleted = true;
        OnAreaComplete?.Invoke(area);
        Debug.Log($"[QuestManager] Completed area: {area.AreaId}");

        // Destroy current quest item before advancing
        DestroyCurrentAreaItem();

        // Spawn reward chest at NPC position
        SpawnChest();

        // Advance to next area
        currentAreaIndex++;

        // Check if all areas are done
        if (currentAreaIndex >= areaChain.Count)
        {
            allAreasComplete = true;
            DestroyCurrentAreaItem();
            // Stop revivals, keep alive monsters patrolling
            foreach (var a in areaChain)
                if (a != null) a.OnAllAreasComplete();
            OnAllAreasComplete?.Invoke();
            Debug.Log("[QuestManager] All quest areas completed!");
        }
        else
        {
            // Spawn next area quest item (visible before NPC arrives)
            SpawnCurrentAreaItem();
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
    /// Get the midpoint between the previous area and the current area.
    /// Used for rescue behavior - NPC moves back toward this point.
    /// </summary>
    public Vector3? GetRescueMidpoint()
    {
        if (currentAreaIndex <= 0 || currentAreaIndex >= areaChain.Count) return null;

        QuestArea previous = areaChain[currentAreaIndex - 1];
        QuestArea current = areaChain[currentAreaIndex];

        if (previous == null || current == null) return null;

        return (previous.Center + current.Center) * 0.5f;
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

    // ============================================
    // Quest Item Lifecycle
    // ============================================

    /// <summary>Spawn the quest item for the current area at a random spawn point.</summary>
    private void SpawnChest()
    {
        QuestArea area = CurrentArea;
        if (area == null) return;

        var npc = FindFirstObjectByType<NPCGoddess>();
        if (npc == null) return;

        foreach (var drop in area.ChestDrops)
        {
            if (drop.chestPrefab == null) continue;
            int count = Mathf.Max(1, drop.count);
            float radius = drop.spawnRadius;
            Vector3 center = area.Center;

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = SpawnPositionValidator.GetValidPosition(center, radius);
                GameObject chestObj = Instantiate(drop.chestPrefab, pos, Quaternion.identity);

                // Add BoxCollider for click interaction if missing
                if (chestObj.GetComponent<Collider>() == null)
                {
                    var bc = chestObj.AddComponent<BoxCollider>();
                    bc.size = new Vector3(1f, 1f, 1f);
                    bc.center = new Vector3(0f, 0.5f, 0f);
                }

                TreasureChest tc = chestObj.GetComponent<TreasureChest>();
                if (tc == null) tc = chestObj.AddComponent<TreasureChest>();
                if (tc != null) tc.Type = drop.chestType;
            }
        }
    }

    private void SpawnCurrentAreaItem()
    {
        Debug.Log("[QuestManager] SpawnCurrentAreaItem called, qp=" + (questItemPrefab != null) + " area=" + (CurrentArea != null));
        DestroyCurrentAreaItem();
        if (questItemPrefab == null) return;
        QuestArea area = CurrentArea;
        if (area == null) return;

        itemSpawned = true;
        currentItemPosition = area.GetRandomSpawnPoint();
        currentItemInstance = Instantiate(questItemPrefab, currentItemPosition, Quaternion.identity);
        currentItemInstance.transform.localScale = Vector3.one * 1.5f;

        var questLight = currentItemInstance.AddComponent<Light>();
        questLight.color = Color.yellow;
        questLight.range = 6f;
        questLight.intensity = 2.5f;
        questLight.shadows = LightShadows.None;
    }

    /// <summary>Destroy the current quest item visual.</summary>
    private void DestroyCurrentAreaItem()
    {
        if (currentItemInstance == null) return;
        Destroy(currentItemInstance);
        currentItemInstance = null;
        itemSpawned = false;
    }

    /// <summary>Get the current quest item's world position.</summary>
    public Vector3? GetCurrentItemPosition()
    {
        Debug.Log("[QuestManager] GetCurrentItemPosition: instance=" + (currentItemInstance != null) + " pos=" + currentItemPosition);
        if (currentItemInstance != null)
            return currentItemInstance.transform.position;
        if (itemSpawned)
            return currentItemPosition;
        return null;
    }
}

