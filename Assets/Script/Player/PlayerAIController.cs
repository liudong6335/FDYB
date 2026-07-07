using UnityEngine;

/// <summary>
/// Semi-automatic AI controller for the secondary player (P2).
/// HP > threshold: Kite mode - aggro demons near NPC and pull them away.
/// HP <= threshold: Kill mode - normal combat + follow NPC.
/// HP < 0.5: Use health potions when available.
/// HP < 0.3 + under attack: Smart flee with kill-before-run logic.
/// </summary>
[RequireComponent(typeof(PlayerMove))]
public class PlayerAIController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float tickInterval = 0.3f;
    [SerializeField] private float followDistance = 4f;
    [SerializeField] private float aggroRange = 15f;

    [Header("Kite Settings")]
    [SerializeField] private float kiteHealthThreshold = 0.6f;
    [SerializeField] private float npcAggroRange = 12f;
    [SerializeField] private float pullStartDistance = 15f;
    [SerializeField] private float returnDistance = 25f;

    [Header("Flee Settings")]
    [SerializeField] private float fleeHealthThreshold = 0.3f;
    [SerializeField] private int fleeEnemyCount = 1;
    [SerializeField] private float fleeKillAndRunMultiplier = 1.5f;
    [SerializeField] private float fleeInstaRunMultiplier = 2.0f;
    [SerializeField] private float potionHealthThreshold = 0.5f;
    [SerializeField] private float potionCooldown = 10f;

    private PlayerMove aiPlayer;
    private PlayerCombat aiCombat;
    private PlayerHealth aiHealth;
    private PlayerMove player1;
    private NPCGoddess npc;
    private float nextTickTime;
    private float nextPotionTime;

    private void Awake()
    {
        aiPlayer = GetComponent<PlayerMove>();
        aiCombat = GetComponent<PlayerCombat>();
        aiHealth = GetComponent<PlayerHealth>();
        aiPlayer.SetPlayerAI(true);
    }

    private void Start()
    {
        var allPlayers = PlayerMove.AllPlayers;
        foreach (var p in allPlayers)
        {
            if (p != aiPlayer) { player1 = p; break; }
        }
        npc = FindFirstObjectByType<NPCGoddess>();
    }

    private void Update()
    {
        if (aiPlayer == null || aiPlayer.IsDead) return;
        if (Time.time < nextTickTime) return;
        nextTickTime = Time.time + tickInterval;

        TryUseHealthPotion();

        // Flee evaluation returns true if we fled
        if (TryFleeIfNeeded()) return;

        // If flee decision set a combat target (kill before run), let PlayerMove auto-combat handle it
        if (aiCombat.AttackTarget != null) return;

        // Normal behavior
        if (aiPlayer.HealthPercent > kiteHealthThreshold)
            UpdateKite();
        else
            UpdateCombat();
    }

    private void TryUseHealthPotion()
    {
        if (Time.time < nextPotionTime) return;
        if (aiPlayer.HealthPercent >= potionHealthThreshold) return;

        var inv = InventoryManager.Instance;
        if (inv == null) return;

        inv.UseConsumable("health_potion_1", aiPlayer);
        nextPotionTime = Time.time + potionCooldown;
    }

    /// <summary>
    /// Evaluate flee situation with kill-before-run logic.
    /// Returns true if AI fled (caller should return from Update).
    /// Returns false but may set a combat target (kill before fleeing).
    /// </summary>
    private bool TryFleeIfNeeded()
    {
        if (aiPlayer.HealthPercent >= fleeHealthThreshold) return false;
        if (CountNearbyEnemies() < fleeEnemyCount) return false;

        float totalHP = CalculateTotalEnemyHP();
        float aiHP = aiPlayer.CurrentHealth;
        float oneShot = aiHealth.EffectiveDamage;

        if (totalHP > aiHP * fleeInstaRunMultiplier)
        {
            // Danger too high - flee immediately
            FleeToNPC();
            return true;
        }

        if (totalHP > aiHP * fleeKillAndRunMultiplier)
        {
            // Risky - try to one-shot one enemy before fleeing
            if (!TryAttackOneShotEnemy(oneShot))
            {
                FleeToNPC();
                return true;
            }
            return false; // target set, combat will handle kill, next tick will flee
        }

        // Manageable - kill the weakest enemy first
        TryAttackWeakestEnemy();
        return false; // target set, combat handles kill, next tick evaluates again
    }

    private int CountNearbyEnemies()
    {
        float sqrRange = aggroRange * aggroRange;
        int count = 0;
        foreach (var e in DemonMinion.AllDemons)
        {
            if (e == null || e.IsDead) continue;
            float dx = transform.position.x - e.transform.position.x;
            float dz = transform.position.z - e.transform.position.z;
            if (dx * dx + dz * dz < sqrRange)
                count++;
        }
        return count;
    }

    private float CalculateTotalEnemyHP()
    {
        float total = 0f;
        float sqrRange = aggroRange * aggroRange;
        foreach (var e in DemonMinion.AllDemons)
        {
            if (e == null || e.IsDead) continue;
            float dx = transform.position.x - e.transform.position.x;
            float dz = transform.position.z - e.transform.position.z;
            if (dx * dx + dz * dz < sqrRange)
                total += e.CurrentHealth;
        }
        return total;
    }

    private bool TryAttackOneShotEnemy(float damage)
    {
        float sqrRange = aggroRange * aggroRange;
        foreach (var e in DemonMinion.AllDemons)
        {
            if (e == null || e.IsDead) continue;
            float dx = transform.position.x - e.transform.position.x;
            float dz = transform.position.z - e.transform.position.z;
            if (dx * dx + dz * dz < sqrRange && e.CurrentHealth <= damage)
            {
                aiCombat.TryEngage(e.transform);
                return true;
            }
        }
        return false;
    }

    private void TryAttackWeakestEnemy()
    {
        float sqrRange = aggroRange * aggroRange;
        float lowestHP = float.MaxValue;
        Transform weakest = null;
        foreach (var e in DemonMinion.AllDemons)
        {
            if (e == null || e.IsDead) continue;
            float dx = transform.position.x - e.transform.position.x;
            float dz = transform.position.z - e.transform.position.z;
            if (dx * dx + dz * dz < sqrRange && e.CurrentHealth < lowestHP)
            {
                lowestHP = e.CurrentHealth;
                weakest = e.transform;
            }
        }
        if (weakest != null)
            aiCombat.TryEngage(weakest);
    }

    private void FleeToNPC()
    {
        if (npc != null && !npc.IsDead)
        {
            aiCombat.ClearTarget();
            aiPlayer.SetMoveDestination(npc.transform.position);
        }
        else
        {
            UpdateCombat();
        }
    }

    private void UpdateKite()
    {
        if (npc == null) return;

        float distToNPC = Vector3.Distance(transform.position, npc.transform.position);

        if (distToNPC > returnDistance)
        {
            aiCombat.ClearTarget();
            aiPlayer.SetMoveDestination(npc.transform.position);
            return;
        }

        Transform demon = FindDemonNearNPC();

        if (demon != null && distToNPC < pullStartDistance)
        {
            var minion = demon.GetComponent<DemonMinion>();
            if (minion != null && !minion.IsDead)
            {
                minion.TakeDamage(1f);
                minion.RegisterAttacker(transform);
            }

            aiCombat.ClearTarget();
            Vector3 awayDir = (transform.position - npc.transform.position).normalized;
            if (awayDir.sqrMagnitude < 0.01f) awayDir = transform.forward;
            aiPlayer.SetMoveDestination(transform.position + awayDir * 20f);
        }
        else
        {
            FollowNPC();
        }
    }

    private void UpdateCombat()
    {
        if (TryCopyPlayer1Target()) return;
        if (TryAttackNearbyEnemy()) return;
        FollowNPC();
    }

    private Transform FindDemonNearNPC()
    {
        if (npc == null) return null;

        float npcSqr = npcAggroRange * npcAggroRange;
        float nearestSqr = npcSqr;
        Transform nearest = null;

        foreach (var e in DemonMinion.AllDemons)
        {
            if (e == null || e.IsDead) continue;

            float dx = npc.transform.position.x - e.transform.position.x;
            float dz = npc.transform.position.z - e.transform.position.z;
            float sqrDist = dx * dx + dz * dz;

            if (sqrDist < nearestSqr)
            {
                nearestSqr = sqrDist;
                nearest = e.transform;
            }
        }

        return nearest;
    }

    private bool TryCopyPlayer1Target()
    {
        if (player1 == null) return false;
        var p1Combat = player1.GetComponent<PlayerCombat>();
        if (p1Combat == null || p1Combat.AttackTarget == null) return false;
        var damageable = p1Combat.AttackTarget.GetComponent<IDamageable>();
        if (damageable == null || damageable.IsDead) return false;
        aiCombat.TryEngage(p1Combat.AttackTarget);
        return true;
    }

    private bool TryAttackNearbyEnemy()
    {
        Transform nearestTarget = null;
        float nearestSqr = aggroRange * aggroRange;
        foreach (var e in DemonMinion.AllDemons)
        {
            if (e == null || e.IsDead) continue;
            float dx = transform.position.x - e.transform.position.x;
            float dz = transform.position.z - e.transform.position.z;
            float sqrDist = dx * dx + dz * dz;
            if (sqrDist < nearestSqr)
            {
                nearestSqr = sqrDist;
                nearestTarget = e.transform;
            }
        }
        if (nearestTarget != null)
        {
            aiCombat.TryEngage(nearestTarget);
            return true;
        }
        return false;
    }

    private void FollowNPC()
    {
        if (npc == null || npc.IsDead || npc.HasArrived) return;
        float dx = transform.position.x - npc.transform.position.x;
        float dz = transform.position.z - npc.transform.position.z;
        float sqrDist = dx * dx + dz * dz;
        if (sqrDist > followDistance * followDistance * 2f)
        {
            Vector3 targetPos = npc.transform.position + new Vector3(1.5f, 0f, 1.5f);
            aiCombat.ClearTarget();
            aiPlayer.SetMoveDestination(targetPos);
        }
        else if (sqrDist > followDistance * followDistance)
        {
            aiCombat.ClearTarget();
            aiPlayer.SetMoveDestination(npc.transform.position);
        }
        else
        {
            aiCombat.ClearTarget();
            aiPlayer.StopMoving();
        }
    }
}

