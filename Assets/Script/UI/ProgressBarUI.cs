using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable progress bar UI rendered on the shared HealthBarCanvas.
/// Attach to any interactable GameObject (chest, door, lever, etc.).
/// Call Show(pct) / Hide() to control display and color gradient.
/// All visual parameters are exposed in the Inspector.
/// </summary>
public class ProgressBarUI : MonoBehaviour
{
    [Header("Bar Size & Position")]
    [SerializeField] private Vector2 barSize = new Vector2(80f, 8f);
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.5f, 0f);
    [SerializeField] private float borderThick = 2f;

    [Header("Colors")]
    [SerializeField] private Color frameColor = new Color(0.05f, 0.05f, 0.05f, 0.9f);
    [SerializeField] private Color bgColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    [SerializeField] private Color lowColor = new Color(0.9f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color midColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color highColor = new Color(0.2f, 0.85f, 0.2f, 1f);

    [Header("Behavior")]
    [SerializeField] private bool startHidden = true;

    private RectTransform panelRect;
    private Image fillImage;
    private Camera cachedCamera;
    private bool isVisible;

    private void Awake()
    {
        cachedCamera = Camera.main;
        HealthBarCanvas.EnsureSharedCanvas();
        BuildBar();
        if (startHidden) Hide();
    }

    private void BuildBar()
    {
        // Destroy lingering panel from previous scene load
        var existing = HealthBarCanvas.SharedCanvasRect.Find("PB_" + gameObject.GetInstanceID());
        if (existing != null) DestroyImmediate(existing.gameObject);

        float totalW = barSize.x + borderThick * 2f;
        float totalH = barSize.y + borderThick * 2f;

        var panel = new GameObject("PB_" + gameObject.GetInstanceID(), typeof(RectTransform));
        panel.transform.SetParent(HealthBarCanvas.SharedCanvasRect, false);
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(totalW, totalH);

        // Frame
        var frame = MakeImage("Frame", panelRect, frameColor, true);
        frame.rectTransform.anchorMin = Vector2.zero;
        frame.rectTransform.anchorMax = Vector2.one;
        frame.rectTransform.offsetMin = Vector2.zero;
        frame.rectTransform.offsetMax = Vector2.zero;

        // Background
        var bg = MakeImage("Bg", frame.rectTransform, bgColor, true);
        var bgRt = bg.rectTransform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = new Vector2(borderThick, borderThick);
        bgRt.offsetMax = new Vector2(-borderThick, -borderThick);

        // Fill
        fillImage = MakeImage("Fill", bgRt, highColor, false);
        fillImage.sprite = HealthBarCanvas.GetWhiteSprite();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 0f;
        var fillRt = fillImage.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
    }

    private static Image MakeImage(string name, Transform parent, Color color, bool setSprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        if (setSprite) img.sprite = HealthBarCanvas.GetWhiteSprite();
        return img;
    }

    /// <summary>Show the progress bar at a given percentage (0~1). Called every frame during progress.</summary>
    public void Show(float pct)
    {
        if (panelRect == null) return;
        isVisible = true;
        panelRect.gameObject.SetActive(true);

        if (fillImage != null)
        {
            pct = Mathf.Clamp01(pct);
            fillImage.fillAmount = pct;

            // Color gradient: < 0.5 red→yellow, >= 0.5 yellow→green
            if (pct < 0.5f)
                fillImage.color = Color.Lerp(lowColor, midColor, pct / 0.5f);
            else
                fillImage.color = Color.Lerp(midColor, highColor, (pct - 0.5f) / 0.5f);
        }

        UpdatePosition();
    }

    /// <summary>Hide the progress bar.</summary>
    public void Hide()
    {
        isVisible = false;
        if (panelRect != null)
            panelRect.gameObject.SetActive(false);
    }

    private void UpdatePosition()
    {
        if (cachedCamera == null) cachedCamera = Camera.main;
        if (cachedCamera == null || panelRect == null) return;

        Vector3 worldPos = transform.position + worldOffset;
        Vector3 screenPos = cachedCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z > 0f)
        {
            panelRect.gameObject.SetActive(isVisible);
            Vector2 anchoredPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                HealthBarCanvas.SharedCanvasRect, screenPos, null, out anchoredPos);
            panelRect.anchoredPosition = anchoredPos;
        }
        else
        {
            panelRect.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (isVisible)
            UpdatePosition();
    }

    private void OnDestroy()
    {
        if (panelRect != null && panelRect.gameObject != null)
            Destroy(panelRect.gameObject);
    }
}
