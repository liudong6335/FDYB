using UnityEngine;

public class FleeAction : IAction
{
    public string Name => "Flee";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (ctx.healthPercent > card.fleeHealthThreshold && ctx.threatLevel < 0.5f) return 0f;
        float score = card.caution * 0.4f;
        score += Mathf.Max(0f, card.fleeHealthThreshold - ctx.healthPercent) * 1.5f;
        score += ctx.threatLevel * 0.3f * card.caution;
        if (ctx.nearbyEnemyCount >= 2) score += Mathf.Min((ctx.nearbyEnemyCount - 1) / 3f, 1f) * 0.2f;
        score -= card.aggression * 0.15f;
        return Mathf.Clamp01(score);
    }

    public void Execute(PlayerMove player, PlayerCombat combat, GameContext ctx, CharacterCard card)
    {
        combat.ClearTarget();
        var npc = Object.FindFirstObjectByType<NPCGoddess>();
        if (npc != null && !npc.IsDead)
            player.SetMoveDestination(npc.transform.position);
    }
}
