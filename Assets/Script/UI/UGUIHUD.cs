using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main HUD orchestrator. Creates a Canvas at runtime and delegates
/// panel logic to dedicated controllers.
/// </summary>
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
public class UGUIHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private PlayerMove player1;
    [SerializeField] private PlayerMove player2;

    [Header("Icons")]
    [SerializeField] private Sprite speedBoostIcon;

    [Header("Colors")]
    [SerializeField] private Color panelBgColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
    // Retained for Inspector backward compatibility (no longer used internally)
    [SerializeField] [HideInInspector] private Color barBgColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [SerializeField] [HideInInspector] private Color hpHighColor = Color.green;
    [SerializeField] [HideInInspector] private Color hpMidColor = Color.yellow;
    [SerializeField] [HideInInspector] private Color hpLowColor = Color.red;

    private Canvas canvas;
    private InventoryManager inventoryManager;
    private TopBarController topBar;
    private PlayerPanelController p1PanelCtrl;
    private PlayerPanelController p2PanelCtrl;
    private ShopController shop;
    private BackpackController backpack;

    // Expose player references so BackpackController can resolve P1/P2 without coupling
    public static PlayerMove Player1Ref { get; private set; }
    public static PlayerMove Player2Ref { get; private set; }

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Build sub-panels
        topBar = new TopBarController();
        topBar.Build(canvas.transform, panelBgColor);

        p1PanelCtrl = new PlayerPanelController();
        p1PanelCtrl.Build(canvas.transform, 1, 20f, 55f, false, panelBgColor, speedBoostIcon, OnShopClick);

        p2PanelCtrl = new PlayerPanelController();
        p2PanelCtrl.Build(canvas.transform, 2, 260f, 55f, true, panelBgColor, speedBoostIcon, OnShopClick);

        shop = new ShopController();
        shop.Build(canvas.transform, panelBgColor);

        backpack = new BackpackController();
        backpack.Build(canvas.transform, panelBgColor);
    }

    private void Start()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        inventoryManager = FindFirstObjectByType<InventoryManager>();
        if (player1 == null || player2 == null)
        {
            var players = FindObjectsByType<PlayerMove>(FindObjectsSortMode.None);
            if (players.Length > 0) { player1 = players[0]; if (players.Length > 1) player2 = players[1]; }
        }

        Player1Ref = player1;
        Player2Ref = player2;
    }

    private void Update()
    {
        if (gameManager == null) return;

        topBar.Refresh(gameManager);
        RefreshPlayerPanel(p1PanelCtrl, player1);
        RefreshPlayerPanel(p2PanelCtrl, player2);
    }

    private void OnShopClick(int playerIndex)
    {
        shop.Toggle(playerIndex == 1 ? player1 : player2);
    }

    private void RefreshPlayerPanel(PlayerPanelController panel, PlayerMove player)
    {
        if (player == null) { panel.Refresh(null, 0, 0); return; }

        int equipCount = 0;
        int gold = 0;
        if (inventoryManager != null)
        {
            var equipped = player == player1
                ? inventoryManager.GetP1Equipped()
                : inventoryManager.GetP2Equipped();
            equipCount = equipped.Count;
            gold = inventoryManager.GetGold(player);
        }
        panel.Refresh(player, equipCount, gold);
    }

    /// <summary>Toggle the backpack panel for a given player (1 or 2).</summary>
    public void ToggleInventory(int playerIndex)
    {
        PlayerMove player = playerIndex == 1 ? player1 : player2;
        var backpackList = player == player1
            ? inventoryManager.GetP1Backpack()
            : inventoryManager.GetP2Backpack();
        backpack.Toggle(player, backpackList);
    }
}
