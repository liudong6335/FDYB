using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Abstract base class for all behaviour-driven AI characters.
/// Manages the tick loop, action scoring, and debug data.
/// Subclasses implement BuildContext() and CanTick() for their specific role.
/// </summary>
public abstract class BehaviourModelBase : MonoBehaviour
{
    [Header("Character")]
    [SerializeField] protected CharacterCard card;

    [Header("Tick")]
    [SerializeField] protected float tickInterval = 0.3f;

    protected List<IAction> actions = new List<IAction>();
    protected float nextTickTime;
    protected List<TickResult> lastTickScores = new List<TickResult>();

    protected abstract bool CanTick();
    protected abstract GameContext BuildContext();
    protected abstract void RegisterActions();

    protected virtual void Awake()
    {
        RegisterActions();
    }

    private void Update()
    {
        if (!CanTick()) return;
        if (Time.time < nextTickTime) return;
        nextTickTime = Time.time + tickInterval;

        var ctx = BuildContext();

        float bestScore = float.MinValue;
        IAction bestAction = null;
        lastTickScores.Clear();

        foreach (var action in actions)
        {
            float score = action.Evaluate(card, ctx);
            lastTickScores.Add(new TickResult { actionName = action.Name, score = score });
            if (score > bestScore) { bestScore = score; bestAction = action; }
        }
        foreach (var t in lastTickScores) t.isWinner = t.score >= bestScore;

        bestAction?.Execute(gameObject, ctx, card);
    }

    public List<TickResult> GetLastTickData() => lastTickScores;
    public string GetCardName() => card != null ? card.characterName : "Unset";
}
