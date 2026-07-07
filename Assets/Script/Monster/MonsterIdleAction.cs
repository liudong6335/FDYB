using UnityEngine;

public class MonsterIdleAction : IAction
{
    public string Name => "Idle";
    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (card == null) return 0f;
        float score = 0.05f;
        score += card.caution * 0.1f;
        score += (1f - card.aggression) * 0.05f;
        return score;
    }
    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        var minion = owner.GetComponent<DemonMinion>();
        if (minion == null) return;
        minion.PerformIdle();
    }
}
