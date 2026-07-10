using UnityEngine;

/// <summary>
/// AttackAction - only fights when there's a reason:
/// - NPC is under attack
/// - Player was damaged recently (self-defense)
/// - Very high aggression (proactive clearing, rare)
/// Otherwise returns near-zero score so FollowNPC/etc can win.
/// </summary>
public class AttackAction : IAction
{
    public string Name => "Attack";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (ctx.nearbyEnemyCount == 0) return 0f;
        if (ctx.healthPercent <= 0f) return 0f;

        // --- Threat gate: only fight when there's a reason ---
        bool npcUnderThreat = ctx.npcUnderAttack;
        bool selfUnderThreat = ctx.timeSinceLastDamaged < 3f;
        bool hasReasonToFight = npcUnderThreat || selfUnderThreat;

        if (!hasReasonToFight)
        {
            // No threat at all: very low prio (only extremely aggressive chars might poke)
            return Mathf.Clamp01(card.aggression * 0.1f - card.caution * 0.2f);
        }

        // --- Full attack evaluation ---
        float score = card.aggression * 0.5f;
        score -= card.caution * 0.3f;
        score -= (1f - ctx.healthPercent) * card.selfPreservation * 0.5f;

        // Proximity: close targets are more compelling
        float proximity = Mathf.Max(0f, 1f - ctx.nearestEnemyDistance / card.aggroRange);
        score += proximity * 0.15f;

        // Allies make you braver
        score += ctx.nearbyAllyCount * 0.06f * card.supportiveness;

        // VictoryFocus when healthy
        if (ctx.healthPercent > 0.5f) score += card.victoryFocus * 0.2f;

        // Revenge: if just hit (< 2s ago), fight back harder
        if (ctx.timeSinceLastDamaged < 2f)
        {
            float recency = 1f - ctx.timeSinceLastDamaged / 2f;
            score += recency * card.aggression * 0.25f;
            score += recency * card.forcefulness * 0.2f;
            score -= recency * card.caution * 0.15f;
        }

        // NPC protection bonus - major driver when NPC is attacked
        if (npcUnderThreat)
        {
            if (ctx.npcUnderAttack) score += 0.4f;
            score += ctx.nearbyEnemyCount * 0.05f * card.victoryFocus;
        }

        score += card.oppression * 0.15f + card.forcefulness * 0.15f;

        return Mathf.Clamp01(score);
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        var player = owner.GetComponent<PlayerMove>();
        var combat = owner.GetComponent<PlayerCombat>();
        if (combat == null || player == null) return;

        if (ctx.player1Target != null)
        {
            var dmg = ctx.player1Target.GetComponent<IDamageable>();
            if (dmg != null && !dmg.IsDead) { combat.TryEngage(ctx.player1Target); return; }
        }

        if (ctx.allDemons != null && ctx.allDemons.Count > 0)
        {
            Transform best = null;
            float bestSqr = float.MaxValue;
            Vector3 pos = player.transform.position;
            foreach (var demon in ctx.allDemons)
            {
                if (demon == null || demon.IsDead || demon.transform == null) continue;
                Vector3 diff = pos - demon.transform.position;
                float sqr = diff.x * diff.x + diff.z * diff.z;
                if (sqr < bestSqr) { bestSqr = sqr; best = demon.transform; }
            }
            if (best != null) combat.TryEngage(best);
        }
    }
}
