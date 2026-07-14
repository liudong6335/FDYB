using UnityEngine;

[RequireComponent(typeof(NPCGoddess))]
public class NPCGoddessHealing : MonoBehaviour
{
    private NPCGoddess npc;
    private static readonly int HealHash = Animator.StringToHash("Heal");
    private Health health;

    [Header("Heal Skill")]
    [SerializeField] private float healAmount = 200f;
    [SerializeField] private float healCooldown = 15f;
    [SerializeField] private float healCastTime = 3f;
    [SerializeField] private float healRange = 5f;
    [SerializeField][Range(0f, 1f)] private float selfHealThresholdPercent = 0.6f;
    [SerializeField][Range(0f, 1f)] private float playerHealThresholdPercent = 0.5f;

    [Header("Passive Heal Aura")]
    [SerializeField] private float passiveHealCooldown = 10f;
    [SerializeField] private float passiveHealDuration = 5f;
    [SerializeField] private float passiveHealRange = 5f;
    [SerializeField] private float passiveHealPerSecond = 30f;

    // Private state
    private float nextHealTime;
    private bool isHealing;
    private float healStartTime;
    private Vector3 healPosition;
    private float nextPassiveHealTime;
    private float passiveHealActiveTimer;
    private bool isPassiveHealing;

    // Public properties
    public bool IsHealing { get { return isHealing; } }
    public float NextHealTime { get { return nextHealTime; } }
    public float HealRange { get { return healRange; } }
    public float HealCooldown { get { return healCooldown; } }

    // Events
    public System.Action OnHealStart;
    public System.Action OnHealComplete;

    private void Awake()
    {
        npc = GetComponent<NPCGoddess>();
        health = GetComponent<Health>();
    }

    // ============================================
    // Per-frame update - called from NPCGoddess
    // ============================================

    public void UpdateHealing()
    {
        // Heal decision
        if (Time.time >= nextHealTime && !isHealing && !npc.IsDead && !npc.HasArrived)
            CheckAutoHeal();

        // Heal casting update
        if (isHealing) UpdateHealCast();

        // Passive heal aura
        UpdatePassiveHealAura();
    }

    // ============================================
    // Active healing
    // ============================================

    private void CheckAutoHeal()
    {
        if (isHealing || Time.time < nextHealTime) return;

        // Self-heal
        if (npc.HealthPercent < selfHealThresholdPercent)
        {
            StartHealCast();
            return;
        }

        // Heal nearby players
        if (npc.HealthPercent >= selfHealThresholdPercent)
        {
            PlayerMove bestTarget = null;
            float bestDist = float.MaxValue;

            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p == null || p.CurrentHealth <= 0f) continue;
                if (p.HealthPercent >= playerHealThresholdPercent) continue;
                if (p.HealthPercent >= 1f) continue;

                float dist = transform.position.DistanceXZ(p.transform.position);
                if (dist < bestDist && dist <= healRange)
                {
                    bestDist = dist;
                    bestTarget = p;
                }
            }

            if (bestTarget != null)
                StartHealCast();
        }
    }

    public bool RequestHeal(Vector3 playerPos)
    {
        if (npc.IsDead || npc.HasArrived || isHealing) return false;
        if (Time.time < nextHealTime) return false;
        if (health.IsFullHealth) return false;

        float dist = transform.position.DistanceXZ(playerPos);
        if (dist > healRange) return false;

        StartHealCast();
        return true;
    }

    private void StartHealCast()
    {
        isHealing = true;
        healStartTime = Time.time;
        healPosition = transform.position;
        npc.AnimSetTrigger(HealHash);
        OnHealStart?.Invoke();
    }

    private void UpdateHealCast()
    {
        if (Vector3.Distance(transform.position, healPosition) > 0.1f)
        {
            isHealing = false;
            return;
        }
        if (Time.time - healStartTime >= healCastTime)
            CompleteHeal();
    }

    private void CompleteHeal()
    {
        isHealing = false;
        nextHealTime = Time.time + healCooldown;
        health.Heal(healAmount);

        // Also heal nearby players
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null || p.CurrentHealth <= 0f) continue;
            float dist = transform.position.DistanceXZ(p.transform.position);
            if (dist <= healRange && p.CurrentHealth < p.EffectiveMaxHealth)
            {
                p.Heal(healAmount);
            }
        }

        OnHealComplete?.Invoke();
    }

    // ============================================
    // Passive heal aura
    // ============================================

    private void UpdatePassiveHealAura()
    {
        if (npc.IsDead || npc.HasArrived) return;

        if (!isPassiveHealing)
        {
            if (Time.time >= nextPassiveHealTime)
            {
                isPassiveHealing = true;
                passiveHealActiveTimer = 0f;
            }
        }
        else
        {
            passiveHealActiveTimer += Time.deltaTime;

            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p == null || p.CurrentHealth <= 0f) continue;
                float dist = transform.position.DistanceXZ(p.transform.position);
                if (dist <= passiveHealRange && p.CurrentHealth < p.EffectiveMaxHealth)
                {
                    p.Heal(passiveHealPerSecond * Time.deltaTime);
                }
            }

            if (passiveHealActiveTimer >= passiveHealDuration)
            {
                isPassiveHealing = false;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healRange);
    }
}
