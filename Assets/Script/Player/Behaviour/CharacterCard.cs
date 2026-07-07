using UnityEngine;

/// <summary>
/// 角色性格卡片 — 定义角色的世界观、性格特质和行为倾向。
/// 创建方式：Assets → Create → Game/Character Card
/// </summary>
[CreateAssetMenu(menuName = "Game/Character Card")]
public class CharacterCard : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Unnamed";
    [TextArea(3, 8)] public string background = "";

    [Header("Personality Traits")]
    [Range(0f, 1f)] public float aggression = 0.5f;       // 攻击性
    [Range(0f, 1f)] public float caution = 0.5f;           // 谨慎
    [Range(0f, 1f)] public float supportiveness = 0.5f;    // 护卫倾向
    [Range(0f, 1f)] public float independence = 0.5f;      // 独立行动
    [Range(0f, 1f)] public float greed = 0.5f;              // 贪婪 — 越倾向于捡宝箱

    [Header("Combat Style")]
    [Range(0f, 1f)] public float preferredRange = 0.5f;    // 0=近身 1=远程风筝
    [Range(0f, 1f)] public float focusFire = 0.5f;          // 0=分散 1=集火P1
    public float potionThreshold = 0.5f;                    // 用药血量阈值

    [Header("Movement")]
    public float followDistance = 5f;
    public float aggroRange = 15f;

    [Header("Kiting")]
    public float kiteHealthThreshold = 0.6f;
    public float npcPullRadius = 12f;
    public float fleeHealthThreshold = 0.3f;

    [Header("Looting")]
    public float lootRadius = 10f;                          // 拾取范围
}
