using UnityEngine;

/// <summary>
/// 琛屼负鍔ㄤ綔鎺ュ彛 鈥?姣忎釜鍔ㄤ綔鐙珛璇勫垎鍜屾墽琛岀殑绛栫暐鍗曞厓銆?/// </summary>
public interface IAction
{
    string Name { get; }
    float Evaluate(CharacterCard card, GameContext ctx);
    void Execute(PlayerMove player, PlayerCombat combat, GameContext ctx, CharacterCard card);
}

