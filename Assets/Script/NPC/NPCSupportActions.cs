using UnityEngine;

/// <summary>Heal the team member (player or self) with the lowest HP.</summary>
public class NPCTeamHealAction : IAction
{
    public string Name => "Heal";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (!ctx.npcHealReady) return 0f;
        if (ctx.lowestTeamHealthPercent >= 0.95f) return 0f;

        float score = 0f;

        // Lower team HP = more urgent
        if (ctx.lowestTeamHealthPercent < 0.8f)
            score += (0.8f - ctx.lowestTeamHealthPercent) * 6f;

        // Emergency: someone is critically low
        if (ctx.lowestTeamHealthPercent < 0.3f)
            score += 3.5f;

        // Slight bias toward healing players (supportiveness)
        if (ctx.lowestTeamMemberIsPlayer)
            score += 0.3f * card.supportiveness;

        // Self-preservation: heal self if very low
        if (ctx.healthPercent < 0.3f)
            score += 0.5f * card.selfPreservation;

        // Under threat + team hurt = heal
        if (ctx.threatLevel > 0.4f && ctx.lowestTeamHealthPercent < 0.6f)
            score += ctx.threatLevel * 0.4f;

        return score;
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        owner.GetComponent<NPCGoddess>().recommendedAction = "Heal";
    }
}

/// <summary>Attack nearby enemies when the team is relatively safe.</summary>
public class NPCAttackAction : IAction
{
    public string Name => "Attack";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (ctx.nearbyEnemyCount == 0) return 0f;

        float score = 0f;

        // Base aggression
        score += card.aggression * 0.1f;
        score += card.victoryFocus * 0.05f;

        // Close enemies
        if (ctx.nearestEnemyDistance < 8f)
            score += (1f - ctx.nearestEnemyDistance / 8f) * 0.3f;

        // Many enemies = more threat
        if (ctx.nearbyEnemyCount >= 3)
            score += 0.2f * card.supportiveness;

        // Team is healthy, clear to fight
        if (ctx.lowestTeamHealthPercent > 0.6f && ctx.nearbyEnemyCount > 0)
            score += 0.15f * card.victoryFocus;

        // Team needs heal more than attack
        if (ctx.lowestTeamHealthPercent < 0.85f)
            score -= 2.0f;

        return score;
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        owner.GetComponent<NPCGoddess>().recommendedAction = "Attack";
    }
}

/// <summary>Aggressively attack monsters to draw aggro when the team is overwhelmed.</summary>
public class NPCDamageShareAction : IAction
{
    public string Name => "DamageShare";

    public float Evaluate(CharacterCard card, GameContext ctx)
    {
        if (ctx.nearbyEnemyCount == 0) return 0f;

        float score = 0f;

        // Team is under pressure (hurt + outnumbered)
        if (ctx.lowestTeamHealthPercent < 0.8f && ctx.nearbyEnemyCount >= 2)
        {
            float pressure = (0.8f - ctx.lowestTeamHealthPercent) * 1.5f;
            pressure += Mathf.Min(ctx.nearbyEnemyCount / 5f, 1f) * 0.5f;
            score += pressure * card.supportiveness;
        }

        // Enemies very close to the team
        if (ctx.nearestEnemyDistance < 5f)
            score += 0.3f;

        // Don't rush into death
        if (ctx.healthPercent < 0.3f)
            score -= 1.0f * card.selfPreservation;

        return score;
    }

    public void Execute(GameObject owner, GameContext ctx, CharacterCard card)
    {
        owner.GetComponent<NPCGoddess>().recommendedAction = "DamageShare";
    }
}
