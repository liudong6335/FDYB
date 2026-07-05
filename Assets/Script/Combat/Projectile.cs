/*
 * ============================================================
 *  Projectile  -  投射物（子弹/技能弹道）
 * ============================================================
 *
 * 【功能】
 *   朝目标方向飞行的投射物，命中敌人后造成伤害并销毁。
 *   支持指定目标和指定方向两种发射方式。
 *
 * 【挂载对象】
 *   投射物预制体（Projectile Prefab）
 *
 * 【可调节参数】
 *   speed         - 飞行速度（默认20）
 *
 * 【外部调用】
 *   Initialize(targetPos, damage, layerMask)
 *     - 朝目标位置发射，自动计算方向
 *   InitializeDirection(dir, damage, range, layerMask)
 *     - 朝指定方向发射，指定飞行距离
 *
 * 【说明】
 *   需要场景中有 IDamageable 接口的物体才能造成伤害
 */
using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Vector3 direction;
    private float speed = 20f;
    private float damage;
    private float maxDistance;
    private Vector3 startPosition;
    private LayerMask targetMask;
    private bool initialized;

    public void Initialize(Vector3 targetPosition, float dmg, LayerMask mask)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        direction = toTarget.normalized;
        damage = dmg;
        targetMask = mask;
        maxDistance = toTarget.magnitude + 2f;
        startPosition = transform.position;
        initialized = true;

        // Face projectile direction
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    public void InitializeDirection(Vector3 dir, float dmg, float range, LayerMask mask)
    {
        direction = dir.normalized;
        direction.y = 0f;
        damage = dmg;
        targetMask = mask;
        maxDistance = range;
        startPosition = transform.position;
        initialized = true;

        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void Update()
    {
        if (!initialized) return;

        float step = speed * Time.deltaTime;
        transform.position += direction * step;

        // Check hit
        if (Physics.Raycast(transform.position - direction * step * 0.5f, direction, out RaycastHit hit, step, targetMask, QueryTriggerInteraction.Collide))
        {
            ApplyDamage(hit.collider);
            Destroy(gameObject);
            return;
        }

        // Check max distance
        if (Vector3.Distance(startPosition, transform.position) >= maxDistance)
            Destroy(gameObject);
    }

    private void ApplyDamage(Collider target)
    {
        var damageable = target.GetComponent<IDamageable>();
        if (damageable != null && !damageable.IsDead)
        {
            damageable.TakeDamage(damage);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized) return;
        if ((targetMask.value & (1 << other.gameObject.layer)) != 0)
        {
            ApplyDamage(other);
            Destroy(gameObject);
        }
    }
}

