/*
 * ============================================================
 *  PlayerMove  -  Player Movement + Combat Controller
 * ============================================================
 *
 * 【Functions】
 *   Handles player movement: click-to-move (MOBA style),
 *   WASD keyboard movement, auto-chase enemies, speed boost (Q key).
 *   Works with PlayerHealth and PlayerCombat.
 *
 * 【Singleton】
 *   PlayerMove instances register to the static AllPlayers list.
 *   Each player object also has PlayerHealth and PlayerCombat.
 *
 * 【Adjustable Parameters】
 *   Movement:
 *   moveSpeed              - Base movement speed
 *   rotationSpeed          - Rotation speed
 *   stoppingDistance       - Distance to stop from destination
 *   groundLayer            - Ground layer for raycasting
 *
 *   Speed Boost - Q Key:
 *   speedBoostMultiplier   - Speed multiplier when boosted
 *   speedBoostDuration     - Duration of speed boost
 *   speedBoostCooldown     - Cooldown time
 *   speedBoostKey          - Hotkey (default Q)
 *
 * 【Control Scheme】
 *   - Left click on enemy = engage combat
 *   - Left click on ground = move
 *   - WASD = keyboard movement
 *   - Q = speed boost
 *
 * 【Note】
 *   Uses CharacterController for movement with collision blocking.
 */
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerCombat))]
public class PlayerMove : MonoBehaviour, IDamageable
{
    #region Serialized Fields

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float rotationSpeed = 14f;
    [SerializeField] private float stoppingDistance = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Skill - Speed Boost")]

    [SerializeField] private float speedBoostMultiplier = 1.5f;
    [SerializeField] private float speedBoostDuration = 3f;
    [SerializeField] private float speedBoostCooldown = 30f;
    [SerializeField] private KeyCode speedBoostKey = KeyCode.Q;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string isMovingParameter = "IsMoving";
    [SerializeField] private string moveSpeedParameter = "MoveSpeed";
    private int isMovingParamHash;
    private int moveSpeedParamHash;

    #endregion

    #region Private State

    private static readonly List<PlayerMove> allPlayers = new List<PlayerMove>();
    public static IReadOnlyList<PlayerMove> AllPlayers => allPlayers;

    private PlayerHealth playerHealth;
    private PlayerCombat playerCombat;
    private CharacterController cc;
    private Camera mainCamera;
    private Vector3 moveDestination;
    private bool isPlayerAI;
   private bool hasMoveCommand;
    private DamageSlowEffect slowEffect;
   
   // Speed boost
    private bool isSpeedBoosted;
    private float speedBoostEndTime;
    private float speedBoostNextTime;

    private Vector3 keyboardDirection;
    public bool IsKeyboardMoving => keyboardDirection.sqrMagnitude > 0.1f;

    #endregion

    #region Public Properties

    public float SpeedMultiplier { get; set; } = 1f;
    public bool IsSpeedBoosted { get { return isSpeedBoosted; } }
    public float SpeedBoostCooldownRemaining { get { return Mathf.Max(0f, speedBoostNextTime - Time.time); } }
    public float SpeedBoostCooldownTotal { get { return speedBoostCooldown; } }

