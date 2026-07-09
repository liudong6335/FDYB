using UnityEngine;
public class NPCWalkAction : IAction
{
    public string Name => "Walk";
    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        float score = 0.3f;
        score += card.independence * 0.2f;
        score += card.aggression * 0.15f;
        if (ctx.healthPercent < 0.6f) score -= (0.6f - ctx.healthPercent) * 0.5f * card.selfPreservation;
        if (ctx.threatLevel > 0.3f) score -= ctx.threatLevel * 0.2f;
        return Mathf.Clamp01(score);
    }
    public void Execute(GameObject owner, GameContext ctx, CharacterCard card) { owner.GetComponent<NPCGoddess>().recommendedAction = "Walk"; }
}

public class NPCPauseAction : IAction
{
    public string Name => "Pause";
    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        float score = card.caution * 0.3f + (1f - card.aggression) * 0.15f;
        if (ctx.healthPercent > 0.8f) score += 0.2f;
        if (ctx.healthPercent < 0.5f) score -= (0.5f - ctx.healthPercent) * 0.4f;
        if (ctx.threatLevel > 0.2f) score -= ctx.threatLevel * 0.3f;
        score -= card.victoryFocus * 0.15f;
        return Mathf.Clamp01(score);
    }
    public void Execute(GameObject owner, GameContext ctx, CharacterCard card) { owner.GetComponent<NPCGoddess>().recommendedAction = "Pause"; }
}

public class NPCIdleAction : IAction
{
    public string Name => "Idle";
    public float Evaluate(CharacterCard card, GameContext ctx) { return 0.05f; }
    public void Execute(GameObject owner, GameContext ctx, CharacterCard card) { owner.GetComponent<NPCGoddess>().recommendedAction = "Idle"; }
}
