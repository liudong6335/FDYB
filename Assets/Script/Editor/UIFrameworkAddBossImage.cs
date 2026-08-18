using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds a placeholder boss avatar image below the Boss countdown
/// inside StatsPanel without changing the panel border.
/// </summary>
public static class UIFrameworkAddBossImage
{
    [MenuItem("FDYB/UI/Add Boss Image Below Countdown")]
    public static void AddBossImage()
    {
        var ui = GameObject.Find("UI_HUD");
        if (ui == null)
        {
            Debug.LogError("[UI] UI_HUD not found.");
            return;
        }

        var stats = FindChild(ui.transform, "StatsPanel");
        if (stats == null)
        {
            Debug.LogError("[UI] StatsPanel not found.");
            return;
        }

        var existing = stats.Find("BossAvatarImage") as RectTransform;
        if (existing == null)
        {
            var go = new GameObject("BossAvatarImage", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Add Boss avatar placeholder");
            go.transform.SetParent(stats, false);
            existing = go.GetComponent<RectTransform>();
        }

        Undo.RecordObject(existing, "Position Boss avatar placeholder");
        existing.anchorMin = new Vector2(0.5f, 1f);
        existing.anchorMax = new Vector2(0.5f, 1f);
        existing.pivot = new Vector2(0.5f, 0.5f);
        existing.anchoredPosition = new Vector2(0f, -50f);
        existing.sizeDelta = new Vector2(96f, 96f);

        var img = existing.GetComponent<Image>();
        if (img != null)
        {
            Undo.RecordObject(img, "Style Boss avatar placeholder");
            img.color = new Color(0.35f, 0.38f, 0.42f, 0.85f);
            img.raycastTarget = false;
        }

        EditorSceneManager.MarkSceneDirty(ui.scene);
        EditorSceneManager.SaveScene(ui.scene);
        Debug.Log("[UI] Boss avatar placeholder added below the countdown.");
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
