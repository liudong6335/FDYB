/* 
    /// <summary>
    /// Auto-discover spawn point children in Awake.
    /// Any child GameObject with "SpawnPoint" in its name is added to the list.
    /// Falls back to random position if no children found.
    /// </summary>
    private void Awake()
    {
        RefreshSpawnPoints();
    }

    /// <summary>Scan children for spawn point references to populate the list.</summary>
    public void RefreshSpawnPoints()
    {
        spawnPoints.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("SpawnPoint"))
                spawnPoints.Add(child);
        }
    }
 * ============================================================
 *  QuestArea  -  任务区域配置组件
 * ============================================================
 *
 * 【功能】
 *   定义游戏中的一个任务区域：位置、半径、任务物品刷出点、怪物等级。
 *   挂在场景中的 GameObject 上，Scene 视图用 Gizmos 绘制半径范围。
 *   数据由 QuestManager 读取和管理。
 *
 * 【挂载对象】
 *   场景中的空对象，作为区域的容器
 *
 * 【可调节参数】
 *   areaId            - 区域 ID（唯一标识）
 *   radius            - 区域半径（影响搜索耗时和节奏）
 *   spawnPoints       - 任务物品随机刷出点列表
 *   monsterLevel      - 区域内怪物等级
 *   monsterCount      - 区域内怪物数量
 *   entryOffset       - NPC 到达区域时的停留偏移点（可选，默认区域边缘）
 *
 * 【方法】
 *   IsWithinArea(position) - 判断位置是否在区域内
 *   GetRandomSpawnPoint()  - 随机选一个刷出点
 *   GetEntryPosition()     - 获取 NPC 进入区域时的目标位置
 */
using UnityEngine;
using System.Collections.Generic;


[System.Serializable]
public class ChestDropConfig
{
    [Tooltip("宝箱掉落品质")]
    public ChestType chestType = ChestType.Wooden;
    [Tooltip("宝箱预制体")]
    public GameObject chestPrefab;
    [Tooltip("数量")]
    public int count = 1;
    [Tooltip("散布范围")]
    public float spawnRadius = 3f;
}


public class QuestArea : MonoBehaviour
{
    [Header("Area")]
    [Tooltip("是否需要任务物品搜索（false = 过渡区域，NPC 直接通过不做搜索）")]
    [SerializeField] private bool requiresItem = true;
    [SerializeField] private string areaId;
    [SerializeField] private float radius = 15f;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 0.4f, 0.25f);

    [Header("Item Spawn Points")]
    [Tooltip("任务物品可能的刷出位置，运行时随机选一个")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    [Header("Monsters")]
    [Tooltip("该区域刷新的怪物预制体")]
    public GameObject monsterPrefab;
    [SerializeField] private int monsterLevel = 1;
    [SerializeField] private int monsterCount = 3;
    [SerializeField] private float demonReviveTime = 10f;
    private NPCGoddess cachedNpc;
    private List<GameObject> spawnedMonsters = new List<GameObject>();

    private void OnDestroy()
    {
        DeactivateMonsters();
    }

    [Header("NPC Entry")]
    [Tooltip("NPC 到达区域时的目标停留点（留空则自动选区域边缘靠近下一区域的方向）")]
    [SerializeField] private Transform entryPoint;

    [Header("Chest Rewards")]
    [SerializeField] private List<ChestDropConfig> chestDrops = new List<ChestDropConfig>();

    // State
    private bool isCompleted;
    private Vector3 lastUsedSpawnPos;

    // ========================================
    // Public Properties
    // ========================================

    public string AreaId { get { return areaId; } }
    public float Radius { get { return radius; } }
    public Vector3 Center { get { return transform.position; } }
    public int MonsterLevel { get { return monsterLevel; } }
    public int MonsterCount { get { return monsterCount; } }
    public bool IsCompleted { get { return isCompleted; } set { isCompleted = value; } }
    public IReadOnlyList<ChestDropConfig> ChestDrops { get { return chestDrops; } }
    public bool RequiresItem { get { return requiresItem; } set { requiresItem = value; } }

    // ========================================
    // Monster Lifecycle
    // ========================================

    /// <summary>Spawn and activate monsters for this area.</summary>
    public void ActivateMonsters(NPCGoddess npc)
    {
        if (monsterPrefab == null) return;

        for (int i = 0; i < monsterCount; i++)
        {
            Vector3 pos = GetRandomSpawnPoint();
            // Add small random offset so monsters don't stack
            pos += new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
            GameObject obj = Object.Instantiate(monsterPrefab, pos, Quaternion.identity);
            spawnedMonsters.Add(obj);

            DemonMinion demon = obj.GetComponent<DemonMinion>();
            if (demon != null && demon.IsDormant)
                demon.Activate(monsterLevel, npc);
        }
    }

    private void HandleDemonDeath(DemonMinion demon)
    {
        StartCoroutine(ReviveDemonCoroutine(demon));
    }

    private System.Collections.IEnumerator ReviveDemonCoroutine(DemonMinion demon)
    {
        yield return new WaitForSeconds(demonReviveTime);

        if (demon != null && demon.IsDead && cachedNpc != null)
        {
            int newLevel = Mathf.Min(monsterLevel + 1, 3);
            demon.Revive(newLevel, cachedNpc);
        }
    }

    /// <summary>Destroy all spawned monsters for this area.</summary>
    public void DeactivateMonsters()
    {
        StopAllCoroutines();
        foreach (var obj in spawnedMonsters)
        {
            if (obj != null) Object.Destroy(obj);
        }
        spawnedMonsters.Clear();
    }

    // ========================================
    // Public Methods
    // ========================================

    /// <summary>Check if a position is within this quest area.</summary>
    public bool IsWithinArea(Vector3 position)
    {
        float dx = position.x - transform.position.x;
        float dz = position.z - transform.position.z;
        return (dx * dx + dz * dz) <= radius * radius;
    }

    /// <summary>Get a random spawn point position within the area.</summary>
    public Vector3 GetRandomSpawnPoint()
    {
        if (spawnPoints.Count == 0)
        {
            // Fallback: random point within radius
            Vector2 offset = Random.insideUnitCircle * radius * 0.6f;
            lastUsedSpawnPos = transform.position + new Vector3(offset.x, 0f, offset.y);
            return lastUsedSpawnPos;
        }

        int index = Random.Range(0, spawnPoints.Count);
        Transform selected = spawnPoints[index];
        lastUsedSpawnPos = selected != null ? selected.position : transform.position;
        return lastUsedSpawnPos;
    }

    /// <summary>Get the NPC entry target position for this area.</summary>
    public Vector3 GetEntryPosition()
    {
        if (entryPoint != null) return entryPoint.position;

        // Default: point on the radius edge in the direction of the center
        return transform.position;
    }

    // ========================================
    // Gizmos
    // ========================================

    private void OnDrawGizmos()
    {
        // Area radius
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, radius);

        // Spawn points
        if (spawnPoints.Count > 0)
        {
            Gizmos.color = Color.yellow;
            foreach (var sp in spawnPoints)
            {
                if (sp != null)
                {
                    Gizmos.DrawSphere(sp.position, 0.3f);
                    Gizmos.DrawLine(transform.position, sp.position);
                }
            }
        }

        // Entry point
        if (entryPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(entryPoint.position, 0.4f);
        }

        // Area label
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
            string.IsNullOrEmpty(areaId) ? "QuestArea" : areaId);
        #endif
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.15f);
        Gizmos.DrawSphere(transform.position, radius);
    }
}
