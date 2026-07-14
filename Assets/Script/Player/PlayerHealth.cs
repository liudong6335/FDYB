/*
 * ============================================================
 *  PlayerHealth  -  玩家血量/属性
 * ============================================================
 *
 * 【功能】
 *   管理玩家的血量、攻击力、攻击范围等战斗属性。
 *   支持装备加成（通过 InventoryManager 动态刷新）。
 *
 * 【挂载对象】
 *   玩家对象（与 PlayerMove 在同一对象）
 *
 * 【可调节参数】
 *   maxHealth   - 基础最大血量
 *   baseDamage  - 基础攻击力
 *   attackRange - 攻击范围
 *
 * 【外部调用】
 *   TakeDamage(dmg)     - 受伤
 *   RefreshStats()      - 刷新属性（装备变化后调用）
 *
 * 【属性】
 *   EffectiveMaxHealth  - 实际最大血量（基础+装备加成）
 *   EffectiveDamage     - 实际攻击力（基础 × 倍率）
 *   EffectiveAttackRange- 实际攻击范围
 *   DamageMultiplier    - 伤害倍率（装备可修改）
 *   MaxHealthBonus      - 血量加成（装备可修改）
 */
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
    private int hitParamHash;
    private int deathParamHash;
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
        hitParamHash = Animator.StringToHash(hitParam);
        deathParamHash = Animator.StringToHash(deathParam);
        health.onDeath.AddListener(OnHealthDepleted);
    }

    private void OnHealthDepleted()
    {
        if (animator != null) { animator.ResetTrigger(deathParamHash); animator.SetTrigger(deathParamHash); }
        OnDeath?.Invoke();
    }

    public void TakeDamage(float dmg)
    {
        if (dmg > 0 && animator != null) { animator.ResetTrigger(hitParamHash); animator.SetTrigger(hitParamHash); }
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

