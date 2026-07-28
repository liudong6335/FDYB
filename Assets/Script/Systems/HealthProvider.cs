/* 
 * ============================================================
 *  HealthProvider  -  通用生命值/进度数据提供组件
 * ============================================================
 *
 * 【功能】
 *   实现 IHealthProvider 接口的 MonoBehaviour 组件。
 *   可以挂到任何需要 UGUIFloatingHealthBar 显示血条/进度条的对象上。
 *   通过 SetProgress() 方法可由外部更新进度。
 *
 * 【挂载对象】
 *   需要显示血条/进度条的 GameObject（宝箱、机关、NPC 等）
 *
 * 【外部调用】
 *   SetProgress(0~1)    - 设置进度（0=空，1=满）
 *   SetMaxHealth(val)   - 设置最大值
 *   CurrentHealth       - 获取当前值
 *   HealthPercent       - 获取当前百分比
 *
 * 【参数】
 *   maxHealth     - 最大值（默认 100）
 *   initialPercent - 初始百分比（默认 1.0 = 满）
 *   autoHideAtFull - 满值时是否自动隐藏（默认 false）
 */
using UnityEngine;

public class HealthProvider : MonoBehaviour, IHealthProvider
{
    [Header("Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField, Range(0f, 1f)] private float initialPercent = 1f;
    [SerializeField] private bool autoHideAtFull;
    public bool AutoHideAtFull { get { return autoHideAtFull; } set { autoHideAtFull = value; } }

    private float currentHealth;

    // ========================================
    // IHealthProvider Implementation
    // ========================================

    public float CurrentHealth { get { return currentHealth; } }
    public float MaxHealth { get { return maxHealth; } }
    public float HealthPercent { get { return maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f; } }
    public event System.Action<float> OnHealthChanged;

    // ========================================
    // Unity Lifecycle
    // ========================================

    private void Awake()
    {
        currentHealth = maxHealth * Mathf.Clamp01(initialPercent);
    }

    // ========================================
    // Public Methods
    // ========================================

    /// <summary>Set progress directly (0~1).</summary>
    public void SetProgress(float pct)
    {
        currentHealth = maxHealth * Mathf.Clamp01(pct);
        OnHealthChanged?.Invoke(HealthPercent);
    }

    /// <summary>Set current health value directly.</summary>
    public void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
        OnHealthChanged?.Invoke(HealthPercent);
    }

    /// <summary>Change max health (preserves ratio).</summary>
    public void SetMaxHealth(float newMax)
    {
        float ratio = maxHealth > 0f ? currentHealth / maxHealth : 1f;
        maxHealth = Mathf.Max(1f, newMax);
        currentHealth = maxHealth * Mathf.Clamp01(ratio);
        OnHealthChanged?.Invoke(HealthPercent);
    }

    /// <summary>Reset to full.</summary>
    public void ResetToFull()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(HealthPercent);
    }
}
