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
        base.Awake();
    }

    protected override void RegisterActions()
    {
        // Monster-specific actions will be added here
        // For now, the monster retains its existing logic (DemonMinion.Update)
        // This class provides the hook to replace it incrementally.
    }

    protected override bool CanTick()
    {
        return health != null && !health.IsDead;
    }

    protected override GameContext BuildContext()
    {
        var ctx = new GameContext();
        ctx.healthPercent = health != null ? health.HealthPercent : 1f;
        ctx.healthAbsolute = health != null ? health.CurrentHealth : 0f;
        ctx.position = transform.position;

        // Scan players
        ctx.nearbyEnemyCount = PlayerMove.AllPlayers.Count;
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
        return ctx;
    }
}
