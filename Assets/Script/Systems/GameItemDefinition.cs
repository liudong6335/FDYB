using System;
using UnityEngine;

/// <summary>
/// ScriptableObject asset for item templates.
/// Create instances via Assets > Create > Game/Item Definition.
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

    [Header("UI & Visual")]
    [Tooltip("背包/商城展示用图标")]
    public Sprite icon;
    [Tooltip("场景掉落 3D 模型预制体（null = 不掉落在地面）")]
    public GameObject dropPrefab;
    [Tooltip("玩家死亡时该物品是否从背包掉出")]
    public bool dropOnDeath;
    [Tooltip("死亡掉落概率（0~1），默认 0 = 不掉")]
    [Range(0f, 1f)] public float deathDropChance;
    [Tooltip("商城售价（0 = 不出售）")]
    public int shopPrice;
    [TextArea]
    [Tooltip("鼠标悬浮提示文本")]
    public string tooltip;

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
            definition = this,
            instanceId = Guid.NewGuid().ToString("N")
        };
    }
}