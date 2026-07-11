using UnityEngine;

/// <summary>
/// Standalone damage slow component.
/// Attach to any GameObject with a Health component.
/// Automatically slows movement speed for a duration after taking damage.
/// </summary>
[RequireComponent(typeof(Health))]
public class DamageSlowEffect : MonoBehaviour
{
    [Header("Slow Settings")]
    [SerializeField] private float slowMultiplier = 0.5f;
    [SerializeField] private float slowDuration = 2f;

    private Health health;
    private float slowTimer;
    private float prevTimeSinceLastDamage;

    /// <summary>Multiply movement speed by this value. 1 = no slow.</summary>
    public float SpeedMultiplier { get { return slowTimer > 0f ? slowMultiplier : 1f; } }

    /// <summary>True if currently slowed.</summary>
    public bool IsSlowed { get { return slowTimer > 0f; } }

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void Start()
    {
        prevTimeSinceLastDamage = health.TimeSinceLastDamage;
    }

    private void Update()
    {
        // Detect damage by checking if TimeSinceLastDamage was reset
        if (health.TimeSinceLastDamage < prevTimeSinceLastDamage - 0.001f)
            slowTimer = slowDuration;

        prevTimeSinceLastDamage = health.TimeSinceLastDamage;

        if (slowTimer > 0f)
            slowTimer -= Time.deltaTime;
    }
}
