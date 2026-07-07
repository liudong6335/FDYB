using UnityEngine;

/// <summary>
/// 特质定义 — 每个 ScriptableObject 实例代表一个可配置的 trait（元目标/性格/战斗风格等）。
/// CharacterCard 持有 TraitEntry 列表，通过 TraitDefinition 索引取值。
/// 创建方式：Assets → Create → Game/Trait 或右键批量创建。
/// </summary>
[CreateAssetMenu(menuName = "Game/Trait")]
public class TraitDefinition : ScriptableObject
{
    public string displayName;
    [TextArea] public string description;
    public string category;         // "MetaGoal", "Personality", "Combat", etc.
    [Range(0f, 1f)] public float defaultValue = 0.5f;
}
