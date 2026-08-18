using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aligns and repairs the UI framework scene after manual layout edits.
/// </summary>
public static class UIFrameworkLayoutFixer
{
    private static readonly Color PanelColor = new Color(0.10f, 0.10f, 0.12f, 0.92f);

    [MenuItem("FDYB/UI/Fix Layout Alignment")]
    public static void FixLayout()
    {
        var ui = GameObject.Find("UI_HUD");
        if (ui == null)
        {
            EditorUtility.DisplayDialog("FDYB UI", "UI_HUD not found in the current scene.", "OK");
            return;
        }

        var root = ui.transform;
        FixTopBar(root);
        FixLeftTargets(root);
        FixDialogueBar(root);
        FixEquipmentPanel(root);
        FixStatsPanel(root);
        FixTargetSelector(root);
        FixBackpackPanel(root);
        FixMinimap(root);

        EditorSceneManager.MarkSceneDirty(ui.scene);
        EditorSceneManager.SaveScene(ui.scene);
        EditorUtility.DisplayDialog("FDYB UI", "Layout alignment fixed and scene saved.", "OK");
    }

    private static void FixTopBar(Transform root)
    {
        var topBar = FindChild(root, "TopBar");
        if (topBar == null) return;

        SetRect(FindChild(topBar, "GameTitle"), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(140f, 30f));
        SetRect(FindChild(topBar, "GameTimer"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 40f));
        SetRect(FindChild(topBar, "ExitBtn"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(72f, 40f));
    }

    private static void FixLeftTargets(Transform root)
    {
        var targets = EnsurePanel(root, "LeftTargets");
        SetRect(targets, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -100f), new Vector2(250f, 560f));

        var topBar = FindChild(root, "TopBar");
        if (topBar != null)
        {
            Reparent(topBar, targets, "Row_OpponentPlayer");
            Reparent(topBar, targets, "Avatar_Boss");
            Reparent(topBar, targets, "Avatar_Elite01");
            Reparent(topBar, targets, "Avatar_Elite02");
            Reparent(topBar, targets, "Avatar_Elite03");
        }

        var playerRow = FindChild(targets, "Row_OpponentPlayer");
        if (playerRow != null)
        {
            SetRect(playerRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(8f, -10f), new Vector2(234f, 96f));

            SetRect(FindChild(playerRow, "Avatar_OpponentPlayer"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(64f, 64f));
            SetRect(FindChild(playerRow, "Title_OpponentPlayer"), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(150f, 24f));

            var hp = FindChild(playerRow, "Info_OpponentPlayer");
            if (hp != null)
            {
                hp.name = "Info_HP_OpponentPlayer";
                SetRect(hp, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(10f, -36f), new Vector2(150f, 22f));
                var hpText = hp.GetComponent<Text>();
                if (hpText != null) hpText.text = "生命值: 100";
            }

            var atk = FindChild(playerRow, "Info_OpponentPlayer (1)");
            if (atk != null)
            {
                atk.name = "Info_ATK_OpponentPlayer";
                SetRect(atk, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(10f, -60f), new Vector2(150f, 22f));
                var atkText = atk.GetComponent<Text>();
                if (atkText != null) atkText.text = "攻击力: 10";
            }
        }

        SetRect(FindChild(targets, "Avatar_Boss"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(8f, -120f), new Vector2(64f, 64f));
        SetRect(FindChild(targets, "Avatar_Elite01"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(8f, -230f), new Vector2(64f, 64f));
        SetRect(FindChild(targets, "Avatar_Elite02"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(8f, -340f), new Vector2(64f, 64f));
        SetRect(FindChild(targets, "Avatar_Elite03"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(8f, -450f), new Vector2(64f, 64f));
    }

    private static void FixDialogueBar(Transform root)
    {
        var dialogue = FindChild(root, "DialogueBar");
        if (dialogue == null) return;

        SetRect(dialogue, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(16f, 16f), new Vector2(720f, 120f));
        SetRect(FindChild(dialogue, "DialogueText"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(680f, 90f));
    }

    private static void FixEquipmentPanel(Transform root)
    {
        var panel = FindChild(root, "EquipmentPanel");
        if (panel == null) return;

        SetRect(panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(250f, -60f), new Vector2(380f, 250f));
        SetRect(FindChild(panel, "EquipmentTitle"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(200f, 24f));

        string[] left = { "EquipSlot_肩甲", "EquipSlot_胸甲", "EquipSlot_护腕", "EquipSlot_鞋子" };
        for (int i = 0; i < left.Length; i++)
        {
            SetRect(FindChild(panel, left[i]), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(16f, -44f - i * 52f), new Vector2(64f, 44f));
        }

        SetRect(FindChild(panel, "EquipSlot_武器"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(296f, -44f), new Vector2(72f, 52f));
        SetRect(FindChild(panel, "EquipSlot_宝石"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(296f, -108f), new Vector2(72f, 52f));
    }

    private static void FixStatsPanel(Transform root)
    {
        var panel = FindChild(root, "StatsPanel");
        if (panel == null) return;

        SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -60f), new Vector2(380f, 280f));

        SetRect(FindChild(panel, "BossCountdown"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(320f, 30f));
        SetRect(FindChild(panel, "StatsTitle"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(16f, -46f), new Vector2(160f, 24f));
        SetRect(FindChild(panel, "StatATK"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(16f, -76f), new Vector2(180f, 22f));
        SetRect(FindChild(panel, "StatHP"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(16f, -102f), new Vector2(180f, 22f));
        SetRect(FindChild(panel, "StatDEF"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(16f, -128f), new Vector2(180f, 22f));

        SetRect(FindChild(panel, "Skill1"), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-16f, -56f), new Vector2(56f, 56f));
        SetRect(FindChild(panel, "Skill2"), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-80f, -56f), new Vector2(56f, 56f));
    }

    private static void FixTargetSelector(Transform root)
    {
        var bar = FindChild(root, "TargetSelector");
        if (bar == null) return;

        SetRect(bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 180f), new Vector2(420f, 64f));

        string[] targets = { "Target_P1", "Target_P2", "Target_P3", "Target_P4" };
        for (int i = 0; i < targets.Length; i++)
        {
            SetRect(FindChild(bar, targets[i]), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-160f + i * 108f, 0f), new Vector2(56f, 56f));
        }
    }

    private static void FixBackpackPanel(Transform root)
    {
        var panel = FindChild(root, "BackpackPanel");
        if (panel == null) return;

        SetRect(panel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-270f, -60f), new Vector2(420f, 360f));

        SetRect(FindChild(panel, "GoldLabel"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(140f, 26f));
        SetRect(FindChild(panel, "StopBtn"), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-16f, -12f), new Vector2(80f, 36f));

        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                string slotName = "BackpackSlot_" + (row * 4 + col + 1);
                var slot = FindChild(panel, slotName);
                SetRect(slot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f),
                    new Vector2(16f + col * 96f, -60f - row * 110f), new Vector2(64f, 64f));
            }
        }
    }

    private static void FixMinimap(Transform root)
    {
        var panel = FindChild(root, "Minimap");
        if (panel == null) return;

        SetRect(panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-16f, 16f), new Vector2(300f, 300f));

        var body = FindChild(panel, "MinimapBody");
        if (body != null)
        {
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.pivot = new Vector2(0.5f, 0.5f);
            body.offsetMin = new Vector2(8f, 8f);
            body.offsetMax = new Vector2(-44f, -8f);
        }

        SetRect(FindChild(panel, "MinimapToggle"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(36f, 80f));
        SetRect(FindChild(body, "MinimapHint"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 30f));
    }

    private static RectTransform EnsurePanel(Transform parent, string name)
    {
        var existing = FindChild(parent, name);
        if (existing != null) return existing;

        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();
        img.color = PanelColor;
        img.raycastTarget = false;
        return rt;
    }

    private static void Reparent(Transform from, Transform to, string name)
    {
        var child = FindChild(from, name);
        if (child != null && child.parent != to)
            child.SetParent(to, false);
    }

    private static void SetRect(
        RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rt == null) return;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;
    }

    private static RectTransform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
        {
            if (child.name == name) return child as RectTransform;
            var deeper = FindChild(child, name);
            if (deeper != null) return deeper;
        }
        return null;
    }
}
