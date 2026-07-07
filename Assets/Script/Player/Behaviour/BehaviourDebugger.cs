using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 行为调试器 — 运行时显示 AI 每个动作的评分和最终选择。
/// 附加到 BehaviourModel 同一对象上，或场景中任意位置。
/// 会在屏幕上绘制半透明覆盖层显示评分数据。
/// </summary>
public class BehaviourDebugger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BehaviourModel targetModel;
    [SerializeField] private KeyCode toggleKey = KeyCode.F8;

    [Header("Display")]
    [SerializeField] private bool showOnStart = true;
    [SerializeField] private int fontSize = 14;
    [SerializeField] private float bgAlpha = 0.7f;

    private bool visible;
    private GUIStyle labelStyle;
    private GUIStyle titleStyle;
    private GUIStyle winnerStyle;
    private Texture2D bgTex;
    private float nextLogTime;

    private void Awake()
    {
        if (targetModel == null)
            targetModel = GetComponent<BehaviourModel>();
        if (targetModel == null)
            targetModel = FindFirstObjectByType<BehaviourModel>();

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

        float x = 20f, y = 60f;
        float w = 380f;
        float lineH = 22f;
        float headerH = 30f;
        float totalH = headerH + data.Count * lineH + 20f;

        // Background
        GUI.DrawTexture(new Rect(x - 10, y - 10, w, totalH), bgTex);

        // Title
        GUI.Label(new Rect(x, y, w, headerH), "Behaviour Model — Action Scores", titleStyle);
        y += headerH;

        // Find the winner
        string winner = "";
        float bestScore = float.MinValue;
        foreach (var d in data)
            if (d.score > bestScore) { bestScore = d.score; winner = d.name; }

        // Each action row
        foreach (var d in data)
        {
            bool isWinner = d.name == winner;
            var style = isWinner ? winnerStyle : labelStyle;

            string bar = "";
            int barLen = Mathf.RoundToInt(d.score * 20f);
            for (int i = 0; i < barLen; i++) bar += "█";
            for (int i = barLen; i < 20; i++) bar += "░";

            string prefix = isWinner ? "► " : "  ";
            string scoreStr = d.score.ToString("F3").PadRight(6);

            GUI.Label(new Rect(x, y, w, lineH),
                $"{prefix}{d.name,-14} {scoreStr} {bar}", style);
            y += lineH;
        }

        // Footer
        GUI.Label(new Rect(x, y, w, 20f),
            $"F8: toggle  |  Card: {targetModel.GetCardName()}", labelStyle);
    }
}
