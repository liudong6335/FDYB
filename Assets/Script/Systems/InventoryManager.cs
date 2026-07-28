/*
 * ============================================================
 *  InventoryManager  -  鑳屽寘/瑁呭绠＄悊鍣?
 * ============================================================
 *
 * 銆愬姛鑳姐€?
 *   绠＄悊涓や釜鐜╁鐨勮澶囨爮銆佽儗鍖呫€侀噾甯併€?
 *   鏀寔瑁呭绌挎埓銆佽嚜鍔ㄥ悎鎴愩€佺墿鍝佸嚭鍞€佷娇鐢ㄦ秷鑰楀搧銆?
 *   瑁呭灞炴€т細鑷姩鍚屾鍒扮帺瀹讹紙鍔犺閲?鍔犱激瀹筹級銆?
 *
 * 銆愭寕杞藉璞°€?
 *   鍦烘櫙涓殑鍗曚緥瀵硅薄锛圖ontDestroyOnLoad锛?
 *
 * 銆愬彲璋冭妭鍙傛暟銆?
 *   startingItems  - 鍒濆鐗╁搧鍒楄〃
 *   player1 / 2    - 涓や釜鐜╁瀵硅薄鐨勫紩鐢?
 *
 * 銆愮墿鍝佺被鍨嬨€?
 *   Equipment  - 瑁呭锛堣厱鐢?鑳哥敳/鎶よ偐/瑁ゅ瓙/姝﹀櫒瀹濈煶锛?
 *   Consumable - 娑堣€楀搧锛堢敓鍛借嵂姘达級
 *   Currency   - 閲戝竵
 *
 * 銆愯澶囧悎鎴愩€?
 *   鍚屼竴閮ㄤ綅+鍚屼竴灞炴€?鍚屽搧璐ㄧ殑涓や欢瑁呭鍙互鑷姩鍚堟垚涓烘洿楂樺搧璐?
 *
 * 銆愬閮ㄨ皟鐢ㄣ€?
 *   AddItem(item, player)      - 缁欑帺瀹舵坊鍔犵墿鍝?
 *   UseConsumable(id, player)  - 浣跨敤娑堣€楀搧
 *   SellItem(id, player)       - 鍑哄敭鐗╁搧
 *   AddGold/SpendGold           - 澧炲噺閲戝竵
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
            goldAmount = goldAmount,
            definition = definition
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

// Per-player inventory data container
public class PlayerInventory
{
    public Dictionary<EquipmentSlotType, GameItem> Equipped = new Dictionary<EquipmentSlotType, GameItem>();
    public List<GameItem> Backpack = new List<GameItem>();
    public int Gold;
}


public class InventoryManager : MonoBehaviour
{
    [Header("Starting Items")]
    [SerializeField] private List<GameItem> startingItems = new List<GameItem>();

    private Dictionary<PlayerMove, PlayerInventory> playerInventories = new Dictionary<PlayerMove, PlayerInventory>();

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
        if (player1 != null) playerInventories[player1] = new PlayerInventory();
        if (player2 != null) playerInventories[player2] = new PlayerInventory();
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

        var ph = player.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            ph.MaxHealthBonus = totalHPBonus;
            ph.DamageMultiplier = totalDPSMultiplier;
            ph.RefreshStats();
        }
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
                player.GetComponent<Health>()?.Heal(item.healAmount);
            }
            OnInventoryChanged?.Invoke();
        }
    }

    /// <summary>Use a consumable from one player's backpack to heal another player.</summary>
    public void UseConsumable(string itemId, PlayerMove giver, PlayerMove target)
    {
        var backpack = GetBackpack(giver);
        GameItem item = backpack.Find(i => i.itemId == itemId && i.itemType == ItemType.Consumable);
        if (item != null)
        {
            backpack.Remove(item);
            // Heal effect
            if (item.healAmount > 0)
            {
                target.GetComponent<Health>()?.Heal(item.healAmount);
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
        var inv = GetInventory(player);
        if (inv != null) inv.Gold += amount;
        OnInventoryChanged?.Invoke();
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

    public bool SpendGold(int amount, PlayerMove player)
    {
        var inv = GetInventory(player);
        if (inv != null && inv.Gold >= amount) { inv.Gold -= amount; OnInventoryChanged?.Invoke(); return true; }
        return false;
    }

    private PlayerInventory GetInventory(PlayerMove player)
    {
        if (playerInventories.TryGetValue(player, out var inv)) return inv;
        var newInv = new PlayerInventory();
        playerInventories[player] = newInv;
        return newInv;
    }

    private Dictionary<EquipmentSlotType, GameItem> GetEquipped(PlayerMove player)
    {
        return GetInventory(player)?.Equipped;
    }

    private List<GameItem> GetBackpack(PlayerMove player)
    {
        return GetInventory(player)?.Backpack;
    }

    public Dictionary<EquipmentSlotType, GameItem> GetP1Equipped() { return player1 != null ? GetInventory(player1).Equipped : null; }
    public Dictionary<EquipmentSlotType, GameItem> GetP2Equipped() { return player2 != null ? GetInventory(player2).Equipped : null; }
    public List<GameItem> GetP1Backpack() { return player1 != null ? GetInventory(player1).Backpack : null; }
    public List<GameItem> GetP2Backpack() { return player2 != null ? GetInventory(player2).Backpack : null; }
    public int P1Gold { get { return player1 != null ? GetInventory(player1).Gold : 0; } }
    public int P2Gold { get { return player2 != null ? GetInventory(player2).Gold : 0; } }
}




