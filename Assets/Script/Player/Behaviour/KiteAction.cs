using UnityEngine;

public class KiteAction : IAction
{
    public string Name => "Kite";
    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (ctx.healthPercent < card.kiteHealthThreshold) return 0f;
        if (ctx.nearbyEnemyCount == 0 && (!ctx.npcExists || ctx.distanceToNPC > card.npcPullRadius)) return 0f;

        float score = card.preferredRange * 0.5f;
        score += card.caution * 0.2f;
        score += card.supportiveness * 0.3f;

        if (ctx.npcExists && ctx.npcAlive && ctx.npcIsWalking)
        {
            float nearNpc = Mathf.Max(0f, 1f - ctx.distanceToNPC / card.npcPullRadius);
            score += nearNpc * 0.3f;
        }

        // Recent damage: selfPreservation drives defensive kite
        if (ctx.timeSinceLastDamaged < 4f && ctx.nearbyEnemyCount > 0)
        {
            float urgency = 1f - ctx.timeSinceLastDamaged / 4f;
            score += urgency * card.selfPreservation * 0.4f;
            score += urgency * card.caution * 0.2f;
        }

        score += card.aggression * (1f - card.caution) * 0.15f;
        if (card.victoryFocus > 0.5f && ctx.npcExists && ctx.npcAlive && ctx.npcIsWalking)
            score += card.victoryFocus * 0.2f;

        return Mathf.Clamp01(score);
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        var player = owner.GetComponent<PlayerMove>();
        var combat = owner.GetComponent<PlayerCombat>();
        if (player == null || combat == null) return;
        var npc = Object.FindFirstObjectByType<NPCGoddess>();
        if (npc == null || npc.IsDead) return;

        Transform demonTarget = null;
        float nearestSqr = card.npcPullRadius * card.npcPullRadius;
        if (ctx.allDemons != null)
        {
            foreach (var e in ctx.allDemons)
            {
                if (e == null || e.IsDead) continue;
                float dx = npc.transform.position.x - e.transform.position.x;
                float dz = npc.transform.position.z - e.transform.position.z;
                float sqr = dx * dx + dz * dz;
                if (sqr < nearestSqr) { nearestSqr = sqr; demonTarget = e.transform; }
            }
        }

        if (demonTarget != null)
        {
            var minion = demonTarget.GetComponent<DemonMinion>();
            if (minion != null) { minion.TakeDamage(1f); minion.RegisterAttacker(player.transform); }
            combat.ClearTarget();
            Vector3 awayDir = (player.transform.position - npc.transform.position).normalized;
            if (awayDir.sqrMagnitude < 0.01f) awayDir = player.transform.forward;
            player.SetMoveDestination(player.transform.position + awayDir * 20f);
        }
    }
}
