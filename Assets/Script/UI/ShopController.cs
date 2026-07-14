using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shop panel: purchase potions and equipment with gold.
/// Built programmatically as part of the HUD canvas.
/// </summary>
public class ShopController
{
    private GameObject panel;
    private Transform contentRoot;
    private PlayerMove currentPlayer;

    public void Build(Transform parent, Color bgColor)
    {
        panel = UIHelpers.MakePanel("ShopPanel", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-150f, -150f), new Vector2(300f, 300f));
        panel.GetComponent<Image>().color = bgColor;

        var title = UIHelpers.MakeText("ShopTitle", panel.transform, "=== Map Shop ===", 16, FontStyle.Bold, TextAnchor.UpperCenter, Color.yellow);
        UIHelpers.SetAnchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -8f), new Vector2(0f, 22f));

        contentRoot = UIHelpers.MakePanel("ShopContent", panel.transform,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, -40f)).transform;
        Object.Destroy(contentRoot.GetComponent<Image>());

        AddShopItem("HP Potion Lv1", "50HP/s, 4s", 50);
        AddShopItem("HP Wrist Common", "HP+40", 80);
        AddShopItem("ATK Wrist Common", "DPS+15", 80);
        AddShopItem("HP Chest Common", "HP+80", 120);
        AddShopItem("ATK Chest Common", "DPS+20", 120);

        var closeBtn = UIHelpers.MakeButton("ShopCloseBtn", panel.transform, "Close", new Vector2(100f, 30f));
        UIHelpers.SetAnchor(closeBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(100f, 30f));
        closeBtn.onClick.AddListener(() => { panel.SetActive(false); currentPlayer = null; });

        panel.SetActive(false);
    }

    public void Toggle(PlayerMove player)
    {
        currentPlayer = player;
        panel.SetActive(!panel.activeSelf);
    }

    private void AddShopItem(string name, string desc, int price)
    {
        float yOffset = contentRoot.childCount * -38f;
        var row = UIHelpers.MakePanel("ShopItem" + contentRoot.childCount, contentRoot,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, yOffset), new Vector2(0f, 38f));
        Object.Destroy(row.GetComponent<Image>());

        var nameLabel = UIHelpers.MakeText("Name", row.transform, name, 12, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
        UIHelpers.SetAnchor(nameLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(170f, 0f));

        var descLabel = UIHelpers.MakeText("Desc", row.transform, desc, 11, FontStyle.Normal, TextAnchor.MiddleLeft, Color.gray);
        UIHelpers.SetAnchor(descLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(6f, 2f), new Vector2(120f, 16f));

        var buyBtn = UIHelpers.MakeButton("BuyBtn", row.transform, price + "G", new Vector2(60f, 24f));
        UIHelpers.SetAnchor(buyBtn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-65f, 0f), new Vector2(60f, 24f));

        var capturedName = name;
        var capturedDesc = desc;
        var capturedPrice = price;
        buyBtn.onClick.AddListener(() =>
        {
            var inv = InventoryManager.Instance;
            if (currentPlayer != null && inv != null && inv.SpendGold(capturedPrice, currentPlayer))
            {
                GameItem item = CreateShopItem(capturedName, capturedDesc);
                inv.AddItem(item, currentPlayer);
            }
        });
    }

    private static GameItem CreateShopItem(string name, string desc)
    {
        if (name.Contains("Potion"))
            return new GameItem { itemType = ItemType.Consumable, itemId = "health_potion_1", itemName = name, description = desc, healAmount = 200f };

        bool isHealth = name.Contains("HP");
        EquipmentSlotType slot = EquipmentSlotType.Wrist;
        if (name.Contains("Chest")) slot = EquipmentSlotType.Chest;
        else if (name.Contains("Shoulder")) slot = EquipmentSlotType.Shoulder;
        else if (name.Contains("Pants")) slot = EquipmentSlotType.Pants;

        float value = 40f;
        if (isHealth)
        {
            if (name.Contains("Chest")) value = 80f;
            else if (slot == EquipmentSlotType.Shoulder) value = 50f;
            else if (slot == EquipmentSlotType.Pants) value = 60f;
            else value = 40f;
        }
        else
        {
            if (name.Contains("Chest")) value = 20f;
            else value = 15f;
        }

        return new GameItem
        {
            itemType = ItemType.Equipment,
            itemId = "shop_" + name,
            itemName = name,
            description = desc,
            slotType = slot,
            rarity = ItemRarity.Common,
            statType = isHealth ? StatType.Health : StatType.DPS,
            statValue = value
        };
    }
}
