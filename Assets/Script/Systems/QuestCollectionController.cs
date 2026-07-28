using UnityEngine;

[RequireComponent(typeof(NPCGoddessMovement))]
[RequireComponent(typeof(Health))]
public class QuestCollectionController : MonoBehaviour
{
    [Header("Collection")]
    [SerializeField] private float baseCollectTime = 2.5f;
    [SerializeField] private float hitPenalty = 0.5f;

    [Header("Progress Bar")]
    [SerializeField] private ProgressBarUI progressBar;

    private NPCGoddessMovement movement;
    private Health health;
    private float previousHealth;
    private float progressTimer;
    private bool isCollecting;
    private bool collectionComplete;

    public System.Action OnCollectionComplete;

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
    }

    private void Update()
    {
        if (!isCollecting || collectionComplete) return;

        if (health != null && health.IsDead)
        {
            isCollecting = false;
            if (progressBar != null) progressBar.Hide();
            return;
        }

        float currentHealth = health.CurrentHealth;
        if (currentHealth < previousHealth - 0.01f)
        {
            progressTimer -= hitPenalty;
            progressTimer = Mathf.Max(0f, progressTimer);
        }
        previousHealth = currentHealth;

        progressTimer += Time.deltaTime;
        if (progressBar != null)
            progressBar.Show(Mathf.Clamp01(progressTimer / baseCollectTime));

        if (progressTimer >= baseCollectTime)
            FinishCollection();
    }

    private void OnNPCReadyToCollect()
    {
        isCollecting = true;
        collectionComplete = false;
        progressTimer = 0f;
        previousHealth = health != null ? health.CurrentHealth : 0f;

        if (progressBar != null) progressBar.Show(0f);
        Debug.Log("[Collection] Started - " + baseCollectTime + "s base, " + hitPenalty + "s penalty per hit");
    }

    private void FinishCollection()
    {
        isCollecting = false;
        collectionComplete = true;

        if (progressBar != null) progressBar.Hide();

        if (movement != null)
            movement.NotifyCollectionComplete();

        OnCollectionComplete?.Invoke();
        Debug.Log("[Collection] Complete!");
    }
}
