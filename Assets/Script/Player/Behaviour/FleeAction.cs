using UnityEngine;

public class FleeAction : IAction
{
    public string Name => "Flee";
    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (ctx.healthPercent > card.fleeHealthThreshold && ctx.threatLevel < 0.5f) return 0f;

        float score = card.caution * 0.3f + card.selfPreservation * 0.4f;
        score -= card.aggression * 0.3f;

        // Health threshold scales with selfPreservation: high selfPres = flee sooner
        float fleeAt = 0.2f + card.selfPreservation * 0.4f;
        if (ctx.healthPercent < fleeAt)
            score += (fleeAt - ctx.healthPercent) * 2f;

        if (ctx.nearbyEnemyCount >= 2)
            score += Mathf.Min((ctx.nearbyEnemyCount - 1) / 3f, 1f) * 0.2f;

        if (ctx.timeSinceLastDamaged < 3f)
        {
            float urgency = 1f - ctx.timeSinceLastDamaged / 3f;
            score += urgency * card.selfPreservation * 0.4f;
            score += urgency * card.caution * 0.3f;
        }

        score += card.selfPreservation * 0.2f;
        score -= card.victoryFocus * 0.15f;
        score += ctx.threatLevel * 0.2f;
        score -= (card.oppression + card.forcefulness) * 0.1f;
        return Mathf.Clamp01(score);
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        var player = owner.GetComponent<PlayerMove>();
        var combat = owner.GetComponent<PlayerCombat>();
        if (player == null || combat == null) return;
        combat.ClearTarget();
        var npc = Object.FindFirstObjectByType<NPCGoddess>();
        if (npc != null && !npc.IsDead)
            player.SetMoveDestination(npc.transform.position);
        else if (ctx.player1Exists && ctx.player1Alive && ctx.player1Target != null)
            player.SetMoveDestination(ctx.player1Target.position);
    }
}
