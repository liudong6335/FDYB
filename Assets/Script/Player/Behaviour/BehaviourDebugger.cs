using System.Collections.Generic;
using UnityEngine;

public class BehaviourDebugger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BehaviourModelBase targetModel;
    [SerializeField] private KeyCode toggleKey = KeyCode.F8;

    [Header("Display")]
    [SerializeField] private bool showOnStart = true;
    [SerializeField] private int fontSize = 14;
    [SerializeField] private float bgAlpha = 0.7f;

    [Header("Panel Position")]
    [SerializeField] private float panelX = 20f;
    [SerializeField] private float panelY = 60f;
    [SerializeField] private float panelWidth = 380f;

    private bool visible;
    private GUIStyle labelStyle;
    private GUIStyle titleStyle;
    private GUIStyle winnerStyle;
    private Texture2D bgTex;

    private void Awake()
    {
        if (targetModel == null)
            targetModel = GetComponent<BehaviourModelBase>();
        if (targetModel == null)
            targetModel = FindFirstObjectByType<BehaviourModelBase>();
        visible = showOnStart;
        SetupStyles();
    }

    private void SetupStyles()
    {
        bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, bgAlpha));
        bgTex.Apply();

        labelStyle = new GUIStyle();
        labelStyle.fontSize = fontSize;
        labelStyle.normal.textColor = Color.white;
        labelStyle.padding = new RectOffset(4, 4, 1, 1);

        titleStyle = new GUIStyle(labelStyle);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.fontSize = fontSize + 2;
        titleStyle.normal.textColor = Color.yellow;

        winnerStyle = new GUIStyle(labelStyle);
        winnerStyle.fontStyle = FontStyle.Bold;
        winnerStyle.normal.textColor = Color.green;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            visible = !visible;
    }

    private void OnGUI()
    {
        if (!visible || targetModel == null) return;

        var data = targetModel.GetLastTickData();
        if (data == null || data.Count == 0) return;

        float x = panelX, y = panelY;
        float w = panelWidth;
        float lineH = 22f;
        float headerH = 30f;
        float totalH = headerH + data.Count * lineH + 20f;

        GUI.DrawTexture(new Rect(x - 10, y - 10, w, totalH), bgTex);

        string playerLabel = gameObject.name;
        GUI.Label(new Rect(x, y, w, headerH),
            "Behaviour - " + playerLabel, titleStyle);
        y += headerH;

        foreach (var d in data)
        {
            var style = d.isWinner ? winnerStyle : labelStyle;
            string bar = "";
            int barLen = Mathf.RoundToInt(d.score * 20f);
            for (int i = 0; i < barLen; i++) bar += "#";
            for (int i = barLen; i < 20; i++) bar += "-";
            string prefix = d.isWinner ? "> " : "  ";
            GUI.Label(new Rect(x, y, w, lineH),
                prefix + d.actionName + " " + d.score.ToString("F3") + " " + bar, style);
            y += lineH;
        }

        GUI.Label(new Rect(x, y, w, 20f),
            "F8: toggle | " + targetModel.GetCardName(), labelStyle);
    }
}
