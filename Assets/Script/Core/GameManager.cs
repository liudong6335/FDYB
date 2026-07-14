/*
 * ============================================================
 *  GameManager  -  Game Phase & Spawn Manager (Singleton)
 * ============================================================
 *
 * 【Functions】
 *   Manages the game flow: Escort phase -> Exploration -> BossBattle.
 *   Controls demon spawns, chest spawns, penalty (drain HP when far from NPC),
 *   and Boss awakening timer.
 *
 * 【Singleton】
 *   GameManager is a singleton, attached to DontDestroyOnLoad.
 *
 * 【Adjustable Parameters】
 *   Escort:
 *   npcMaxPlayerDistance   - Max distance from NPC before penalty starts (default 30m)
 *
 *   Spawn:
 *   demonMinionPrefab      - Demon minion prefab
 *   demonSpawnInterval     - Spawn interval range (min/max)
 *   maxDemonMinions        - Max concurrent demon minions
 *   maxDemonLevel          - Max demon level
 *   demonReviveTime        - Revive delay in seconds
 *
 *   Boss Awakening:
 *   bossAwakenTime         - Time until Boss awakens (default 480s = 8min)
 *   killAccelerationPerKill- Each demon kill accelerates Boss timer
 *
 *   Chests:
 *   wooden/copper chest prefab - Wooden/Copper chest prefabs
 *   totalWoodenChests      - Total wooden chest count
 *   chestSpawnPerMinute    - Chests spawned per minute
 *   woodenChestStartDelay  - Initial delay before first wooden chest
 *   copperChestSpawnTime   - Time at which copper chest appears
 *
 *   Penalty:
 *   penaltyStartDistance   - Distance threshold for penalty (default 30m)
 *   penaltyBaseDrain       - Base HP drain per second (default 10/s)
 *   penaltyDrainPerMeter   - Additional drain per meter over threshold (default 2/s)
 *
 * 【Note】
 *   If players stray too far from NPC, they take damage.
 *   When Boss timer reaches 0, BossBattle phase begins.
 */
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Collections;

public enum GamePhase { Menu, Escort, Exploration, BossBattle, Victory, GameOver }

public class GameManager : MonoBehaviour
{
   [Header("Escort")]

    [Header("Spawn")]
    [SerializeField] private GameObject demonMinionPrefab;
    [Header("Spawn")]
    [SerializeField] private float demonSpawnIntervalMin = 20f;
    [SerializeField] private float demonSpawnIntervalMax = 40f;
    [SerializeField] private int maxDemonMinions = 4;
    [SerializeField] private int maxDemonLevel = 3;
    [SerializeField] private float demonReviveTime = 10f;

    [Header("Boss Awakening")]
    [SerializeField] private float bossAwakenTime = 480f;
    [SerializeField] private float killAccelerationPerKill = 15f;

    [Header("Chests")]
    [SerializeField] private GameObject woodenChestPrefab;
    [SerializeField] private GameObject copperChestPrefab;
    [SerializeField] private int totalWoodenChests = 6;
    [SerializeField] private float chestSpawnPerMinute = 2f;
    [SerializeField] private float woodenChestStartDelay = 60f;
    [SerializeField] private float copperChestSpawnTime = 180f;

   [Header("Penalty")]
    [SerializeField] private float penaltyStartDistance = 30f;
    [SerializeField] private float penaltyBaseDrain = 10f;
    [SerializeField] private float penaltyDrainPerMeter = 2f;


    [Header("References")]
    [SerializeField] private NPCGoddess npcGoddess;
    [SerializeField] private PlayerMove player1;
    [SerializeField] private PlayerMove player2;

    [Header("Events")]
    public UnityEvent onEscortStart;
    public UnityEvent onEscortEnd;
    public UnityEvent onGameOver;

    private GamePhase currentPhase = GamePhase.Menu;
    private List<DemonMinion> activeMinions = new List<DemonMinion>();
    private float demonActivateTimer;
    private float bossAwakenTimer;
    private float gameTimer;
    private int demonKillCount;
    private int minionLevel = 1;
    private float chestSpawnTimer;
    private int woodenChestsSpawned;
    private bool copperChestSpawned;
    private bool phase1Complete;
    private static GameManager instance;

