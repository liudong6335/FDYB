# Player 与 AI 开发索引

> 定位：统一玩家、AI 玩家和玩家侧通用能力的文档入口。
> 原则：AI 玩家是玩家系统的子模块，不复制 Boss、Common 或 Endgame 的规则。

## 一、职责范围

| 模块 | 职责 | 权威文档 |
| --- | --- | --- |
| Player | 输入、移动、生命、战斗、装备、技能、背包、淘汰 | 《玩家机制设计》及相关系统文档 |
| AI Player | 目标选择、路线、战斗行为、结盟申请、翻脸、感知 | 《AI 机制开发设计文档》《AI 决策落地执行规格》 |
| PlayerView | AI 可见的对手和市场公开信息 | 《AI 机制开发设计文档》 |
| Player Memory | 信任、背叛、短期记忆、路线锁 | 《AI 决策落地执行规格》 |
| 玩家死亡 | 回魂、永久淘汰和后续状态 | 《玩家机制设计》 |

## 二、状态与接口

AI 玩家可复用：

- Common 的 EntityMemory、Health、CombatState、Damage。
- 通用技能系统和背包接口。
- 观测 Provider，不直接读取对手私有状态。

AI 玩家不负责：

- Boss 内部快照和仇恨公式。
- Boss 死亡掉落和恶魔之心。
- Endgame 胜负结算。
- Common 的通用伤害和状态效果框架。

## 三、感知与快照

```text
PlayerView:
    hpRatio
    powerRatio
    bossProgress
    killDeltaSinceRefresh
    towerStockVisible
    hasRelic
    allianceState
    allianceVersion
```

规则：

- 低频快照只提供战略输入。
- 被攻击、结盟申请、死亡、Boss 死亡等事件立即响应。
- 关键生命阈值、结盟变化和死亡可强制刷新。
- AI 只读取公开或允许感知的数据。

## 四、AI 玩家归属

AI 玩家的完整行为规格继续保存在：

- 《AI 机制开发设计文档》
- 《AI 决策落地执行规格》
- 《AI 数值测试与验收文档》

本文档只定义它们与 Player/Common 的归属关系。

## 五、相关文档

- 《系统总览与文档索引》
- 《玩家机制设计》
- 《Common 基础系统》
- 《AI 机制开发设计文档》
- 《AI 决策落地执行规格》
- 《AI 数值测试与验收文档》
