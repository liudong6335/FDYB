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
        actions.Add(new NPCCastHealAction());
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
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null || p.IsDead) continue;
            float dx = transform.position.x - p.transform.position.x;
            float dz = transform.position.z - p.transform.position.z;
            if (dx * dx + dz * dz < healSqr) ctx.nearbyAllyCount++;
        }

        ctx.threatLevel = 0f;
        if (ctx.timeSinceLastDamaged < 5f)
            ctx.threatLevel = Mathf.Clamp01(1f - ctx.timeSinceLastDamaged / 5f);

        return ctx;
    }
}
