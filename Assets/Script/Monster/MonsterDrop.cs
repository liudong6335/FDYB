using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物掉落组件，挂在 DemonMinion 预制体上。
/// 配置 DropEntry[]（itemDef + weight），怪物死亡时掉落一件物品。
/// 掉落物先落地停留，等怪物尸体消失后再飞向击杀者（无击杀者时飞向最近玩家）。
/// </summary>
public class MonsterDrop : MonoBehaviour
{
    [Header("Drop Table")]
    [Tooltip("掉落表条目（itemDef + weight），由 LootBudgetManager 统一抽取")]
    [SerializeField] private List<DropEntry> dropEntries = new List<DropEntry>();

    [Header("Drop Behavior")]
    [Tooltip("无尸体隐藏事件时的兜底地面停留时间（秒）")]
    [SerializeField] private float dropGroundDelay = 0.4f;

    // ========================================
    // Public API
    // ========================================

    /// <summary>
    /// 怪物死亡时调用，生成掉落物并飞向击杀者。
    /// killerTransform 为 null 时回退到最近玩家。
    /// </summary>
    public void SpawnLoot(Transform killerTransform)
    {
        PlayerMove target = ResolveTarget(killerTransform);
        if (target == null)
        {
            Debug.Log("[MonsterDrop] 无有效目标玩家，不生成掉落");
            return;
        }

        // 从全局预算中抽取一件物品（预算耗尽或表为空时返回 null）
        GameItem item = LootBudgetManager.Instance != null
            ? LootBudgetManager.Instance.PickOne(dropEntries)
            : null;
        if (item == null)
            return;

        // 有 3D 模型：生成场景掉落物
        if (item.definition != null && item.definition.dropPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
            GameObject go = Instantiate(item.definition.dropPrefab, spawnPos, Random.rotation);
            DroppedItem dropped = go.GetComponent<DroppedItem>();
            if (dropped == null) dropped = go.AddComponent<DroppedItem>();

            // 等怪物尸体消失后再飞向玩家，让玩家先看到掉落物
            var lifecycle = GetComponent<DemonMinionLifecycle>();
            if (lifecycle != null)
            {
                dropped.Launch(item, target, float.MaxValue); // 先停留在地面

                System.Action onBodyHidden = null;
                onBodyHidden = () =>
                {
                    lifecycle.OnBodyHidden -= onBodyHidden;
                    if (dropped != null)
                        dropped.BeginFlight(target);
                };
                lifecycle.OnBodyHidden += onBodyHidden;
            }
            else
            {
                // 兜底：没有生命周期组件时按固定延迟飞行
                dropped.Launch(item, target, dropGroundDelay);
            }
        }
        // 无 3D 模型：直接进背包
        else
        {
            var inv = InventoryManager.Instance;
            if (inv != null)
                inv.AddItem(item, target);
        }
    }

    // ========================================
    // Target Resolution
    // ========================================

    /// <summary>击杀者优先；击杀者无效时回退到最近的非死亡玩家。</summary>
    private PlayerMove ResolveTarget(Transform killerTransform)
    {
        if (killerTransform != null)
        {
            var killerPlayer = killerTransform.GetComponent<PlayerMove>();
            if (killerPlayer != null && !killerPlayer.IsDead)
                return killerPlayer;
        }

        PlayerMove nearest = null;
        float nearestSqr = float.MaxValue;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null || p.IsDead) continue;
            float sqr = transform.position.SqrDistanceXZ(p.transform.position);
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = p;
            }
        }
        return nearest;
    }
}
