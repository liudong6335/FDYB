using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Standalone UI framework builder. Creates the big screen regions and
/// placeholder controls so the layout can be iterated before art is added.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
public class UIFrameworkBuilder : MonoBehaviour
{
    [Header("Layout Colors")]
    [SerializeField] private Color barColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
    [SerializeField] private Color panelColor = new Color(0.10f, 0.10f, 0.12f, 0.92f);
    [SerializeField] private Color viewportColor = new Color(0.03f, 0.04f, 0.06f, 0.10f);

    private void OnEnable()
    {
        Build();
    }

    public void Build()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) return;

        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (transform.Find("TopBar") != null) return;

        var roots = UIFrameworkRoots.Build(transform, barColor, panelColor, viewportColor);
        BuildTopBar(roots.TopBar);
        BuildToolbar(roots.LeftToolbar);
        BuildBottomBar(roots.BottomBar);
        BuildRightPanel(roots.RightPanel);
    }

    private static void BuildTopBar(RectTransform topBar)
    {
        MakeText("PhaseLabel", topBar, "Escort", 18, FontStyle.Bold, TextAnchor.MiddleLeft,
            Color.yellow, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(300f, 30f), new Vector2(0f, 0.5f));

        MakeText("TitleLabel", topBar, "FDYB", 26, FontStyle.Bold, TextAnchor.MiddleCenter,
            Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 36f), new Vector2(0.5f, 0.5f));

        MakeText("Timer", topBar, "00:00", 24, FontStyle.Bold, TextAnchor.MiddleRight,
            Color.white, new Vector2(1f, 0.5f), new Vector2(-88f, 0f), new Vector2(160f, 30f), new Vector2(1f, 0.5f));

        var menuBtn = UIHelpers.MakeButton("TopMenuBtn", topBar, "Menu", new Vector2(56f, 36f));
        var menuRt = menuBtn.GetComponent<RectTransform>();
        menuRt.pivot = new Vector2(1f, 0.5f);
        UIHelpers.SetAnchor(menuRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(56f, 36f));
    }

    private static void BuildToolbar(RectTransform toolbar)
    {
        string[] labels = { "Bag", "Shop", "Quest", "Settings" };
        for (int i = 0; i < labels.Length; i++)
        {
            var btn = UIHelpers.MakeButton("ToolBtn_" + labels[i], toolbar, labels[i], new Vector2(72f, 64f));
            var rt = btn.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0f, 1f);
            UIHelpers.SetAnchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -10f - i * 74f), new Vector2(72f, 64f));

            string label = labels[i];
            btn.onClick.AddListener(() => Debug.Log("[UI] Toolbar button: " + label));
        }
    }

    private static void BuildBottomBar(RectTransform bottomBar)
    {
        for (int i = 0; i < 4; i++)
        {
            var slot = UIHelpers.MakeButton("ActionSlot" + (i + 1), bottomBar, (i + 1).ToString(), new Vector2(72f, 44f));
            UIHelpers.SetAnchor(slot.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-165f + i * 110f, 0f), new Vector2(72f, 44f));
        }
    }

    private static void BuildRightPanel(RectTransform rightPanel)
    {
        string[] tabs = { "Bag", "Shop", "Quest" };
        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = UIHelpers.MakeButton("RightTab_" + tabs[i], rightPanel, tabs[i], new Vector2(100f, 30f));
            var rt = tab.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0f, 1f);
            UIHelpers.SetAnchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(10f + i * 108f, -10f), new Vector2(100f, 30f));

            string label = tabs[i];
            tab.onClick.AddListener(() => Debug.Log("[UI] Right panel tab: " + label));
        }

        var content = UIHelpers.MakePanel("RightPanelContent", rightPanel,
            new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        content.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.28f);

        var contentRt = content.GetComponent<RectTransform>();
        contentRt.offsetMin = new Vector2(8f, 8f);
        contentRt.offsetMax = new Vector2(-8f, -46f);

        MakeText("ContentHint", content.transform, "Content Area", 16, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Color(1f, 1f, 1f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 40f), new Vector2(0.5f, 0.5f));
    }

    private static Text MakeText(
        string name, Transform parent, string text, int fontSize, FontStyle style,
        TextAnchor alignment, Color color, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var label = UIHelpers.MakeText(name, parent, text, fontSize, style, alignment, color);
        var rt = label.rectTransform;
        rt.pivot = pivot;
        UIHelpers.SetAnchor(rt, anchor, anchor, pos, size);
        return label;
    }
}
