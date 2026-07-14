using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles player input (mouse click, keyboard WASD, speed boost key).
/// Translates input into calls on PlayerMove and PlayerCombat.
/// Disable this component when AI controls the player.
/// </summary>
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerCombat))]
public class PlayerInputController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Skill - Speed Boost")]
    [SerializeField] private KeyCode speedBoostKey = KeyCode.Q;

    private Camera mainCamera;
    private PlayerMove playerMove;
    private PlayerCombat playerCombat;

    private void Awake()
    {
        mainCamera = Camera.main;
        playerMove = GetComponent<PlayerMove>();
        playerCombat = GetComponent<PlayerCombat>();
    }

    private void Update()
    {
        if (playerMove == null || playerMove.IsDead || playerMove.IsPlayerAI) return;

        HandleMouseInput();
        HandleKeyboardMovement();
        HandleSpeedBoostKey();
    }

    private void HandleMouseInput()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // Click enemy 鈫?engage combat
        if (playerCombat.EnemyLayerMask.value != 0 &&
            Physics.Raycast(ray, out RaycastHit enemyHit, 500f, playerCombat.EnemyLayerMask, QueryTriggerInteraction.Collide))
        {
            bool inRange = playerCombat.TryEngage(enemyHit.collider.transform);
            if (!inRange)
                playerMove.SetMoveDestination(enemyHit.point);
            return;
        }

        // Click ground 鈫?move
        if (Physics.Raycast(ray, out RaycastHit groundHit, 500f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            playerCombat.ClearTarget();
            playerMove.SetMoveDestination(groundHit.point);
        }
    }

    private void HandleKeyboardMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
            playerMove.SetKeyboardMove(new Vector3(h, 0f, v).normalized);
        else
            playerMove.SetKeyboardMove(Vector3.zero);
    }

    private void HandleSpeedBoostKey()
    {
        if (Input.GetKeyDown(speedBoostKey))
            playerMove.TryActivateSpeedBoost();
    }
}



