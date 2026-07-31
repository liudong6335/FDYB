using UnityEngine;
using UnityEngine.Events;

public enum GamePhase { Menu, Escort, Exploration, BossBattle, Victory, GameOver }

/// <summary>
/// Manages the escort phase: penalty, phase transitions, NPC lifecycle.
/// Attach to a GameObject in the scene alongside NPCGoddess.
/// Replaces GameManagers escort-phase responsibilities.
/// </summary>
public class EscortPhaseController : MonoBehaviour
{
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
    private float gameTimer;
    private bool phase1Complete;

    public GamePhase CurrentPhase { get { return currentPhase; } }
    public float GameTimer { get { return gameTimer; } }

    private void Start()
    {
        if (npcGoddess == null) npcGoddess = FindFirstObjectByType<NPCGoddess>();
        if (player1 == null || player2 == null)
        {
            PlayerMove[] players = FindObjectsByType<PlayerMove>(FindObjectsSortMode.None);
            if (players.Length > 0 && player1 == null) player1 = players[0];
            if (players.Length > 1 && player2 == null) player2 = players[1];
        }
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnAllAreasComplete += HandleAllAreasComplete;
        StartEscortPhase();
    }

    private void Update()
    {
        if (currentPhase != GamePhase.Escort || npcGoddess == null) return;

        gameTimer += Time.deltaTime;

        if (npcGoddess.IsDead) { SetPhase(GamePhase.GameOver); return; }
        if (npcGoddess.HasArrived && !phase1Complete)
        {
            phase1Complete = true;
            SetPhase(GamePhase.Exploration);
            return;
        }

        ApplyPenalty(player2);
        ApplyPenalty(player1);
    }

    public void StartEscortPhase()
    {
        // Reset global loot budget for a new game
        if (LootBudgetManager.Instance != null)
            LootBudgetManager.Instance.Reset();

        gameTimer = 0f;
        phase1Complete = false;

        var allDemons = FindObjectsByType<DemonMinion>(FindObjectsSortMode.None);
        foreach (var m in allDemons)
            m.SetDormant();

        if (npcGoddess != null) npcGoddess.StartWalking();
        SetPhase(GamePhase.Escort);
    }

    public void SetPhase(GamePhase newPhase)
    {
        if (currentPhase == newPhase) return;
        currentPhase = newPhase;

        if (newPhase == GamePhase.Escort) onEscortStart?.Invoke();
        if (newPhase == GamePhase.Exploration) onEscortEnd?.Invoke();
        if (newPhase == GamePhase.GameOver) onGameOver?.Invoke();
    }

    private void HandleAllAreasComplete()
    {
        Debug.Log("[EscortPhaseController] All quest areas completed, transitioning...");
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

    private void OnDrawGizmosSelected()
    {
        if (npcGoddess == null) return;
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(npcGoddess.transform.position, penaltyStartDistance);
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnAllAreasComplete -= HandleAllAreasComplete;
    }
}
