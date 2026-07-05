using UnityEngine;

/// <summary>
/// Interface for any entity that can take damage and die.
/// Implemented by DemonMinion, NPCGoddess, and PlayerMove.
/// Replaces direct type dependencies (PlayerMove / NPCGoddess) in damage dispatch.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float amount);
    bool IsDead { get; }
}
