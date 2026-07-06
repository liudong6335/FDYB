using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Backpack / inventory panel: view, use consumables, sell items.
/// Built programmatically as part of the HUD canvas.
/// </summary>
public class BackpackController
{
    private GameObject panel;
    private Transform contentRoot;
    private PlayerMove currentPlayer;
    private List<GameItem> currentBackpack;

    public bool IsOpen { get { return panel != null && panel.activeSelf; } }

    public void Build(Transform parent, Color bgColor)
    {
        panel = UIHelpers.MakePanel("InventoryPanel", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-140f, -125f), new Vector2(280f, 250f));
        panel.GetComponent<Image>().color = bgColor;

        var title = UIHelpers.MakeText("InvTitle", panel.transform, "=== Backpack ===", 16, FontStyle.Bold, TextAnchor.UpperCenter, Color.yellow);
        UIHelpers.SetAnchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -8f), new Vector2(0f, 22f));

        contentRoot = UIHelpers.MakePanel("InvContent", panel.transform,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, -40f)).transform;
        Object.Destroy(contentRoot.GetComponent<Image>());

        var closeBtn = UIHelpers.MakeButton("InvCloseBtn", panel.transform, "Close", new Vector2(100f, 30f));
        UIHelpers.SetAnchor(closeBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(100f, 30f));
        closeBtn.onClick.AddListener(Close);

        panel.SetActive(false);
    }

    /// <summary>Toggle backpack open/closed for the given player.</summary>
    public void Toggle(PlayerMove player, List<GameItem> backpack)
    {
        currentPlayer = player;
        currentBackpack = backpack;
        bool opening = !panel.activeSelf;
        panel.SetActive(opening);
        if (opening) RefreshItems();
    }

    public void Close()
    {
        panel.SetActive(false);
        currentPlayer = null;
        currentBackpack = null;
    }

    private void RefreshItems()
    {
        foreach (Transform child in contentRoot)
            Object.Destroy(child.gameObject);

        if (currentPlayer == null || currentBackpack == null) return;

        float yOffset = 0f;
        foreach (var item in currentBackpack)
        {
            var row = UIHelpers.MakePanel("InvItem" + yOffset, contentRoot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, yOffset), new Vector2(0f, 28f));
            Object.Destroy(row.GetComponent<Image>());

            var label = UIHelpers.MakeText("Label", row.transform,
                item.itemName + " - " + item.description, 11, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
            UIHelpers.SetAnchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(190f, 0f));

            if (item.itemType == ItemType.Consumable)
            {
                var useBtn = UIHelpers.MakeButton("UseBtn", row.transform, "Use", new Vector2(50f, 22f));
                UIHelpers.SetAnchor(useBtn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-55f, 0f), new Vector2(50f, 22f));
                var capturedId = item.itemId;
                useBtn.onClick.AddListener(() =>
                {
                    InventoryManager.Instance.UseConsumable(capturedId, currentPlayer);
                    RefreshItems();
                });
            }
            else
            {
                var sellBtn = UIHelpers.MakeButton("SellBtn", row.transform, "Sell", new Vector2(50f, 22f));
                UIHelpers.SetAnchor(sellBtn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-55f, 0f), new Vector2(50f, 22f));
                var capturedId = item.itemId;
                sellBtn.onClick.AddListener(() =>
                {
                    InventoryManager.Instance.SellItem(capturedId, currentPlayer);
                    RefreshItems();
                });
            }

            yOffset -= 26f;
        }
    }
}
