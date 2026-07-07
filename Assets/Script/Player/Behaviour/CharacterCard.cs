using UnityEngine;

[CreateAssetMenu(menuName = "Game/Character Card")]
public class CharacterCard : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Unnamed";
    [TextArea(3, 8)] public string background = "";

    [Header("Personality Traits")]
    [Range(0f, 1f)] public float aggression = 0.5f;
    [Range(0f, 1f)] public float caution = 0.5f;
    [Range(0f, 1f)] public float supportiveness = 0.5f;
    [Range(0f, 1f)] public float independence = 0.5f;
    [Range(0f, 1f)] public float greed = 0.5f;

    [Header("Meta Goals")]
    [Range(0f, 1f)] public float selfPreservation = 0.6f;   // 自保 — 越高越惜命
    [Range(0f, 1f)] public float victoryFocus = 0.6f;        // 求胜 — 越高越做推动胜利的事

    [Header("Combat Style")]
    [Range(0f, 1f)] public float preferredRange = 0.5f;
    [Range(0f, 1f)] public float focusFire = 0.5f;
    public float potionThreshold = 0.5f;

    [Header("Movement")]
    public float followDistance = 5f;
    public float aggroRange = 15f;

    [Header("Kiting")]
    public float kiteHealthThreshold = 0.6f;
    public float npcPullRadius = 12f;
    public float fleeHealthThreshold = 0.3f;

    [Header("Looting")]
    public float lootRadius = 10f;
}
