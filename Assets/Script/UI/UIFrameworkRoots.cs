using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the large screen regions of the HUD: top bar, left toolbar,
/// center viewport, bottom action bar and right side panel.
/// </summary>
public class UIFrameworkRoots
{
    public RectTransform TopBar { get; private set; }
    public RectTransform LeftToolbar { get; private set; }
    public RectTransform CenterViewport { get; private set; }
    public RectTransform BottomBar { get; private set; }
    public RectTransform RightPanel { get; private set; }

    public static UIFrameworkRoots Build(Transform root, Color barColor, Color panelColor, Color viewportColor)
    {
        var ui = new UIFrameworkRoots();

        ui.TopBar = MakeRegion("TopBar", root,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, -90f), barColor);

        ui.LeftToolbar = MakeRegion("LeftToolbar", root,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -90f), new Vector2(96f, 520f), barColor);

        ui.BottomBar = MakeRegion("BottomBar", root,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 0f), new Vector2(720f, 56f), barColor);

        ui.RightPanel = MakeRegion("RightPanel", root,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 0f), new Vector2(348f, 374f), panelColor);

        ui.CenterViewport = MakeStretchRegion("CenterViewport", root,
            new Vector2(96f, 46f), new Vector2(-348f, -90f), viewportColor);

        return ui;
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
}
