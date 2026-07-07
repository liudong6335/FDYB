using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared canvas and white sprite for UGUI overlay health bars.
/// Both UGUIFloatingHealthBar and UGUINPCHealthBar use this to avoid code duplication.
/// </summary>
public static class HealthBarCanvas
{
    private static Canvas sharedCanvas;
    private static RectTransform sharedCanvasRect;
    private static Sprite whiteSprite;

    public static Canvas SharedCanvas
    {
        get { EnsureSharedCanvas(); return sharedCanvas; }
    }

    public static RectTransform SharedCanvasRect
    {
        get { EnsureSharedCanvas(); return sharedCanvasRect; }
    }

    public static void EnsureSharedCanvas()
    {
        if (sharedCanvas != null) return;

        // Domain reload may have reset the static reference,
        // but the DontDestroyOnLoad GameObject may still exist.
        var existing = GameObject.Find("HealthBarsCanvas");
        if (existing != null)
        {
            sharedCanvas = existing.GetComponent<Canvas>();
            sharedCanvasRect = existing.GetComponent<RectTransform>();
            return;
        }

        var go = new GameObject("HealthBarsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Object.DontDestroyOnLoad(go);
        sharedCanvas = go.GetComponent<Canvas>();
        sharedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        sharedCanvas.sortingOrder = 90;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        sharedCanvasRect = go.GetComponent<RectTransform>();
    }

    public static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        return whiteSprite;
    }
}
