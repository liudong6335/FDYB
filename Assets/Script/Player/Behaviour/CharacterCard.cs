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
    [Range(0f, 1f)] public float aggression = 0.5f;       // 攻击性 — 越倾向于战斗
    [Range(0f, 1f)] public float caution = 0.5f;           // 谨慎 — 越倾向于保命/撤退
    [Range(0f, 1f)] public float supportiveness = 0.5f;    // 护卫倾向 — 保护NPC
    [Range(0f, 1f)] public float independence = 0.5f;      // 独立行动 — 不与P1扎堆

    [Header("Combat Style")]
    [Range(0f, 1f)] public float preferredRange = 0.5f;    // 0=近身缠斗 1=远程风筝
    [Range(0f, 1f)] public float focusFire = 0.5f;          // 0=分散打 1=集火P1目标
    public float potionThreshold = 0.5f;                    // 低于此血量用血瓶

    [Header("Movement")]
    public float followDistance = 5f;                       // 跟随NPC的距离
    public float aggroRange = 15f;                          // 索敌范围

    [Header("Kiting")]
    public float kiteHealthThreshold = 0.6f;                // 高于此血量才风筝
    public float npcPullRadius = 12f;                       // 从NPC周围多远拉怪
    public float fleeHealthThreshold = 0.3f;                // 低于此血量逃跑
}
