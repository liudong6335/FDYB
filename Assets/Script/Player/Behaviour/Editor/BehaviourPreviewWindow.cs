using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Editor window for previewing action scores without entering Play Mode.
/// Window 鈫?Behaviour Preview
/// </summary>
public class BehaviourPreviewWindow : EditorWindow
{
    private CharacterCard card;
    private Vector2 scrollPos;

    // Scenario context
    private float ctxHealth = 0.8f;
    private int ctxEnemies = 2;
    private float ctxEnemyDist = 5f;
    private float ctxNpcDist = 8f;
    private bool ctxNpcWalking = true;
    private int ctxPotions = 2;

    // Action cache
    private List<IAction> actions = new List<IAction>();

    // Map trait list index 鈫?fixed field name (for cards that still use legacy fields)
    private Dictionary<int, string> traitFieldMap = new Dictionary<int, string>();
    private bool mapBuilt = false;

    // Window size
    private const float LabelWidth = 130f;
    private const float ValueWidth = 40f;

    [MenuItem("Window/Behaviour Preview")]
    public static void ShowWindow()
    {
        var w = GetWindow<BehaviourPreviewWindow>("Behaviour Preview");
        w.minSize = new Vector2(480, 600);
    }

    private void OnEnable()
    {
        actions.Clear();
        actions.Add(new AttackAction());
        actions.Add(new FleeAction());
        actions.Add(new HealAction());
        actions.Add(new KiteAction());
        actions.Add(new LootAction());
        actions.Add(new FollowNPCAction());

        if (card == null)
            card = AssetDatabase.LoadAssetAtPath<CharacterCard>(
                "Assets/Script/Player/Behaviour/DefaultAICard.asset");
    }

    private void BuildFieldMap()
    {
        traitFieldMap.Clear();
        if (card == null) return;

        var fieldNames = new Dictionary<string, string>
        {
            { "MetaGoal_selfPreservation", "selfPreservation" },
            { "MetaGoal_victoryFocus", "victoryFocus" },
            { "Personality_aggression", "aggression" },
            { "Personality_caution", "caution" },
            { "Personality_supportiveness", "supportiveness" },
            { "Personality_independence", "independence" },
            { "Personality_greed", "greed" },
            { "Combat_preferredRange", "preferredRange" },
            { "Combat_focusFire", "focusFire" },
            { "Combat_potionThreshold", "potionThreshold" },
            { "Combat_oppression", "oppression" },
            { "Combat_forcefulness", "forcefulness" },
            { "Movement_followDistance", "followDistance" },
            { "Movement_aggroRange", "aggroRange" },
            { "Kiting_kiteHealthThreshold", "kiteHealthThreshold" },
            { "Kiting_npcPullRadius", "npcPullRadius" },
            { "Kiting_fleeHealthThreshold", "fleeHealthThreshold" },
            { "Looting_lootRadius", "lootRadius" }
        };

        for (int i = 0; i < card.traits.Count; i++)
        {
            var t = card.traits[i];
            if (t == null || t.definition == null) continue;
            string fname;
            if (fieldNames.TryGetValue(t.definition.name, out fname))
                traitFieldMap[i] = fname;
        }
        mapBuilt = true;
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // Card selector
        EditorGUI.BeginChangeCheck();
        card = (CharacterCard)EditorGUILayout.ObjectField("Character Card", card, typeof(CharacterCard), false);
        if (EditorGUI.EndChangeCheck()) mapBuilt = false;

        if (card == null)
        {
            EditorGUILayout.HelpBox("Drag a Character Card here to preview", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        if (!mapBuilt) BuildFieldMap();

        // 鈹€鈹€ Traits 鈹€鈹€
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Traits 鈥?drag to adjust, scores update live", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        Undo.RecordObject(card, "Modify Character Card");
        bool changed = false;
        var cardType = card.GetType();

        for (int i = 0; i < card.traits.Count; i++)
        {
            var t = card.traits[i];
            if (t == null || t.definition == null) continue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(t.definition.displayName, GUILayout.Width(LabelWidth));

            EditorGUI.BeginChangeCheck();
            float newVal = EditorGUILayout.Slider(t.value, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                t.value = newVal;
                // Sync the corresponding fixed field if it exists
                string fieldName;
                if (traitFieldMap.TryGetValue(i, out fieldName))
                {
                    var field = cardType.GetField(fieldName);
                    if (field != null) field.SetValue(card, newVal);
                }
                changed = true;
            }

            EditorGUILayout.LabelField(t.value.ToString("F2"), GUILayout.Width(ValueWidth));
            EditorGUILayout.EndHorizontal();
        }

        if (changed) EditorUtility.SetDirty(card);

        // 鈹€鈹€ Scenario 鈹€鈹€
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Scenario 鈥?simulate game conditions", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        ctxHealth = EditorGUILayout.Slider("Health %", ctxHealth, 0f, 1f);
        ctxEnemies = EditorGUILayout.IntSlider("Nearby Enemies", ctxEnemies, 0, 10);
        ctxEnemyDist = EditorGUILayout.Slider("Nearest Enemy (m)", ctxEnemyDist, 0f, 30f);
        ctxNpcDist = EditorGUILayout.Slider("Distance to NPC (m)", ctxNpcDist, 0f, 30f);
        ctxNpcWalking = EditorGUILayout.Toggle("NPC Is Walking", ctxNpcWalking);
        ctxPotions = EditorGUILayout.IntSlider("Potions Available", ctxPotions, 0, 10);

        // 鈹€鈹€ Scores 鈹€鈹€
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Action Scores", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        var ctx = new GameContext
        {
            healthPercent = ctxHealth,
            healthAbsolute = 1000f * ctxHealth,
            nearbyEnemyCount = ctxEnemies,
            nearestEnemyDistance = ctxEnemyDist,
            distanceToNPC = ctxNpcDist,
            npcExists = true,
            npcAlive = true,
            npcIsWalking = ctxNpcWalking,
            potionCount = ctxPotions,
            player1Exists = true,
            threatLevel = Mathf.Clamp01(ctxEnemies * 0.15f + (1f - ctxHealth) * 0.3f)
        };

        float bestScore = float.MinValue;
        string bestName = "";
        var results = new List<(string name, float score)>();

        foreach (var a in actions)
        {
            float s = a.Evaluate(card, ctx);
            results.Add((a.Name, s));
            if (s > bestScore) { bestScore = s; bestName = a.Name; }
        }

        var style = new GUIStyle(EditorStyles.label);
        style.richText = true;

        foreach (var r in results)
        {
            bool win = r.name == bestName;
            string bar = "";
            int n = Mathf.RoundToInt(r.score * 25f);
            bar = new string('鈻?, n) + new string('鈻?, 25 - n);
            string prefix = win ? "鈻?" : "  ";
            Color c = win ? Color.green : Color.white;

            var rowStyle = new GUIStyle(EditorStyles.label);
            rowStyle.normal.textColor = c;
            rowStyle.fontStyle = win ? FontStyle.Bold : FontStyle.Normal;

            EditorGUILayout.LabelField(
                $"{prefix}{r.name,-14}  {r.score:F3}  {bar}", rowStyle);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(
            $"Winner: {bestName}  ({bestScore:F3})",
            EditorStyles.boldLabel);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Open Card in Inspector", GUILayout.Height(30)))
        {
            Selection.activeObject = card;
            EditorGUIUtility.PingObject(card);
        }

        EditorGUILayout.EndScrollView();
    }
}

