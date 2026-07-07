using UnityEngine;

/// <summary>
/// 鎷惧彇瀹濈鍔ㄤ綔 鈥?闄勮繎鏈夋湭寮€鍚殑瀹濈鏃惰蛋杩囧幓寮€绠便€?/// 璇勫垎鐢辫椽濠害銆佸懆鍥存晫鎯呫€佽閲忓叡鍚岄┍鍔ㄣ€?/// </summary>
public class LootAction : IAction
{
    public string Name => "Loot";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        // 鎴樻枟绱ц揩鏃朵笉鎹?        if (ctx.threatLevel > 0.4f) return 0f;
        if (ctx.healthPercent < 0.4f) return 0f;

        // 鎵弿闄勮繎瀹濈
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

        // 璐┆椹卞姩
        float score = card.greed * 0.5f;

        // 瓒婅繎瓒婃兂鎹?        score += Mathf.Max(0f, 1f - nearestDist / card.lootRadius) * 0.3f;

        // 璋ㄦ厧鐨勪汉浼氭洿鐘硅鲍
        if (card.caution > 0.6f) score -= card.caution * 0.15f;

        return Mathf.Clamp01(score);
    }

    public void Execute(PlayerMove player, PlayerCombat combat, GameContext ctx, CharacterCard card)
    {
        if (player == null || combat == null) return;
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

        // 璧板埌瀹濈鏃佽竟
        if (nearestSqr > 2.5f * 2.5f)
        {
            combat.ClearTarget();
            player.SetMoveDestination(nearestChest.transform.position);
        }
        else
        {
            // 澶熻繎浜嗭紝浜や簰寮€绠?            nearestChest.TryInteract();
        }
    }
}

