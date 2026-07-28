/*
 * ============================================================
 *  UGUIFloatingHealthBar  -  娴姩琛€鏉★紙鎬墿/閫氱敤锛?
 * ============================================================
 *
 * 銆愬姛鑳姐€?
 *   鍦ㄧ墿浣撳ご椤舵樉绀鸿鏉★紝璺熼殢鐗╀綋鍦ㄥ睆骞曚笂鐨勪綅缃Щ鍔ㄣ€?
 *   琛€鏉￠鑹查殢琛€閲忓彉鍖栵紙缁库啋榛勨啋绾級锛屾湁骞虫粦杩囨浮鏁堟灉銆?
 *   鎵€鏈夎鏉″叡浜竴涓敾甯冿紙HealthBarsCanvas锛夈€?
 *
 * 銆愭寕杞藉璞°€?
 *   闇€瑕佹樉绀鸿鏉＄殑娓告垙瀵硅薄锛堟€墿/鐗╀綋绛夛級
 *
 * 銆愬彲璋冭妭鍙傛暟銆?
 *   healthProvider   - 琛€閲忔暟鎹彁渚涜€咃紙瀹炵幇 IHealthProvider 鎺ュ彛锛?
 *   offset           - 琛€鏉＄浉瀵瑰ご椤剁殑鍋忕Щ
 *   barWidth/Height  - 琛€鏉″昂瀵?
 *   borderThick      - 杈规鍘氬害
 *   frameColor       - 杈规棰滆壊
 *   bgColor          - 鑳屾櫙鑹?
 *   high/mid/lowColor- 楂?涓?浣庤閲忛鑹?
 *   smoothSpeed      - 琛€鏉″彉鍖栧钩婊戦€熷害
 *   hideAtFullHealth - 婊¤鏃舵槸鍚﹂殣钘?
 *
 * 銆愯鏄庛€?
 *   浠ｇ爜杩愯鏃跺姩鎬佺敓鎴怳I锛屼笉闇€瑕佸湪Canvas涓嬫墜鍔ㄥ垱寤?
 */
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UGUI overlay health bar replacing the legacy OnGUI FloatingHealthBar.
/// Renders on a shared Screen Space Overlay Canvas for native resolution,
/// positioning via Camera.WorldToScreenPoint 閳?no world-space jitter or aliasing.
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
    public bool HideAtFullHealth { get { return hideAtFullHealth; } set { hideAtFullHealth = value; } }

    #endregion

    #region Private State

    private IHealthProvider provider;
    private float displayPct = 1f;

    private RectTransform panelRect;
    private Image fillImage;
    private Camera cachedCamera;
    private float totalW, totalH;

    // Shared overlay canvas

    #endregion



    private void Awake()
    {
        // Skip if NPC health bar already handles this object
        if (GetComponent<UGUINPCHealthBar>() != null)
        {
            enabled = false;
            return;
        }
        cachedCamera = Camera.main;

        // Resolve provider
        if (healthProvider != null)
            provider = healthProvider as IHealthProvider;
        if (provider == null)
            provider = GetComponent<IHealthProvider>()
                ?? GetComponentInParent<IHealthProvider>()
                ?? GetComponentInChildren<IHealthProvider>();

        HealthBarCanvas.EnsureSharedCanvas();
        BuildBar();
    }


    private void BuildBar()
    {
        // Destroy any lingering panel from a previous scene load (DontDestroyOnLoad canvas)
        string panelName = "HB_" + gameObject.GetInstanceID();


        Debug.Log("[UGUIFloatingHealthBar] BuildBar for: " + gameObject.name + " | path: " + GetFullPath() + " | ID: " + gameObject.GetInstanceID());
        totalW = barWidth + borderThick * 2f;
        totalH = barHeight + borderThick * 2f;

        // Panel root
        var panelGo = new GameObject(panelName, typeof(RectTransform));
        panelGo.transform.SetParent(HealthBarCanvas.SharedCanvasRect, false);
        panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(totalW, totalH);

        // Frame
        var frame = UIHelpers.MakeImage("Frame", panelRect, frameColor);
        frame.sprite = HealthBarCanvas.GetWhiteSprite();
            frame.rectTransform.anchorMin = Vector2.zero;
            frame.rectTransform.anchorMax = Vector2.one;
            frame.rectTransform.offsetMin = Vector2.zero;
            frame.rectTransform.offsetMax = Vector2.zero;

        // Background
        var bg = UIHelpers.MakeImage("Bg", frame.rectTransform, bgColor);
        bg.sprite = HealthBarCanvas.GetWhiteSprite();
        var bgRt = bg.rectTransform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = new Vector2(borderThick, borderThick);
        bgRt.offsetMax = new Vector2(-borderThick, -borderThick);

        // Fill
        fillImage = UIHelpers.MakeImage("Fill", bgRt, highColor);
        fillImage.sprite = HealthBarCanvas.GetWhiteSprite();
        var fillRt = fillImage.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        fillImage.sprite = HealthBarCanvas.GetWhiteSprite();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 1f;
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
            HealthBarCanvas.SharedCanvasRect, screenPos, null, out anchoredPos);
        panelRect.anchoredPosition = anchoredPos;
    }

    private void OnDestroy()
    {
        if (panelRect != null)
            Destroy(panelRect.gameObject);
    }

    private void OnDisable()
    {
        if (panelRect != null)
            panelRect.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (panelRect != null)
            panelRect.gameObject.SetActive(true);
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



