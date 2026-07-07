using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时上下文快照 — 每次 tick 构建一次，供所有动作评分使用。
/// 避免每个动作重复扫描场景。
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

    // P1
    public bool player1Exists;
    public bool player1Alive;
    public Transform player1Target;

    // Danger
    public float threatLevel;        // 0~1 综合威胁度

    // Inventory
    public int potionCount;

    // Utility
    public float timeSinceLastHeal;
}
