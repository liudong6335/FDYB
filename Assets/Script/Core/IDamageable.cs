/*
 * ============================================================
 *  IDamageable  -  受伤接口
 * ============================================================
 *
 * 【功能】
 *   任何可以受伤+死亡的对象都实现此接口。
 *   由 DemonMinion（怪物）、NPCGoddess（NPC）、
 *   PlayerMove（玩家）实现。
 *
 * 【方法】
 *   TakeDamage(amount)  - 造成伤害
 *   IsDead              - 是否已死亡
 */
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

