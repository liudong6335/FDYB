using UnityEngine;

/// <summary>
/// Chase action — personality heavily modulates the urge to pursue.
/// Aggressive demons chase relentlessly; cautious/cowardly ones hang back.
/// </summary>
public class MonsterChaseAction : IAction
{
    public string Name => "Chase";
    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (card == null) return 0f;
        if (ctx.primaryTarget == null) return 0f;
        if (ctx.healthPercent <= 0f) return 0f;

        float score = card.aggression * 0.8f;           // aggression = primary driver
        score -= card.caution * 0.5f;                    // caution suppresses chase
        score += Mathf.Max(0f, card.aggression - 0.3f) * card.forcefulness * 0.3f;

        float proximity = Mathf.Max(0f, 1f - ctx.distanceToTarget / Mathf.Max(ctx.attackRange * 3f, 10f));
        score += proximity * 0.2f;

        // Self-preservation: low health = stop chasing
        if (ctx.healthPercent < 0.6f)
            score -= (0.6f - ctx.healthPercent) * 2f * card.selfPreservation;

        // Alone and cautious: won't chase without allies
        if (card.caution > 0.5f && ctx.nearbyAllyCount == 0)
            score -= 0.3f * card.caution;

        // Allies nearby = confidence boost (especially for less aggressive)
        score += ctx.nearbyAllyCount * 0.08f * (1f - card.caution);

        // Revenge: aggression makes you fight back, caution makes you flee
        if (ctx.timeSinceLastDamaged < 4f)
        {
            float urgency = 1f - ctx.timeSinceLastDamaged / 4f;
            score += urgency * card.aggression * 0.25f;
            score -= urgency * card.caution * 0.35f;
        }

        if (ctx.healthPercent > 0.7f)
            score += card.victoryFocus * 0.15f;

        return Mathf.Clamp01(score);
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        var minion = owner.GetComponent<DemonMinion>();
        if (minion == null || ctx.primaryTarget == null) return;
        minion.PerformChase(ctx.primaryTarget);
    }
}
