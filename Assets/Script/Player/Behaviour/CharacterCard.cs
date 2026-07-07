using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Game/Character Card")]
public class CharacterCard : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Unnamed";
    [TextArea(3, 8)] public string background = "";

    [Header("Traits — add/remove entries here")]
    public List<TraitEntry> traits = new List<TraitEntry>();

    [Header("Personality (legacy)")]
    [Range(0f, 1f)] public float aggression = 0.5f;
    [Range(0f, 1f)] public float caution = 0.5f;
    [Range(0f, 1f)] public float supportiveness = 0.5f;
    [Range(0f, 1f)] public float independence = 0.5f;
    [Range(0f, 1f)] public float greed = 0.5f;

    [Header("Meta Goals (legacy)")]
    [Range(0f, 1f)] public float selfPreservation = 0.6f;
    [Range(0f, 1f)] public float victoryFocus = 0.6f;

    [Header("Combat Style (legacy)")]
    [Range(0f, 1f)] public float preferredRange = 0.5f;
    [Range(0f, 1f)] public float focusFire = 0.5f;
    [Range(0f, 1f)] public float oppression = 0.5f;
    [Range(0f, 1f)] public float forcefulness = 0.5f;
    public float potionThreshold = 0.5f;

    [Header("Movement (legacy)")]
    public float followDistance = 5f;
    public float aggroRange = 15f;

    [Header("Kiting (legacy)")]
    public float kiteHealthThreshold = 0.6f;
    public float npcPullRadius = 12f;
    public float fleeHealthThreshold = 0.3f;

    [Header("Looting (legacy)")]
    public float lootRadius = 10f;

    public float GetTrait(string traitName)
    {
        for (int i = 0; i < traits.Count; i++)
            if (traits[i] != null && traits[i].definition != null &&
                traits[i].definition.name == traitName)
                return traits[i].value;
        return 0.5f;
    }

    public float GetTrait(TraitDefinition def)
    {
        for (int i = 0; i < traits.Count; i++)
            if (traits[i] != null && traits[i].definition == def)
                return traits[i].value;
        return def != null ? def.defaultValue : 0.5f;
    }
}
