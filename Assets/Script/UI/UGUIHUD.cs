/*
 * ============================================================
 *  UGUIHUD  -  游戏界面 HUD
 * ============================================================
 *
 * 【功能】
 *   运行时动态生成游戏的完整UI界面，包括：
 *   - 顶部栏：阶段名称、游戏时间、Boss苏醒倒计时、小怪信息
 *   - 玩家面板：攻击力、范围、速度状态、技能CD、装备数、金币
 *   - 商店面板：可以购买药水和装备
 *   - 背包面板：查看和使用/出售物品
 *
 * 【挂载对象】
 *   场景中的空对象（运行时自动创建Canvas）
 *
 * 【操作说明】
 *   - 左下/右下角面板的 "Shop" 按钮 → 打开商店
 *   - 快捷键打开背包（需要在 Inventory 面板中自行绑定快捷键）
 *
 * 【可调节参数】
 *   gameManager  - 游戏管理器引用
 *   player1 / 2  - 两个玩家的引用
 *   panelBgColor - 面板背景色
 *   barBgColor   - 进度条背景色
 *   hpHigh/Mid/LowColor - 血量颜色
 */
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// UGUI Canvas-based HUD replacing the legacy OnGUI GameHUD.
/// Creates all UI elements programmatically at runtime.
/// </summary>
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
public class UGUIHUD : MonoBehaviour
{
    #region Serialized Fields

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private PlayerMove player1;
    [SerializeField] private PlayerMove player2;

    [Header("Colors")]
    [Header("Icons")]
    [SerializeField] private Sprite speedBoostIcon;
    [SerializeField] private Color panelBgColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
    [SerializeField] private Color barBgColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color hpHighColor = Color.green;
    [SerializeField] private Color hpMidColor = Color.yellow;
    [SerializeField] private Color hpLowColor = Color.red;

    #endregion

    #region Private State

    private Canvas canvas;
    private InventoryManager inventoryManager;

    // Top bar
    private Text phaseLabel;
    private Text timerLabel;
    private Text bossTimerLabel;
    private Text minionInfoLabel;

    // Player panels
    private GameObject p1Panel;
    private GameObject p2Panel;
    private Text p1StatsLabel, p2StatsLabel;
    private Text p1SpeedLabel, p2SpeedLabel;
    private Text p1CdLabel, p2CdLabel;
    private Text p1EquipLabel, p2EquipLabel;

    // Shop
    private GameObject shopPanel;
    private Transform shopContentRoot;

    // Inventory
    private GameObject inventoryPanel;
    private Transform inventoryContentRoot;

