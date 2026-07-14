using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public static class CharacterCardGenerator
{
    static readonly string CardDir = "Assets/Script/Player/Behaviour/Cards";
    static readonly string TraitDir = "Assets/Script/Player/Behaviour/Traits";

    [MenuItem("Tools/FitNPC/Generate All Character Cards")]
    static void GenerateAll()
    {
        var traits = LoadTraitDefinitions();
        if (traits.Count == 0) { return; }
        Directory.CreateDirectory(CardDir);
        GenPlayerCards(traits);
        GenMonsterCards(traits);
        GenNPCCards(traits);
        AssetDatabase.SaveAssets();
    }

    static Dictionary<string, TraitDefinition> LoadTraitDefinitions()
    {
        var dict = new Dictionary<string, TraitDefinition>();
        foreach (var guid in AssetDatabase.FindAssets("t:TraitDefinition", new[] { TraitDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<TraitDefinition>(path);
            if (def != null) dict[def.name] = def;
        }
        return dict;
    }

    static CharacterCard MakeCard(string name, string desc, Dictionary<string, float> vals, Dictionary<string, TraitDefinition> defs)
    {
        var card = ScriptableObject.CreateInstance<CharacterCard>();
        card.characterName = name;
        card.background = desc;
        foreach (var kv in vals)
            if (defs.TryGetValue(kv.Key, out var d))
                card.traits.Add(new TraitEntry { definition = d, value = kv.Value });
        ApplyLegacy(card, vals);
        var path = Path.Combine(CardDir, name + ".asset").Replace("\\", "/");
        AssetDatabase.CreateAsset(card, path);
        return card;
    }

    static void ApplyLegacy(CharacterCard c, Dictionary<string, float> v)
    {
        if (v.ContainsKey("Personality_aggression")) c.aggression = v["Personality_aggression"];
        if (v.ContainsKey("Personality_caution")) c.caution = v["Personality_caution"];
        if (v.ContainsKey("Personality_supportiveness")) c.supportiveness = v["Personality_supportiveness"];
        if (v.ContainsKey("Personality_independence")) c.independence = v["Personality_independence"];
        if (v.ContainsKey("Personality_greed")) c.greed = v["Personality_greed"];
        if (v.ContainsKey("MetaGoal_selfPreservation")) c.selfPreservation = v["MetaGoal_selfPreservation"];
        if (v.ContainsKey("MetaGoal_victoryFocus")) c.victoryFocus = v["MetaGoal_victoryFocus"];
        if (v.ContainsKey("Combat_preferredRange")) c.preferredRange = v["Combat_preferredRange"];
        if (v.ContainsKey("Combat_focusFire")) c.focusFire = v["Combat_focusFire"];
        if (v.ContainsKey("Combat_oppression")) c.oppression = v["Combat_oppression"];
        if (v.ContainsKey("Combat_forcefulness")) c.forcefulness = v["Combat_forcefulness"];
        if (v.ContainsKey("Combat_potionThreshold")) c.potionThreshold = v["Combat_potionThreshold"];
        if (v.ContainsKey("Movement_followDistance")) c.followDistance = v["Movement_followDistance"];
        if (v.ContainsKey("Movement_aggroRange")) c.aggroRange = v["Movement_aggroRange"];
        if (v.ContainsKey("Kiting_kiteHealthThreshold")) c.kiteHealthThreshold = v["Kiting_kiteHealthThreshold"];
        if (v.ContainsKey("Kiting_npcPullRadius")) c.npcPullRadius = v["Kiting_npcPullRadius"];
        if (v.ContainsKey("Kiting_fleeHealthThreshold")) c.fleeHealthThreshold = v["Kiting_fleeHealthThreshold"];
        if (v.ContainsKey("Looting_lootRadius")) c.lootRadius = v["Looting_lootRadius"];
    }

    static Dictionary<string, float> V(params float[] vals)
    {
        var keys = new[] { "Personality_aggression", "Personality_caution", "Personality_supportiveness",
            "Personality_independence", "Personality_greed", "MetaGoal_selfPreservation", "MetaGoal_victoryFocus",
            "Combat_preferredRange", "Combat_focusFire", "Combat_oppression", "Combat_forcefulness",
            "Combat_potionThreshold", "Movement_followDistance", "Movement_aggroRange",
            "Kiting_kiteHealthThreshold", "Kiting_npcPullRadius", "Kiting_fleeHealthThreshold", "Looting_lootRadius" };
        var d = new Dictionary<string, float>();
        for (int i = 0; i < keys.Length && i < vals.Length; i++) d[keys[i]] = vals[i];
        return d;
    }

    static void GenPlayerCards(Dictionary<string, TraitDefinition> t)
    {
        MakeCard("Guardian","Loyal protector,stays close,never abandons.",V(0.7f,0.3f,0.9f,0.2f,0.2f,0.3f,0.8f,0.3f,0.8f,0.6f,0.7f,0.35f,3f,12f,0.4f,10f,0.15f,5f),t);
        MakeCard("Veteran","Seasoned fighter,picks battles carefully.",V(0.5f,0.6f,0.6f,0.6f,0.4f,0.7f,0.6f,0.5f,0.6f,0.5f,0.5f,0.5f,4f,14f,0.5f,10f,0.25f,8f),t);
        MakeCard("Rookie","Eager newcomer,charges in recklessly.",V(0.8f,0.2f,0.5f,0.4f,0.5f,0.2f,0.7f,0.3f,0.5f,0.7f,0.8f,0.25f,2f,18f,0.2f,8f,0.1f,6f),t);
        MakeCard("Hunter","Resourceful scavenger,kites and loots.",V(0.6f,0.5f,0.3f,0.8f,0.9f,0.5f,0.4f,0.7f,0.6f,0.4f,0.5f,0.5f,6f,16f,0.7f,12f,0.3f,15f),t);
        MakeCard("Scholar","Timid bookworm,avoids danger,heals proactively.",V(0.2f,0.8f,0.7f,0.2f,0.2f,0.9f,0.3f,0.7f,0.3f,0.2f,0.2f,0.6f,2f,8f,0.5f,6f,0.4f,3f),t);
    }

    static void GenMonsterCards(Dictionary<string, TraitDefinition> t)
    {
        MakeCard("Demon_Berserker","Relentless,never retreats.",V(0.95f,0.1f,0.3f,0.7f,0.3f,0.15f,0.8f,0.2f,0.7f,0.8f,0.9f,0.5f,5f,20f,0.3f,12f,0.05f,10f),t);
        MakeCard("Demon_Cunning","Tactical,retreats to regenerate.",V(0.6f,0.6f,0.5f,0.7f,0.4f,0.7f,0.5f,0.6f,0.5f,0.4f,0.5f,0.5f,5f,16f,0.4f,12f,0.35f,10f),t);
        MakeCard("Demon_Coward","Skittish,strikes once then flees.",V(0.3f,0.85f,0.2f,0.9f,0.3f,0.9f,0.2f,0.7f,0.2f,0.2f,0.2f,0.5f,5f,10f,0.3f,8f,0.5f,10f),t);
    }

    static void GenNPCCards(Dictionary<string, TraitDefinition> t)
    {
        MakeCard("NPC_Runner","Focused on destination,rarely pauses.",V(0.7f,0.2f,0.3f,0.8f,0.2f,0.3f,0.9f,0.5f,0.5f,0.5f,0.5f,0.5f,5f,15f,0.5f,12f,0.3f,10f),t);
        MakeCard("NPC_Steady","Balanced escort,heals when needed.",V(0.4f,0.6f,0.7f,0.4f,0.3f,0.6f,0.6f,0.5f,0.5f,0.5f,0.4f,0.5f,5f,15f,0.5f,12f,0.3f,10f),t);
        MakeCard("NPC_Nervous","Jumpy escort,pauses and heals constantly.",V(0.15f,0.9f,0.6f,0.15f,0.2f,0.85f,0.3f,0.5f,0.5f,0.5f,0.2f,0.5f,5f,15f,0.5f,12f,0.3f,10f),t);
    }
}
