using UnityEngine;

public class HealAction : IAction
{
    public string Name => "Heal";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (ctx.potionCount <= 0) return 0f;
        if (ctx.healthPercent >= card.potionThreshold) return 0f;

        float score = Mathf.Max(0f, card.potionThreshold - ctx.healthPercent) * 2f;
        score += card.caution * 0.15f;

        if (card.selfPreservation > 0.5f)
            score += card.selfPreservation * 0.2f;

        if (ctx.threatLevel > 0.6f && ctx.healthPercent < 0.4f) score += 0.2f;

                score -= card.forcefulness * 0.15f;
        return Mathf.Clamp01(score);
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        var player = owner.GetComponent<PlayerMove>();
        if (player == null) return;

        var inv = InventoryManager.Instance;
        if (inv != null) inv.UseConsumable("health_potion_1", player);
    }
}

