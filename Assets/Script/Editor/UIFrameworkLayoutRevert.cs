using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Restores the UI framework scene to the manually adjusted layout
/// that existed before the automatic alignment pass.
/// </summary>
public static class UIFrameworkLayoutRevert
{
    [MenuItem("FDYB/UI/Revert Layout To Manual Version")]
    public static void RevertLayout()
    {
        var ui = GameObject.Find("UI_HUD");
        if (ui == null)
        {
            EditorUtility.DisplayDialog("FDYB UI", "UI_HUD not found in the current scene.", "OK");
            return;
        }

        var root = ui.transform;
        var topBar = FindChild(root, "TopBar");
        var leftTargets = FindChild(root, "LeftTargets");

        if (topBar != null && leftTargets != null)
        {
            Reparent(leftTargets, topBar, "Row_OpponentPlayer");
            Reparent(leftTargets, topBar, "Avatar_Boss");
            Reparent(leftTargets, topBar, "Avatar_Elite01");
            Reparent(leftTargets, topBar, "Avatar_Elite02");
            Reparent(leftTargets, topBar, "Avatar_Elite03");
            Object.DestroyImmediate(leftTargets.gameObject);
        }

        var hp = FindChild(root, "Info_HP_OpponentPlayer");
        if (hp != null) hp.name = "Info_OpponentPlayer";
        var atk = FindChild(root, "Info_ATK_OpponentPlayer");
        if (atk != null) atk.name = "Info_OpponentPlayer (1)";

        RestoreTopBar(root);
        RestoreDialogueBar(root);
        RestoreEquipmentPanel(root);
        RestoreStatsPanel(root);
        RestoreTargetSelector(root);
        RestoreBackpackPanel(root);
        RestoreMinimap(root);

        EditorSceneManager.MarkSceneDirty(ui.scene);
        EditorSceneManager.SaveScene(ui.scene);
        EditorUtility.DisplayDialog("FDYB UI", "Restored the manually adjusted layout.", "OK");
    }

