using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Behaviour model for monster characters (DemonMinion).
/// Drives chase, attack, patrol, and flee actions using a CharacterCard.
/// </summary>
[RequireComponent(typeof(Health))]
public class MonsterBehaviourModel : BehaviourModelBase
{
    private Health health;
    private DemonMinion minion;
    private CharacterController cc;

    protected override void Awake()
    {
        health = GetComponent<Health>();
        minion = GetComponent<DemonMinion>();
        cc = GetComponent<CharacterController>();
        if (minion != null) minion.SetBehaviourAI(true);
        base.Awake();
    }

    protected override void RegisterActions()
    {
        actions.Add(new MonsterChaseAction());
        actions.Add(new MonsterPatrolAction());
        actions.Add(new MonsterIdleAction());
        actions.Add(new MonsterFleeAction());
    }

    protected override bool CanTick()
    {
        return health != null && !health.IsDead && (minion == null || minion.IsBehaviourAIEnabled);
    }

    protected override GameContext BuildContext()
    {
        var ctx = new GameContext();
        ctx.healthPercent = health != null ? health.HealthPercent : 1f;
        ctx.healthAbsolute = health != null ? health.CurrentHealth : 0f;
        ctx.position = transform.position;

        // Short-term memory
        ctx.timeSinceLastDamaged = health != null ? health.TimeSinceLastDamage : float.MaxValue;

       // Scan players
        float aggroSqr = minion != null ? minion.SqAggroRange : 225f;
        ctx.nearbyEnemyCount = 0;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null || p.IsDead) continue;
            float dx = transform.position.x - p.transform.position.x;
            float dz = transform.position.z - p.transform.position.z;
            if (dx * dx + dz * dz < aggroSqr) ctx.nearbyEnemyCount++;
        }

        // Group perception: nearby demon allies
        ctx.nearbyAllyCount = 0;
        float allySenseSqr = aggroSqr * 0.5f;
        foreach (var d in DemonMinion.AllDemons)
        {
            if (d == null || d.IsDead || d.gameObject == gameObject) continue;
            float dx = transform.position.x - d.transform.position.x;
            float dz = transform.position.z - d.transform.position.z;
            if (dx * dx + dz * dz < allySenseSqr) ctx.nearbyAllyCount++;
        }

        ctx.threatLevel = Mathf.Clamp01((1f - ctx.healthPercent) * 0.5f);

        // Scan NPC
        var npc = FindFirstObjectByType<NPCGoddess>();
        if (npc != null && !npc.IsDead)
        {
            ctx.npcExists = true;
            ctx.npcAlive = true;
            float dx = transform.position.x - npc.transform.position.x;
            float dz = transform.position.z - npc.transform.position.z;
            ctx.distanceToNPC = Mathf.Sqrt(dx * dx + dz * dz);
        }

        // --- Target selection ---
        ctx.attackRange = minion != null ? minion.AttackRange : 5f;
        ctx.primaryTarget = null;
        ctx.distanceToTarget = float.MaxValue;

        if (minion != null)
        {
            Transform selected = null;
            Transform attacker = minion.AttackerTransform;

            // First priority: revenge the attacker
            if (attacker != null)
            {
                float dx = transform.position.x - attacker.position.x;
                float dz = transform.position.z - attacker.position.z;
                float sqr = dx * dx + dz * dz;
                var dmg = attacker.GetComponent<IDamageable>();
               if (dmg != null && !dmg.IsDead && sqr < minion.SqDisengageDistance)
                   selected = attacker;
                else if (attacker != null && sqr >= minion.SqDisengageDistance)
                    minion.ClearAttacker();
           }

            // First priority: chase NPC
            if (selected == null && npc != null && !npc.IsDead && !npc.HasArrived)
                selected = npc.transform;

            // Second priority: scan nearest player within aggro range
            if (selected == null)
            {
                float ns = aggroSqr;
                foreach (var p in PlayerMove.AllPlayers)
                {
                    if (p == null || p.IsDead) continue;
                    float dx = transform.position.x - p.transform.position.x;
                    float dz = transform.position.z - p.transform.position.z;
                    float sqr = dx * dx + dz * dz;
                    if (sqr < ns) { ns = sqr; selected = p.transform; }
                }
            }

            ctx.primaryTarget = selected;
        }

        if (ctx.primaryTarget != null)
        {
            float dx = transform.position.x - ctx.primaryTarget.position.x;
            float dz = transform.position.z - ctx.primaryTarget.position.z;
            ctx.distanceToTarget = Mathf.Sqrt(dx * dx + dz * dz);
        }

        return ctx;
    }
}
