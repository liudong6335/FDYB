using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(Button))]
public class SpeedBoostButton : MonoBehaviour
{
    [SerializeField] private PlayerMove player;
    [SerializeField] private bool autoFindPlayer = true;

    private void Awake()
    {
        if (autoFindPlayer && player == null)
            player = FindFirstObjectByType<PlayerMove>();
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (player == null || player.IsDead) return;
        var method = typeof(PlayerMove).GetMethod("ActivateSpeedBoost",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null) method.Invoke(player, null);
    }

    private void Update()
    {
        if (player == null) return;
        GetComponent<Button>().interactable = player.SpeedBoostCooldownRemaining <= 0f && !player.IsDead;
    }
}
