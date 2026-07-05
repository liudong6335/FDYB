/*
 * ============================================================
 *  TreasureChest  -  宝箱
 * ============================================================
 *
 * 【功能】
 *   游戏中的可交互宝箱。点击后读条开启，开启后获得随机奖励。
 *   读条期间移动会取消开启。
 *   不同等级的宝箱出不同品质的奖励。
 *
 * 【挂载对象】
 *   场景中的宝箱游戏对象（带碰撞体）
 *
 * 【可调节参数】
 *   chestType        - 宝箱类型（木/铜/银/金）
 *   openTime         - 开启读条时间（秒）
 *   interactDistance - 玩家可交互的最大距离
 *   closedVisual     - 关闭状态的视觉效果
 *   openVisual       - 打开状态的视觉效果
 *   highlightEffect  - 读条时的高亮效果
 *
 * 【宝箱类型说明】
 *   Wooden - 普通奖励（药水/金币/普通装备）
 *   Copper - 更好的奖励（含高品质装备和武器宝石）
 *
 * 【操作说明】
 *   - 靠近宝箱，鼠标点击即可开始开启
 *   - 读条期间移动会取消
 */
using UnityEngine;
using System.Collections.Generic;

public enum ChestType { Wooden, Copper, Silver, Gold }

public class TreasureChest : MonoBehaviour
{
    [Header("Chest Settings")]
    [SerializeField] private ChestType chestType = ChestType.Wooden;
    [SerializeField] private float openTime = 1f;
    [SerializeField] private float interactDistance = 2f;

    [Header("Visual")]
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openVisual;
    [SerializeField] private GameObject highlightEffect;

    private bool isOpened;
    private bool isOpening;
    private PlayerMove openingPlayer;
    private float currentOpenTimer;
    private InventoryManager cachedInventory;
    private static PlayerMove[] cachedPlayers;
    private static float playerCacheTime;
    private const float PlayerCacheInterval = 1f;

    private float sqrInteractDistance;
    private float sqrInteractPadding;

    public ChestType Type { get { return chestType; } }
    public bool IsOpened { get { return isOpened; } }
    public bool IsOpening { get { return isOpening; } }

    private void Awake()
    {
        sqrInteractDistance = interactDistance * interactDistance;
        sqrInteractPadding = (interactDistance + 0.5f) * (interactDistance + 0.5f);
    }

    private void Start()
    {
        if (closedVisual != null) closedVisual.SetActive(true);
        if (openVisual != null) openVisual.SetActive(false);
        if (highlightEffect != null) highlightEffect.SetActive(false);
    }

    private void Update()
    {
        if (isOpened || !isOpening || openingPlayer == null) return;

        float sqrDist = SqrDistanceTo(openingPlayer.transform.position);
        if (sqrDist > sqrInteractPadding)
        {
            CancelOpening();
            return;
        }

        currentOpenTimer += Time.deltaTime;
        if (currentOpenTimer >= openTime)
            OpenChest(openingPlayer);
    }

    private void OnMouseDown()
    {
        if (isOpened || isOpening) return;
        TryInteract();
    }

    public void TryInteract()
    {
        if (Time.time - playerCacheTime > PlayerCacheInterval || cachedPlayers == null)
        {
            cachedPlayers = FindObjectsByType<PlayerMove>(FindObjectsSortMode.None);
            playerCacheTime = Time.time;
        }

        PlayerMove nearest = null;
        float nearestSqrDist = float.MaxValue;

        foreach (var p in cachedPlayers)
        {
            if (p == null) continue;
            float d = SqrDistanceTo(p.transform.position);
            if (d < nearestSqrDist)
            {
                nearestSqrDist = d;
                nearest = p;
            }
        }

        if (nearest != null && nearestSqrDist <= sqrInteractDistance)
            StartOpening(nearest);
    }

    public void StartOpening(PlayerMove player)
    {
        if (isOpened || isOpening) return;
        isOpening = true;
        openingPlayer = player;
        currentOpenTimer = 0f;
        if (highlightEffect != null) highlightEffect.SetActive(true);
    }

    private void CancelOpening()
    {
        isOpening = false;
        openingPlayer = null;
        currentOpenTimer = 0f;
        if (highlightEffect != null) highlightEffect.SetActive(false);
    }

