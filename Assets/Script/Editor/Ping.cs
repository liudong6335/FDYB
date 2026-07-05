using UnityEngine;
using UnityEditor;

public static class EmptyEditorScript
{
    [MenuItem("Tools/Ping")]
    static void Ping() { Debug.Log("Ping"); }
}
