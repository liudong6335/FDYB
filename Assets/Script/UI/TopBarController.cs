using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top bar HUD: game phase, timer, boss countdown, minion info.
/// Built programmatically on a shared canvas.
/// </summary>
public class TopBarController
{
    private Text phaseLabel;
    private Text timerLabel;
    private Text bossTimerLabel;
    private Text minionInfoLabel;

    public void Build(Transform parent, Color bgColor)
    {
        var bar = UIHelpers.MakePanel("TopBar", parent,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -44f), new Vector2(0f, 0f));
        bar.GetComponent<Image>().color = bgColor;

        phaseLabel = UIHelpers.MakeText("PhaseLabel", bar.transform, "Escort", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Color.yellow);
        UIHelpers.SetAnchor(phaseLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(250f, 0f));

        timerLabel = UIHelpers.MakeText("Timer", bar.transform, "00:00", 24, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        UIHelpers.SetAnchor(timerLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        bossTimerLabel = UIHelpers.MakeText("BossTimer", bar.transform, "BOSS Awake: 8:00 | Kills: 0", 13, FontStyle.Normal, TextAnchor.MiddleRight, Color.white);
        UIHelpers.SetAnchor(bossTimerLabel.rectTransform, new Vector2(1f, 0.75f), new Vector2(1f, 0.75f), new Vector2(-12f, 0f), new Vector2(-300f, 0f));

        minionInfoLabel = UIHelpers.MakeText("MinionInfo", bar.transform, "Demon Lv: Lv1 | Alive: 0/4", 13, FontStyle.Normal, TextAnchor.MiddleRight, Color.white);
        UIHelpers.SetAnchor(minionInfoLabel.rectTransform, new Vector2(1f, 0.25f), new Vector2(1f, 0.25f), new Vector2(-12f, 0f), new Vector2(-260f, 0f));
    }

    public void Refresh(GameManager gm)
    {
        if (gm == null) return;

        string phaseText = gm.CurrentPhase switch
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

        int mins = Mathf.FloorToInt(gm.GameTimer / 60f);
        int secs = Mathf.FloorToInt(gm.GameTimer % 60f);
        timerLabel.text = $"{mins:00}:{secs:00}";

        int bossMins = Mathf.FloorToInt(Mathf.Max(0, gm.BossAwakenTimer) / 60f);
        int bossSecs = Mathf.FloorToInt(Mathf.Max(0, gm.BossAwakenTimer) % 60f);
        bossTimerLabel.text = $"BOSS Awake: {bossMins}:{bossSecs:00} | Kills: {gm.DemonKillCount}";
        bossTimerLabel.color = gm.BossAwakenTimer < 120 ? Color.red : Color.white;

        minionInfoLabel.text = $"Demon Lv: Lv{gm.MinionLevel} | Alive: {gm.ActiveMinionCount}/4";
    }
}
