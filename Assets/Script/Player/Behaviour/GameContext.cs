using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime context snapshot built once per tick, shared across all actions.
/// </summary>
public struct GameContext
{
    // Player
    public float healthPercent;
    public float healthAbsolute;
    public Vector3 position;

    // Enemies
    public int nearbyEnemyCount;
    public float totalEnemyHealth;
    public float nearestEnemyDistance;
    public List<DemonMinion> allDemons;

    // Short-term memory
    public float timeSinceLastDamaged;
    public GameObject lastAttacker;

    // Group perception
    public int nearbyAllyCount;

    // Targeting
    public Transform primaryTarget;
    public float distanceToTarget;
    public float attackRange;

    // Allies
    public bool npcExists;
    public bool npcAlive;
    public bool npcIsWalking;
    public float distanceToNPC;

    // NPC protection threat
    public int enemiesNearNPC;
    public Transform nearestEnemyToNPC;
    public float nearestEnemyToNPCDistance;
    public bool npcUnderAttack;
    public int playerIndex;

    // P1
    public bool player1Exists;
    public bool player1Alive;
    public Transform player1Target;

    // Danger
    public float threatLevel;

    // Inventory
    public int potionCount;

    // Utility
    public float timeSinceLastHeal;

    // NPC support actions
    public float lowestTeamHealthPercent;
    public bool lowestTeamMemberIsPlayer;
    public bool npcHealReady;
    public bool npcCanAttack;
}
