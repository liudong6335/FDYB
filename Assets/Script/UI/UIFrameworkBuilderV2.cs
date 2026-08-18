using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the in-game UI layout described by the design notes:
/// target avatars, top timer/exit, dialogue, equipment, stats,
/// backpack grid and collapsible minimap.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
public class UIFrameworkBuilderV2 : MonoBehaviour
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

        BuildTopBar(transform);
        BuildLeftTargets(transform);
        BuildDialogueBar(transform);
        BuildEquipmentPanel(transform);
        BuildStatsPanel(transform);
        BuildTargetSelector(transform);
        BuildBackpackPanel(transform);
        BuildMinimap(transform);
    }

    private void BuildTopBar(Transform root)
    {
        var topBar = MakeRegion("TopBar", root,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, -90f), barColor);

        MakeText("GameTitle", topBar, "FDYB", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Color.gray,
            new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(140f, 30f), new Vector2(0f, 0.5f));

        MakeText("GameTimer", topBar, "00:00", 30, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 40f), new Vector2(0.5f, 0.5f));

        var exitBtn = UIHelpers.MakeButton("ExitBtn", topBar, "退出", new Vector2(72f, 40f));
        var exitRt = exitBtn.GetComponent<RectTransform>();
        exitRt.pivot = new Vector2(1f, 0.5f);
        UIHelpers.SetAnchor(exitRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(72f, 40f));
        exitBtn.onClick.AddListener(() => Debug.Log("[UI] Exit confirm dialog placeholder"));
    }

    private void BuildLeftTargets(Transform root)
    {
        var panel = MakeRegion("LeftTargets", root,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -100f), new Vector2(250f, 560f), panelColor);

        MakeTargetRow(panel, "OpponentPlayer", "对手玩家", "HP: 100  ATK: 10", 0, true);
        MakeTargetRow(panel, "Boss", "Boss", "", 1, false);
        MakeTargetRow(panel, "Elite01", "精英怪 01", "", 2, false);
        MakeTargetRow(panel, "Elite02", "精英怪 02", "", 3, false);
        MakeTargetRow(panel, "Elite03", "精英怪 03", "", 4, false);
    }

    private static void MakeTargetRow(
        Transform parent, string name, string title, string info, int index, bool showInfo)
    {
        var row = MakeRegion("Row_" + name, parent,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(8f, -10f - index * 104f), new Vector2(234f, 96f),
            new Color(0f, 0f, 0f, 0.25f));

        var avatar = UIHelpers.MakeButton("Avatar_" + name, row, "", new Vector2(64f, 64f));
        var avatarRt = avatar.GetComponent<RectTransform>();
        avatarRt.pivot = new Vector2(1f, 0.5f);
        UIHelpers.SetAnchor(avatarRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-6f, 0f), new Vector2(64f, 64f));

        MakeText("Title_" + name, row, title, 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white,
            new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(150f, 24f), new Vector2(0f, 1f));

        if (showInfo)
        {
            MakeText("Info_" + name, row, info, 12, FontStyle.Normal, TextAnchor.UpperLeft,
                new Color(1f, 1f, 1f, 0.8f),
                new Vector2(0f, 1f), new Vector2(10f, -36f), new Vector2(150f, 44f), new Vector2(0f, 1f));
        }
    }

    private void BuildDialogueBar(Transform root)
    {
        var dialogue = MakeRegion("DialogueBar", root,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(16f, 16f), new Vector2(700f, 110f), panelColor);

        MakeText("DialogueText", dialogue, "对话内容区域", 15, FontStyle.Normal, TextAnchor.UpperLeft, Color.white,
            new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(660f, 80f), new Vector2(0f, 1f));
    }

    private void BuildEquipmentPanel(Transform root)
    {
        var panel = MakeRegion("EquipmentPanel", root,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(16f, 0f), new Vector2(380f, 250f), panelColor);

        MakeText("EquipmentTitle", panel, "装备栏", 15, FontStyle.Bold, TextAnchor.UpperLeft, Color.yellow,
            new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(200f, 24f), new Vector2(0f, 1f));

        string[] leftSlots = { "肩甲", "胸甲", "护腕", "鞋子" };
        for (int i = 0; i < leftSlots.Length; i++)
        {
            var slot = UIHelpers.MakeButton("EquipSlot_" + leftSlots[i], panel, leftSlots[i], new Vector2(64f, 44f));
            var rt = slot.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0f, 0f);
            UIHelpers.SetAnchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -40f - i * 52f), new Vector2(64f, 44f));
        }

        string[] rightSlots = { "武器", "宝石" };
        for (int i = 0; i < rightSlots.Length; i++)
        {
            var slot = UIHelpers.MakeButton("EquipSlot_" + rightSlots[i], panel, rightSlots[i], new Vector2(72f, 52f));
            var rt = slot.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0f, 0f);
            UIHelpers.SetAnchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(92f, -40f - i * 60f), new Vector2(72f, 52f));
        }
    }

    private void BuildStatsPanel(Transform root)
    {
        var panel = MakeRegion("StatsPanel", root,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(380f, 280f), panelColor);

        MakeText("BossCountdown", panel, "Boss 倒计时 00:00", 16, FontStyle.Bold, TextAnchor.MiddleCenter, Color.red,
            new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(320f, 30f), new Vector2(0.5f, 1f));

        MakeText("StatsTitle", panel, "角色属性", 14, FontStyle.Bold, TextAnchor.UpperLeft, Color.yellow,
            new Vector2(0f, 1f), new Vector2(12f, -44f), new Vector2(160f, 24f), new Vector2(0f, 1f));

        MakeText("StatATK", panel, "攻击力: 0", 13, FontStyle.Normal, TextAnchor.UpperLeft, Color.white,
            new Vector2(0f, 1f), new Vector2(12f, -72f), new Vector2(180f, 22f), new Vector2(0f, 1f));

        MakeText("StatHP", panel, "生命值: 0", 13, FontStyle.Normal, TextAnchor.UpperLeft, Color.white,
            new Vector2(0f, 1f), new Vector2(12f, -98f), new Vector2(180f, 22f), new Vector2(0f, 1f));

        MakeText("StatDEF", panel, "防御值: 0", 13, FontStyle.Normal, TextAnchor.UpperLeft, Color.white,
            new Vector2(0f, 1f), new Vector2(12f, -124f), new Vector2(180f, 22f), new Vector2(0f, 1f));

        var skill1 = UIHelpers.MakeButton("Skill1", panel, "S1", new Vector2(56f, 56f));
        var skill1Rt = skill1.GetComponent<RectTransform>();
        skill1Rt.pivot = new Vector2(1f, 1f);
        UIHelpers.SetAnchor(skill1Rt, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-12f, -60f), new Vector2(56f, 56f));

        var skill2 = UIHelpers.MakeButton("Skill2", panel, "S2", new Vector2(56f, 56f));
        var skill2Rt = skill2.GetComponent<RectTransform>();
        skill2Rt.pivot = new Vector2(1f, 1f);
        UIHelpers.SetAnchor(skill2Rt, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-12f, -124f), new Vector2(56f, 56f));
    }

    private static void BuildTargetSelector(Transform root)
    {
        var bar = MakeRegion("TargetSelector", root,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 180f), new Vector2(420f, 64f), new Color(0f, 0f, 0f, 0.22f));

        string[] targets = { "P1", "P2", "P3", "P4" };
        for (int i = 0; i < targets.Length; i++)
        {
            var btn = UIHelpers.MakeButton("Target_" + targets[i], bar, targets[i], new Vector2(56f, 56f));
            UIHelpers.SetAnchor(btn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-160f + i * 108f, 0f), new Vector2(56f, 56f));
        }
    }

    private void BuildBackpackPanel(Transform root)
    {
        var panel = MakeRegion("BackpackPanel", root,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-16f, 20f), new Vector2(420f, 360f), panelColor);

        MakeText("GoldLabel", panel, "金币: 0", 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.yellow,
            new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(140f, 26f), new Vector2(0f, 1f));

        var stopBtn = UIHelpers.MakeButton("StopBtn", panel, "止戈", new Vector2(80f, 36f));
        var stopRt = stopBtn.GetComponent<RectTransform>();
        stopRt.pivot = new Vector2(1f, 1f);
        UIHelpers.SetAnchor(stopRt, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-12f, -8f), new Vector2(80f, 36f));

        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                string slotName = "BackpackSlot_" + (row * 2 + col + 1);
                var slot = UIHelpers.MakeButton(slotName, panel, (row * 2 + col + 1).ToString(), new Vector2(64f, 64f));
                var rt = slot.GetComponent<RectTransform>();
                rt.pivot = new Vector2(0f, 0f);
                UIHelpers.SetAnchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(12f + col * 76f, -52f - row * 72f), new Vector2(64f, 64f));
            }
        }
    }

    private void BuildMinimap(Transform root)
    {
        var panel = MakeRegion("Minimap", root,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-16f, 16f), new Vector2(300f, 300f), panelColor);

        var body = MakeStretchRegion("MinimapBody", panel,
            new Vector2(8f, 8f), new Vector2(-44f, -8f), new Color(0f, 0f, 0f, 0.40f));

        MakeText("MinimapHint", body, "小地图", 14, FontStyle.Normal, TextAnchor.MiddleCenter,
            new Color(1f, 1f, 1f, 0.55f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 30f), new Vector2(0.5f, 0.5f));

        var toggleBtn = UIHelpers.MakeButton("MinimapToggle", panel, "收起", new Vector2(36f, 80f));
        var toggleRt = toggleBtn.GetComponent<RectTransform>();
        toggleRt.pivot = new Vector2(1f, 0.5f);
        UIHelpers.SetAnchor(toggleRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-4f, 0f), new Vector2(36f, 80f));

        var toggleLabel = toggleBtn.GetComponentInChildren<Text>();
        toggleBtn.onClick.AddListener(() =>
        {
            bool show = !body.gameObject.activeSelf;
            body.gameObject.SetActive(show);
            if (toggleLabel != null) toggleLabel.text = show ? "收起" : "打开";
        });
    }

    private static RectTransform MakeRegion(
        string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    private static RectTransform MakeStretchRegion(
        string name, Transform parent,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
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