    private static void RestoreTopBar(Transform root)
    {
        var topBar = FindChild(root, "TopBar");
        if (topBar == null) return;

        SetRect(topBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, -90f));
        SetRect(FindChild(topBar, "GameTitle"), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(849.4f, 22.1f), new Vector2(140f, 30f));
        SetRect(FindChild(topBar, "GameTimer"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -8.8f), new Vector2(200f, 40f));
        SetRect(FindChild(topBar, "ExitBtn"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(72f, 40f));

        SetRect(FindChild(topBar, "Avatar_Boss"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(-1821f, -145f), new Vector2(64f, 64f));
        SetRect(FindChild(topBar, "Avatar_Elite01"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(-1821f, -249f), new Vector2(64f, 64f));
        SetRect(FindChild(topBar, "Avatar_Elite02"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(-1819f, -353f), new Vector2(64f, 64f));
        SetRect(FindChild(topBar, "Avatar_Elite03"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(-1816f, -457f), new Vector2(64f, 64f));

        var row = FindChild(topBar, "Row_OpponentPlayer");
        if (row != null)
        {
            SetRect(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(27f, 52f), new Vector2(234f, 96f));
            SetRect(FindChild(row, "Avatar_OpponentPlayer"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-156f, 0f), new Vector2(64f, 64f));
            SetRect(FindChild(row, "Title_OpponentPlayer"), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(80f, -12f), new Vector2(150f, 24f));
            SetRect(FindChild(row, "Info_OpponentPlayer"), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(80f, -38.3f), new Vector2(150f, 44f));
            SetRect(FindChild(row, "Info_OpponentPlayer (1)"), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(80f, -62f), new Vector2(150f, 44f));
        }
    }

    private static void RestoreDialogueBar(Transform root)
    {
        var dialogue = FindChild(root, "DialogueBar");
        if (dialogue == null) return;

        SetRect(dialogue, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(27f, 8f), new Vector2(700f, 110f));
        SetRect(FindChild(dialogue, "DialogueText"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(18f, -10f), new Vector2(660f, 80f));
    }

    private static void RestoreEquipmentPanel(Transform root)
    {
        var panel = FindChild(root, "EquipmentPanel");
        if (panel == null) return;

        SetRect(panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(558f, -441f), new Vector2(380f, 250f));
        SetRect(FindChild(panel, "EquipmentTitle"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(200f, 24f));

        SetRect(FindChild(panel, "EquipSlot_肩甲"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(95f, -87.8f), new Vector2(64f, 44f));
        SetRect(FindChild(panel, "EquipSlot_胸甲"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(25f, -147.9f), new Vector2(64f, 44f));
        SetRect(FindChild(panel, "EquipSlot_护腕"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(167f, -144f), new Vector2(64f, 44f));
        SetRect(FindChild(panel, "EquipSlot_鞋子"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(97f, -215f), new Vector2(64f, 44f));

        SetRect(FindChild(panel, "EquipSlot_武器"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(278f, -97f), new Vector2(72f, 52f));
        SetRect(FindChild(panel, "EquipSlot_宝石"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(276f, -218.5f), new Vector2(72f, 52f));
    }

    private static void RestoreStatsPanel(Transform root)
    {
        var panel = FindChild(root, "StatsPanel");
        if (panel == null) return;

        SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-4f, -479f), new Vector2(380f, 280f));
        SetRect(FindChild(panel, "BossCountdown"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(17.3f, 29f), new Vector2(320f, 30f));
        SetRect(FindChild(panel, "StatsTitle"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(3.3f, -17f), new Vector2(160f, 24f));
        SetRect(FindChild(panel, "StatATK"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(110.9f, -172f), new Vector2(180f, 22f));
        SetRect(FindChild(panel, "StatHP"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(110f, -56f), new Vector2(180f, 22f));
        SetRect(FindChild(panel, "StatDEF"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(110.4f, -118f), new Vector2(180f, 22f));

        SetRect(FindChild(panel, "Skill1"), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-102.4f, -65.274f), new Vector2(56f, 56f));
        SetRect(FindChild(panel, "Skill2"), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-22.5f, -64.349f), new Vector2(56f, 56f));
    }

    private static void RestoreTargetSelector(Transform root)
    {
        var bar = FindChild(root, "TargetSelector");
        if (bar == null) return;

        SetRect(bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(11f, -372f), new Vector2(420f, 64f));

        string[] targets = { "Target_P1", "Target_P2", "Target_P3", "Target_P4" };
        float[] xs = { -160f, -52f, 56f, 164f };
        for (int i = 0; i < targets.Length; i++)
        {
            SetRect(FindChild(bar, targets[i]), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(xs[i], 0f), new Vector2(56f, 56f));
        }
    }

    private static void RestoreBackpackPanel(Transform root)
    {
        var panel = FindChild(root, "BackpackPanel");
        if (panel == null) return;

        SetRect(panel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-545.6f, -478.77f), new Vector2(420f, 360f));
        SetRect(FindChild(panel, "GoldLabel"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(0.2f, 90f), new Vector2(140f, 26f));
        SetRect(FindChild(panel, "StopBtn"), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-1.4f, 91f), new Vector2(80f, 36f));

        Vector2[] slotPositions =
        {
            new Vector2(0.912903f, -174.33f),
            new Vector2(106.53f, -174.33f),
            new Vector2(0.912964f, -329.95f),
            new Vector2(106.53f, -329.95f),
            new Vector2(214.92f, -177.36f),
            new Vector2(326.1f, -175.63f),
            new Vector2(215.2f, -331.3f),
            new Vector2(326.38f, -332.4f),
        };

        for (int i = 0; i < slotPositions.Length; i++)
        {
            var slot = FindChild(panel, "BackpackSlot_" + (i + 1));
            SetRect(slot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f),
                slotPositions[i], new Vector2(64f, 64f));
        }
    }

    private static void RestoreMinimap(Transform root)
    {
        var panel = FindChild(root, "Minimap");
        if (panel == null) return;

        SetRect(panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-2.40002f, 1.20001f), new Vector2(300f, 300f));

        var body = FindChild(panel, "MinimapBody");
        if (body != null)
        {
            SetRect(body, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(0.700012f, -4.7f), new Vector2(-52f, -16f));
            SetRect(FindChild(body, "MinimapHint"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 30f));
        }

        SetRect(FindChild(panel, "MinimapToggle"), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-4f, 140.1f), new Vector2(36f, 80f));
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
