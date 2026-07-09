using UnityEngine;

public class AttackAction : IAction
{
    public string Name => "Attack";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (ctx.nearbyEnemyCount == 0) return 0f;
        if (ctx.healthPercent <= 0f) return 0f;

        float score = card.aggression * 0.5f;
        score -= card.caution * 0.3f;

        // Continuous health penalty: low health suppresses attack proportionally
        score -= (1f - ctx.healthPercent) * card.selfPreservation * 0.5f;

        // Proximity: close targets are more compelling
        float proximity = Mathf.Max(0f, 1f - ctx.nearestEnemyDistance / card.aggroRange);
        score += proximity * 0.15f;

        // Group: allies make you braver
        score += ctx.nearbyAllyCount * 0.06f * card.supportiveness;

        // VictoryFocus adds when healthy enough to push objectives
        if (ctx.healthPercent > 0.5f) score += card.victoryFocus * 0.2f;

        // SelfPreservation penalty (old - kept for backward compat)
        score += ctx.nearbyEnemyCount * 0.03f;

        // Revenge: if just hit (< 2s ago), fight back harder
        if (ctx.timeSinceLastDamaged < 2f)
        {
            float recency = 1f - ctx.timeSinceLastDamaged / 2f;
            score += recency * card.aggression * 0.25f;
            score += recency * card.forcefulness * 0.2f;
            score -= recency * card.caution * 0.15f;
        }

        // 姹傝儨锛氭竻闄ゅ▉鑳?= 鎺ㄥ姩鑳滃埄
        score += ctx.nearbyEnemyCount * 0.05f * card.victoryFocus;

        if (ctx.npcExists && ctx.npcAlive && ctx.npcIsWalking)
        {
            float npcDanger = Mathf.Max(0f, 1f - ctx.distanceToNPC / 10f);
            score += npcDanger * card.supportiveness * 0.4f;
        }

        // 鑷繚锛氭畫琛€鏃跺噺灏戞敾鍑绘剰鎰?        if (ctx.healthPercent < 0.35f)
            score -= (0.35f - ctx.healthPercent) * 2f * (card.caution + card.selfPreservation) * 0.5f;

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



