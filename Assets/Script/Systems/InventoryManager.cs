/*
 * ============================================================
 *  InventoryManager - 背包/装备管理器
 * ============================================================
 *
 * 【功能】
 *   管理玩家的装备栏、背包、金币，以及玩家出售池。
 *   所有写入操作统一经过容量/金币/归属校验，失败不产生状态变化。
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

    // Optional link to ScriptableObject definition
    public GameItemDefinition definition;

    // Unique runtime instance id
    public string instanceId;

    public void EnsureInstanceId()
    {
        if (string.IsNullOrEmpty(instanceId))
            instanceId = Guid.NewGuid().ToString("N");
    }

    public GameItem Clone()
    {
        var copy = new GameItem
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
            goldAmount = goldAmount,
            definition = definition
        };
        copy.EnsureInstanceId();
        return copy;
    }

    public bool IsSameBaseType(GameItem other)
    {
        if (other == null) return false;
        return slotType == other.slotType && statType == other.statType && rarity == other.rarity && itemType == ItemType.Equipment;
    }

    public GameItem SynthesizeWith(GameItem other)
    {
        if (!IsSameBaseType(other)) return null;
        if (rarity >= ItemRarity.Mythic) return null;

        ItemRarity nextRarity = rarity + 1;
        float newValue = statValue + other.statValue;
        string rarityPrefix = nextRarity == ItemRarity.Quality ? "Quality" : nextRarity == ItemRarity.Rare ? "Rare" : "Epic";

        var result = new GameItem
        {
            itemType = ItemType.Equipment,
            itemId = $"synthesized_{itemId}",
            itemName = $"{rarityPrefix}{itemName.Replace("Common", "").Replace("Quality", "").Replace("Rare", "")}",
            description = statType == StatType.Health ? $"HP+{newValue}" : $"DPS+{newValue}",
            slotType = slotType,
            rarity = nextRarity,
            statType = statType,
            statValue = newValue
        };
        result.EnsureInstanceId();
        return result;
    }
}

// Per-player inventory data container
public class PlayerInventory
{
    public Dictionary<EquipmentSlotType, GameItem> Equipped = new Dictionary<EquipmentSlotType, GameItem>();
    public List<GameItem> Backpack = new List<GameItem>();
    public int Gold;
    public int maxSlots = 20;
    public int UsedSlots { get { return Backpack.Count; } }
    public bool IsFull { get { return Backpack.Count >= maxSlots; } }
}

// One listing in the shared player-sold pool
[System.Serializable]
public class PlayerSoldItem
{
    public GameItem item;
    public PlayerMove seller;
    public float listedTime;
}

public class InventoryManager : MonoBehaviour
{
    [Header("Starting Items")]
    [SerializeField] private List<GameItem> startingItems = new List<GameItem>();

    [Header("Sell Prices")]
    [Tooltip("按稀有度配置玩家出售价；留空则从 Resources/SellPriceTable 加载，再回退默认值")]
    [SerializeField] private SellPriceTable sellPriceTable;

    private Dictionary<PlayerMove, PlayerInventory> playerInventories = new Dictionary<PlayerMove, PlayerInventory>();
    private readonly List<PlayerSoldItem> soldItems = new List<PlayerSoldItem>();
    private SellPriceTable resolvedSellPriceTable;

    [SerializeField] private PlayerMove player1;
    [SerializeField] private PlayerMove player2;

    private static InventoryManager instance;
    public static InventoryManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<InventoryManager>();
                if (instance == null && Application.isPlaying)
                {
                    var go = new GameObject("InventoryManager");
                    instance = go.AddComponent<InventoryManager>();
                }
            }
            return instance;
        }
    }

    public System.Action OnInventoryChanged;
    public static event System.Action<PlayerMove> OnBackpackFull;

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
        if (player1 != null) playerInventories[player1] = new PlayerInventory();
        if (player2 != null) playerInventories[player2] = new PlayerInventory();

        ApplyStartingItems();
    }

    private void ApplyStartingItems()
    {
        if (startingItems == null || startingItems.Count == 0) return;

        if (player1 != null)
        {
            var inv1 = GetInventory(player1);
            foreach (var item in startingItems)
                if (item != null) AddItemCore(item.Clone(), inv1, player1);
        }
        if (player2 != null)
        {
            var inv2 = GetInventory(player2);
            foreach (var item in startingItems)
                if (item != null) AddItemCore(item.Clone(), inv2, player2);
        }

        OnInventoryChanged?.Invoke();
    }

    // ========================================
    // Public API
    // ========================================

    /// <summary>Legacy wrapper for TryAddItem. Success returns true.</summary>
    public bool AddItem(GameItem item, PlayerMove player)
    {
        return TryAddItem(item, player);
    }

    public bool TryAddItem(GameItem item, PlayerMove player)
    {
        if (item == null || player == null) return false;

        var inv = GetInventory(player);
        item.EnsureInstanceId();

        if (item.itemType == ItemType.Currency)
        {
            if (item.goldAmount <= 0) return false;
            inv.Gold += item.goldAmount;
            OnInventoryChanged?.Invoke();
            return true;
        }

        if (!CanAddItem(item, player))
        {
            FireBackpackFull(player);
            return false;
        }

        if (!AddItemCore(item, inv, player))
        {
            FireBackpackFull(player);
            return false;
        }
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool CanAddItem(GameItem item, PlayerMove player)
    {
        if (item == null || player == null) return false;
        if (item.itemType == ItemType.Currency) return item.goldAmount > 0;

        var inv = GetInventory(player);
        if (item.itemType == ItemType.Equipment)
        {
            if (!inv.Equipped.TryGetValue(item.slotType, out var current) || current == null)
                return true;
            if (current.IsSameBaseType(item))
                return true;
        }
        return !inv.IsFull;
    }

    public int GetBackpackCapacity(PlayerMove player)
    {
        return GetInventory(player)?.maxSlots ?? 0;
    }

    public int GetBackpackCount(PlayerMove player)
    {
        var inv = GetInventory(player);
        return inv != null ? inv.UsedSlots : 0;
    }

    public void AddGold(int amount, PlayerMove player)
    {
        if (player == null || amount <= 0) return;
        var inv = GetInventory(player);
        inv.Gold += amount;
        OnInventoryChanged?.Invoke();
    }

    public bool SpendGold(int amount, PlayerMove player)
    {
        if (player == null || amount <= 0) return false;
        var inv = GetInventory(player);
        if (inv.Gold < amount) return false;
        inv.Gold -= amount;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool UseConsumable(GameItem item, PlayerMove player)
    {
        if (item == null || player == null) return false;
        var backpack = GetBackpack(player);
        if (backpack == null || !backpack.Contains(item)) return false;
        if (item.itemType != ItemType.Consumable || item.healAmount <= 0) return false;

        backpack.Remove(item);
        player.GetComponent<Health>()?.Heal(item.healAmount);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool UseConsumable(GameItem item, PlayerMove giver, PlayerMove target)
    {
        if (item == null || giver == null || target == null) return false;
        var backpack = GetBackpack(giver);
        if (backpack == null || !backpack.Contains(item)) return false;
        if (item.itemType != ItemType.Consumable || item.healAmount <= 0) return false;

        backpack.Remove(item);
        target.GetComponent<Health>()?.Heal(item.healAmount);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>Legacy id-based overload; resolves the first matching backpack instance.</summary>
    public bool UseConsumable(string itemId, PlayerMove player)
    {
        if (string.IsNullOrEmpty(itemId) || player == null) return false;
        var backpack = GetBackpack(player);
        if (backpack == null) return false;
        var item = backpack.Find(i => i != null && i.itemId == itemId && i.itemType == ItemType.Consumable);
        return item != null && UseConsumable(item, player);
    }

    public bool UseConsumable(string itemId, PlayerMove giver, PlayerMove target)
    {
        if (string.IsNullOrEmpty(itemId) || giver == null || target == null) return false;
        var backpack = GetBackpack(giver);
        if (backpack == null) return false;
        var item = backpack.Find(i => i != null && i.itemId == itemId && i.itemType == ItemType.Consumable);
        return item != null && UseConsumable(item, giver, target);
    }

    public bool SellItem(GameItem item, PlayerMove player)
    {
        if (item == null || player == null) return false;

        var inv = GetInventory(player);
        if (!inv.Backpack.Contains(item)) return false;

        int price = GetSellPrice(item);
        if (price <= 0) return false;

        inv.Backpack.Remove(item);
        soldItems.Add(new PlayerSoldItem { item = item, seller = player, listedTime = Time.time });
        inv.Gold += price;
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>Legacy id-based overload; resolves the first matching backpack instance.</summary>
    public bool SellItem(string itemId, PlayerMove player)
    {
        if (string.IsNullOrEmpty(itemId) || player == null) return false;
        var backpack = GetBackpack(player);
        if (backpack == null) return false;
        var item = backpack.Find(i => i != null && i.itemId == itemId);
        return item != null && SellItem(item, player);
    }

    public int GetSellPrice(GameItem item)
    {
        if (item == null) return 0;
        return ResolveSellPriceTable().GetPrice(item.rarity);
    }

    public IReadOnlyList<PlayerSoldItem> GetSoldItems(PlayerMove viewer)
    {
        var result = new List<PlayerSoldItem>();
        foreach (var entry in soldItems)
        {
            if (entry == null || entry.item == null) continue;
            if (viewer != null && ReferenceEquals(entry.seller, viewer)) continue;
            result.Add(entry);
        }
        return result;
    }

    public bool BuySoldItem(PlayerSoldItem entry, PlayerMove buyer)
    {
        if (entry == null || entry.item == null || buyer == null) return false;
        if (ReferenceEquals(entry.seller, buyer)) return false;
        if (!soldItems.Contains(entry)) return false;

        var inv = GetInventory(buyer);
        int price = GetSellPrice(entry.item);
        if (price > 0 && inv.Gold < price) return false;
        if (!CanAddItem(entry.item, buyer))
        {
            FireBackpackFull(buyer);
            return false;
        }

        if (price > 0) inv.Gold -= price;
        soldItems.Remove(entry);

        if (!AddItemCore(entry.item, inv, buyer))
        {
            if (price > 0) inv.Gold += price;
            soldItems.Add(entry);
            return false;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool CancelListing(PlayerSoldItem entry, PlayerMove seller)
    {
        if (entry == null || entry.item == null || seller == null) return false;
        if (!ReferenceEquals(entry.seller, seller)) return false;
        if (!soldItems.Contains(entry)) return false;

        var inv = GetInventory(seller);
        if (!CanAddItem(entry.item, seller))
        {
            FireBackpackFull(seller);
            return false;
        }

        soldItems.Remove(entry);
        if (!AddItemCore(entry.item, inv, seller))
        {
            soldItems.Add(entry);
            return false;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool DestroyItem(GameItem item, PlayerMove player)
    {
        if (item == null || player == null) return false;
        var inv = GetInventory(player);
        if (!inv.Backpack.Contains(item)) return false;

        inv.Backpack.Remove(item);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool Unequip(EquipmentSlotType slot, PlayerMove player)
    {
        if (player == null) return false;
        var inv = GetInventory(player);
        if (!inv.Equipped.TryGetValue(slot, out var item) || item == null) return false;
        if (inv.IsFull)
        {
            FireBackpackFull(player);
            return false;
        }

        inv.Equipped.Remove(slot);
        inv.Backpack.Add(item);
        ApplyEquipmentStats(player);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public List<GameItem> GetPlayerBackpack(PlayerMove player)
    {
        var inv = GetInventory(player);
        return inv != null ? inv.Backpack : null;
    }

    public int GetGold(PlayerMove player)
    {
        var inv = GetInventory(player);
        return inv != null ? inv.Gold : 0;
    }

    public Dictionary<EquipmentSlotType, GameItem> GetP1Equipped() { return player1 != null ? GetInventory(player1).Equipped : null; }
    public Dictionary<EquipmentSlotType, GameItem> GetP2Equipped() { return player2 != null ? GetInventory(player2).Equipped : null; }
    public List<GameItem> GetP1Backpack() { return player1 != null ? GetInventory(player1).Backpack : null; }
    public List<GameItem> GetP2Backpack() { return player2 != null ? GetInventory(player2).Backpack : null; }
    public int P1Gold { get { return player1 != null ? GetInventory(player1).Gold : 0; } }
    public int P2Gold { get { return player2 != null ? GetInventory(player2).Gold : 0; } }

    // ========================================
    // Internal
    // ========================================

    private bool AddItemCore(GameItem item, PlayerInventory inv, PlayerMove player)
    {
        if (item == null || inv == null) return false;
        item.EnsureInstanceId();

        if (item.itemType == ItemType.Equipment)
        {
            if (!inv.Equipped.TryGetValue(item.slotType, out var current) || current == null)
            {
                inv.Equipped[item.slotType] = item;
                ApplyEquipmentStats(player);
                return true;
            }

            if (current.IsSameBaseType(item))
            {
                GameItem synthesized = current.SynthesizeWith(item);
                if (synthesized != null)
                {
                    inv.Equipped[item.slotType] = synthesized;
                    ApplyEquipmentStats(player);
                    return true;
                }
            }

            if (inv.IsFull) return false;

            bool isBetter = item.rarity > current.rarity ||
                (item.rarity == current.rarity && item.statValue > current.statValue);

            if (isBetter)
            {
                inv.Backpack.Add(current);
                inv.Equipped[item.slotType] = item;
            }
            else
            {
                inv.Backpack.Add(item);
            }

            ApplyEquipmentStats(player);
            return true;
        }

        if (inv.IsFull) return false;
        inv.Backpack.Add(item);
        return true;
    }

    private void ApplyEquipmentStats(PlayerMove player)
    {
        if (player == null) return;

        float totalHPBonus = 0f;
        float totalDPSMultiplier = 1f;

        var equipped = GetInventory(player).Equipped;
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

        var ph = player.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            ph.MaxHealthBonus = totalHPBonus;
            ph.DamageMultiplier = totalDPSMultiplier;
            ph.RefreshStats();
        }
    }

    private SellPriceTable ResolveSellPriceTable()
    {
        if (resolvedSellPriceTable != null) return resolvedSellPriceTable;

        if (sellPriceTable != null)
        {
            resolvedSellPriceTable = sellPriceTable;
        }
        else
        {
            resolvedSellPriceTable = Resources.Load<SellPriceTable>("SellPriceTable");
            if (resolvedSellPriceTable == null)
                resolvedSellPriceTable = ScriptableObject.CreateInstance<SellPriceTable>();
        }
        return resolvedSellPriceTable;
    }

    private void FireBackpackFull(PlayerMove player)
    {
        OnBackpackFull?.Invoke(player);
    }

    private PlayerInventory GetInventory(PlayerMove player)
    {
        if (player == null) return null;
        if (playerInventories.TryGetValue(player, out var inv)) return inv;
        inv = new PlayerInventory();
        playerInventories[player] = inv;
        return inv;
    }

    private List<GameItem> GetBackpack(PlayerMove player)
    {
        return GetInventory(player)?.Backpack;
    }
}