    // Pass-through to PlayerHealth (backward compat for GameManager / NPCGoddess)
    public float CurrentHealth { get { return playerHealth != null ? playerHealth.CurrentHealth : 0f; } }
    public float HealthPercent { get { return playerHealth != null ? playerHealth.HealthPercent : 1f; } }
    public bool IsDead { get { return playerHealth == null || playerHealth.CurrentHealth <= 0f; } }
    public float EffectiveMaxHealth { get { return playerHealth != null ? playerHealth.EffectiveMaxHealth : 0f; } }
    public void TakeDamage(float dmg) { playerHealth?.TakeDamage(dmg); }
    public void Heal(float amount) { playerHealth?.TakeDamage(-amount); }
    public float BaseDamage { get { return playerHealth != null ? playerHealth.BaseDamage : 0f; } set { if (playerHealth != null) playerHealth.BaseDamage = value; } }
    public float DamageMultiplier { get { return playerHealth != null ? playerHealth.DamageMultiplier : 1f; } set { if (playerHealth != null) playerHealth.DamageMultiplier = value; } }
    public float MaxHealthBonus { get { return playerHealth != null ? playerHealth.MaxHealthBonus : 0f; } set { if (playerHealth != null) playerHealth.MaxHealthBonus = value; } }
    public float EffectiveDamage { get { return playerHealth != null ? playerHealth.EffectiveDamage : 0f; } }
    public float EffectiveAttackRange { get { return playerHealth != null ? playerHealth.EffectiveAttackRange : 0f; } }
    public void RefreshStats() { playerHealth?.RefreshStats(); }

    #endregion

