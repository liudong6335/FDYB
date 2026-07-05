/*
 * ============================================================
 *  InventoryManager  -  背包/装备管理器
 * ============================================================
 *
 * 【功能】
 *   管理两个玩家的装备栏、背包、金币。
 *   支持装备穿戴、自动合成、物品出售、使用消耗品。
 *   装备属性会自动同步到玩家（加血量/加伤害）。
 *
 * 【挂载对象】
 *   场景中的单例对象（DontDestroyOnLoad）
 *
 * 【可调节参数】
 *   startingItems  - 初始物品列表
 *   player1 / 2    - 两个玩家对象的引用
 *
 * 【物品类型】
 *   Equipment  - 装备（腕甲/胸甲/护肩/裤子/武器宝石）
 *   Consumable - 消耗品（生命药水）
 *   Currency   - 金币
 *
 * 【装备合成】
 *   同一部位+同一属性+同品质的两件装备可以自动合成为更高品质
 *
 * 【外部调用】
 *   AddItem(item, player)      - 给玩家添加物品
 *   UseConsumable(id, player)  - 使用消耗品
 *   SellItem(id, player)       - 出售物品
 *   AddGold/SpendGold           - 增减金币
 */
using UnityEngine;
using System;
using System.Collections.Generic;

public enum ItemType { Equipment, Consumable, Currency }
public enum ItemRarity { Common, Quality, Rare, Epic, Mythic }
public enum EquipmentSlotType { Wrist, Chest, Shoulder, Pants, WeaponGem }
public enum StatType { Health, DPS }

[System.Serializable]
public class GameItem
{
    public ItemType itemType;
    public string itemId;
    public string itemName;
    public string description;
    public ItemRarity rarity;

    // Equipment
    public EquipmentSlotType slotType;
    public StatType statType;
    public float statValue;

    // Consumable
    public float healAmount;

    // Currency
    public int goldAmount;

    public GameItem Clone()
    {
        return new GameItem
        {
            itemType = itemType,
            itemId = itemId,
            itemName = itemName,
            description = description,
            rarity = rarity,
            slotType = slotType,
            statType = statType,
            statValue = statValue,
            healAmount = healAmount,
            goldAmount = goldAmount
        };
    }

    public bool IsSameBaseType(GameItem other)
    {
        if (other == null) return false;
        // Same slot, same stat type, same rarity = can synthesize
        return slotType == other.slotType && statType == other.statType && rarity == other.rarity && itemType == ItemType.Equipment;
    }

    public GameItem SynthesizeWith(GameItem other)
    {
        if (!IsSameBaseType(other)) return null;
        // Upgrade rarity
        ItemRarity nextRarity = rarity + 1;
        float newValue = statValue + other.statValue;
        string rarityPrefix = nextRarity == ItemRarity.Quality ? "Quality" : nextRarity == ItemRarity.Rare ? "Rare" : "Epic";

        return new GameItem
        {
            itemType = ItemType.Equipment,
            itemId = $"synthesized_{itemId}",
            itemName = $"{rarityPrefix}{itemName.Replace("Common","").Replace("Quality","").Replace("Rare","")}",
            description = statType == StatType.Health ? $"HP+{newValue}" : $"DPS+{newValue}",
            slotType = slotType,
            rarity = nextRarity,
            statType = statType,
            statValue = newValue
        };
    }
}

public class InventoryManager : MonoBehaviour
{
    [Header("Starting Items")]
    [SerializeField] private List<GameItem> startingItems = new List<GameItem>();

    // Player 1 inventory
    private Dictionary<EquipmentSlotType, GameItem> p1Equipped = new Dictionary<EquipmentSlotType, GameItem>();
    private List<GameItem> p1Backpack = new List<GameItem>();
    private int p1Gold;

    // Player 2 inventory
    private Dictionary<EquipmentSlotType, GameItem> p2Equipped = new Dictionary<EquipmentSlotType, GameItem>();
    private List<GameItem> p2Backpack = new List<GameItem>();
    private int p2Gold;

    [SerializeField] private PlayerMove player1;
    [SerializeField] private PlayerMove player2;

    private static InventoryManager instance;
    public static InventoryManager Instance { get { return instance; } }

    public System.Action OnInventoryChanged;

    private void Awake()
    {
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (player1 == null || player2 == null)
        {
            PlayerMove[] players = FindObjectsByType<PlayerMove>(FindObjectsSortMode.None);
            if (players.Length > 0 && player1 == null) player1 = players[0];
            if (players.Length > 1 && player2 == null) player2 = players[1];
        }
    }

