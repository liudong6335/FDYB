using UnityEngine;

/// <summary>
/// Interface for any entity that exposes health to UI bars.
/// Implemented by PlayerMove, NPCGoddess, and DemonMinion.
/// </summary>
public interface IHealthProvider
{
    float CurrentHealth { get; }
    float MaxHealth { get; }
    float HealthPercent { get; }
    event System.Action<float> OnHealthChanged;
}