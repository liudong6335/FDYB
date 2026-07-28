using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top bar HUD: game phase and timer.
/// Built programmatically on a shared canvas.
/// </summary>
public class TopBarController
{
    private Text phaseLabel;
    private Text timerLabel;

    public void Build(Transform parent, Color bgColor)
    {
        var bar = UIHelpers.MakePanel("TopBar", parent,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -44f), new Vector2(0f, 0f));
        bar.GetComponent<Image>().color = bgColor;

        phaseLabel = UIHelpers.MakeText("PhaseLabel", bar.transform, "Escort", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Color.yellow);
        UIHelpers.SetAnchor(phaseLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(250f, 0f));

        timerLabel = UIHelpers.MakeText("Timer", bar.transform, "00:00", 24, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        UIHelpers.SetAnchor(timerLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }

    public void Refresh(EscortPhaseController escort)
    {
        if (escort == null) return;

        string phaseText = escort.CurrentPhase switch
        {
            GamePhase.Menu => "Menu",
            GamePhase.Escort => "Phase 1: Escort",
            GamePhase.Exploration => "Phase 2: Explore",
            GamePhase.BossBattle => "BOSS Fight",
            GamePhase.Victory => "Victory!",
            GamePhase.GameOver => "Defeat",
            _ => ""
        };
        phaseLabel.text = phaseText;

        int mins = Mathf.FloorToInt(escort.GameTimer / 60f);
        int secs = Mathf.FloorToInt(escort.GameTimer % 60f);
        timerLabel.text = $"{mins:00}:{secs:00}";
    }
}
