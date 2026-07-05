using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Regeneration")]
    [SerializeField] private bool enableRegen = true;
    [SerializeField] private float regenPerSecond = 1f;
    [SerializeField] private float regenDelayAfterDamage = 3f;
    [SerializeField] private bool disableRegenInCombat = true;
    [SerializeField] private float combatTimeout = 5f;

    public float RegenPerSecond { get { return regenPerSecond; } set { regenPerSecond = Mathf.Max(0f, value); } }
    public bool EnableRegen { get { return enableRegen; } set { enableRegen = value; } }
    public bool DisableRegenInCombat { get { return disableRegenInCombat; } set { disableRegenInCombat = value; } }
    public float CombatTimeout { get { return combatTimeout; } set { combatTimeout = Mathf.Max(0f, value); } }
    public float RegenDelayAfterDamage { get { return regenDelayAfterDamage; } set { regenDelayAfterDamage = Mathf.Max(0f, value); } }

    [Header("Events")]
    public UnityEvent<float> onHealthChanged;
    public UnityEvent onDeath;
    public UnityEvent onRevive;

    private bool isDead;
    private bool inCombat;
    private float combatTimer;
    private float regenTimer;

    public float MaxHealth { get { return maxHealth; } }
    public float CurrentHealth { get { return currentHealth; } }
    public float HealthPercent { get { return currentHealth / maxHealth; } }
    public bool IsDead { get { return isDead; } }
    public bool IsFullHealth { get { return currentHealth >= maxHealth; } }

    public bool InCombat { get { return inCombat; } }
    private void Awake()
    {
        if (currentHealth <= 0f) currentHealth = maxHealth;
    }

    private void Update()
    {
        if (isDead || !enableRegen || currentHealth >= maxHealth) return;

        if (inCombat) { combatTimer -= Time.deltaTime; if (combatTimer <= 0f) inCombat = false; }
        if (disableRegenInCombat && inCombat) return;
        regenTimer -= Time.deltaTime;
        if (regenTimer <= 0f)
        {
            Heal(regenPerSecond * Time.deltaTime);
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount == 0f) return;
        // Negative damage = healing (used by consumables)
        if (amount < 0f) { Heal(-amount); return; }
        currentHealth -= amount;
        regenTimer = regenDelayAfterDamage; // reset regen delay
        SetInCombat(true);

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            isDead = true;
            onDeath?.Invoke();
        }

        onHealthChanged?.Invoke(HealthPercent);
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        onHealthChanged?.Invoke(HealthPercent);
    }

    public void Revive(float healthPercent = 1f)
    {
        if (!isDead) return;
        isDead = false;
        currentHealth = maxHealth * Mathf.Clamp01(healthPercent);
        regenTimer = 0f;
        onRevive?.Invoke();
        onHealthChanged?.Invoke(HealthPercent);
    }

    public void SetMaxHealth(float newMax)
    {
        maxHealth = Mathf.Max(1f, newMax);
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        onHealthChanged?.Invoke(HealthPercent);
    }

    public void ResetToFull()
    {
        isDead = false;
        currentHealth = maxHealth;
        regenTimer = 0f;
        onHealthChanged?.Invoke(HealthPercent);
    }

    public void SetInCombat(bool value)
    {
        inCombat = value;
        if (value) combatTimer = combatTimeout;
    }
}
