using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(PlayerHealth))]
public class BehaviourModel : MonoBehaviour
{
    [Header("Character")]
    [SerializeField] private CharacterCard card;

    [Header("Tick")]
    [SerializeField] private float tickInterval = 0.3f;

    private PlayerMove player;
    private PlayerCombat combat;
    private PlayerHealth health;
    private List<IAction> actions = new List<IAction>();
    private float nextTickTime;

    private NPCGoddess cachedNPC;
    private PlayerMove cachedPlayer1;
    private static readonly float CacheInterval = 1f;

    private void Awake()
    {
        player = GetComponent<PlayerMove>();
        combat = GetComponent<PlayerCombat>();
        health = GetComponent<PlayerHealth>();
        player.SetPlayerAI(true);

        actions.Add(new AttackAction());
        actions.Add(new KiteAction());
        actions.Add(new HealAction());
        actions.Add(new FleeAction());
        actions.Add(new FollowNPCAction());
    }

    private void Update()
    {
        if (player == null || player.IsDead) return;
        if (Time.time < nextTickTime) return;
        nextTickTime = Time.time + tickInterval;

        if (cachedNPC == null || cachedNPC.IsDead)
            cachedNPC = FindFirstObjectByType<NPCGoddess>();

        GameContext ctx = BuildContext();

        float bestScore = float.MinValue;
        IAction bestAction = null;
        foreach (var action in actions)
        {
            float score = action.Evaluate(card, ctx);
            if (score > bestScore) { bestScore = score; bestAction = action; }
        }
        bestAction?.Execute(player, combat, ctx, card);
    }

    private GameContext BuildContext()
    {
        var ctx = new GameContext();

        ctx.healthPercent = health != null ? health.HealthPercent : 1f;
        ctx.healthAbsolute = health != null ? health.CurrentHealth : 0f;
        ctx.position = transform.position;

        ctx.allDemons = new List<DemonMinion>(DemonMinion.AllDemons);
        ctx.nearbyEnemyCount = 0;
        ctx.totalEnemyHealth = 0f;
        float nearestSqr = float.MaxValue;

        foreach (var e in ctx.allDemons)
        {
            if (e == null || e.IsDead) continue;
            float dx = transform.position.x - e.transform.position.x;
            float dz = transform.position.z - e.transform.position.z;
            float sqrDist = dx * dx + dz * dz;
            float aggroSqr = card.aggroRange * card.aggroRange;
            if (sqrDist < aggroSqr)
            {
                ctx.nearbyEnemyCount++;
                ctx.totalEnemyHealth += e.CurrentHealth;
                if (sqrDist < nearestSqr) { nearestSqr = sqrDist; ctx.nearestEnemyDistance = Mathf.Sqrt(sqrDist); }
            }
        }

        if (cachedNPC != null && !cachedNPC.IsDead)
        {
            ctx.npcExists = true;
            ctx.npcAlive = true;
            ctx.npcIsWalking = cachedNPC.IsWalking;
            float dx = transform.position.x - cachedNPC.transform.position.x;
            float dz = transform.position.z - cachedNPC.transform.position.z;
            ctx.distanceToNPC = Mathf.Sqrt(dx * dx + dz * dz);
        }

        if (cachedPlayer1 == null || cachedPlayer1.IsDead)
        {
            foreach (var p in PlayerMove.AllPlayers)
                if (p != player) { cachedPlayer1 = p; break; }
        }
        if (cachedPlayer1 != null)
        {
            ctx.player1Exists = true;
            ctx.player1Alive = !cachedPlayer1.IsDead;
            var p1Combat = cachedPlayer1.GetComponent<PlayerCombat>();
            if (p1Combat != null) ctx.player1Target = p1Combat.AttackTarget;
        }

        float threat = ctx.nearbyEnemyCount * 0.15f;
        threat += (1f - ctx.healthPercent) * 0.3f;
        if (ctx.totalEnemyHealth > ctx.healthAbsolute && ctx.healthAbsolute > 0f) threat += 0.2f;
        ctx.threatLevel = Mathf.Clamp01(threat);

        // Potions
        ctx.potionCount = 0;
        var inv = InventoryManager.Instance;
        if (inv != null)
        {
            ctx.potionCount += inv.GetP1Backpack().FindAll(i => i.itemId == "health_potion_1" && i.itemType == ItemType.Consumable).Count;
            ctx.potionCount += inv.GetP2Backpack().FindAll(i => i.itemId == "health_potion_1" && i.itemType == ItemType.Consumable).Count;
        }

        return ctx;
    }
}
