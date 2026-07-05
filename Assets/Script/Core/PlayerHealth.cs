using UnityEngine;

[RequireComponent(typeof(Health))]
public class PlayerHealth : MonoBehaviour, IHealthProvider
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 1000f;
    [SerializeField] private float baseDamage = 100f;

    [Header("Attack Range")]
    [SerializeField] private float attackRange = 8f;

    [SerializeField] private string hitParam = "Hit";
    [SerializeField] private string deathParam = "Death";
    private Animator animator;

    private Health health;

    public float MaxHealth { get { return health != null ? health.MaxHealth : maxHealth; } }
    public float CurrentHealth { get { return health != null ? health.CurrentHealth : 0f; } }
    public float HealthPercent { get { return health != null ? health.HealthPercent : 1f; } }
    public float BaseDamage { get { return baseDamage; } set { baseDamage = value; } }
    public float DamageMultiplier { get; set; } = 1f;
    public float MaxHealthBonus { get; set; }
    public float EffectiveMaxHealth { get { return maxHealth + MaxHealthBonus; } }
    public float EffectiveDamage { get { return baseDamage * DamageMultiplier; } }
    public float EffectiveAttackRange { get { return attackRange; } }

    public event System.Action<float> OnHealthChanged;
    public System.Action OnDeath;

    private void Awake()
    {
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();
        if (health == null) health = gameObject.AddComponent<Health>();
        health.SetMaxHealth(maxHealth);
        health.ResetToFull();
        health.onDeath.AddListener(OnHealthDepleted);
    }

    private void OnHealthDepleted()
    {
        if (animator != null) { animator.ResetTrigger(deathParam); animator.SetTrigger(deathParam); }
        OnDeath?.Invoke();
    }

    public void TakeDamage(float dmg)
    {
        if (animator != null) { animator.ResetTrigger(hitParam); animator.SetTrigger(hitParam); }
        if (health != null) health.TakeDamage(dmg);
    }

    public void RefreshStats()
    {
        if (health == null) return;
        float ratio = health.HealthPercent;
        health.SetMaxHealth(EffectiveMaxHealth);
        health.Heal(EffectiveMaxHealth * ratio - health.CurrentHealth);
        OnHealthChanged?.Invoke(HealthPercent);
    }
}
