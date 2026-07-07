/*
 * ============================================================
 *  Health  -  通用血量组件
 * ============================================================
 *
 * 【功能】
 *   提供血量、受伤、治疗、复活、自动回血、战斗状态。
 *   所有有血量的物体（玩家/NPC/怪物）共用此组件。
 *
 * 【挂载对象】
 *   需要血量的游戏对象（通过 RequireComponent 自动添加）
 *
 * 【可调节参数】
 *   maxHealth             - 最大血量
 *   currentHealth         - 当前血量（运行时自动初始化为满血）
 *
 *   enableRegen           - 是否启用自动回血
 *   regenPerSecond        - 每秒回血量
 *   regenDelayAfterDamage - 受伤后延迟多久开始回血
 *   disableRegenInCombat  - 战斗中是否禁用回血
 *   combatTimeout         - 受伤后维持战斗状态的时间
 *
 * 【外部调用】
 *   TakeDamage(amount)     - 受伤（负数=治疗）
 *   Heal(amount)           - 治疗
 *   Revive(healthPercent)  - 复活（指定血量百分比）
 *   SetMaxHealth(newMax)   - 动态修改最大血量
 *   ResetToFull()          - 回满血
 *   SetInCombat(bool)      - 设置战斗状态
 *
 * 【事件】
 *   onHealthChanged(百分比)  - 血量变化时触发
 *   onDeath                 - 死亡时触发
 *   onRevive                - 复活时触发
 */
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

    /// <summary>Seconds since last damage taken. Resets to 0 on each TakeDamage call.</summary>
    public float TimeSinceLastDamage { get; private set; }
    /// <summary>Optional reference to the entity that last dealt damage to us.</summary>
    public GameObject LastAttacker { get; set; }

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
        TimeSinceLastDamage += Time.deltaTime;

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
        TimeSinceLastDamage = 0f;
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

