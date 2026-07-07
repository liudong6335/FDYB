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

        // 鑷繚锛氭洿鎯抽€冭窇
        if (card.selfPreservation > 0.5f)
            score += card.selfPreservation * 0.2f;

        // 姹傝儨锛氭兂璧㈢殑浜轰笉浼氳交鏄撻€?        if (card.victoryFocus > 0.5f && ctx.nearbyEnemyCount > 0)
            score -= card.victoryFocus * 0.1f;

                score -= (card.oppression + card.forcefulness) * 0.12f;
        return Mathf.Clamp01(score);
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        var player = owner.GetComponent<PlayerMove>();
        var combat = owner.GetComponent<PlayerCombat>();
        if (player == null || combat == null) return;
        combat.ClearTarget();

        // 浼樺厛閫冨線NPC锛孨PC涓嶅湪鍒欓€冨線P1
        var npc = Object.FindFirstObjectByType<NPCGoddess>();
        if (npc != null && !npc.IsDead)
            player.SetMoveDestination(npc.transform.position);
        else if (ctx.player1Exists && ctx.player1Alive && ctx.player1Target != null)
            player.SetMoveDestination(ctx.player1Target.position);
    }
}


