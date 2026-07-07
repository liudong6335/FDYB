using UnityEngine;

/// <summary>
/// 鏀诲嚮鍔ㄤ綔 鈥?璇勫垎鍙栧喅浜庤鑹叉敾鍑绘€?+ 鍛ㄥ洿鏁屼汉鏁伴噺 + 濞佽儊绋嬪害銆?/// 浼樺厛鎵撴渶杩戠殑鐩爣锛岃嫢瑙掕壊鍊惧悜闆嗙伀鍒欐墦P1姝ｅ湪鎵撶殑鐩爣銆?/// </summary>
public class AttackAction : IAction
{
    public string Name => "Attack";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (ctx.nearbyEnemyCount == 0) return 0f;
        if (ctx.healthPercent <= 0f) return 0f;

        float score = card.aggression * 0.5f;

        // 鏁屼汉鏁拌秺澶氭敾鍑讳环鍊艰秺楂?        score += Mathf.Min(ctx.nearbyEnemyCount / 5f, 1f) * 0.3f;

        // 鏁屼汉寰堣繎鏃舵敾鍑讳环鍊奸珮
        score += Mathf.Max(0f, 1f - ctx.nearestEnemyDistance / card.aggroRange) * 0.2f;

        // NPC鏈夊嵄闄╂椂瑙﹀彂淇濇姢鍊惧悜
        if (ctx.npcExists && ctx.npcAlive && ctx.npcIsWalking)
        {
            float npcDanger = Mathf.Max(0f, 1f - ctx.distanceToNPC / 10f);
            score += npcDanger * card.supportiveness * 0.4f;
        }

        // 琛€閲忎綆鏃舵敾鍑绘剰鎰块檷浣?        if (ctx.healthPercent < 0.3f)
            score -= (0.3f - ctx.healthPercent) * 2f * card.caution;

        return Mathf.Clamp01(score);
    }

    public void Execute(PlayerMove player, PlayerCombat combat, GameContext ctx, CharacterCard card)
    {
        // 闆嗙伀妯″紡锛氭墦P1鐨勭洰鏍?        if (ctx.player1Exists && ctx.player1Alive && ctx.player1Target != null)
        {
            var dmg = ctx.player1Target.GetComponent<IDamageable>();
            if (dmg != null && !dmg.IsDead)
            {
                combat.TryEngage(ctx.player1Target);
                return;
            }
        }

        // 榛樿锛氭墦鏈€杩戠殑鏁屼汉
        if (ctx.allDemons != null)
        {
            Transform nearest = null;
            float nearestSqr = float.MaxValue;

            foreach (var e in ctx.allDemons)
            {
                if (e == null || e.IsDead) continue;
                float dx = player.transform.position.x - e.transform.position.x;
                float dz = player.transform.position.z - e.transform.position.z;
                float sqr = dx * dx + dz * dz;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = e.transform;
                }
            }

            if (nearest != null)
                combat.TryEngage(nearest);
        }
    }
}