    public static GameManager Instance { get { return instance; } }
    public GamePhase CurrentPhase { get { return currentPhase; } }
    public float NPCMaxPlayerDistance { get { return penaltyStartDistance; } }
    public float GameTimer { get { return gameTimer; } }
    public float BossAwakenTimer { get { return bossAwakenTimer; } }
    public float BossAwakenTimeTotal { get { return bossAwakenTime; } }
    public int DemonKillCount { get { return demonKillCount; } }
    public int MinionLevel { get { return minionLevel; } }
    public int ActiveMinionCount { get { return activeMinions.Count; } }

    private void Awake()
    {
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (npcGoddess == null) npcGoddess = FindFirstObjectByType<NPCGoddess>();
        if (player1 == null || player2 == null)
        {
            PlayerMove[] players = FindObjectsByType<PlayerMove>(FindObjectsSortMode.None);
            if (players.Length > 0 && player1 == null) player1 = players[0];
            if (players.Length > 1 && player2 == null) player2 = players[1];
        }
        if (npcGoddess != null)
        npcGoddess.OnArrived += HandleNPCArrived;
        StartEscortPhase();
    }

    private void Update()
    {
        if (currentPhase != GamePhase.Escort || npcGoddess == null) return;

        gameTimer += Time.deltaTime;

        // NPC death check
        if (npcGoddess.IsDead) { SetPhase(GamePhase.GameOver); return; }

        // NPC arrived check
        if (npcGoddess.HasArrived && !phase1Complete)
        {
            phase1Complete = true;
            SetPhase(GamePhase.Exploration);
            return;
        }

        // Gradual demon activation
        if (!phase1Complete)
        {
            demonActivateTimer -= Time.deltaTime;
            if (demonActivateTimer <= 0f && activeMinions.Count < maxDemonMinions)
            {
                ActivateNextDormantDemon();
                int aliveCount = activeMinions.Count;
                float nextInterval;
                if (aliveCount <= 0)
                    nextInterval = demonSpawnIntervalMin;
                else if (aliveCount >= maxDemonMinions - 1)
                    nextInterval = demonSpawnIntervalMax;
                else
                {
                    float t = (float)(aliveCount - 1) / (maxDemonMinions - 2);
                    nextInterval = Mathf.Lerp(demonSpawnIntervalMin, demonSpawnIntervalMax, t);
                }
                demonActivateTimer = nextInterval;
        }
        }

        // Boss awakening timer
        bossAwakenTimer -= Time.deltaTime;

        // Chest spawning

        if (!phase1Complete)
        {
            chestSpawnTimer -= Time.deltaTime;
            if (chestSpawnTimer <= 0f)
            {
                SpawnChests();

                chestSpawnTimer = 60f / chestSpawnPerMinute;
            }
        }

        ApplyPenalty(player2);
        ApplyPenalty(player1);
        // Penalty (only in Escort phase - guarded by the phase check at top of Update)

    }


    public void OnMinionKilledPublic(DemonMinion minion) { OnMinionKilled(minion); }

    private void OnMinionKilled(DemonMinion minion)
    {
        activeMinions.Remove(minion);
        demonKillCount++;
        bossAwakenTimer -= killAccelerationPerKill;

        if (demonKillCount % 2 == 0 && minionLevel < maxDemonLevel)
            minionLevel++;

        StartCoroutine(ReviveMinionAfterDelay(minion, demonReviveTime));
    }

    private IEnumerator ReviveMinionAfterDelay(DemonMinion originalMinion, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (phase1Complete || originalMinion == null) yield break;
        if (activeMinions.Count >= maxDemonMinions) yield break;

        int revivedLevel = Mathf.Min(originalMinion.Level + 1, maxDemonLevel);
        originalMinion.Revive(revivedLevel, npcGoddess);
        RegisterMinion(originalMinion);
    }

    /// <summary>
    /// Called when NPC reaches the final altar.
    /// Starts a death countdown on all active minions (replaces old static DemonMinion.OnNPCReachedAltar).
    /// </summary>
    private void HandleNPCArrived()
    {
        var allDemons = FindObjectsByType<DemonMinion>(FindObjectsSortMode.None);
        foreach (var m in allDemons)
        {
            if (m.IsDead)
                Destroy(m.gameObject);
        }
    }
    private void SpawnChests()
    {
        if (npcGoddess == null) return;

        // Wooden chests
        int toSpawn = Mathf.Min(2, totalWoodenChests - woodenChestsSpawned);
        for (int i = 0; i < toSpawn; i++)
        {
            if (woodenChestPrefab != null)
            {
                Vector3 pos = GetChestSpawnPosOnPath(5f);
                Instantiate(woodenChestPrefab, pos, Quaternion.identity);
                woodenChestsSpawned++;
            }
        }

        // Copper chest at 3 minutes
        if (gameTimer >= copperChestSpawnTime && !copperChestSpawned && copperChestPrefab != null)
        {
            Vector3 pos = GetChestSpawnPosOnPath(8f);
            Instantiate(copperChestPrefab, pos, Quaternion.identity);
            copperChestSpawned = true;
        }
    }

