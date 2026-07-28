/* ============================================================
 *  TreasureChest  -  宝箱（状态机 + 进度条 + 掉落）
 * ============================================================
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ChestOpenState { Idle, Reading, OpeningItems, ItemFlying, Complete }
public enum ChestType { Wooden, Copper, Silver, Gold }

/// <summary>One entry in a chest's loot table. Configured in the Inspector.</summary>
[System.Serializable]
public class ChestLootEntry
{
    [Tooltip("Relative probability weight (higher = more likely)")]
    public float weight = 1f;
    [Tooltip("Inline item data (used when definition is null)")]
    public GameItem item;
    [Tooltip("Optional ScriptableObject definition (overrides inline item)")]
    public GameItemDefinition definition;
    [Min(1)] public int minCount = 1;
    [Min(1)] public int maxCount = 1;
}


public class TreasureChest : MonoBehaviour
{
    [Header("Chest Settings")]
    [SerializeField] private ChestType chestType = ChestType.Wooden;

    [Header("Opening")]
    [SerializeField] private float readTime = 3f;
    [SerializeField] private float hitPenalty = 0.8f;
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private float destroyDelay = 3f;

    [Header("Progress Bar")]
    [SerializeField] private ProgressBarUI progressBar;

    [Header("Animation")]
    [SerializeField] private float openAnimationDelay = 0.3f;
    [SerializeField] private Transform itemSpawnPoint;
    [SerializeField] private GameObject goldVisualPrefab;
    [SerializeField] private float itemFlySpeed = 5f;
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private string chestOpenTrigger = "Open";
    private int chestOpenTriggerHash;

    [Header("Player Animation")]
    [Tooltip("Animator trigger name on the player for open-chest animation")]
    [SerializeField] private string playerOpenTriggerName = "Open";
    private int playerOpenTriggerHash;


    [Header("Loot Table")]
    [Tooltip("Weighted loot entries. If empty, uses built-in defaults for the chest type.")]
    [SerializeField] private List<ChestLootEntry> lootTable = new List<ChestLootEntry>();
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
        yield return new WaitForSeconds(openAnimationDelay);

        currentState = ChestOpenState.ItemFlying;
        if (highlightEffect != null) highlightEffect.SetActive(false);

        List<GameItem> loot = GenerateLoot();
        Vector3 spawnPos = itemSpawnPoint != null
            ? itemSpawnPoint.position
            : transform.position + Vector3.up * 1.5f;

        List<GameObject> visuals = new List<GameObject>();

        int totalGold = 0;
        foreach (var item in loot)
            if (item.itemType == ItemType.Currency)
                totalGold += item.goldAmount;

        if (totalGold > 0 && goldVisualPrefab != null && openingPlayer != null)
        {
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
        }

        foreach (var v in visuals)
            if (v != null) Destroy(v);

        UnsubscribePlayerHealth();
        isOpened = true;
        isOpening = false;
        currentState = ChestOpenState.Complete;

        if (cachedInventory == null)
            cachedInventory = FindFirstObjectByType<InventoryManager>();
        foreach (var item in loot)
            if (cachedInventory != null) cachedInventory.AddItem(item, openingPlayer);

        StartCoroutine(DelayedDestroy());
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

    private List<GameItem> GenerateLoot()
    {
        // Use Inspector-configured loot table if available
        if (lootTable.Count > 0)
            return RollLootTable();
        // Fallback: built-in defaults
        return GenerateDefaultLoot();
    }

    /// <summary>Roll a weighted random entry from the loot table.</summary>
    private List<GameItem> RollLootTable()
    {
        List<GameItem> items = new List<GameItem>();
        float totalWeight = 0f;
        foreach (var entry in lootTable) totalWeight += entry.weight;
        if (totalWeight <= 0f) return items;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (var entry in lootTable)
        {
            cumulative += entry.weight;
            if (roll <= cumulative)
            {
                int count = Random.Range(entry.minCount, entry.maxCount + 1);
                for (int i = 0; i < count; i++)
                {
                    if (entry.definition != null)
                        items.Add(entry.definition.ToGameItem());
                    else if (entry.item != null)
                        items.Add(entry.item.Clone());
                }
                break;
            }
        }
        return items;
    }

    private List<GameItem> GenerateDefaultLoot()
    {
        List<GameItem> items = new List<GameItem>();
        float roll = Random.value;

        switch (chestType)
        {
            case ChestType.Wooden:
                if (roll < 0.3f)
                    items.Add(new GameItem { itemType = ItemType.Consumable, itemId = "health_potion_1", itemName = "初级生命药水", description = "50HP/秒，持续4秒", healAmount = 200f });
                else if (roll < 0.6f)
                    items.Add(new GameItem { itemType = ItemType.Currency, itemId = "gold", itemName = "金币", goldAmount = 50 });
                else
                    items.Add(GenerateRandomEquipment(false));
                break;

            case ChestType.Copper:
                if (roll < 0.4f)
                    items.Add(GenerateRandomEquipment(false));
                else if (roll < 0.6f)
                    items.Add(GenerateRandomEquipment(true));
                else if (roll < 0.8f)
                    items.Add(new GameItem { itemType = ItemType.Consumable, itemId = "health_potion_1", itemName = "初级生命药水", description = "50HP/秒，持续4秒", healAmount = 200f });
                else
                    items.Add(new GameItem { itemType = ItemType.Currency, itemId = "gold", itemName = "金币", goldAmount = 100 });

                if (Random.value < 0.1f)
                    items.Add(new GameItem { itemType = ItemType.Equipment, itemId = "weapon_gem_1", itemName = "普通武器宝石", description = "DPS+40", slotType = EquipmentSlotType.WeaponGem, rarity = ItemRarity.Common, statType = StatType.DPS, statValue = 40 });
                break;
        }

        return items;
    }

    private static readonly EquipmentSlotType[] ArmorSlots =
        { EquipmentSlotType.Wrist, EquipmentSlotType.Chest, EquipmentSlotType.Shoulder, EquipmentSlotType.Pants };

    private static readonly float[] NormalHP  = { 40f, 80f, 50f, 60f };
    private static readonly float[] QualityHP = { 100f, 200f, 150f, 150f };
    private static readonly float[] NormalDPS  = { 15f, 20f, 15f, 15f };
    private static readonly float[] QualityDPS = { 40f, 50f, 40f, 40f };

    private static readonly string[] SlotNames = { "护腕", "铠甲", "护肩", "裤子" };

    private GameItem GenerateRandomEquipment(bool isQuality)
    {
        int slotIndex = Random.Range(0, ArmorSlots.Length);
        EquipmentSlotType slot = ArmorSlots[slotIndex];
        float statValue;
        string statDesc;

        if (Random.value < 0.5f)
        {
            statValue = isQuality ? QualityHP[slotIndex] : NormalHP[slotIndex];
            statDesc = "生命+" + statValue + "HP";
        }
        else
        {
            statValue = isQuality ? QualityDPS[slotIndex] : NormalDPS[slotIndex];
            statDesc = "DPS+" + statValue;
        }

        bool isHealth = statValue > 50;
        string prefix = isHealth ? "生命" : "力量";

        return new GameItem
        {
            itemType = ItemType.Equipment,
            itemId = (isQuality ? "quality_" : "common_") + prefix.ToLower() + "_" + SlotNames[slotIndex],
            itemName = (isQuality ? "精品" : "普通") + prefix + SlotNames[slotIndex],
            description = statDesc,
            slotType = slot,
            rarity = isQuality ? ItemRarity.Quality : ItemRarity.Common,
            statType = isHealth ? StatType.Health : StatType.DPS,
            statValue = statValue
        };
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOpened ? Color.gray : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}

