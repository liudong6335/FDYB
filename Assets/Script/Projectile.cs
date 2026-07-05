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
