using UnityEngine;

/// <summary>
/// 掉落物的飞行/地面状态阶段。
/// </summary>
public enum DropItemPhase
{
    Ground,   // 在地面弹跳等待
    Flying,   // 飞向玩家
    Done      // 已完成
}

/// <summary>
/// 场景掉落物组件，挂在 dropPrefab 上。
/// 管理从生成到进入背包的完整生命周期。
/// 支持自动飞向玩家（宝箱/怪物）和地面拾取（背包满回落）。
/// </summary>
public class DroppedItem : MonoBehaviour
{
    private GameItem itemData;
    private PlayerMove flyTarget;
    private float groundDelay;
    private Vector3 spawnPos;
    private float timer;
    private DropItemPhase phase;
    private bool isPickupLocked;

    [SerializeField, Tooltip("玩家靠近到该距离自动拾取（地面模式）")]
    private float pickupRange = 4f;

    private const float FlyDuration = 0.6f;

    // ========================================
    // Public API
    // ========================================

    public void Launch(GameItem item, PlayerMove target, float groundDelay = 0f)
    {
        itemData = item;
        flyTarget = target;
        spawnPos = transform.position;
        this.groundDelay = groundDelay;
        timer = 0f;
        isPickupLocked = false;

        phase = groundDelay > 0f ? DropItemPhase.Ground : DropItemPhase.Flying;
    }

    /// <summary>外部触发：从地面阶段切换到飞行阶段（用于等怪物尸体消失后再飞）。</summary>
    public void BeginFlight(PlayerMove target)
    {
        if (target == null) return;
        flyTarget = target;
        phase = DropItemPhase.Flying;
        timer = 0f;
        spawnPos = transform.position;
    }

    // ========================================
    // Unity Lifecycle
    // ========================================

    private void Update()
    {
        if (phase == DropItemPhase.Done) return;

        if (phase == DropItemPhase.Ground)
        {
            UpdateGroundPhase();
            TryAutoPickup();
        }
        else if (phase == DropItemPhase.Flying)
        {
            UpdateFlyingPhase();
        }
    }

    private void OnDestroy()
    {
        isPickupLocked = true;
    }

    // ========================================
    // Ground Phase
    // ========================================

    private void UpdateGroundPhase()
    {
        timer += Time.deltaTime;

        float bob = Mathf.Sin(timer * 12f) * 0.03f;
        transform.position += Vector3.up * bob * Time.deltaTime;

        if (timer >= groundDelay)
        {
            phase = DropItemPhase.Flying;
            timer = 0f;
            spawnPos = transform.position;
        }
    }

    // ========================================
    // Flying Phase
    // ========================================

    private void UpdateFlyingPhase()
    {
        if (flyTarget == null || flyTarget.gameObject == null)
        {
            phase = DropItemPhase.Done;
            return;
        }

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / FlyDuration);

        Vector3 targetPos = flyTarget.transform.position + Vector3.up * 1f;
        Vector3 basePos = Vector3.Lerp(spawnPos, targetPos, t);
        float arcHeight = Mathf.Sin(t * Mathf.PI) * 0.8f;
        transform.position = basePos + Vector3.up * arcHeight;

        transform.Rotate(0f, 360f * Time.deltaTime, 0f);

        if (t >= 1f)
            OnArrived();
    }

    private void OnArrived()
    {
        if (isPickupLocked || itemData == null)
        {
            Destroy(gameObject);
            return;
        }

        if (CanAddToBackpack())
        {
            InventoryManager.Instance.TryAddItem(itemData, flyTarget);
            phase = DropItemPhase.Done;
            Destroy(gameObject);
        }
        else
        {
            phase = DropItemPhase.Ground;
            timer = 0f;
            groundDelay = float.MaxValue;
            spawnPos = transform.position;
        }
    }

    private bool CanAddToBackpack()
    {
        if (flyTarget == null || InventoryManager.Instance == null || itemData == null) return false;
        return InventoryManager.Instance.CanAddItem(itemData, flyTarget);
    }

    // ========================================
    // Ground Pickup (Mouse Click)
    // ========================================

    private void OnMouseDown()
    {
        if (isPickupLocked) return;
        if (phase == DropItemPhase.Flying) return;

        TryPickup();
    }

    private void TryPickup()
    {
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

        if (nearest == null) return;

        float sqrRange = pickupRange * pickupRange;
        if (nearestSqr <= sqrRange)
        {
            if (isPickupLocked || itemData == null) return;

            if (InventoryManager.Instance.TryAddItem(itemData, nearest))
                Destroy(gameObject);
        }
        else
        {
            nearest.SetMoveDestination(transform.position);
        }
    }

    // ========================================
    // Auto Pickup (地面模式：玩家靠近自动拾取)
    // ========================================

    private void TryAutoPickup()
    {
        if (isPickupLocked || itemData == null) return;
        if (InventoryManager.Instance == null) return;
        // 有飞行目标的物品（如怪物掉落等待起飞）不自动拾取
        if (flyTarget != null) return;

        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null || p.IsDead) continue;
            if (transform.position.SqrDistanceXZ(p.transform.position) > pickupRange * pickupRange)
                continue;

            if (InventoryManager.Instance.TryAddItem(itemData, p))
            {
                Destroy(gameObject);
                return;
            }
        }
    }
}