    public void AddItem(GameItem item, PlayerMove player)
    {
        if (item == null || player == null) return;

        if (item.itemType == ItemType.Currency)
        {
            AddGold(item.goldAmount, player);
            return;
        }

        if (item.itemType == ItemType.Equipment)
        {
            AddEquipment(item, player);
        }
        else
        {
            var backpack = GetBackpack(player);
            backpack.Add(item);
        }

        OnInventoryChanged?.Invoke();
    }

    private void AddEquipment(GameItem item, PlayerMove player)
    {
        var equipped = GetEquipped(player);

        if (!equipped.ContainsKey(item.slotType) || equipped[item.slotType] == null)
        {
            // Auto-equip first item in slot
            equipped[item.slotType] = item;
            ApplyEquipmentStats(player);
            return;
        }

        GameItem current = equipped[item.slotType];

        // Check if can synthesize (same base type)
        if (current.IsSameBaseType(item))
        {
            GameItem synthesized = current.SynthesizeWith(item);
            if (synthesized != null)
            {
                equipped[item.slotType] = synthesized;
                ApplyEquipmentStats(player);
                return;
            }
        }

        // Check if new item is better (higher rarity or higher stat)
        bool isBetter = item.rarity > current.rarity ||
            (item.rarity == current.rarity && item.statValue > current.statValue);

        if (isBetter)
        {
            var backpack = GetBackpack(player);
            backpack.Add(current);
            equipped[item.slotType] = item;
        }
        else
        {
            var backpack = GetBackpack(player);
            backpack.Add(item);
        }

        ApplyEquipmentStats(player);
    }

    private void ApplyEquipmentStats(PlayerMove player)
    {
        if (player == null) return;

        float totalHPBonus = 0f;
        float totalDPSMultiplier = 1f;

        var equipped = GetEquipped(player);
        foreach (var kvp in equipped)
        {
            if (kvp.Value == null) continue;
            if (kvp.Value.slotType == EquipmentSlotType.WeaponGem)
            {
                totalDPSMultiplier += kvp.Value.statValue / 100f;
            }
            else if (kvp.Value.statType == StatType.Health)
            {
                totalHPBonus += kvp.Value.statValue;
            }
            else if (kvp.Value.statType == StatType.DPS)
            {
                totalDPSMultiplier += kvp.Value.statValue / 100f;
            }
        }

        player.MaxHealthBonus = totalHPBonus;
        player.DamageMultiplier = totalDPSMultiplier;
        player.RefreshStats();
    }

    public void UseConsumable(string itemId, PlayerMove player)
    {
        var backpack = GetBackpack(player);
        GameItem item = backpack.Find(i => i.itemId == itemId && i.itemType == ItemType.Consumable);
        if (item != null)
        {
            backpack.Remove(item);
            // Heal effect
            if (item.healAmount > 0)
            {
                player.TakeDamage(-item.healAmount); // Negative damage = heal
            }
            OnInventoryChanged?.Invoke();
        }
    }

    public void SellItem(string itemId, PlayerMove player)
    {
        var backpack = GetBackpack(player);
        GameItem item = backpack.Find(i => i.itemId == itemId);
        if (item != null)
        {
            backpack.Remove(item);
            int sellPrice = item.rarity == ItemRarity.Common ? 25 : (item.rarity == ItemRarity.Quality ? 50 : 100);
            AddGold(sellPrice, player);
            OnInventoryChanged?.Invoke();
        }
    }

    public void AddGold(int amount, PlayerMove player)
    {
        if (player == player1) p1Gold += amount;
        else if (player == player2) p2Gold += amount;
        OnInventoryChanged?.Invoke();
    }

    public int GetGold(PlayerMove player)
    {
        if (player == player1) return p1Gold;
        if (player == player2) return p2Gold;
        return 0;
    }

    public bool SpendGold(int amount, PlayerMove player)
    {
        if (player == player1 && p1Gold >= amount) { p1Gold -= amount; OnInventoryChanged?.Invoke(); return true; }
        if (player == player2 && p2Gold >= amount) { p2Gold -= amount; OnInventoryChanged?.Invoke(); return true; }
        return false;
    }

    private Dictionary<EquipmentSlotType, GameItem> GetEquipped(PlayerMove player)
    {
        return player == player1 ? p1Equipped : p2Equipped;
    }

    private List<GameItem> GetBackpack(PlayerMove player)
    {
        return player == player1 ? p1Backpack : p2Backpack;
    }

    public Dictionary<EquipmentSlotType, GameItem> GetP1Equipped() { return p1Equipped; }
    public Dictionary<EquipmentSlotType, GameItem> GetP2Equipped() { return p2Equipped; }
    public List<GameItem> GetP1Backpack() { return p1Backpack; }
    public List<GameItem> GetP2Backpack() { return p2Backpack; }
    public int P1Gold { get { return p1Gold; } }
    public int P2Gold { get { return p2Gold; } }
}

