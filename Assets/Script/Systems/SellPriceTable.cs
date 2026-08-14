using UnityEngine;

/// <summary>
/// 玩家出售价按稀有度配置的资产。
/// 留空字段时 InventoryManager 会回退到默认值。
/// </summary>
[CreateAssetMenu(menuName = "Game/Sell Price Table")]
public class SellPriceTable : ScriptableObject
{
    [Header("Sell Prices")]
    [SerializeField] private int commonPrice = 25;
    [SerializeField] private int qualityPrice = 50;
    [SerializeField] private int rarePrice = 100;
    [SerializeField] private int epicPrice = 200;
    [SerializeField] private int mythicPrice = 500;

    public int GetPrice(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return commonPrice;
            case ItemRarity.Quality: return qualityPrice;
            case ItemRarity.Rare: return rarePrice;
            case ItemRarity.Epic: return epicPrice;
            case ItemRarity.Mythic: return mythicPrice;
            default: return 0;
        }
    }
}