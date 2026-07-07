using UnityEngine;

/// <summary>
/// 行为动作接口 — 每个动作独立评分和执行的策略单元。
/// Evaluate 返回此动作在当前情境下的拟合度 (0~1)。
/// Execute 通过 owner GameObject 获取所需组件，不绑定具体类型。
/// </summary>
public interface IAction
{
    string Name { get; }
    float Evaluate(CharacterCard card, GameContext ctx);
    void Execute(GameObject owner, GameContext ctx, CharacterCard card);
}
