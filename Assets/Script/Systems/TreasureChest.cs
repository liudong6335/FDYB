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
using UnityEngine.UI;
using System.Collections.Generic;

public enum ChestOpenState { Idle, Reading, OpeningItems, ItemFlying, Complete }

public enum ChestType { Wooden, Copper, Silver, Gold }

public class TreasureChest : MonoBehaviour
{
    [Header("Chest Settings")]
    [SerializeField] private ChestType chestType = ChestType.Wooden;

    [Header("Opening")]
    [SerializeField] private float readTime = 3f;
    [SerializeField] private float hitPenalty = 0.8f;
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private Transform itemSpawnPoint;
    [SerializeField] private GameObject goldVisualPrefab;
    [SerializeField] private float itemFlySpeed = 5f;
    [SerializeField] private float openAnimationDelay = 0.3f;

    [Header("Visual")]
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openVisual;
    [SerializeField] private GameObject highlightEffect;

    private bool isOpened;
    private bool isOpening;
    private PlayerMove openingPlayer;
    private InventoryManager cachedInventory;
    private ChestOpenState currentState = ChestOpenState.Idle;
    private float currentReadProgress;
    private float previousPlayerHealth;

    // Progress bar
    private GameObject barPanel;
    private Image fillImage;
    private readonly Vector3 barOffset = new Vector3(0f, 2.5f, 0f);
    private readonly Vector2 barSize = new Vector2(80f, 8f);

    private float sqrInteractDistance;
    private float sqrInteractPadding;

    // Static registry for efficient AI queries (avoids FindObjectsByType)
    private static readonly List<TreasureChest> allChests = new List<TreasureChest>();
    public static IReadOnlyList<TreasureChest> ActiveChests => allChests;

    public ChestType Type { get { return chestType; } set { chestType = value; } }
    public bool IsOpened { get { return isOpened; } }
    public bool IsOpening { get { return isOpening; } }

    private void Awake()
    {
        allChests.Add(this);
        sqrInteractDistance = interactDistance * interactDistance;
        sqrInteractPadding = (interactDistance + 0.5f) * (interactDistance + 0.5f);
        BuildProgressBar();
    }

    private void OnDestroy()
    {
        allChests.Remove(this);
        if (barPanel != null) Destroy(barPanel);
    }

    private void Start()
    {
        if (closedVisual != null) closedVisual.SetActive(true);
        if (openVisual != null) openVisual.SetActive(false);
        if (highlightEffect != null) highlightEffect.SetActive(false);
        if (barPanel != null) barPanel.SetActive(false);
    }

    private void Update()
    {
        if (isOpened || !isOpening || openingPlayer == null) return;

        switch (currentState)
        {
            case ChestOpenState.Reading:
                UpdateReading();
                break;
        }
    }

    private void OnMouseDown()
    {
        if (isOpened || isOpening) return;
        TryInteract();
    }

    public void TryInteract()
    {
        PlayerMove nearest = null;
        float nearestSqrDist = float.MaxValue;

        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null) continue;
            float d = transform.position.SqrDistanceXZ(p.transform.position);
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
        if (currentState != ChestOpenState.Idle) return;

