/*
 * ============================================================
 *  UGUIFloatingHealthBar  -  浮动血条（怪物/通用）
 * ============================================================
 *
 * 【功能】
 *   在物体头顶显示血条，跟随物体在屏幕上的位置移动。
 *   血条颜色随血量变化（绿→黄→红），有平滑过渡效果。
 *   所有血条共享一个画布（HealthBarsCanvas）。
 *
 * 【挂载对象】
 *   需要显示血条的游戏对象（怪物/物体等）
 *
 * 【可调节参数】
 *   healthProvider   - 血量数据提供者（实现 IHealthProvider 接口）
 *   offset           - 血条相对头顶的偏移
 *   barWidth/Height  - 血条尺寸
 *   borderThick      - 边框厚度
 *   frameColor       - 边框颜色
 *   bgColor          - 背景色
 *   high/mid/lowColor- 高/中/低血量颜色
 *   smoothSpeed      - 血条变化平滑速度
 *   hideAtFullHealth - 满血时是否隐藏
 *
 * 【说明】
 *   代码运行时动态生成UI，不需要在Canvas下手动创建
 */
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UGUI overlay health bar replacing the legacy OnGUI FloatingHealthBar.
/// Renders on a shared Screen Space Overlay Canvas for native resolution,
/// positioning via Camera.WorldToScreenPoint 鈥?no world-space jitter or aliasing.
/// </summary>
public class UGUIFloatingHealthBar : MonoBehaviour
{
    #region Serialized Fields

    [Header("Target")]
    [SerializeField] private MonoBehaviour healthProvider;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.8f, 0f);

    [Header("Bar")]
    [SerializeField] private float barWidth = 120f;
    [SerializeField] private float barHeight = 10f;
    [SerializeField] private float borderThick = 2f;
    [SerializeField] private Color frameColor = new Color(0.05f, 0.05f, 0.05f, 0.9f);
    [SerializeField] private Color bgColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    [SerializeField] private Color highColor = new Color(0.2f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color midColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color lowColor = new Color(0.9f, 0.15f, 0.15f, 1f);

    [Header("Behavior")]
    [SerializeField] private float smoothSpeed = 8f;
    [SerializeField] private bool hideAtFullHealth;

    #endregion

    #region Private State

    private IHealthProvider provider;
    private float displayPct = 1f;

    private RectTransform panelRect;
    private Image fillImage;
    private Camera cachedCamera;
    private float totalW, totalH;

    // Shared overlay canvas
    private static Canvas sharedCanvas;
    private static RectTransform sharedCanvasRect;

    #endregion

    private static Sprite whiteSprite;

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        return whiteSprite;
    }

    private void Awake()
    {
        cachedCamera = Camera.main;

        // Resolve provider
        if (healthProvider != null)
            provider = healthProvider as IHealthProvider;
        if (provider == null)
            provider = GetComponent<IHealthProvider>()
                ?? GetComponentInParent<IHealthProvider>()
                ?? GetComponentInChildren<IHealthProvider>();

        EnsureSharedCanvas();
        BuildBar();
    }

    private static void EnsureSharedCanvas()
    {
        if (sharedCanvas != null) return;

        // Domain reload or script rebuild may have reset static sharedCanvas,
        // but the DontDestroyOnLoad HealthBarsCanvas GameObject may still exist.
        // If so, reuse it instead of creating a duplicate.
        var existing = GameObject.Find("HealthBarsCanvas");
        if (existing != null)
        {
            sharedCanvas = existing.GetComponent<Canvas>();
            sharedCanvasRect = existing.GetComponent<RectTransform>();
            return;
        }

        var go = new GameObject("HealthBarsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(go);
        sharedCanvas = go.GetComponent<Canvas>();
        sharedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        sharedCanvas.sortingOrder = 90;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        sharedCanvasRect = go.GetComponent<RectTransform>();
    }

    private void BuildBar()
    {
        // Destroy any lingering panel from a previous scene load (DontDestroyOnLoad canvas)
        var oldPanel = sharedCanvasRect.Find("HB_" + gameObject.name);
        if (oldPanel != null) GameObject.DestroyImmediate(oldPanel.gameObject);

        Debug.Log("[UGUIFloatingHealthBar] BuildBar for: " + gameObject.name + " | path: " + GetFullPath() + " | ID: " + gameObject.GetInstanceID());
        totalW = barWidth + borderThick * 2f;
        totalH = barHeight + borderThick * 2f;

        // Panel root
        var panelGo = new GameObject("HB_" + gameObject.name, typeof(RectTransform));
        panelGo.transform.SetParent(sharedCanvasRect, false);
        panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(totalW, totalH);

        // Frame
        var frame = MakeImage("Frame", panelRect, frameColor);
        frame.sprite = GetWhiteSprite();
        Stretch(frame.rectTransform);

        // Background
        var bg = MakeImage("Bg", frame.rectTransform, bgColor);
        bg.sprite = GetWhiteSprite();
        var bgRt = bg.rectTransform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = new Vector2(borderThick, borderThick);
        bgRt.offsetMax = new Vector2(-borderThick, -borderThick);

        // Fill
        fillImage = MakeImage("Fill", bgRt, highColor);
        fillImage.sprite = GetWhiteSprite();
        var fillRt = fillImage.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        fillImage.sprite = GetWhiteSprite();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 1f;
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

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (cachedCamera == null || panelRect == null) return;

        // Smooth health display
        if (provider != null)
        {
            float target = provider.HealthPercent;
            displayPct = Mathf.Lerp(displayPct, target, Time.deltaTime * smoothSpeed);
            if (Mathf.Abs(displayPct - target) < 0.001f) displayPct = target;
        }

        // Update fill
        if (fillImage != null)
        {
            float pct = Mathf.Clamp01(displayPct);
            fillImage.fillAmount = pct;
            fillImage.color = pct > 0.5f
                ? Color.Lerp(midColor, highColor, (pct - 0.5f) * 2f)
                : Color.Lerp(lowColor, midColor, pct * 2f);
        }

        // World to screen position
        Vector3 worldPos = transform.position + offset;
        Vector3 screenPos = cachedCamera.WorldToScreenPoint(worldPos);

        // Behind camera or off-screen
        bool visible = screenPos.z > 0f;
        if (hideAtFullHealth && displayPct > 0.999f) visible = false;

        panelRect.gameObject.SetActive(visible);
        if (!visible) return;

        // Convert to canvas-space (Screen.height - y for overlay)
        Vector2 anchoredPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            sharedCanvasRect, screenPos, null, out anchoredPos);
        panelRect.anchoredPosition = anchoredPos;
    }

    private void OnDestroy()
    {
        if (panelRect != null)
            Destroy(panelRect.gameObject);
    }

    public void SetProvider(MonoBehaviour p)
    {
        healthProvider = p;
        provider = p as IHealthProvider;
    }
    private string GetFullPath()
    {
        var path = gameObject.name;
        var parent = transform.parent;
        while (parent != null) { path = parent.name + "/" + path; parent = parent.parent; }
        return path;
    }
}

