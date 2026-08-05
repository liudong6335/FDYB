/* ============================================================
 *  TreasureChest  -  宝箱（状态机 + 进度条 + 掉落）
 * ============================================================
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ChestOpenState { Idle, Reading, OpeningItems, ItemFlying, Complete }
public enum ChestType { Wooden, Copper, Silver, Gold }

public class TreasureChest : MonoBehaviour
{
    [Header("Chest Settings")]
    [SerializeField] private ChestType chestType = ChestType.Wooden;

    [Header("Opening")]
    [SerializeField] private float readTime = 3f;
    [SerializeField] private float hitPenalty = 0.8f;
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private float destroyDelay = 3f;
    [Tooltip("无 Animator 时的兜底等待时长")]
    [SerializeField] private float openAnimationDelay = 0.3f;

    [Header("Progress Bar")]
    [SerializeField] private ProgressBarUI progressBar;

    [Header("Animation")]
    [SerializeField] private Transform itemSpawnPoint;
    [SerializeField] private GameObject goldVisualPrefab;
    [Tooltip("物品没有专属 3D 模型时使用的通用掉落物预制体")]
    [SerializeField] private GameObject defaultDropPrefab;
    [Tooltip("预算耗尽时兜底掉落的物品（必出物品）")]
    [SerializeField] private GameItemDefinition fallbackGold;
    [SerializeField] private float itemFlySpeed = 5f;
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private string chestOpenTrigger = "Open";
    [Tooltip("Animator 中开箱状态名，物品会等这个状态播完才飞出")]
    [SerializeField] private string chestOpenStateName = "open";
    private int chestOpenTriggerHash;

    [Header("Player Animation")]
    [Tooltip("Animator trigger name on the player for open-chest animation")]
    [SerializeField] private string playerOpenTriggerName = "Open";
    private int playerOpenTriggerHash;

    [Header("Loot Table")]
    [Tooltip("Weighted loot entries (itemDef + weight). DropEntry 定义在 LootBudgetManager.cs。")]
    [SerializeField] private List<DropEntry> lootTable = new List<DropEntry>();

    [Header("Visual")]
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openVisual;
    [SerializeField] private GameObject highlightEffect;

    // State
    private bool isOpened;
    private bool isOpening;
    private PlayerMove openingPlayer;
    private PlayerChestInteraction chestInteraction;
    private InventoryManager cachedInventory;
    private ChestOpenState currentState = ChestOpenState.Idle;
    private float currentReadProgress;
    private float previousHealthPercent;

    private float sqrInteractDistance;
    private float sqrInteractPadding;

    // Health (for hit penalty)
    private Health playerHealth;

    private static readonly List<TreasureChest> allChests = new List<TreasureChest>();
    public static IReadOnlyList<TreasureChest> ActiveChests => allChests;

    public ChestType Type { get { return chestType; } set { chestType = value; } }
    public bool IsOpened { get { return isOpened; } }
    public bool IsOpening { get { return isOpening; } }
    public float InteractDistance { get { return interactDistance; } }

    private void Awake()
    {
        allChests.Add(this);
        sqrInteractDistance = interactDistance * interactDistance;
        sqrInteractPadding = (interactDistance + 0.5f) * (interactDistance + 0.5f);

        // Ensure a collider for OnMouseDown() click detection
        if (GetComponent<Collider>() == null)
        {
            var col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = false;
        }

        // Auto-find chest animator
        if (chestAnimator == null) chestAnimator = GetComponent<Animator>();
        if (progressBar == null) progressBar = GetComponentInChildren<ProgressBarUI>();
        chestOpenTriggerHash = Animator.StringToHash(chestOpenTrigger);
        playerOpenTriggerHash = Animator.StringToHash(playerOpenTriggerName);
    }

    private void OnDestroy()
    {
        allChests.Remove(this);
        UnsubscribePlayerHealth();
    }

    private void Start()
    {
        if (closedVisual != null) closedVisual.SetActive(true);
        if (openVisual != null) openVisual.SetActive(false);
        if (highlightEffect != null) highlightEffect.SetActive(false);
    }

    private void Update()
    {
        if (isOpened) return;
        if (isOpening && openingPlayer != null && currentState == ChestOpenState.Reading)
            UpdateReading();
    }

    private void OnMouseDown()
    {
        if (isOpened || isOpening) return;
        TryInteract();
    }

    public void TryInteract()
    {
        if (isOpened || isOpening) return;

        // Find nearest player
        PlayerMove nearest = null;
        float nearestSqrDist = float.MaxValue;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null) continue;
            float d = transform.position.SqrDistanceXZ(p.transform.position);
            if (d < nearestSqrDist)
            {
                nearestSqrDist = d;
                nearest = p;
            }
        }
        if (nearest == null) return;

        PlayerChestInteraction interaction = nearest.GetComponent<PlayerChestInteraction>();
        if (interaction == null) return;

        if (nearestSqrDist <= sqrInteractDistance)
        {
            StartOpening(nearest);
        }
        else
        {
            interaction.NavigateTo(this);
        }
    }

    public void StartOpening(PlayerMove player)
    {
        if (isOpened || isOpening) return;
        if (currentState != ChestOpenState.Idle) return;

        isOpening = true;
        openingPlayer = player;
        currentState = ChestOpenState.Reading;
        currentReadProgress = 0f;

        chestInteraction = player.GetComponent<PlayerChestInteraction>();
        if (chestInteraction != null)
            chestInteraction.LockForReading(this);

        SubscribePlayerHealth(player);

        ShowProgressBar(0f);
        if (highlightEffect != null) highlightEffect.SetActive(true);

        player.StopMoving();
        if (!string.IsNullOrEmpty(playerOpenTriggerName))
        {
            var pAnim = player.GetComponent<Animator>();
            if (pAnim != null) pAnim.SetTrigger(playerOpenTriggerHash);
        }
    }

    public void CancelInteraction()
    {
        UnsubscribePlayerHealth();
        isOpening = false;
        openingPlayer = null;
        currentState = ChestOpenState.Idle;
        currentReadProgress = 0f;
        HideProgressBar();
        if (highlightEffect != null) highlightEffect.SetActive(false);
    }

    private void OnReadingComplete()
    {
        currentState = ChestOpenState.OpeningItems;

        if (closedVisual != null) closedVisual.SetActive(false);
        if (openVisual != null) openVisual.SetActive(true);
        HideProgressBar();

        if (chestAnimator != null)
            chestAnimator.SetTrigger(chestOpenTriggerHash);

        if (chestInteraction != null)
            chestInteraction.Release();

        StartCoroutine(OpeningSequence());
    }

    private IEnumerator OpeningSequence()
    {
        // 等开箱动画播放结束，物品才开始飞出
        yield return WaitForOpenAnimation();

        currentState = ChestOpenState.ItemFlying;
        if (highlightEffect != null) highlightEffect.SetActive(false);

        // 从全局预算中抽取一件物品（预算耗尽时返回 null）
        GameItem loot = LootBudgetManager.Instance != null
            ? LootBudgetManager.Instance.PickOne(lootTable)
            : null;

        // 兜底：必出物品。掉落表未配置或预算耗尽时默认给金币。
        if (loot == null)
        {
            loot = fallbackGold != null
                ? fallbackGold.ToGameItem()
                : new GameItem { itemType = ItemType.Currency, itemId = "gold", itemName = "金币", goldAmount = 50 };
        }

        Vector3 spawnPos = itemSpawnPoint != null
            ? itemSpawnPoint.position
            : transform.position + Vector3.up * 1.5f;

        // 金币视觉：仅当金币没有 3D dropPrefab 时使用旧的金币飞行特效
        if (loot != null && loot.itemType == ItemType.Currency
            && (loot.definition == null || loot.definition.dropPrefab == null)
            && goldVisualPrefab != null && openingPlayer != null)
        {
            yield return StartCoroutine(FlyGoldVisual(spawnPos, loot.goldAmount));
        }

        UnsubscribePlayerHealth();
        isOpened = true;
        isOpening = false;
        currentState = ChestOpenState.Complete;

        // 生成掉落物：优先专属模型，其次通用掉落物模型，都无则直接进背包
        GameObject dropPrefab = loot != null && loot.definition != null && loot.definition.dropPrefab != null
            ? loot.definition.dropPrefab
            : defaultDropPrefab;

        if (loot != null && openingPlayer != null && dropPrefab != null)
        {
            GameObject go = Instantiate(dropPrefab, spawnPos, Quaternion.identity);
            DroppedItem dropped = go.GetComponent<DroppedItem>();
            if (dropped == null) dropped = go.AddComponent<DroppedItem>();
            dropped.Launch(loot, openingPlayer, 0f);
        }
        else if (loot != null)
        {
            if (cachedInventory == null)
                cachedInventory = FindFirstObjectByType<InventoryManager>();
            if (cachedInventory != null)
                cachedInventory.AddItem(loot, openingPlayer);
        }

        StartCoroutine(DelayedDestroy());
    }

    /// <summary>等待宝箱 open 动画播放结束；无 Animator 时按 openAnimationDelay 兜底。</summary>
    private IEnumerator WaitForOpenAnimation()
    {
        if (chestAnimator == null)
        {
            yield return new WaitForSeconds(openAnimationDelay);
            yield break;
        }

        // 先等一帧，让 Open trigger 生效进入 open 状态
        yield return null;

        float timeout = Mathf.Max(5f, openAnimationDelay);
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            var state = chestAnimator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(chestOpenStateName))
            {
                // open 状态播完（非循环动画 normalizedTime 到 1）
                if (state.normalizedTime >= 1f && !chestAnimator.IsInTransition(0))
                    yield break;
            }
            else if (elapsed > openAnimationDelay)
            {
                // 一直没有进入 open 状态（动画未配置），按兜底时间继续
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>金币飞行特效：从宝箱口生成金币并飞向玩家。</summary>
    private IEnumerator FlyGoldVisual(Vector3 spawnPos, int totalGold)
    {
        if (openingPlayer == null || goldVisualPrefab == null) yield break;

        List<GameObject> visuals = new List<GameObject>();
        int coinCount = Mathf.Clamp(totalGold / 10, 1, 10);
        for (int i = 0; i < coinCount; i++)
        {
            Vector3 off = new Vector3(Random.Range(-0.3f, 0.3f), 0.5f, Random.Range(-0.3f, 0.3f));
            visuals.Add(Instantiate(goldVisualPrefab, spawnPos + off, Random.rotation));
        }

        float duration = 0.6f;
        float elapsed = 0f;
        float speedFactor = Mathf.Max(0.5f, itemFlySpeed * 0.2f);
        Vector3 target = openingPlayer.transform.position + Vector3.up * 1f;

        while (elapsed < duration * speedFactor)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / (duration * speedFactor));
            for (int i = 0; i < visuals.Count; i++)
            {
                if (visuals[i] != null)
                {
                    Vector3 start = spawnPos + Vector3.right * i * 0.15f;
                    Vector3 end = target + Random.insideUnitSphere * 0.3f;
                    Vector3 pos = Vector3.Lerp(start, end, t);
                    pos.y += Mathf.Sin(t * Mathf.PI) * 0.8f;
                    visuals[i].transform.position = pos;
                    visuals[i].transform.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one, t);
                }
            }
            yield return null;
        }

        foreach (var v in visuals)
            if (v != null) Destroy(v);
    }

    private void UpdateReading()
    {
        if (openingPlayer == null) return;

        // Player died during reading - cancel (matches NPC collection behavior)
        var pHealth = openingPlayer.GetComponent<Health>();
        if (pHealth == null || pHealth.IsDead)
        {
            CancelInteraction();
            return;
        }

        currentReadProgress += Time.deltaTime;
        ShowProgressBar(Mathf.Clamp01(currentReadProgress / readTime));

        if (currentReadProgress >= readTime)
            OnReadingComplete();
    }

    private void ShowProgressBar(float pct)
    {
        if (progressBar != null)
            progressBar.Show(pct);
    }

    private void HideProgressBar()
    {
        if (progressBar != null)
            progressBar.Hide();
    }

    private void SubscribePlayerHealth(PlayerMove player)
    {
        if (player == null) return;
        var health = player.GetComponent<Health>();
        if (health != null)
        {
            playerHealth = health;
            previousHealthPercent = health.HealthPercent;
            health.onHealthChanged.AddListener(OnPlayerDamaged);
        }
    }

    private void UnsubscribePlayerHealth()
    {
        if (playerHealth != null)
        {
            playerHealth.onHealthChanged.RemoveListener(OnPlayerDamaged);
            playerHealth = null;
        }
    }

    private void OnPlayerDamaged(float currentPercent)
    {
        if (currentPercent < previousHealthPercent - 0.001f)
        {
            currentReadProgress -= hitPenalty;
            currentReadProgress = Mathf.Max(0f, currentReadProgress);
        }
        previousHealthPercent = currentPercent;
    }

    private IEnumerator DelayedDestroy()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOpened ? Color.gray : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