        isOpening = true;
        openingPlayer = player;
        currentState = ChestOpenState.Reading;
        currentReadProgress = 0f;
        previousPlayerHealth = player.CurrentHealth;
        if (highlightEffect != null) highlightEffect.SetActive(true);
        ShowProgressBar();
    }

    private void CancelOpening()
    {
        isOpening = false;
        openingPlayer = null;
        currentState = ChestOpenState.Idle;
        currentReadProgress = 0f;
        if (highlightEffect != null) highlightEffect.SetActive(false);
        if (barPanel != null) barPanel.SetActive(false);
    }

    private void OpenChest(PlayerMove player)
    {
        isOpened = true;
        isOpening = false;
        currentState = ChestOpenState.Complete;
        if (closedVisual != null) closedVisual.SetActive(false);
        if (openVisual != null) openVisual.SetActive(true);
        if (highlightEffect != null) highlightEffect.SetActive(false);
        if (barPanel != null) barPanel.SetActive(false);

        if (cachedInventory == null)
            cachedInventory = FindFirstObjectByType<InventoryManager>();

        foreach (var item in GenerateLoot())
        {
            if (cachedInventory != null)
                cachedInventory.AddItem(item, player);
        }
    }

    private void UpdateReading()
    {
        float sqrDist = transform.position.SqrDistanceXZ(openingPlayer.transform.position);
        if (sqrDist > sqrInteractPadding)
        {
            CancelOpening();
            return;
        }

        // Hit detection: track health decrease
        float currentHealth = openingPlayer.CurrentHealth;
        if (currentHealth < previousPlayerHealth - 0.01f)
        {
            currentReadProgress -= hitPenalty;
            currentReadProgress = Mathf.Max(0f, currentReadProgress);
        }
        previousPlayerHealth = currentHealth;

        currentReadProgress += Time.deltaTime;
        UpdateProgressBar();

        if (currentReadProgress >= readTime)
            OpenChest(openingPlayer);
    }

    private void BuildProgressBar()
    {
        HealthBarCanvas.EnsureSharedCanvas();
        
        barPanel = new GameObject("ChestBar_" + gameObject.name, typeof(RectTransform));
        barPanel.transform.SetParent(HealthBarCanvas.SharedCanvasRect, false);
        var panelRect = barPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = barSize;

        var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(panelRect.transform, false);
        var bgImage = bg.GetComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bg.transform, false);
        fillImage = fill.GetComponent<Image>();
        fillImage.color = Color.red;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 0f;
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;

        barPanel.SetActive(false);
    }

    private void ShowProgressBar()
    {
        if (barPanel != null) barPanel.SetActive(true);
        if (fillImage != null) fillImage.fillAmount = 0f;
    }

    private void HideProgressBar()
    {
        if (barPanel != null) barPanel.SetActive(false);
    }

    private void UpdateProgressBar()
    {
        if (fillImage == null || barPanel == null || Camera.main == null) return;

        float pct = Mathf.Clamp01(currentReadProgress / readTime);
        fillImage.fillAmount = pct;

        if (pct < 0.3f)
            fillImage.color = Color.Lerp(Color.red, Color.yellow, pct / 0.3f);
        else if (pct < 0.7f)
            fillImage.color = Color.Lerp(Color.yellow, Color.green, (pct - 0.3f) / 0.4f);
        else
            fillImage.color = Color.Lerp(Color.green, new Color(0.2f, 0.85f, 0.2f), (pct - 0.7f) / 0.3f);

        Vector3 worldPos = transform.position + barOffset;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        if (screenPos.z > 0f)
        {
            Vector2 anchoredPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                HealthBarCanvas.SharedCanvasRect, screenPos, null, out anchoredPos);
            barPanel.GetComponent<RectTransform>().anchoredPosition = anchoredPos;
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
                    items.Add(new GameItem { itemType = ItemType.Consumable, itemId = "health_potion_1", itemName = "初级生命药水", description = "50HP/秒，持续4秒", healAmount = 200f });
                else if (roll < 0.6f)
                    items.Add(new GameItem { itemType = ItemType.Currency, itemId = "gold", itemName = "金币", goldAmount = 50 });
                else
                    items.Add(GenerateRandomEquipment(false));
                break;

            case ChestType.Copper:
                if (roll < 0.4f)
                    items.Add(GenerateRandomEquipment(false));
                else if (roll < 0.6f)
                    items.Add(GenerateRandomEquipment(true));
                else if (roll < 0.8f)
                    items.Add(new GameItem { itemType = ItemType.Consumable, itemId = "health_potion_1", itemName = "初级生命药水", description = "50HP/秒，持续4秒", healAmount = 200f });
                else
                    items.Add(new GameItem { itemType = ItemType.Currency, itemId = "gold", itemName = "金币", goldAmount = 100 });

                if (Random.value < 0.1f)
                    items.Add(new GameItem { itemType = ItemType.Equipment, itemId = "weapon_gem_1", itemName = "普通武器宝石", description = "DPS+40", slotType = EquipmentSlotType.WeaponGem, rarity = ItemRarity.Common, statType = StatType.DPS, statValue = 40 });
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

    private static readonly string[] SlotNames = { "护腕", "铠甲", "护肩", "裤子" };

    private GameItem GenerateRandomEquipment(bool isQuality)
    {
        int slotIndex = Random.Range(0, ArmorSlots.Length);
        EquipmentSlotType slot = ArmorSlots[slotIndex];
        float statValue;
        string statDesc;

        if (Random.value < 0.5f)
        {
            statValue = isQuality ? QualityHP[slotIndex] : NormalHP[slotIndex];
            statDesc = "生命鍊?" + statValue + "HP";
        }
        else
        {
            statValue = isQuality ? QualityDPS[slotIndex] : NormalDPS[slotIndex];
            statDesc = "DPS+" + statValue;
        }

        bool isHealth = statValue > 50;
        string prefix = isHealth ? "生命" : "力量";

        return new GameItem
        {
            itemType = ItemType.Equipment,
            itemId = (isQuality ? "quality_" : "common_") + prefix.ToLower() + "_" + SlotNames[slotIndex],
            itemName = (isQuality ? "精品" : "普通") + prefix + SlotNames[slotIndex],
            description = statDesc,
            slotType = slot,
            rarity = isQuality ? ItemRarity.Quality : ItemRarity.Common,
            statType = isHealth ? StatType.Health : StatType.DPS,
            statValue = statValue
        };
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOpened ? Color.gray : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}

