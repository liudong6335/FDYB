using UnityEngine;

/// <summary>
/// Handles player interaction with treasure chests:
/// - Walking phase: auto-navigate, cancelable by WASD/attack.
/// - Reading phase: locked in place, no external cancel (only death or completion).
/// </summary>
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerCombat))]
public class PlayerChestInteraction : MonoBehaviour
{
    private enum State { Idle, Walking, Reading }

    private State state = State.Idle;
    private TreasureChest targetChest;
    private PlayerMove playerMove;
    private PlayerCombat playerCombat;

    public bool IsBusy { get { return state != State.Idle; } }

    private void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        playerCombat = GetComponent<PlayerCombat>();
    }

    public void NavigateTo(TreasureChest chest)
    {
        if (state != State.Idle || playerMove == null) return;
        targetChest = chest;
        state = State.Walking;
        playerMove.StopMoving();
        playerMove.SetMoveDestination(chest.transform.position);
    }

    public void LockForReading(TreasureChest chest)
    {
        state = State.Reading;
        targetChest = chest;
        playerMove.StopMoving();
    }

    public void Release()
    {
        state = State.Idle;
        targetChest = null;
    }

    private void Update()
    {
        if (state == State.Idle) return;

        // Chest done or destroyed
        if (targetChest == null || targetChest.IsOpened)
        {
            Release();
            return;
        }

        switch (state)
        {
            case State.Walking:
                // Cancel on WASD or attack during walking (player changed mind)
                if (playerMove.IsKeyboardMoving || playerCombat.AttackTarget != null)
                {
                    Release();
                    return;
                }

                // Arrived at chest
                float sqrDist = (transform.position - targetChest.transform.position).sqrMagnitude;
                if (sqrDist <= targetChest.InteractDistance * targetChest.InteractDistance)
                    targetChest.StartOpening(playerMove);
                break;

            case State.Reading:
                // Active operations cancel (WASD move, attack monster)
                if (playerMove.IsKeyboardMoving || playerCombat.AttackTarget != null)
                {
                    targetChest.CancelInteraction();
                    Release();
                    return;
                }
                // Passive hit does NOT cancel - only reduces progress (handled by TreasureChest)

                // Keep player locked in place
                playerMove.StopMoving();
                break;
        }
    }
}

