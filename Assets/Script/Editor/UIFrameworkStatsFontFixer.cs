using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Swaps the StatsPanel text components to a Chinese-capable font
/// without changing any panel rects or borders.
/// </summary>
public static class UIFrameworkStatsFontFixer
{
    [MenuItem("FDYB/UI/Fix StatsPanel Font")]
    public static void FixFont()
    {
        var ui = GameObject.Find("UI_HUD");
        if (ui == null)
        {
            Debug.LogError("[UI] UI_HUD not found.");
            return;
        }

        var statsPanel = FindChild(ui.transform, "StatsPanel");
        if (statsPanel == null)
        {
            Debug.LogError("[UI] StatsPanel not found.");
            return;
        }

        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
        if (font == null)
        {
            Debug.LogError("[UI] SimHei font asset not imported yet.");
            return;
        }

        var texts = statsPanel.GetComponentsInChildren<Text>(true);
        foreach (var text in texts)
        {
            Undo.RecordObject(text, "Fix StatsPanel font");
            text.font = font;
            text.fontSize = GetFontSize(text.gameObject.name);
        }

        EditorSceneManager.MarkSceneDirty(ui.scene);
        EditorSceneManager.SaveScene(ui.scene);
        Debug.Log("[UI] StatsPanel font updated to SimHei.");
    }

    private static int GetFontSize(string name)
    {
        if (name.Contains("BossCountdown")) return 16;
        if (name.Contains("StatsTitle")) return 15;
        if (name.StartsWith("Stat")) return 14;
        if (name == "Label") return 12;
        return 14;
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
