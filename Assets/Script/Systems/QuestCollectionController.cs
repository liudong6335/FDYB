/* 
 * ============================================================
 *  QuestCollectionController  -  任务物品采集控制器
 * ============================================================
 *
 * 【功能】
 *   管理 NPC 采集任务物品的进度：2.5 秒读条，每次受击回退 0.5 秒。
 *   采集完成后在 NPC 附近生成宝箱，并通知 NPC 状态机继续前进。
 *   同时显示采集进度条 UI。
 *
 * 【挂载对象】
 *   NPC 对象（与 NPCGoddessMovement 同对象）
 *
 * 【外部调用】
 *   IsCollecting         - 是否正在采集
 *   Progress             - 当前进度 0~1
 *   OnCollectionComplete - 采集完成事件
 *
 * 【参数】
 *   baseCollectTime  - 基础采集时间（默认 2.5 秒）
 *   hitPenalty       - 每次受击回退（默认 0.5 秒）
 *   (chest spawning moved to QuestManager)
 */
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(NPCGoddessMovement))]
[RequireComponent(typeof(Health))]
public class QuestCollectionController : MonoBehaviour
{
    [Header("Collection")]
    [SerializeField] private float baseCollectTime = 2.5f;
    [SerializeField] private float hitPenalty = 0.5f;





    [Header("Progress Bar")]
    [SerializeField] private Vector3 barOffset = new Vector3(0f, 3.5f, 0f);
    [SerializeField] private Vector2 barSize = new Vector2(100f, 10f);
    [SerializeField] private Color barColor = Color.yellow;
    [SerializeField] private Color barBgColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);

    private NPCGoddessMovement movement;
    private Health health;
    private float previousHealth;
    private float progressTimer;
    private bool isCollecting;
    private bool collectionComplete;

    // Progress bar UI
    private GameObject barPanel;
    private Image fillImage;

    // Events
    public System.Action OnCollectionComplete;

    // Properties
    public bool IsCollecting { get { return isCollecting; } }
    public float Progress { get { return baseCollectTime > 0f ? Mathf.Clamp01(progressTimer / baseCollectTime) : 0f; } }

    private void Awake()
    {
        movement = GetComponent<NPCGoddessMovement>();
        health = GetComponent<Health>();
    }

    private void Start()
    {
        if (movement != null)
            movement.OnReadyToCollect += OnNPCReadyToCollect;
        BuildProgressBar();
    }

    private void OnDestroy()
    {
        if (barPanel != null) Destroy(barPanel);
    }

    private void Update()
    {
        if (!isCollecting || collectionComplete) return;

        // Check if NPC is dead
        if (health != null && health.IsDead)
        {
            isCollecting = false;
            if (barPanel != null) barPanel.SetActive(false);
            return;
        }

        // Track health changes for hit penalty
        float currentHealth = health.CurrentHealth;
        if (currentHealth < previousHealth - 0.01f)
        {
            progressTimer -= hitPenalty;
            progressTimer = Mathf.Max(0f, progressTimer);
        }
        previousHealth = currentHealth;

        // Progress
        progressTimer += Time.deltaTime;

        // Update progress bar
        UpdateProgressBar();

        // Check completion
        if (progressTimer >= baseCollectTime)
            FinishCollection();
    }

    private void OnNPCReadyToCollect()
    {
        isCollecting = true;
        collectionComplete = false;
        progressTimer = 0f;
        previousHealth = health != null ? health.CurrentHealth : 0f;

        if (barPanel != null)
            barPanel.SetActive(true);

        Debug.Log("[Collection] Started - " + baseCollectTime + "s base, " + hitPenalty + "s penalty per hit");
    }


    /// <summary>Spawn a visual quest item at the given position.</summary>


    private void FinishCollection()
    {
        isCollecting = false;
        collectionComplete = true;

        // Destroy quest item visual
        

        // Hide progress bar
        if (barPanel != null)
            barPanel.SetActive(false);



        // Notify NPC to continue moving to next area
        if (movement != null)
            movement.NotifyCollectionComplete();

        // Fire event
        OnCollectionComplete?.Invoke();

        Debug.Log("[Collection] Complete!");
    }

    private void BuildProgressBar()
    {
        HealthBarCanvas.EnsureSharedCanvas();

        barPanel = new GameObject("CollectionProgress", typeof(RectTransform));
        barPanel.transform.SetParent(HealthBarCanvas.SharedCanvasRect, false);
        RectTransform panelRect = barPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = barSize;

        // Background
        GameObject bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(panelRect.transform, false);
        Image bgImage = bg.GetComponent<Image>();
        bgImage.color = barBgColor;
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // Fill bar
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bg.transform, false);
        fillImage = fill.GetComponent<Image>();
        fillImage.color = barColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 0f;
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        barPanel.SetActive(false);
    }

    private void UpdateProgressBar()
    {
        if (fillImage == null || barPanel == null) return;
        if (Camera.main == null) return;

        float pct = Mathf.Clamp01(Progress);
        fillImage.fillAmount = pct;

        // Position above NPC
        Vector3 worldPos = transform.position + barOffset;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        if (screenPos.z > 0f)
        {
            Vector2 anchoredPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                HealthBarCanvas.SharedCanvasRect, screenPos, null, out anchoredPos);
            barPanel.GetComponent<RectTransform>().anchoredPosition = anchoredPos;
        }
    }
}
