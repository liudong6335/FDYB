using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor.SceneManagement;

public class SetupSpeedBoostButton
{
    [MenuItem("Tools/Setup Speed Boost Button")]
    static void Setup()
    {
        // 1. Find button
        var btnGo = GameObject.Find("role_speed_Button");
        if (btnGo == null)
        {
            EditorUtility.DisplayDialog("Error", "Cannot find role_speed_Button!", "OK");
            return;
        }

        // 2. Add SpeedBoostButton component
        if (btnGo.GetComponent<SpeedBoostButton>() == null)
            btnGo.AddComponent<SpeedBoostButton>();

        // 3. Fix EventSystem: StandaloneInputModule -> InputSystemUIInputModule
        var es = GameObject.FindObjectOfType<EventSystem>();
        if (es != null)
        {
            var oldModule = es.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                es.gameObject.AddComponent<InputSystemUIInputModule>();
                Object.DestroyImmediate(oldModule);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Done！", "SpeedBoostButton + EventSystem 已配置完成！", "OK");
    }
}
