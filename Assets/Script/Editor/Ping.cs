/*
 * ============================================================
 *  Ping  -  编辑器工具（测试用）
 * ============================================================
 *
 * 【功能】
 *   在 Unity 编辑器的顶部菜单添加了一个 "Tools/Ping" 按钮，
 *   点击后在控制台输出 "Ping"，用于测试编辑器扩展功能。
 *
 * 【挂载对象】
 *   不需要挂载，通过菜单调用
 *
 * 【使用方法】
 *   在 Unity 编辑器顶部菜单点击 Tools → Ping
 */
using UnityEngine;
using UnityEditor;

public static class EmptyEditorScript
{
    [MenuItem("Tools/Ping")]
    static void Ping() { Debug.Log("Ping"); }
}