    private void Awake()
    {
       allPlayers.Add(this);
        slowEffect = GetComponent<DamageSlowEffect>();
       playerHealth = GetComponent<PlayerHealth>();
        playerCombat = GetComponent<PlayerCombat>();
        mainCamera = Camera.main;
        // Migrate to CharacterController (collision blocking without physics push)
        cc = GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = gameObject.AddComponent<CharacterController>();
            cc.radius = 0.35f; cc.height = 1.8f; cc.center = new Vector3(0, 0.9f, 0);
            cc.slopeLimit = 45f; cc.stepOffset = 0.3f; cc.skinWidth = 0.08f;
        }
        var rb = GetComponent<Rigidbody>(); if (rb != null) Destroy(rb);
        var capsule = GetComponent<CapsuleCollider>(); if (capsule != null) capsule.isTrigger = true;
        if (animator == null) animator = GetComponent<Animator>();
        isMovingParamHash = Animator.StringToHash(isMovingParameter);
        moveSpeedParamHash = Animator.StringToHash(moveSpeedParameter);
        moveDestination = transform.position;
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.CurrentHealth <= 0f) return;

        if (!isPlayerAI) { HandleMouseInput(); HandleKeyboardMovement(); }
        UpdateSpeedBoostTimer();
        playerCombat.UpdateCombat();
        MoveTowardDestination();
        UpdateAnimation();
    }

    #region Input

    private void HandleMouseInput()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // Click enemy -> engage combat
        if (playerCombat.EnemyLayerMask.value != 0 &&
            Physics.Raycast(ray, out RaycastHit enemyHit, 500f, playerCombat.EnemyLayerMask, QueryTriggerInteraction.Collide))
        {
            bool inRange = playerCombat.TryEngage(enemyHit.collider.transform);
            hasMoveCommand = !inRange;
            if (!inRange) moveDestination = enemyHit.point;
            MovementUtility.FaceDirection(transform, enemyHit.point - transform.position, rotationSpeed, Time.deltaTime);
            return;
        }

        // Click ground -> move
        if (Physics.Raycast(ray, out RaycastHit groundHit, 500f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            playerCombat.ClearTarget();
            hasMoveCommand = true;
            moveDestination = groundHit.point;
        }
    }

    private void HandleKeyboardMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
        {
            Vector3 dir = new Vector3(h, 0f, v).normalized;
            float effectiveSpeed = GetEffectiveSpeed();
            Vector3 moveDelta = dir * effectiveSpeed * Time.deltaTime;
            moveDelta.y = cc.isGrounded ? -0.1f : moveDelta.y - 9.81f * Time.deltaTime;
            cc.Move(moveDelta);
            MovementUtility.FaceDirection(transform, dir, rotationSpeed, Time.deltaTime);
            hasMoveCommand = false;
            playerCombat.ClearTarget();
        }
    }

    #endregion

    #region Speed Boost

    private void UpdateSpeedBoostTimer()
    {
        if (isSpeedBoosted && Time.time >= speedBoostEndTime)
            isSpeedBoosted = false;
    }

    public void ActivateSpeedBoost()
    {
        isSpeedBoosted = true;
        speedBoostEndTime = Time.time + speedBoostDuration;
        speedBoostNextTime = Time.time + speedBoostCooldown;
    }

    public void TryActivateSpeedBoost()
    {
        if (Time.time >= speedBoostNextTime)
            ActivateSpeedBoost();
    }

   private float GetEffectiveSpeed()
   {
        float speed = moveSpeed * SpeedMultiplier * (isSpeedBoosted ? speedBoostMultiplier : 1f);
        if (slowEffect != null) speed *= slowEffect.SpeedMultiplier;
        return speed;
   }

    #endregion

    #region AI Control

    /// <summary>Enable/disable AI control. AI mode skips mouse/keyboard input.</summary>
    public void SetPlayerAI(bool value) { isPlayerAI = value; }
    public bool IsPlayerAI { get { return isPlayerAI; } }

    /// <summary>Set a move-to destination (clears any combat target).</summary>
    public void SetMoveDestination(Vector3 dest)
    {
        if (playerCombat != null) playerCombat.ClearTarget();
        moveDestination = dest;
        hasMoveCommand = true;
    }

    /// <summary>Stop all movement and clear combat target.</summary>
    public void StopMoving()
    {
        if (playerCombat != null) playerCombat.ClearTarget();
        hasMoveCommand = false;
        moveDestination = transform.position;
    }

    public void SetKeyboardMove(Vector3 dir)
    {
        keyboardDirection = dir;
        if (dir.sqrMagnitude > 0.1f)
        {
            hasMoveCommand = false;
            playerCombat.ClearTarget();
        }
    }

    #endregion

    #region Movement

    private void MoveTowardDestination()
    {
        Transform attackTarget = playerCombat.AttackTarget;

        // Chase mode
        if (attackTarget != null)
        {
            Vector3 targetPos = attackTarget.position;

            // In range: stop to attack
            if (playerCombat.IsInAttackRange(attackTarget))
            {
                hasMoveCommand = false;
                moveDestination = transform.position;
                MovementUtility.FaceDirection(transform, targetPos - transform.position, rotationSpeed, Time.deltaTime);
                return;
            }

            // Chase: keep moving toward enemy
            MovementUtility.FaceDirection(transform, targetPos - transform.position, rotationSpeed, Time.deltaTime);
            moveDestination = targetPos;
            hasMoveCommand = true;
        }

        if (!hasMoveCommand) return;

        Vector3 flatDest = moveDestination;
        flatDest.y = transform.position.y;
        float sqrDist = transform.position.SqrDistanceXZ(flatDest);

        if (sqrDist <= stoppingDistance * stoppingDistance)
        {
            hasMoveCommand = false;
            moveDestination = transform.position;
            return;
        }

        float effectiveSpeed = GetEffectiveSpeed();
        MovementUtility.FaceDirection(transform, flatDest - transform.position, rotationSpeed, Time.deltaTime);
        Vector3 newPos = Vector3.MoveTowards(transform.position, flatDest, effectiveSpeed * Time.deltaTime);
        Vector3 delta = newPos - transform.position;
        delta.y = cc.isGrounded ? -0.1f : delta.y - 9.81f * Time.deltaTime;
        cc.Move(delta);
    }

    #endregion

    #region Helpers

    private void UpdateAnimation()
    {
        if (animator == null) return;
        bool moving;
        if (isPlayerAI)
        {
            moving = hasMoveCommand;
        }
        else
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            bool keyboardMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;
            moving = keyboardMoving || hasMoveCommand;
        }
        if (playerCombat.IsAttackLocked) moving = false;
        animator.SetBool(isMovingParamHash, moving);
        animator.SetFloat(moveSpeedParamHash, moving ? moveSpeed : 0f);
    }

    #endregion
    private void OnDestroy()
    {
        allPlayers.Remove(this);
    }

}






