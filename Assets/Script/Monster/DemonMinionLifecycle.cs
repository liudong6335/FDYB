using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages dormant/activate/revive/death lifecycle for DemonMinion.
/// Handles renderer/animator/collider/healthbar toggling.
/// </summary>
[RequireComponent(typeof(DemonMinion))]
public class DemonMinionLifecycle : MonoBehaviour
{
    private DemonMinion demon;
    private Health health;
    private Animator animator;
    private Collider col;
    private UGUIFloatingHealthBar healthBar;

    private Renderer[] cachedRenderers;
    private bool isDormant;

    public bool IsDormant { get { return isDormant; } }
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    private void Awake()
    {
        demon = GetComponent<DemonMinion>();
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();
        col = GetComponent<Collider>();
        healthBar = GetComponentInChildren<UGUIFloatingHealthBar>();
        RefreshCachedRenderers();
    }

    /// <summary>Cache all renderers (Skinned + Mesh) for fast enable/disable.</summary>
    private void RefreshCachedRenderers()
    {
        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        var all = new Renderer[renderers.Length + meshRenderers.Length];
        renderers.CopyTo(all, 0);
        meshRenderers.CopyTo(all, renderers.Length);
        cachedRenderers = all;
    }

    // ============================================
    // Public API
    // ============================================

    public void SetDormant()
    {
        isDormant = true;
        demon.NotifyDead();

        RefreshCachedRenderers();
        SetRenderersEnabled(false);

        if (healthBar != null) healthBar.gameObject.SetActive(false);

        if (animator != null)
        {
            animator.SetBool(IsMovingHash, false);
            animator.enabled = false;
        }
        if (col != null) col.enabled = false;
    }

    public void Activate(int level, NPCGoddess npc)
    {
        if (!demon.IsDead) return;

        isDormant = false;
        demon.NotifyAlive();
        ToggleAllComponents(true);
        demon.Initialize(level, npc);
        demon.SpawnPosition = demon.transform.position;
    }

    public void Revive(int newLevel, NPCGoddess npc)
    {

        demon.NotifyAlive();

        ToggleAllComponents(true);
        demon.Initialize(newLevel, npc);
    }

    public void StartHideDeadBody()
    {
        StopAllCoroutines();
        StartCoroutine(HideDeadBodyCoroutine());
    }

    public void OnDetachFromGame()
    {
        // Called by DemonMinion.OnDestroy for static list cleanup
    }

    // ============================================
    // Internal
    // ============================================



    private void ToggleAllComponents(bool enable)
    {
        RefreshCachedRenderers();
        SetRenderersEnabled(enable);

        // Health bar
        if (healthBar == null) healthBar = GetComponentInChildren<UGUIFloatingHealthBar>();
        if (healthBar != null) { healthBar.gameObject.SetActive(enable); healthBar.SetProvider(demon); }

        // Animator
        if (animator == null) animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = enable;

        // Collider
        if (col == null) col = GetComponent<Collider>();
        if (col != null) col.enabled = enable;
    }

    private void SetRenderersEnabled(bool enabled)
    {
        foreach (var r in cachedRenderers)
            if (r != null) r.enabled = enabled;
    }

    private IEnumerator HideDeadBodyCoroutine()
    {
        yield return new WaitForSeconds(1f);
        if (!demon.IsDead) yield break;

        SetRenderersEnabled(false);

        if (healthBar == null) healthBar = GetComponentInChildren<UGUIFloatingHealthBar>();
        if (healthBar != null) healthBar.gameObject.SetActive(false);

        if (animator != null) animator.enabled = false;
        if (col != null) col.enabled = false;
    }
}
