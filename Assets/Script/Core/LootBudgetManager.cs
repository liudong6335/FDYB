using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 掉落预算配置项：哪个物品、全图最多出几件。
/// 在 Inspector 中配置。
/// </summary>
[System.Serializable]
public class BudgetEntry
{
    public GameItemDefinition itemDef;
    public int maxCount = 10;
}

/// <summary>
/// 掉落表条目：哪个物品、掉落权重。
/// 宝箱和怪物掉落共用此配置。
/// </summary>
[System.Serializable]
public class DropEntry
{
    public GameItemDefinition itemDef;
    [Range(0f, 100f)] public float weight = 1f;
}

/// <summary>
/// LootBudgetManager - 全局掉落预算系统。
/// 控制全图物品产出总量，所有掉落来源统一经过此处查询和扣减。
/// </summary>
public class LootBudgetManager : MonoBehaviour
{
    [Header("Budget Config")]
    [Tooltip("全图总产出预算，每局初始化时加载")]
    [SerializeField] private List<BudgetEntry> budgetConfig = new List<BudgetEntry>();

    private Dictionary<string, int> remaining = new Dictionary<string, int>();
    private Dictionary<string, int> maxCounts = new Dictionary<string, int>();

    private static LootBudgetManager instance;
    public static LootBudgetManager Instance { get { return instance; } }

    // ========================================
    // Unity Lifecycle
    // ========================================

    private void Awake()
    {
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        InitFromConfig();
    }

    // ========================================
    // Public Methods
    // ========================================

    /// <summary>从 Inspector 配置初始化预算数据。</summary>
    public void InitFromConfig()
    {
        remaining.Clear();
        maxCounts.Clear();
        foreach (var entry in budgetConfig)
        {
            if (entry == null || entry.itemDef == null) continue;
            string id = entry.itemDef.itemId;
            if (string.IsNullOrEmpty(id)) continue;
            remaining[id] = entry.maxCount;
            maxCounts[id] = entry.maxCount;
        }
    }

    public bool CanDrop(string itemId)
    {
        return remaining.TryGetValue(itemId, out int count) && count > 0;
    }

    public bool TryConsume(string itemId)
    {
        if (!CanDrop(itemId)) return false;
        remaining[itemId]--;
        return true;
    }

    public void Reset()
    {
        foreach (var kvp in maxCounts)
            remaining[kvp.Key] = kvp.Value;
    }

    /// <summary>核心方法：从传入的掉落表中按权重抽取一件物品。</summary>
    public GameItem PickOne(List<DropEntry> entries)
    {
        if (entries == null || entries.Count == 0) return null;

        var available = new List<DropEntry>();
        foreach (var e in entries)
        {
            if (e == null || e.itemDef == null) continue;
            if (CanDrop(e.itemDef.itemId))
                available.Add(e);
        }

        if (available.Count == 0) return null;

        if (available.Count == 1)
        {
            string id = available[0].itemDef.itemId;
            TryConsume(id);
            return available[0].itemDef.ToGameItem();
        }

        float totalWeight = 0f;
        foreach (var e in available)
            totalWeight += Mathf.Max(0f, e.weight);

        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (var e in available)
        {
            cumulative += Mathf.Max(0f, e.weight);
            if (roll <= cumulative)
            {
                string id = e.itemDef.itemId;
                TryConsume(id);
                return e.itemDef.ToGameItem();
            }
        }

        return null;
    }

    // ========================================
    // Editor Helpers
    // ========================================

    public Dictionary<string, int> GetRemainingSnapshot()
    {
        return new Dictionary<string, int>(remaining);
    }

    public Dictionary<string, int> GetMaxSnapshot()
    {
        return new Dictionary<string, int>(maxCounts);
    }
}
