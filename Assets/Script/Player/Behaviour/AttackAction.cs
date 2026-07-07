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

        if (ctx.npcExists && ctx.npcAlive && ctx.npcIsWalking)
        {
            float npcDanger = Mathf.Max(0f, 1f - ctx.distanceToNPC / 10f);
            score += npcDanger * card.supportiveness * 0.4f;
        }

        if (ctx.healthPercent < 0.3f)
            score -= (0.3f - ctx.healthPercent) * 2f * card.caution;

        return Mathf.Clamp01(score);
    }

    public void Execute(PlayerMove player, PlayerCombat combat, GameContext ctx, CharacterCard card)
    {
        if (combat == null || player == null) return;

        // Try following P1's target (focus fire)
        if (ctx.player1Target != null)
        {
            var dmg = ctx.player1Target.GetComponent<IDamageable>();
            if (dmg != null && !dmg.IsDead)
            {
                combat.TryEngage(ctx.player1Target);
                return;
            }
        }

        // Fallback: attack nearest enemy
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
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = demon.transform;
                }
            }

            if (best != null)
                combat.TryEngage(best);
        }
    }
}
