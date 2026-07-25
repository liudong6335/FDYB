using UnityEngine;

/// <summary>
/// 閹锋儳褰囩€规繄顔堥崝銊ょ稊 閳?闂勫嫯绻庨張澶嬫弓瀵偓閸氼垳娈戠€规繄顔堥弮鎯拌泲鏉╁洤骞撳鈧粻渚库偓?/// 鐠囧嫬鍨庨悽杈そ婵狀亜瀹抽妴浣告噯閸ュ瓨鏅幆鍛偓浣筋攨闁插繐鍙￠崥宀勨攳閸斻劊鈧?/// </summary>
public class LootAction : IAction
{
    public string Name => "Loot";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        // 閹存ɑ鏋熺槐褑鎻╅弮鏈电瑝閹?        if (ctx.threatLevel > 0.4f) return 0f;
        if (ctx.healthPercent < 0.4f) return 0f;

        // 閹殿偅寮块梽鍕箮鐎规繄顔?
        var chests = TreasureChest.ActiveChests;
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

        // 鐠愵亜鈹嗘す鍗炲З
        float score = card.greed * 0.5f;

        // 鐡掑﹨绻庣搾濠冨厒閹?        score += Mathf.Max(0f, 1f - nearestDist / card.lootRadius) * 0.3f;

        // 鐠嬨劍鍘ч惃鍕眽娴兼碍娲块悩纭呴矋
        if (card.caution > 0.6f) score -= card.caution * 0.15f;

        return Mathf.Clamp01(score);
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        var player = owner.GetComponent<PlayerMove>();
        var combat = owner.GetComponent<PlayerCombat>();
        if (player == null || combat == null) return;
        var chests = TreasureChest.ActiveChests;
        TreasureChest nearestChest = null;
        float nearestSqr = card.lootRadius * card.lootRadius;

        foreach (var c in chests)
        {
            if (c == null || c.IsOpened) continue;
            float sqr = player.transform.position.SqrDistanceXZ(c.transform.position);
            if (sqr < nearestSqr) { nearestSqr = sqr; nearestChest = c; }
        }

        if (nearestChest == null) return;

        // 鐠ф澘鍩岀€规繄顔堥弮浣界珶
        if (nearestSqr > 2.5f * 2.5f)
        {
            combat.ClearTarget();
            player.SetMoveDestination(nearestChest.transform.position);
        }
        else
        {
            // 婢剁喕绻庢禍鍡礉娴溿倓绨板鈧粻?            nearestChest.TryInteract();
        }
    }
}



