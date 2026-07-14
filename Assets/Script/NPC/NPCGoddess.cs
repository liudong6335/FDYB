using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
public class NPCGoddess : MonoBehaviour, IHealthProvider, IDamageable
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 600f;
    [SerializeField] private float healthRegen = 1f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
        [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string moveSpeedParam = "MoveSpeed";
    [SerializeField] private string healParam = "Heal";
    [SerializeField] private string deathParam = "Death";
    [SerializeField] private string hitParam = "Hit";
    private int isMovingHash;
    private int moveSpeedHash;
    private int healHash;
    private int deathHash;
    private int hitHash;

    // Core components
    private Health health;
    private NPCGoddessMovement movement;
    private NPCGoddessHealing healing;
    private NPCGoddessBehavior behavior;

    // ========================================
    // Public Properties
    // ========================================
    public float CurrentHealth { get { return health != null ? health.CurrentHealth : 0f; } }
    public float MaxHealth { get { return health != null ? health.MaxHealth : maxHealth; } }
    public float HealthPercent { get { return health != null ? health.HealthPercent : 1f; } }
    public bool IsDead { get { return health != null && health.IsDead; } }
    public bool HasArrived { get { return movement != null && movement.HasArrived; } }
    public bool IsHealing { get { return healing != null && healing.IsHealing; } }
    public bool IsWalking { get { return movement != null && movement.IsWalking; } }
    public WaypointPath WaypointPath { get { return movement != null ? movement.WaypointPath : null; } }
    public int CurrentWaypointIndex { get { return movement != null ? movement.CurrentWaypointIndex : 0; } }

    // ========================================
    // Public Events
    // ========================================
    public event System.Action<float> OnHealthChanged;
    public System.Action OnDeath;
    public System.Action OnHealStart;
    public System.Action OnHealComplete;
    public System.Action OnArrived;

    // ========================================
    // Unity Lifecycle
    // ========================================

    private void Awake()
    {
        health = GetComponent<Health>();
        health.SetMaxHealth(maxHealth);
        health.ResetToFull();
        health.RegenPerSecond = healthRegen;
        health.DisableRegenInCombat = false;
        health.RegenDelayAfterDamage = 0f;
        health.EnableRegen = true;
        health.onDeath.AddListener(OnHealthDepleted);
        health.onHealthChanged.AddListener(pct => OnHealthChanged?.Invoke(pct));
        isMovingHash = Animator.StringToHash(isMovingParam);
        moveSpeedHash = Animator.StringToHash(moveSpeedParam);
        healHash = Animator.StringToHash(healParam);
        deathHash = Animator.StringToHash(deathParam);
        hitHash = Animator.StringToHash(hitParam);
        if (animator == null) animator = GetComponent<Animator>();

        movement = GetComponent<NPCGoddessMovement>();
        healing = GetComponent<NPCGoddessHealing>();
        behavior = GetComponent<NPCGoddessBehavior>();

        // Forward sub-component events
        if (healing != null)
        {
            healing.OnHealStart += () => OnHealStart?.Invoke();
            healing.OnHealComplete += () => OnHealComplete?.Invoke();
        }
        if (movement != null)
        {
            movement.OnArrived += () => OnArrived?.Invoke();
        }
    }

    private void Update()
    {
        // Subsystems update in order
        if (healing != null) healing.UpdateHealing();
        if (behavior != null) behavior.UpdateBehavior();
        if (movement != null) movement.UpdateMovement(
            IsHealing,
            behavior != null ? behavior.IsEvading : false,
            behavior != null ? behavior.IsRescuing : false,
            behavior != null ? behavior.IsWaiting : false,
            behavior != null ? behavior.NearestThreat : null,
            behavior != null ? behavior.EvadeSpeedMultiplier : 1.2f,
            behavior != null ? behavior.EvadeStrength : 0.5f,
            behavior != null ? behavior.RescueDetourOffset : 5f
        );

        UpdateAnimation();
    }

    // ========================================
    // Public Combat API
    // ========================================

    public void Heal(float amount)
    {
        health.Heal(amount);
    }

    public void TakeDamage(float dmg)
    {
        if (IsDead || HasArrived) return;
        health.TakeDamage(dmg);
        AnimSetTrigger(hitHash);
    }

    public bool RequestHeal(Vector3 playerPos)
    {
        if (healing == null) return false;
        return healing.RequestHeal(playerPos);
    }

    void IDamageable.TakeDamage(float dmg) { TakeDamage(dmg); }

    // ========================================
    // Public Movement API
    // ========================================

    public void StartWalking()
    {
        if (movement != null && !HasArrived && !IsDead)
            movement.IsWalking = true;
    }

    public void StopWalking()
    {
        if (movement != null) movement.StopWalking();
    }

    public float EstimateJourneyTime()
    {
        if (movement == null) return -1f;
        return movement.EstimateJourneyTime();
    }

    /// <summary>Called by NPCGoddessMovement when it arrives at the final waypoint.</summary>
    public void NotifyArrived()
    {
        // Used by NPCGoddessMovement during TryArrive; animation will update via UpdateAnimation
    }

    // ========================================
    // Animation
    // ========================================

    public void AnimSetTrigger(string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return;
        foreach (var p in animator.parameters)
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Trigger)
            { animator.SetTrigger(paramName); return; }
    }

    public void AnimSetTrigger(int paramHash)
    {
        if (animator == null) return;
        animator.SetTrigger(paramHash);
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;
        bool moving = movement != null && movement.MovingForAnim;
        animator.SetBool(isMovingHash, moving);
        animator.SetFloat(moveSpeedHash, moving ? movement.CurrentMoveSpeed : 0f);
    }

    // ========================================
    // Death
    // ========================================

    private void OnHealthDepleted()
    {
        if (movement != null) movement.StopWalking();
        OnDeath?.Invoke();
        AnimSetTrigger(deathHash);
    }
}