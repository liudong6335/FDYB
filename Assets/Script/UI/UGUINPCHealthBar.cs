/*
 * ============================================================
 *  UGUINPCHealthBar  -  NPC女神专属血条
 * ============================================================
 *
 * 【功能】
 *   NPC女神（阿满）头顶的专用血条，显示名称+血量。
 *   比通用浮血条（UGUIFloatingHealthBar）多了名字显示。
 *   与通用血条共享同一个画布。
 *
 * 【挂载对象】
 *   NPC 女神对象（与 NPCGoddess 在同一对象）
 *
 * 【可调节参数】
 *   npc             - NPCGoddess 组件引用
 *   offset          - 血条偏移位置
 *   barWidth/Height - 血条尺寸
 *   borderThick     - 边框厚度
 *   frameColor / bgColor / high/mid/lowColor - 颜色
 *   nameLabel       - 显示的名称文字（默认"Ah Man"）
 *   labelFontSize   - 名称字体大小
 *   labelColor      - 名称颜色
 *   showAlways      - 是否始终显示（不勾选则满血时隐藏）
 *
 * 【说明】
 *   代码运行时动态生成UI，不需要手动创建
 */
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UGUI overlay NPC health bar (Ah Man), replacing the legacy OnGUI NPCHealthBar.
/// Renders on a shared Screen Space Overlay Canvas for native resolution,
/// positioning via Camera.WorldToScreenPoint.
/// </summary>
public class UGUINPCHealthBar : MonoBehaviour
{
    #region Serialized Fields

    [Header("Target")]
    [SerializeField] private NPCGoddess npc;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, 0f);

    [Header("Bar")]
    [SerializeField] private float barWidth = 80f;
    [SerializeField] private float barHeight = 8f;
    [SerializeField] private float borderThick = 1f;
    [SerializeField] private Color frameColor = new Color(0.05f, 0.05f, 0.05f, 0.9f);
    [SerializeField] private Color bgColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    [SerializeField] private Color highColor = Color.green;
    [SerializeField] private Color midColor = Color.yellow;
    [SerializeField] private Color lowColor = Color.red;

    [Header("Label")]
    [SerializeField] private string nameLabel = "Ah Man";
    [SerializeField] private int labelFontSize = 14;
    [SerializeField] private Color labelColor = Color.white;

    [Header("Behavior")]
    [SerializeField] private bool showAlways = true;

    #endregion

    private RectTransform panelRect;
    private Image fillImage;
    private Camera cachedCamera;
    private float totalW, totalH, labelH;

    private static Canvas sharedCanvas;
    private static RectTransform sharedCanvasRect;

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
        EnsureSharedCanvas();
        BuildBar();
    }

    private void Start()
    {
        if (npc == null) npc = GetComponent<NPCGoddess>();
    }

    private static void EnsureSharedCanvas()
    {
        if (sharedCanvas != null) return;

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
        // Destroy any lingering panel from a previous scene load
        var oldPanel = sharedCanvasRect.Find("HB_NPC_" + gameObject.name);
        if (oldPanel != null) Destroy(oldPanel.gameObject);

        totalW = barWidth + borderThick * 2f;
        totalH = barHeight + borderThick * 2f;
        labelH = labelFontSize + 4f;

        // Panel
        var panelGo = new GameObject("HB_NPC_" + gameObject.name, typeof(RectTransform));
        panelGo.transform.SetParent(sharedCanvasRect, false);
        panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(totalW, totalH + labelH);

        // Name label
        var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
        nameGo.transform.SetParent(panelRect, false);
        var nameText = nameGo.GetComponent<Text>();
        nameText.text = nameLabel;
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = labelFontSize;
        nameText.fontStyle = FontStyle.Bold;
        nameText.alignment = TextAnchor.UpperCenter;
        nameText.color = labelColor;
        nameText.raycastTarget = false;

        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.offsetMin = Vector2.zero;
        nameRt.offsetMax = new Vector2(0f, labelH);

        // Frame
        var frame = MakeImage("Frame", panelRect, frameColor);
        frame.sprite = GetWhiteSprite();
        var frameRt = frame.rectTransform;
        frameRt.anchorMin = new Vector2(0f, 0f);
        frameRt.anchorMax = new Vector2(1f, 0f);
        frameRt.offsetMin = Vector2.zero;
        frameRt.offsetMax = new Vector2(0f, totalH);

        // Background
        var bg = MakeImage("Bg", frameRt, bgColor);
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

    private void LateUpdate()
    {
        if (npc == null || cachedCamera == null || panelRect == null) return;

        float pct = npc.HealthPercent;

        if (fillImage != null)
        {
            fillImage.fillAmount = pct;
            fillImage.color = pct > 0.5f
                ? Color.Lerp(midColor, highColor, (pct - 0.5f) * 2f)
                : Color.Lerp(lowColor, midColor, pct * 2f);
        }

        Vector3 worldPos = npc.transform.position + offset;
        Vector3 screenPos = cachedCamera.WorldToScreenPoint(worldPos);

        bool visible = screenPos.z > 0f && (showAlways || pct < 1f);
        panelRect.gameObject.SetActive(visible);
        if (!visible) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            sharedCanvasRect, screenPos, null, out Vector2 anchoredPos);
        panelRect.anchoredPosition = anchoredPos;
    }

    private void OnDestroy()
    {
        if (panelRect != null) Destroy(panelRect.gameObject);
    }
}

