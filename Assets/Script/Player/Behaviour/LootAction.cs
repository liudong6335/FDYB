using UnityEngine;

/// <summary>
/// 拾取宝箱动作 — 附近有未开启的宝箱时走过去开箱。
/// 评分由贪婪度、周围敌情、血量共同驱动。
/// </summary>
public class LootAction : IAction
{
    public string Name => "Loot";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        // 战斗紧迫时不捡
        if (ctx.threatLevel > 0.4f) return 0f;
        if (ctx.healthPercent < 0.4f) return 0f;

        // 扫描附近宝箱
        var chests = Object.FindObjectsByType<TreasureChest>(FindObjectsSortMode.None);
        int unopened = 0;
        float nearestDist = float.MaxValue;

        foreach (var c in chests)
        {
            if (c == null || c.IsOpened) continue;
            float d = Vector3.Distance(ctx.position, c.transform.position);
            if (d < card.lootRadius)
            {
                unopened++;
                if (d < nearestDist) nearestDist = d;
            }
        }

        if (unopened == 0) return 0f;

        // 贪婪驱动
        float score = card.greed * 0.5f;

        // 越近越想捡
        score += Mathf.Max(0f, 1f - nearestDist / card.lootRadius) * 0.3f;

        // 谨慎的人会更犹豫
        if (card.caution > 0.6f) score -= card.caution * 0.15f;

        return Mathf.Clamp01(score);
    }

    public void Execute(PlayerMove player, PlayerCombat combat, GameContext ctx, CharacterCard card)
    {
        var chests = Object.FindObjectsByType<TreasureChest>(FindObjectsSortMode.None);
        TreasureChest nearestChest = null;
        float nearestSqr = card.lootRadius * card.lootRadius;

        foreach (var c in chests)
        {
            if (c == null || c.IsOpened) continue;
            float dx = player.transform.position.x - c.transform.position.x;
            float dz = player.transform.position.z - c.transform.position.z;
            float sqr = dx * dx + dz * dz;
            if (sqr < nearestSqr) { nearestSqr = sqr; nearestChest = c; }
        }

        if (nearestChest == null) return;

        // 走到宝箱旁边
        if (nearestSqr > 2.5f * 2.5f)
        {
            combat.ClearTarget();
            player.SetMoveDestination(nearestChest.transform.position);
        }
        else
        {
            // 够近了，交互开箱
            nearestChest.TryInteract();
        }
    }
}
