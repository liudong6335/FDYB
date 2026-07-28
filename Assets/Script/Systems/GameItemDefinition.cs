using UnityEngine;

/// <summary>
/// ScriptableObject asset for item templates.
/// Create instances via Assets → Create → Game/Item Definition.
/// </summary>
[CreateAssetMenu(menuName = "Game/Item Definition")]
public class GameItemDefinition : ScriptableObject
{
    [Header("Basic")]
    public ItemType itemType;
    public string itemId;
    public string itemName;
    [TextArea] public string description;

    [Header("Rarity")]
    public ItemRarity rarity = ItemRarity.Common;

    [Header("Equipment")]
    public EquipmentSlotType slotType;
    public StatType statType;
    public float statValue;

    [Header("Consumable")]
    public float healAmount;

    [Header("Currency")]
    public int goldAmount;
    /// <summary>Convert this definition to a runtime GameItem instance.</summary>
    public GameItem ToGameItem()
    {
        return new GameItem
        {
            itemType = itemType,
            itemId = itemId,
            itemName = itemName,
            description = description,
            rarity = rarity,
            slotType = slotType,
            statType = statType,
            statValue = statValue,
            healAmount = healAmount,
            goldAmount = goldAmount,
            definition = this
        };
    }
}
