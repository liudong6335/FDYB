using UnityEngine;

/// <summary>
/// Flee action — cowardly/cautious demons retreat when damaged or outmatched.
/// Execute clears chase target and heads toward NPC or spawn.
/// </summary>
public class MonsterFleeAction : IAction
{
    public string Name => "Flee";
    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (card == null) return 0f;

        float score = 0f;

        // Low health + self-preservation = strong flee urge
        if (ctx.healthPercent < 0.5f)
            score += (0.5f - ctx.healthPercent) * 2f * card.selfPreservation;

        // Caution amplifies fleeing
        score += card.caution * 0.15f;

        // Recently damaged = flee impulse
        if (ctx.timeSinceLastDamaged < 4f)
        {
            float urgency = 1f - ctx.timeSinceLastDamaged / 4f;
            score += urgency * card.selfPreservation * 0.4f;
            score += urgency * card.caution * 0.3f;
            // Aggression counteracts fleeing ("fight back")
            score -= urgency * card.aggression * 0.25f;
        }

        // Outnumbered = flee (especially for cowards)
        if (ctx.nearbyAllyCount == 0 && ctx.nearbyEnemyCount > ctx.nearbyAllyCount + 1)
            score += 0.2f * (1f - card.aggression);

        // Chasing is still preferred unless score is high enough
        // Only flee when frightening enough
        return Mathf.Clamp01(score);
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        var minion = owner.GetComponent<DemonMinion>();
        if (minion == null) return;
        minion.ClearChaseTarget();
        // Move toward spawn position (safe spot) or NPC
        minion.PerformChase(null);  // clear target
        // Set movement toward safe direction via patrol
        minion.PerformPatrol();
    }
}
