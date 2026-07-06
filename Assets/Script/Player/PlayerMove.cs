/*
 * ============================================================
 *  PlayerMove  -  玩家移动+输入控制
 * ============================================================
 *
 * 【功能】
 *   玩家角色的移动控制：鼠标点击移动（类似MOBA）、
 *   键盘WASD移动、自动追击敌人、加速技能（Q键）。
 *   整合了 PlayerHealth 和 PlayerCombat。
 *
 * 【挂载对象】
 *   玩家对象（同时需挂载 PlayerHealth 和 PlayerCombat）
 *
 * 【可调节参数】
 *   （移动）
 *   moveSpeed              - 基础移动速度
 *   rotationSpeed          - 转向速度
 *   stoppingDistance       - 到达目的地停止距离
 *   groundLayer            - 地面层级（用于鼠标点击）
 *
 *   （加速技能 - Q键）
 *   speedBoostMultiplier   - 加速倍率
 *   speedBoostDuration     - 加速持续时间
 *   speedBoostCooldown     - 加速冷却时间
 *   speedBoostKey          - 加速快捷键（默认Q）
 *
 * 【操作说明】
 *   - 鼠标左键点敌人 = 攻击
 *   - 鼠标左键点地面 = 移动
 *   - WASD = 键盘移动
 *   - Q = 加速技能
 *
 * 【说明】
 *   使用 CharacterController 移动，带碰撞阻挡
 */
using UnityEngine;
using UnityEngine.EventSystems;

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

    #endregion

    #region Private State

    private PlayerHealth playerHealth;
    private PlayerCombat playerCombat;
    private CharacterController cc;
    private Camera mainCamera;
    private Vector3 moveDestination;
    private bool hasMoveCommand;

    // Speed boost
    private bool isSpeedBoosted;
    private float speedBoostEndTime;
    private float speedBoostNextTime;

    #endregion

    #region Public Properties

    public float SpeedMultiplier { get; set; } = 1f;
    public bool IsSpeedBoosted { get { return isSpeedBoosted; } }
    public float SpeedBoostCooldownRemaining { get { return Mathf.Max(0f, speedBoostNextTime - Time.time); } }
    public float SpeedBoostCooldownTotal { get { return speedBoostCooldown; } }

    // 透传  Pass-through to PlayerHealth (backward compat for GameManager / NPCGoddess) 透传
    public float CurrentHealth { get { return playerHealth != null ? playerHealth.CurrentHealth : 0f; } }
    public float HealthPercent { get { return playerHealth != null ? playerHealth.HealthPercent : 1f; } }
    public bool IsDead { get { return playerHealth == null || playerHealth.CurrentHealth <= 0f; } }
    public float EffectiveMaxHealth { get { return playerHealth != null ? playerHealth.EffectiveMaxHealth : 0f; } }
    public void TakeDamage(float dmg) { playerHealth?.TakeDamage(dmg); }
    public float BaseDamage { get { return playerHealth != null ? playerHealth.BaseDamage : 0f; } set { if (playerHealth != null) playerHealth.BaseDamage = value; } }
    public float DamageMultiplier { get { return playerHealth != null ? playerHealth.DamageMultiplier : 1f; } set { if (playerHealth != null) playerHealth.DamageMultiplier = value; } }
    public float MaxHealthBonus { get { return playerHealth != null ? playerHealth.MaxHealthBonus : 0f; } set { if (playerHealth != null) playerHealth.MaxHealthBonus = value; } }
    public float EffectiveDamage { get { return playerHealth != null ? playerHealth.EffectiveDamage : 0f; } }
    public float EffectiveAttackRange { get { return playerHealth != null ? playerHealth.EffectiveAttackRange : 0f; } }
    public void RefreshStats() { playerHealth?.RefreshStats(); }

    #endregion

    private void Awake()
    {
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
        moveDestination = transform.position;
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.CurrentHealth <= 0f) return;

        HandleMouseInput();
        HandleKeyboardMovement();
        HandleSpeedBoost();
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

        // Click enemy → engage combat
        if (playerCombat.EnemyLayerMask.value != 0 &&
            Physics.Raycast(ray, out RaycastHit enemyHit, 500f, playerCombat.EnemyLayerMask, QueryTriggerInteraction.Collide))
        {
            bool inRange = playerCombat.TryEngage(enemyHit.collider.transform);
            hasMoveCommand = !inRange;
            if (!inRange) moveDestination = enemyHit.point;
            MovementUtility.FaceDirection(transform, enemyHit.point - transform.position, rotationSpeed, Time.deltaTime);
            return;
        }

        // Click ground → move
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

    private void HandleSpeedBoost()
    {
        if (Input.GetKeyDown(speedBoostKey) && Time.time >= speedBoostNextTime)
            ActivateSpeedBoost();
        if (isSpeedBoosted && Time.time >= speedBoostEndTime)
            isSpeedBoosted = false;
    }

    public void ActivateSpeedBoost()
    {
        isSpeedBoosted = true;
        speedBoostEndTime = Time.time + speedBoostDuration;
        speedBoostNextTime = Time.time + speedBoostCooldown;
    }

    private float GetEffectiveSpeed()
    {
        return moveSpeed * SpeedMultiplier * (isSpeedBoosted ? speedBoostMultiplier : 1f);
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
        float sqrDist = SqrDistanceTo(flatDest);

        if (sqrDist <= stoppingDistance * stoppingDistance)
        {
            hasMoveCommand = false;
            moveDestination = transform.position;
            return;
        }

        float effectiveSpeed = GetEffectiveSpeed();
        MovementUtility.FaceDirection(transform, flatDest - transform.position, rotationSpeed, Time.deltaTime);
        Vector3 newPos = Vector3.MoveTowards(transform.position, flatDest, effectiveSpeed * Time.deltaTime);
        cc.Move(newPos - transform.position);
    }

    #endregion

    #region Helpers

    private float SqrDistanceTo(Vector3 point)
    {
        float dx = transform.position.x - point.x;
        float dz = transform.position.z - point.z;
        return dx * dx + dz * dz;
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool keyboardMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;
        bool moving = keyboardMoving || hasMoveCommand;
        if (playerCombat.IsAttackLocked) moving = false;
        animator.SetBool(isMovingParameter, moving);
        animator.SetFloat(moveSpeedParameter, moving ? moveSpeed : 0f);
    }

    #endregion
}

