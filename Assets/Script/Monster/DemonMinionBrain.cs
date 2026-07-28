using UnityEngine;

/// <summary>
/// Decision module for DemonMinion.
/// Determines the best target each tick based on revenge, NPC pursuit, or player proximity.
/// Writes decisions to DemonMinion.SetTarget() / ClearTarget().
/// </summary>
[RequireComponent(typeof(DemonMinion))]
public class DemonMinionBrain : MonoBehaviour
{
    private DemonMinion demon;

    private void Awake()
    {
        demon = GetComponent<DemonMinion>();
    }

    /// <summary>Evaluate all potential targets and set the best one on DemonMinion.</summary>
    public void DetermineTarget()
    {
        NPCGoddess npc = demon.TargetNPC;

        // If NPC has arrived, monster loses its attack target → auto patrol, no new targets
        if (npc != null && npc.HasArrived)
        {
            demon.ClearTarget();
            demon.ClearAttacker();
            return;
        }

        // == First priority: revenge the attacker ==
        Transform attacker = demon.AttackerTransform;
        if (attacker != null)
        {
            var attackerDamageable = attacker.GetComponent<IDamageable>();
            var attackerPlayerHealth = attackerDamageable == null ? attacker.GetComponent<PlayerHealth>() : null;
            if ((attackerDamageable != null && attackerDamageable.IsDead) ||
                (attackerPlayerHealth != null && attackerPlayerHealth.CurrentHealth <= 0f))
            {
                demon.ClearTarget();
                demon.ClearAttacker();
            }
            else if (demon.transform.position.SqrDistanceXZ(attacker.position) > demon.SqDisengageDistance)
            {
                demon.ClearTarget();
                demon.ClearAttacker();
            }
            else
            {
                demon.SetTarget(attacker);
                return;
            }
        }

        // == First priority: chase NPC ==
        if (npc != null && !npc.IsDead && !npc.HasArrived)
        {
            demon.SetTarget(npc.transform);
            return;
        }

        // == Second priority: scan nearest player within aggro range ==
        float sqrAggro = demon.SqAggroRange;
        PlayerMove nearest = null;
        float nearestSqrDist = float.MaxValue;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null) continue;
            var pHealth = p.GetComponent<PlayerHealth>();
            if (pHealth == null || pHealth.CurrentHealth <= 0f) continue;
            float d = demon.transform.position.SqrDistanceXZ(p.transform.position);
            if (d < nearestSqrDist && d <= sqrAggro)
            {
                nearestSqrDist = d;
                nearest = p;
            }
        }
        if (nearest != null)
        {
            demon.SetTarget(nearest.transform);
            return;
        }

        // No valid target
        demon.ClearTarget();
    }
}
