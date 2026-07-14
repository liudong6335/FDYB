using UnityEngine;

public class HealAction : IAction
{
    public string Name => "Heal";
    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (ctx.healthPercent >= 0.95f && ctx.nearbyAllyCount == 0) return 0f;

        float score = 0f;

        // Heal threshold scales with selfPreservation
        float healAt = 0.3f + card.selfPreservation * 0.4f;
        if (ctx.healthPercent < healAt)
            score += (healAt - ctx.healthPercent) * 2f * card.selfPreservation;

        // Support allies nearby
        if (ctx.nearbyAllyCount > 0 && ctx.healthPercent < 0.95f)
            score += ctx.nearbyAllyCount * 0.12f * card.supportiveness;

        if (ctx.timeSinceLastDamaged < 3f)
        {
            float urgency = 1f - ctx.timeSinceLastDamaged / 3f;
            score += urgency * card.caution * 0.4f;
        }

        if (ctx.threatLevel > 0.2f)
            score += ctx.threatLevel * 0.3f * card.selfPreservation;

        score += card.victoryFocus * 0.1f;
        return Mathf.Clamp01(score);
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        var player = owner.GetComponent<PlayerMove>();
        if (player == null) return;
        var inv = InventoryManager.Instance;
        if (inv == null) return;
        inv.UseConsumable("health_potion_1", player);
    }
}
