using UnityEngine;

/// <summary>
/// Patrol action — cautious demons patrol even when a target exists.
/// Patrol = safer alternative to chasing for timid demons.
/// </summary>
public class MonsterPatrolAction : IAction
{
    public string Name => "Patrol";
    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (card == null) return 0f;
        float score = card.caution * 0.4f;
        score += card.independence * 0.2f;
        score += (1f - card.aggression) * 0.2f;

        // Healthy cautious demons prefer safe patrol over risky chase
        if (ctx.healthPercent > 0.7f)
            score += card.caution * 0.15f;

        // Recently damaged: patrol = retreat to safe position
        if (ctx.timeSinceLastDamaged < 3f)
            score += (1f - ctx.timeSinceLastDamaged / 3f) * card.selfPreservation * 0.3f;

        // Has a target but doesn't want to chase = patrol instead
        if (ctx.primaryTarget != null && card.aggression < 0.5f)
            score += (0.5f - card.aggression) * 0.3f;

        // Victory focus = less patrolling (more goal-oriented)
        score -= card.victoryFocus * 0.1f;

        return Mathf.Clamp01(score);
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        var minion = owner.GetComponent<DemonMinion>();
        if (minion == null) return;
        minion.ClearChaseTarget();
        minion.PerformPatrol();
    }
}
