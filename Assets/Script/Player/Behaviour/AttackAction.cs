using UnityEngine;

public class AttackAction : IAction
{
    public string Name => "Attack";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (ctx.nearbyEnemyCount == 0) return 0f;
        if (ctx.healthPercent <= 0f) return 0f;

        float score = card.aggression * 0.5f;
        score += Mathf.Min(ctx.nearbyEnemyCount / 5f, 1f) * 0.3f;
        score += Mathf.Max(0f, 1f - ctx.nearestEnemyDistance / card.aggroRange) * 0.2f;

        // 姹傝儨锛氭竻闄ゅ▉鑳?= 鎺ㄥ姩鑳滃埄
        score += ctx.nearbyEnemyCount * 0.05f * card.victoryFocus;

        if (ctx.npcExists && ctx.npcAlive && ctx.npcIsWalking)
        {
            float npcDanger = Mathf.Max(0f, 1f - ctx.distanceToNPC / 10f);
            score += npcDanger * card.supportiveness * 0.4f;
        }

        // 鑷繚锛氭畫琛€鏃跺噺灏戞敾鍑绘剰鎰?        if (ctx.healthPercent < 0.35f)
            score -= (0.35f - ctx.healthPercent) * 2f * (card.caution + card.selfPreservation) * 0.5f;

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