    private Vector3 GetChestSpawnPosOnPath(float offsetRadius)
    {
        if (npcGoddess == null || npcGoddess.WaypointPath == null || npcGoddess.WaypointPath.WaypointCount == 0)
            return GetChestSpawnPos(0f, 20f);

        int nextIndex = npcGoddess.CurrentWaypointIndex;
        if (nextIndex >= npcGoddess.WaypointPath.WaypointCount)
            nextIndex = npcGoddess.WaypointPath.WaypointCount - 1;

        Vector3 wpPos = npcGoddess.WaypointPath.GetWaypoint(nextIndex);
        Vector2 offset = Random.insideUnitCircle * offsetRadius;
        return wpPos + new Vector3(offset.x, 0f, offset.y);
    }
    private Vector3 GetChestSpawnPos(float minDist, float maxDist)
    {
        Vector3 npcPos = npcGoddess.transform.position;
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist = Random.Range(minDist, maxDist);
        return npcPos + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
    }

    private void ActivateNextDormantDemon()
    {
        // Find all DemonMinion instances through Monster parent
        var monsterGO = GameObject.Find("Monster");
        if (monsterGO == null)
        {
            Debug.LogError("[GameManager] Monster parent not found in scene!");
            return;
        }
        
        var demons = monsterGO.GetComponentsInChildren<DemonMinion>(true);
        //Debug.Log($"[GameManager] ActivateNextDormant: found {demons.Length} demons, activeMinions={activeMinions.Count}");
        foreach (var m in demons)
        {
            if (m.IsDormant && !activeMinions.Contains(m))
            {
                m.Activate(minionLevel, npcGoddess);
                activeMinions.Add(m);
                m.OnDeath += (dm) => OnMinionKilled(dm);
                return;
            }
        }
        Debug.LogWarning("[GameManager] No dormant demon found!");
    }
    public void RegisterMinion(DemonMinion m) { if (!activeMinions.Contains(m)) activeMinions.Add(m); }
    public void UnregisterMinion(DemonMinion m) { activeMinions.Remove(m); }


    public void SetPhase(GamePhase newPhase)
    {
        if (currentPhase == newPhase) return;
        currentPhase = newPhase;

        if (newPhase == GamePhase.Escort && onEscortStart != null) onEscortStart.Invoke();
        if (newPhase == GamePhase.Exploration && onEscortEnd != null) onEscortEnd.Invoke();
        if (newPhase == GamePhase.GameOver && onGameOver != null) onGameOver.Invoke();
    }

    public void StartEscortPhase()
    {
        gameTimer = 0f;
        // demonActivateTimer will be set below
        bossAwakenTimer = bossAwakenTime;
        chestSpawnTimer = woodenChestStartDelay;
        woodenChestsSpawned = 0;
        copperChestSpawned = false;
        phase1Complete = false;
        demonKillCount = 0;
        minionLevel = 1;
        // Discover, register, and bind OnDeath for pre-placed demons
        activeMinions.Clear();
        var allDemons = FindObjectsByType<DemonMinion>(FindObjectsSortMode.None);
        foreach (var m in allDemons)
        {
            m.SetDormant();
        }

        demonActivateTimer = Random.Range(demonSpawnIntervalMin, demonSpawnIntervalMax);

        if (npcGoddess != null) npcGoddess.StartWalking();
        SetPhase(GamePhase.Escort);
    }

    private void OnDrawGizmosSelected()
    {
        if (npcGoddess == null) return;
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(npcGoddess.transform.position, penaltyStartDistance);
    }

    private void ApplyPenalty(PlayerMove player)
    {
        if (player == null || npcGoddess == null) return;
        float dist = Vector3.Distance(player.transform.position, npcGoddess.transform.position);
        float overDistance = dist - penaltyStartDistance;
        if (overDistance > 0f)
        {
           float totalDrain = penaltyBaseDrain + overDistance * penaltyDrainPerMeter;
           player.GetComponent<Health>()?.TakeDamage(totalDrain * Time.deltaTime);
        }
    }
}
