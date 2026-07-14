/*
 * ============================================================
 *  IHealthProvider  -  血量信息提供接口
 * ============================================================
 *
 * 【功能】
 *   向UI血条暴露血量数据的接口。
 *   由 PlayerMove、NPCGoddess、DemonMinion 实现。
 *
 * 【属性】
 *   CurrentHealth   - 当前血量
 *   MaxHealth       - 最大血量
 *   HealthPercent   - 血量百分比（0~1）
 *   OnHealthChanged - 血量变化事件
 */
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
