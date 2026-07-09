using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NPCGoddess))]
[RequireComponent(typeof(Health))]
public class NPCBehaviourModel : BehaviourModelBase
{
    private Health health;
    private NPCGoddess npc;

    protected override void Awake()
    {
        health = GetComponent<Health>();
        npc = GetComponent<NPCGoddess>();
        base.Awake();
    }

    protected override void RegisterActions()
    {
        actions.Add(new NPCWalkAction());
        actions.Add(new NPCTeamHealAction());
        actions.Add(new NPCDamageShareAction());
        actions.Add(new NPCAttackAction());
        actions.Add(new NPCPauseAction());
        actions.Add(new NPCIdleAction());
    }

    protected override bool CanTick()
    {
        return health != null && !health.IsDead && npc != null && !npc.HasArrived && npc.IsBehaviourAIEnabled;
    }

    protected override GameContext BuildContext()
    {
        var ctx = new GameContext();
        ctx.healthPercent = health != null ? health.HealthPercent : 1f;
        ctx.healthAbsolute = health != null ? health.CurrentHealth : 0f;
        ctx.position = transform.position;
        ctx.timeSinceLastDamaged = health != null ? health.TimeSinceLastDamage : float.MaxValue;

        ctx.nearbyAllyCount = 0;
        float healSqr = npc.HealRange * npc.HealRange;
        float teamSqr = healSqr * 16f; // 4x HealRange (20m) for awareness
        float lowestHP = health != null ? health.HealthPercent : 1f;
        bool lowestIsPlayer = false;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null || p.IsDead) continue;
            float dx = transform.position.x - p.transform.position.x;
            float dz = transform.position.z - p.transform.position.z;
            if (dx * dx + dz * dz < teamSqr)
            {
                ctx.nearbyAllyCount++;
                float pHP = p.HealthPercent;
                if (pHP < lowestHP) { lowestHP = pHP; lowestIsPlayer = true; }
            }
        }

        ctx.threatLevel = 0f;
        if (ctx.timeSinceLastDamaged < 5f)
            ctx.threatLevel = Mathf.Clamp01(1f - ctx.timeSinceLastDamaged / 5f);


        ctx.lowestTeamHealthPercent = lowestHP;
        ctx.lowestTeamMemberIsPlayer = lowestIsPlayer;
        ctx.npcHealReady = Time.time >= npc.NextHealTime;

        // Quick enemy scan
        ctx.nearbyEnemyCount = 0;
        ctx.nearestEnemyDistance = float.MaxValue;
        float detectSqr = teamSqr * 1.5f; // ~24m enemy detection
        var demons = DemonMinion.AllDemons;
        foreach (var d in demons)
        {
            if (d == null || d.IsDead) continue;
            float dx = transform.position.x - d.transform.position.x;
            float dz = transform.position.z - d.transform.position.z;
            float sqrDist = dx * dx + dz * dz;
            if (sqrDist < detectSqr)
            {
                ctx.nearbyEnemyCount++;
                float dist = Mathf.Sqrt(sqrDist);
                if (dist < ctx.nearestEnemyDistance) ctx.nearestEnemyDistance = dist;
            }
        }
        ctx.npcCanAttack = ctx.nearbyEnemyCount > 0;
        return ctx;
    }
}