    // State
    private bool showShop;
    private bool showInventory;
    private PlayerMove selectedPlayer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        BuildTopBar();
        BuildPlayerPanels();
        BuildShopPanel();
        BuildInventoryPanel();
    }

    private void Start()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        inventoryManager = FindFirstObjectByType<InventoryManager>();
        if (player1 == null || player2 == null)
        {
            PlayerMove[] players = FindObjectsByType<PlayerMove>(FindObjectsSortMode.None);
            if (players.Length > 0) { player1 = players[0]; if (players.Length > 1) player2 = players[1]; }
        }
    }

    private void Update()
    {
        if (gameManager == null) return;

        RefreshTopBar();
        RefreshPlayerPanel(player1, p1StatsLabel, p1SpeedLabel, p1CdLabel, p1EquipLabel);
        RefreshPlayerPanel(player2, p2StatsLabel, p2SpeedLabel, p2CdLabel, p2EquipLabel);
    }

    #endregion

    #region Top Bar

    private void BuildTopBar()
    {
        var bar = MakePanel("TopBar", canvas.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -44f), new Vector2(0f, 0f));
        bar.GetComponent<Image>().color = panelBgColor;

        phaseLabel = MakeText("PhaseLabel", bar.transform, "Escort", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Color.yellow);
        SetAnchor(phaseLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(250f, 0f));

        timerLabel = MakeText("Timer", bar.transform, "00:00", 24, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchor(timerLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        bossTimerLabel = MakeText("BossTimer", bar.transform, "BOSS Awake: 8:00 | Kills: 0", 13, FontStyle.Normal, TextAnchor.MiddleRight, Color.white);
        SetAnchor(bossTimerLabel.rectTransform, new Vector2(1f, 0.75f), new Vector2(1f, 0.75f), new Vector2(-12f, 0f), new Vector2(-300f, 0f));

        minionInfoLabel = MakeText("MinionInfo", bar.transform, "Demon Lv: Lv1 | Alive: 0/4", 13, FontStyle.Normal, TextAnchor.MiddleRight, Color.white);
        SetAnchor(minionInfoLabel.rectTransform, new Vector2(1f, 0.25f), new Vector2(1f, 0.25f), new Vector2(-12f, 0f), new Vector2(-260f, 0f));
    }

    private void RefreshTopBar()
    {
        string phaseText = gameManager.CurrentPhase switch
        {
            GamePhase.Menu => "Menu",
            GamePhase.Escort => "Phase 1: Escort",
            GamePhase.Exploration => "Phase 2: Explore",
            GamePhase.BossBattle => "BOSS Fight",
            GamePhase.Victory => "Victory!",
            GamePhase.GameOver => "Defeat",
            _ => ""
        };
        phaseLabel.text = phaseText;

        int mins = Mathf.FloorToInt(gameManager.GameTimer / 60f);
        int secs = Mathf.FloorToInt(gameManager.GameTimer % 60f);
        timerLabel.text = $"{mins:00}:{secs:00}";

        int bossMins = Mathf.FloorToInt(Mathf.Max(0, gameManager.BossAwakenTimer) / 60f);
        int bossSecs = Mathf.FloorToInt(Mathf.Max(0, gameManager.BossAwakenTimer) % 60f);
        bossTimerLabel.text = $"BOSS Awake: {bossMins}:{bossSecs:00} | Kills: {gameManager.DemonKillCount}";
        bossTimerLabel.color = gameManager.BossAwakenTimer < 120 ? Color.red : Color.white;

        minionInfoLabel.text = $"Demon Lv: Lv{gameManager.MinionLevel} | Alive: {gameManager.ActiveMinionCount}/4";
    }

    #endregion

    #region Player Panels

    private void BuildPlayerPanels()
    {
        p1Panel = BuildPlayerPanel("P1Panel", "P1", 20f, 55f);
        p2Panel = BuildPlayerPanel("P2Panel", "P2", -260f, 55f, true);
    }

    private GameObject BuildPlayerPanel(string name, string label, float left, float top, bool anchorRight = false)
    {
        var panel = MakePanel(name, canvas.transform,
            anchorRight ? Vector2.one : Vector2.zero,
            anchorRight ? Vector2.one : Vector2.zero,
            anchorRight ? new Vector2(-left, -top) : new Vector2(left, -top),
            new Vector2(240f, 200f));
        panel.GetComponent<Image>().color = panelBgColor;

        // Title
        var title = MakeText(name + "Title", panel.transform, "=== " + label + " ===", 16, FontStyle.Bold, TextAnchor.UpperLeft, Color.yellow);
        SetAnchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(220f, 22f));

        var boostIcon = MakeImage(name + "BoostIcon", panel.transform, Color.white);
        SetAnchor(boostIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -80f), new Vector2(18f, 18f));

        // Stats
        var statsLabel = MakeText(name + "Stats", panel.transform, "ATK: 100  Range: 8m", 12, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        SetAnchor(statsLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -38f), new Vector2(220f, 18f));

        // Speed
        var speedLabel = MakeText(name + "Speed", panel.transform, "Speed: Normal", 12, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        SetAnchor(speedLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -58f), new Vector2(220f, 18f));

        // Speed boost CD
        var cdLabel = MakeText(name + "Cd", panel.transform, "Q-Boost: Ready", 12, FontStyle.Normal, TextAnchor.UpperLeft, Color.yellow);
        SetAnchor(cdLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -78f), new Vector2(220f, 18f));

        // Equipment
        var equipLabel = MakeText(name + "Equip", panel.transform, "Equip: 0/5  Gold: 0", 12, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        SetAnchor(equipLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -100f), new Vector2(220f, 18f));

        // Shop button
        var shopBtn = MakeButton(name + "ShopBtn", panel.transform, "Shop", new Vector2(60f, 24f));
        SetAnchor(shopBtn.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-70f, 8f), new Vector2(60f, 24f));
        int playerIndex = anchorRight ? 2 : 1;
        shopBtn.onClick.AddListener(() => ToggleShop(playerIndex));

        // Store references
        if (anchorRight)
        {
            p2StatsLabel = statsLabel;
            p2SpeedLabel = speedLabel;
            p2CdLabel = cdLabel;
            p2EquipLabel = equipLabel;
            p2BoostIcon = boostIcon;
        }
        else
        {
            p1StatsLabel = statsLabel;
            p1SpeedLabel = speedLabel;
            p1CdLabel = cdLabel;
            p1EquipLabel = equipLabel;
            p1BoostIcon = boostIcon;
        }

        return panel;
    }

    private void RefreshPlayerPanel(PlayerMove player,
        Text statsLabel, Text speedLabel, Text cdLabel, Text equipLabel)
    {
        if (player == null) return;

        statsLabel.text = $"ATK: {player.EffectiveDamage:F0}  Range: {player.EffectiveAttackRange:F0}m";
        speedLabel.text = $"Speed: {(player.IsSpeedBoosted ? "!!BOOSTED" : "Normal")}";

        if (p2BoostIcon != null) {
            p2BoostIcon.sprite = speedBoostIcon;
            p2BoostIcon.color = player.IsSpeedBoosted ? Color.cyan : new Color(1f, 1f, 1f, 0.35f);
        }

        if (p1BoostIcon != null) {
            p1BoostIcon.sprite = speedBoostIcon;
            p1BoostIcon.color = player.IsSpeedBoosted ? Color.cyan : new Color(1f, 1f, 1f, 0.35f);
        }

        float cdRemain = player.SpeedBoostCooldownRemaining;
        cdLabel.text = cdRemain > 0 ? $"Q-Boost CD: {cdRemain:F0}s" : "Q-Boost: Ready";
        cdLabel.color = cdRemain <= 0 ? Color.yellow : Color.white;

        if (inventoryManager != null)
        {
            var equipped = player == player1 ? inventoryManager.GetP1Equipped() : inventoryManager.GetP2Equipped();
            int gold = inventoryManager.GetGold(player);
            equipLabel.text = $"Equip: {equipped.Count}/5  Gold: {gold}";
        }
    }

    #endregion

    #region Shop

    private void ToggleShop(int playerIndex)
    {
        selectedPlayer = playerIndex == 1 ? player1 : player2;
        showShop = !showShop;
        shopPanel.SetActive(showShop);
    }

    private void BuildShopPanel()
    {
        shopPanel = MakePanel("ShopPanel", canvas.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-150f, -150f), new Vector2(300f, 300f));
        shopPanel.GetComponent<Image>().color = panelBgColor;

        var title = MakeText("ShopTitle", shopPanel.transform, "=== Map Shop ===", 16, FontStyle.Bold, TextAnchor.UpperCenter, Color.yellow);
        SetAnchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -8f), new Vector2(0f, 22f));

        shopContentRoot = MakePanel("ShopContent", shopPanel.transform,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, -40f)).transform;
        Destroy(shopContentRoot.GetComponent<Image>());

        AddShopItem("HP Potion Lv1", "50HP/s, 4s", 50);
        AddShopItem("HP Wrist Common", "HP+40", 80);
        AddShopItem("ATK Wrist Common", "DPS+15", 80);
        AddShopItem("HP Chest Common", "HP+80", 120);
        AddShopItem("ATK Chest Common", "DPS+20", 120);

        var closeBtn = MakeButton("ShopCloseBtn", shopPanel.transform, "Close", new Vector2(100f, 30f));
        SetAnchor(closeBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(100f, 30f));
        closeBtn.onClick.AddListener(() => ToggleShop(1));

        shopPanel.SetActive(false);
    }

    private void AddShopItem(string name, string desc, int price)
    {
        var yOffset = shopContentRoot.childCount * -38f;
        var row = MakePanel("ShopItem" + shopContentRoot.childCount, shopContentRoot,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, yOffset), new Vector2(0f, 38f));
        Destroy(row.GetComponent<Image>());

        var nameLabel = MakeText("Name", row.transform, name, 12, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
        SetAnchor(nameLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(170f, 0f));

        var descLabel = MakeText("Desc", row.transform, desc, 11, FontStyle.Normal, TextAnchor.MiddleLeft, Color.gray);
        SetAnchor(descLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(6f, 2f), new Vector2(120f, 16f));

        bool canAfford = selectedPlayer != null && inventoryManager != null && inventoryManager.GetGold(selectedPlayer) >= price;
        var buyBtn = MakeButton("BuyBtn", row.transform, price + "G", new Vector2(60f, 24f));
        SetAnchor(buyBtn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-65f, 0f), new Vector2(60f, 24f));
        buyBtn.interactable = canAfford;
        var capturedName = name;
        var capturedDesc = desc;
        var capturedPrice = price;
        buyBtn.onClick.AddListener(() =>
        {
            var inv = inventoryManager;
            if (inv != null && inv.SpendGold(capturedPrice, selectedPlayer))
            {
                GameItem item = CreateShopItem(capturedName, capturedDesc);
                inv.AddItem(item, selectedPlayer);
            }
        });
    }

    private GameItem CreateShopItem(string name, string desc)
    {
        if (name.Contains("Potion"))
            return new GameItem { itemType = ItemType.Consumable, itemId = "health_potion_1", itemName = name, description = desc, healAmount = 200f };

        bool isHealth = name.Contains("HP");
        EquipmentSlotType slot = EquipmentSlotType.Wrist;
        if (name.Contains("Chest")) slot = EquipmentSlotType.Chest;
        else if (name.Contains("Shoulder")) slot = EquipmentSlotType.Shoulder;
        else if (name.Contains("Pants")) slot = EquipmentSlotType.Pants;

        float value = 40f;
        if (isHealth)
        {
            if (name.Contains("Chest")) value = 80f;
            else if (slot == EquipmentSlotType.Shoulder) value = 50f;
            else if (slot == EquipmentSlotType.Pants) value = 60f;
            else value = 40f;
        }
        else
        {
            if (name.Contains("Chest")) value = 20f;
            else value = 15f;
        }

        return new GameItem
        {
            itemType = ItemType.Equipment,
            itemId = $"shop_{name}",
            itemName = name,
            description = desc,
            slotType = slot,
            rarity = ItemRarity.Common,
            statType = isHealth ? StatType.Health : StatType.DPS,
            statValue = value
        };
    }

    #endregion

    #region Inventory

    public void ToggleInventory(int playerIndex)
    {
        selectedPlayer = playerIndex == 1 ? player1 : player2;
        showInventory = !showInventory;
        inventoryPanel.SetActive(showInventory);
        if (showInventory) RefreshInventoryItems();
    }

    private void BuildInventoryPanel()
    {
        inventoryPanel = MakePanel("InventoryPanel", canvas.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-140f, -125f), new Vector2(280f, 250f));
        inventoryPanel.GetComponent<Image>().color = panelBgColor;

        var title = MakeText("InvTitle", inventoryPanel.transform, "=== Backpack ===", 16, FontStyle.Bold, TextAnchor.UpperCenter, Color.yellow);
        SetAnchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -8f), new Vector2(0f, 22f));

        inventoryContentRoot = MakePanel("InvContent", inventoryPanel.transform,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, -40f)).transform;
        Destroy(inventoryContentRoot.GetComponent<Image>());

        var closeBtn = MakeButton("InvCloseBtn", inventoryPanel.transform, "Close", new Vector2(100f, 30f));
        SetAnchor(closeBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(100f, 30f));
        closeBtn.onClick.AddListener(() => { showInventory = false; inventoryPanel.SetActive(false); });

        inventoryPanel.SetActive(false);
    }

    private void RefreshInventoryItems()
    {
        // Clear existing
        foreach (Transform child in inventoryContentRoot)
            Destroy(child.gameObject);

        if (selectedPlayer == null) return;
        var inv = inventoryManager;
        if (inv == null) return;

        var backpack = selectedPlayer == player1 ? inv.GetP1Backpack() : inv.GetP2Backpack();
        float yOffset = 0f;
        foreach (var item in backpack)
        {
            var row = MakePanel("InvItem" + yOffset, inventoryContentRoot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, yOffset), new Vector2(0f, 28f));
            Destroy(row.GetComponent<Image>());

            var label = MakeText("Label", row.transform,
                $"{item.itemName} - {item.description}", 11, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
            SetAnchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(190f, 0f));

            if (item.itemType == ItemType.Consumable)
            {
                var useBtn = MakeButton("UseBtn", row.transform, "Use", new Vector2(50f, 22f));
                SetAnchor(useBtn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-55f, 0f), new Vector2(50f, 22f));
                var capturedItem = item;
                useBtn.onClick.AddListener(() =>
                {
                    inventoryManager.UseConsumable(capturedItem.itemId, selectedPlayer);
                    RefreshInventoryItems();
                });
            }
            else
            {
                var sellBtn = MakeButton("SellBtn", row.transform, "Sell", new Vector2(50f, 22f));
                SetAnchor(sellBtn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-55f, 0f), new Vector2(50f, 22f));
                var capturedItem = item;
                sellBtn.onClick.AddListener(() =>
                {
                    inventoryManager.SellItem(capturedItem.itemId, selectedPlayer);
                    RefreshInventoryItems();
                });
            }

            yOffset -= 26f;
        }
    }

    #endregion

    #region UI Helpers

    private GameObject MakePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        SetAnchor(go.GetComponent<RectTransform>(), anchorMin, anchorMax, anchoredPosition, sizeDelta);
        return go;
    }

    private Text MakeText(string name, Transform parent, string text, int fontSize,
        FontStyle style, TextAnchor alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.alignment = alignment;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    private Image MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private Button MakeButton(string name, Transform parent, string label, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.25f, 1f);

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        colors.pressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        btn.colors = colors;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(go.transform, false);
        var t = labelGo.GetComponent<Text>();
        t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 12;
        t.fontStyle = FontStyle.Normal;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;

        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        return btn;
    }

    private void SetAnchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;
    }

    #endregion
}
