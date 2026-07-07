using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time scene fix: adds PlayerBehaviourModel to Player02/03.
/// Accessible via Tools → Fix Scene Components
/// </summary>
public static class SceneFixer
{
    [MenuItem("Tools/Fix Scene Components")]
    public static void FixScene()
    {
        int fixedCount = 0;
        foreach (var name in new[] { "Player02", "Player03" })
        {
            var go = GameObject.Find(name);
            if (go == null) { Debug.Log($"SKIP {name}: not found"); continue; }

            // Remove missing script slots
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

            // Add PlayerBehaviourModel
            var bm = go.GetComponent<PlayerBehaviourModel>();
            if (bm == null) bm = go.AddComponent<PlayerBehaviourModel>();

            // Assign card
            string cardPath = name == "Player02"
                ? "Assets/Script/Player/Behaviour/DefaultAICard.asset"
                : "Assets/Script/Player/Behaviour/BerserkerCard.asset";
            var card = AssetDatabase.LoadAssetAtPath<CharacterCard>(cardPath);
            if (card != null)
            {
                var f = typeof(BehaviourModelBase).GetField("card",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) f.SetValue(bm, card);
            }

            // Link debugger
            var dbg = go.GetComponent<BehaviourDebugger>();
            if (dbg != null)
            {
                var f = typeof(BehaviourDebugger).GetField("targetModel",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (f != null) f.SetValue(dbg, bm);
            }

            fixedCount++;
            Debug.Log($"{name}: fixed");
        }

        Debug.Log($"Scene fixed: {fixedCount} players updated");
    }
}
