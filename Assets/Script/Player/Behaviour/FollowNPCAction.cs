using UnityEngine;

public class FollowNPCAction : IAction
{
    public string Name => "FollowNPC";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (!ctx.npcExists || !ctx.npcAlive) return 0f;

        float score = 0f;

        // 1. Base loyalty
        score += card.supportiveness * 0.5f;
        score -= card.independence * 0.1f;

        // 2. Distance urgency
        if (ctx.distanceToNPC > card.followDistance)
        {
            float urgency = Mathf.Min((ctx.distanceToNPC - card.followDistance) / 5f, 1f);
            score += urgency * 0.4f;
        }

        // 3. Protect NPC bonus
        if (ctx.enemiesNearNPC > 0)
        {
            score += Mathf.Min(ctx.enemiesNearNPC * 0.2f, 0.5f);
            if (ctx.npcUnderAttack) score += 0.25f;
        }

        // 4. Escort phase
        if (card.victoryFocus > 0.5f && ctx.npcIsWalking)
            score += card.victoryFocus * 0.25f;

        return Mathf.Clamp01(score);
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        var player = owner.GetComponent<PlayerMove>();
        var combat = owner.GetComponent<PlayerCombat>();
        if (player == null || combat == null) return;
        combat.ClearTarget();

        var npc = Object.FindFirstObjectByType<NPCGoddess>();
        if (npc == null || npc.IsDead) return;

        Vector3 npcPos = npc.transform.position;
        Vector3 npcForward = npc.transform.forward;
        if (npcForward.sqrMagnitude < 0.01f) npcForward = Vector3.forward;

        // --- Phase 1: NPC threatened - intercept AND engage ---
        bool npcThreatened = ctx.npcUnderAttack || (ctx.enemiesNearNPC > 0 && ctx.nearestEnemyToNPC != null);
        if (npcThreatened && ctx.nearestEnemyToNPC != null)
        {
            Vector3 enemyPos = ctx.nearestEnemyToNPC.position;
            float distBetween = ctx.nearestEnemyToNPCDistance;

            if (distBetween < card.aggroRange * 1.5f)
            {
                // Intercept: position between NPC and enemy
                Vector3 dirToEnemy = (enemyPos - npcPos).normalized;
                Vector3 intercept = npcPos + dirToEnemy * (distBetween * 0.6f);
                float sideOffset = (ctx.playerIndex % 2 == 0 ? 1 : -1) * 1.5f;
                Vector3 perp = Vector3.Cross(dirToEnemy, Vector3.up).normalized;
                intercept += perp * sideOffset;
                player.SetMoveDestination(intercept);

                // Engage: only when NPC is actually under attack
                if (ctx.npcUnderAttack)
                {
                    var minion = ctx.nearestEnemyToNPC.GetComponent<DemonMinion>();
                    if (minion != null && !minion.IsDead)
                        combat.TryEngage(ctx.nearestEnemyToNPC);
                }
                return;
            }
        }

        // --- Phase 2: Free roam blended with return-to-NPC ---
        // Urgency: same linear formula as Evaluate()
        float urgency = 0f;
        if (ctx.distanceToNPC > card.followDistance)
            urgency = Mathf.Min((ctx.distanceToNPC - card.followDistance) / 5f, 1f);

        float seed = ctx.playerIndex * 1000f + owner.GetInstanceID();
        float wanderSpeed = 0.15f + card.independence * 0.1f;
        float t = Time.time * wanderSpeed;

        float angle = Mathf.PerlinNoise(seed, t) * 360f;
        float baseRadius = 3f + card.independence * 5f;
        float radiusNoise = Mathf.PerlinNoise(seed + 100f, t * 0.8f);
        float radius = Mathf.Lerp(baseRadius - 2f, baseRadius + 2f, radiusNoise);
        radius = Mathf.Max(2.5f, radius);
        float forwardBias = 1.5f + card.supportiveness * 2.5f;

        Vector3 localOffset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
        Vector3 worldOffset = npc.transform.TransformDirection(localOffset);
        Vector3 wanderDest = npcPos + worldOffset + npcForward * forwardBias;

        // Blend: urgency↑ = more pull toward NPC
        Vector3 returnDest = npcPos;
        float blend = urgency * 0.85f;
        Vector3 dest = Vector3.Lerp(wanderDest, returnDest, blend);

        player.SetMoveDestination(dest);
    }
}