    private void OpenChest(PlayerMove player)
    {
        isOpened = true;
        isOpening = false;
        if (closedVisual != null) closedVisual.SetActive(false);
        if (openVisual != null) openVisual.SetActive(true);
        if (highlightEffect != null) highlightEffect.SetActive(false);

        if (cachedInventory == null)
            cachedInventory = FindFirstObjectByType<InventoryManager>();

        foreach (var item in GenerateLoot())
        {
            if (cachedInventory != null)
                cachedInventory.AddItem(item, player);
        }
    }

    private List<GameItem> GenerateLoot()
    {
        List<GameItem> items = new List<GameItem>();
        float roll = Random.value;

        switch (chestType)
        {
            case ChestType.Wooden:
                if (roll < 0.3f)
                    items.Add(new GameItem { itemType = ItemType.Consumable, itemId = "health_potion_1", itemName = "鍒濈骇鐢熷懡鑽按", description = "50HP/绉掞紝鎸佺画4绉?, healAmount = 200f });
                else if (roll < 0.6f)
                    items.Add(new GameItem { itemType = ItemType.Currency, itemId = "gold", itemName = "閲戝竵", goldAmount = 50 });
                else
                    items.Add(GenerateRandomEquipment(false));
                break;

            case ChestType.Copper:
                if (roll < 0.4f)
                    items.Add(GenerateRandomEquipment(false));
                else if (roll < 0.6f)
                    items.Add(GenerateRandomEquipment(true));
                else if (roll < 0.8f)
                    items.Add(new GameItem { itemType = ItemType.Consumable, itemId = "health_potion_1", itemName = "鍒濈骇鐢熷懡鑽按", description = "50HP/绉掞紝鎸佺画4绉?, healAmount = 200f });
                else
                    items.Add(new GameItem { itemType = ItemType.Currency, itemId = "gold", itemName = "閲戝竵", goldAmount = 100 });

                if (Random.value < 0.1f)
                    items.Add(new GameItem { itemType = ItemType.Equipment, itemId = "weapon_gem_1", itemName = "鏅€氭鍣ㄥ疂鐭?, description = "DPS+40", slotType = EquipmentSlotType.WeaponGem, rarity = ItemRarity.Common, statType = StatType.DPS, statValue = 40 });
                break;
        }

        return items;
    }

    private static readonly EquipmentSlotType[] ArmorSlots =
        { EquipmentSlotType.Wrist, EquipmentSlotType.Chest, EquipmentSlotType.Shoulder, EquipmentSlotType.Pants };

    private static readonly float[] NormalHP  = { 40f, 80f, 50f, 60f };
    private static readonly float[] QualityHP = { 100f, 200f, 150f, 150f };
    private static readonly float[] NormalDPS  = { 15f, 20f, 15f, 15f };
    private static readonly float[] QualityDPS = { 40f, 50f, 40f, 40f };

    private static readonly string[] SlotNames = { "鎶よ厱", "閾犵敳", "鎶よ偐", "瑁ゅ瓙" };

    private GameItem GenerateRandomEquipment(bool isQuality)
    {
        int slotIndex = Random.Range(0, ArmorSlots.Length);
        EquipmentSlotType slot = ArmorSlots[slotIndex];
        float statValue;
        string statDesc;

        if (Random.value < 0.5f)
        {
            statValue = isQuality ? QualityHP[slotIndex] : NormalHP[slotIndex];
            statDesc = "鐢熷懡鍊?" + statValue + "HP";
        }
        else
        {
            statValue = isQuality ? QualityDPS[slotIndex] : NormalDPS[slotIndex];
            statDesc = "DPS+" + statValue;
        }

        bool isHealth = statValue > 50;
        string prefix = isHealth ? "鐢熷懡" : "鍔涢噺";

        return new GameItem
        {
            itemType = ItemType.Equipment,
            itemId = (isQuality ? "quality_" : "common_") + prefix.ToLower() + "_" + SlotNames[slotIndex],
            itemName = (isQuality ? "绮惧搧" : "鏅€?) + prefix + SlotNames[slotIndex],
            description = statDesc,
            slotType = slot,
            rarity = isQuality ? ItemRarity.Quality : ItemRarity.Common,
            statType = isHealth ? StatType.Health : StatType.DPS,
            statValue = statValue
        };
    }

    private float SqrDistanceTo(Vector3 point)
    {
        float dx = transform.position.x - point.x;
        float dz = transform.position.z - point.z;
        return dx * dx + dz * dz;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOpened ? Color.gray : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
