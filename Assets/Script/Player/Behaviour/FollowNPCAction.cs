using UnityEngine;

public class FollowNPCAction : IAction
{
    public string Name => "FollowNPC";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (!ctx.npcExists || !ctx.npcAlive) return 0f;
        float followThreshold = card.followDistance * 1.5f;
        float distOver = ctx.distanceToNPC - followThreshold;
        if (distOver <= 0f) return card.supportiveness * 0.1f;
        float score = Mathf.Min(distOver / 10f, 1f) * 0.5f;
        score += card.supportiveness * 0.3f;
        score -= card.independence * 0.1f;
        return Mathf.Clamp01(score);
    }

    public void Execute(PlayerMove player, PlayerCombat combat, GameContext ctx, CharacterCard card)
    {
        if (player == null || combat == null) return;
        combat.ClearTarget();
        var npc = Object.FindFirstObjectByType<NPCGoddess>();
        if (npc != null)
            player.SetMoveDestination(npc.transform.position + new Vector3(1.5f, 0f, 1.5f));
    }
}

