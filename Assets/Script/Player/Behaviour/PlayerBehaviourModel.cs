using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Behaviour model for Player-controlled or AI-controlled player characters.
/// Uses PlayerMove, PlayerCombat, PlayerHealth for movement and combat.
/// </summary>
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(Health))]
public class PlayerBehaviourModel : BehaviourModelBase
{
    [Header("Behaviours 鈥?toggle each action on/off")]
    [SerializeField] private bool enableAttack = true;
    [SerializeField] private bool enableKite = true;
    [SerializeField] private bool enableHeal = true;
    [SerializeField] private bool enableLoot = true;
    [SerializeField] private bool enableFlee = true;
    [SerializeField] private bool enableFollowNPC = true;

    private PlayerMove player;
    private PlayerCombat combat;
    private PlayerHealth health;
    private Health healthComp;

    private NPCGoddess cachedNPC;
    private PlayerMove cachedPlayer1;

    protected override void Awake()
    {
        player = GetComponent<PlayerMove>();
        combat = GetComponent<PlayerCombat>();
        health = GetComponent<PlayerHealth>();
        healthComp = GetComponent<Health>();
        // Disable old AI controller to prevent both systems from fighting
        var oldAI = GetComponent<PlayerAIController>();
        if (oldAI != null) oldAI.enabled = false;
        player.SetPlayerAI(true);
        base.Awake();
    }

    protected override void RegisterActions()
    {
        if (enableAttack) actions.Add(new AttackAction());
        if (enableKite)   actions.Add(new KiteAction());
        if (enableHeal)   actions.Add(new HealAction());
        if (enableLoot)   actions.Add(new LootAction());
        if (enableFlee)   actions.Add(new FleeAction());
        if (enableFollowNPC) actions.Add(new FollowNPCAction());
    }

    protected override bool CanTick()
    {
        return player != null && !player.IsDead;
    }

    protected override GameContext BuildContext()
    {
        var ctx = new GameContext();
        ctx.healthPercent = health != null ? health.HealthPercent : 1f;
        ctx.healthAbsolute = health != null ? health.CurrentHealth : 0f;
        ctx.position = transform.position;

        // Phase 1: short-term memory
        ctx.timeSinceLastDamaged = healthComp != null ? healthComp.TimeSinceLastDamage : float.MaxValue;

        ctx.allDemons = new List<DemonMinion>(DemonMinion.AllDemons);
        ctx.nearbyEnemyCount = 0;
        ctx.totalEnemyHealth = 0f;
        float nearestSqr = float.MaxValue;
        foreach (var e in ctx.allDemons)
        {
            if (e == null || e.IsDead) continue;
            float sqrDist = transform.position.SqrDistanceXZ(e.transform.position);
            float aggroSqr = card.aggroRange * card.aggroRange;
            if (sqrDist < aggroSqr)
            {
                ctx.nearbyEnemyCount++;
                ctx.totalEnemyHealth += e.CurrentHealth;
                if (sqrDist < nearestSqr) { nearestSqr = sqrDist; ctx.nearestEnemyDistance = Mathf.Sqrt(sqrDist); }
            }
        }

        // Phase 1: group perception
        ctx.nearbyAllyCount = 0;
        if (card == null) return ctx;
        float allyRangeSqr = (card.aggroRange * 1.5f) * (card.aggroRange * 1.5f);
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null || p.IsDead || p == player) continue;
            float sqrDistAlly = transform.position.SqrDistanceXZ(p.transform.position);
            if (sqrDistAlly < allyRangeSqr) ctx.nearbyAllyCount++;
        }

        if (cachedNPC == null || cachedNPC.IsDead)
            cachedNPC = FindFirstObjectByType<NPCGoddess>();
        if (cachedNPC != null && !cachedNPC.IsDead)
        {
            ctx.npcExists = true;
            ctx.npcAlive = true;
            ctx.npcIsWalking = cachedNPC.IsWalking;
            ctx.distanceToNPC = transform.position.DistanceXZ(cachedNPC.transform.position);
        }

        // --- NPC threat detection ---
        ctx.enemiesNearNPC = 0;
        ctx.nearestEnemyToNPC = null;
        ctx.nearestEnemyToNPCDistance = float.MaxValue;
        ctx.npcUnderAttack = false;
        if (cachedNPC != null && !cachedNPC.IsDead && ctx.allDemons != null)
        {
        foreach (var e in ctx.allDemons)
        {
            if (e == null || e.IsDead) continue;
            float dex = cachedNPC.transform.position.x - e.transform.position.x;
            float dez = cachedNPC.transform.position.z - e.transform.position.z;
            float sqr = dex * dex + dez * dez;
            if (sqr < card.aggroRange * card.aggroRange)
            {
                ctx.enemiesNearNPC++;
                if (sqr < ctx.nearestEnemyToNPCDistance)
                {
                    ctx.nearestEnemyToNPCDistance = Mathf.Sqrt(sqr);
                    ctx.nearestEnemyToNPC = e.transform;
                }
                // Check if NPC is being attacked (enemy within attack range of NPC)
                if (sqr < e.AttackRange * e.AttackRange)
                    ctx.npcUnderAttack = true;
            }
        }
        }

        // Player index for formation positioning
        ctx.playerIndex = 0;
        var allP = PlayerMove.AllPlayers;
        for (int pi = 0; pi < allP.Count; pi++)
        {
        if (allP[pi] != null && allP[pi].gameObject == gameObject)
        { ctx.playerIndex = pi; break; }
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
        if (ctx.timeSinceLastDamaged < 5f)
            threat += (1f - ctx.timeSinceLastDamaged / 5f) * 0.25f;
        if (ctx.totalEnemyHealth > ctx.healthAbsolute && ctx.healthAbsolute > 0f) threat += 0.2f;
        ctx.threatLevel = Mathf.Clamp01(threat);

        ctx.potionCount = 0;
        var inv = InventoryManager.Instance;
        if (inv != null && player != null)
        {
            ctx.potionCount = inv.GetPlayerBackpack(player)
                .FindAll(i => i.itemId == "health_potion_1" && i.itemType == ItemType.Consumable).Count;
        }
        return ctx;
    }
}