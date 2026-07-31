using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键创建所有初始道具 SO 资产。
/// Tools → Create All Item Assets 运行。
/// 数值均为默认值，请在 Inspector 中调参。
/// </summary>
public static class ItemAssetCreator
{
    [MenuItem("Tools/Create All Item Assets")]
    public static void CreateAll()
    {
        CreatePotion("LifePotion_Small", "life_potion_small", "小生命药水", "恢复少量生命值", "Assets/Prefab/LifePotion_B.prefab");
        CreatePotion("LifePotion_Large", "life_potion_large", "大生命药水", "恢复大量生命值", "Assets/Prefab/LifePotion_L.prefab");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ItemAssetCreator] 所有物品资产创建完成");
    }

    private static void CreatePotion(string assetName, string itemId, string itemName, string desc, string prefabPath)
    {
        var def = ScriptableObject.CreateInstance<GameItemDefinition>();
        def.itemType = ItemType.Consumable;
        def.itemId = itemId;
        def.itemName = itemName;
        def.description = desc;
        def.rarity = ItemRarity.Common;
        def.healAmount = 200f;
        def.dropOnDeath = true;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null) def.dropPrefab = prefab;
        else Debug.LogWarning($"[ItemAssetCreator] 未找到 {prefabPath}，dropPrefab 留空");

        AssetDatabase.CreateAsset(def, $"Assets/ScriptableObjects/Items/Consumables/{assetName}.asset");
    }
}
