using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A single player stat panel (P1 or P2) with ATK, speed, CD, equipment info.
/// Built programmatically as part of the HUD canvas.
/// </summary>
public class PlayerPanelController
{
    private Text statsLabel;
    private Text speedLabel;
    private Text cdLabel;
    private Text equipLabel;
    private Image boostIcon;
    private Sprite boostIconSprite;
    private int playerIndex; // 1 or 2

    public void Build(Transform parent, int index, float left, float top,
        bool anchorRight, Color bgColor, Sprite speedBoostIcon, System.Action<int> onShopClick)
    {
        playerIndex = index;
        boostIconSprite = speedBoostIcon;

        string name = "P" + index + "Panel";
        string title = "P" + index;

        var panel = UIHelpers.MakePanel(name, parent,
            anchorRight ? Vector2.one : Vector2.zero,
            anchorRight ? Vector2.one : Vector2.zero,
            anchorRight ? new Vector2(-left, -top) : new Vector2(left, -top),
            new Vector2(240f, 200f));
        panel.GetComponent<Image>().color = bgColor;

        var titleText = UIHelpers.MakeText(name + "Title", panel.transform, "=== " + title + " ===", 16, FontStyle.Bold, TextAnchor.UpperLeft, Color.yellow);
        UIHelpers.SetAnchor(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(220f, 22f));

        boostIcon = UIHelpers.MakeImage(name + "BoostIcon", panel.transform, Color.white);
        UIHelpers.SetAnchor(boostIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -80f), new Vector2(18f, 18f));

        statsLabel = UIHelpers.MakeText(name + "Stats", panel.transform, "ATK: 100  Range: 8m", 12, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        UIHelpers.SetAnchor(statsLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -38f), new Vector2(220f, 18f));

        speedLabel = UIHelpers.MakeText(name + "Speed", panel.transform, "Speed: Normal", 12, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        UIHelpers.SetAnchor(speedLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -58f), new Vector2(220f, 18f));

        cdLabel = UIHelpers.MakeText(name + "Cd", panel.transform, "Q-Boost: Ready", 12, FontStyle.Normal, TextAnchor.UpperLeft, Color.yellow);
        UIHelpers.SetAnchor(cdLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -78f), new Vector2(220f, 18f));

        equipLabel = UIHelpers.MakeText(name + "Equip", panel.transform, "Equip: 0/5  Gold: 0", 12, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        UIHelpers.SetAnchor(equipLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -100f), new Vector2(220f, 18f));

        var shopBtn = UIHelpers.MakeButton(name + "ShopBtn", panel.transform, "Shop", new Vector2(60f, 24f));
        UIHelpers.SetAnchor(shopBtn.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-70f, 8f), new Vector2(60f, 24f));
        int capturedIndex = index;
        shopBtn.onClick.AddListener(() => onShopClick?.Invoke(capturedIndex));
    }

    public void Refresh(PlayerMove player, int equippedCount, int gold)
    {
        if (player == null) return;

        statsLabel.text = $"ATK: {player.EffectiveDamage:F0}  Range: {player.EffectiveAttackRange:F0}m";
        speedLabel.text = $"Speed: {(player.IsSpeedBoosted ? "!!BOOSTED" : "Normal")}";

        if (boostIcon != null)
        {
            boostIcon.sprite = boostIconSprite;
            boostIcon.color = player.IsSpeedBoosted ? Color.cyan : new Color(1f, 1f, 1f, 0.35f);
        }

        float cdRemain = player.SpeedBoostCooldownRemaining;
        cdLabel.text = cdRemain > 0 ? $"Q-Boost CD: {cdRemain:F0}s" : "Q-Boost: Ready";
        cdLabel.color = cdRemain <= 0 ? Color.yellow : Color.white;

        equipLabel.text = $"Equip: {equippedCount}/5  Gold: {gold}";
    }
}
