# Common 基础系统

> 定位：为 Boss、怪物、玩家和 AI 提供可复用底层能力。
> 原则：Common 只保存基础设施和共享契约，不包含 Boss、玩家或终局的具体决策。

## 一、职责范围

| 模块 | 职责 | 明确不负责 |
| --- | --- | --- |
| `EntityMemory` | 伤害历史、目标快照、决策快照、命名计时器、位置记忆、关系版本 | 选择目标、执行技能、决定行为 |
| Snapshot | 5 秒目标、5 秒求生、10 秒策略、死亡和阈值强制刷新 | 决定哪些阈值业务上有效 |
| `Health` | 当前生命、受伤、死亡、回血执行、回血上限 | 判断是否脱战、是否应该回血 |
| `CombatState` | 进入/退出战斗、战斗活动时间、脱战延迟 | 具体 AI 行为 |
| `Damage` | 统一伤害入口、防御、护盾、减伤、伤害来源和预览 | 圣物归属、Boss 目标选择 |
| `SkillCard` | 技能效果、目标、施法、范围、表现引用 | CD、优先级、槽位状态 |
| `SkillSlotBinding` | CD、冷却开始时机、触发策略、优先级、斩杀参数 | 技能具体效果 |
| `SkillSlotRuntime` | 单槽冷却、充能、Ready/Cooldown/Disabled | 槽位选择策略 |
| EventBus | 发布和订阅稳定事件 | 事件业务处理 |
| Clock / Random | 游戏时间、确定性随机源 | 业务阈值 |
| LootDropService | 掉落请求、物品生成、掉落上下文 | 恶魔之心拾取和交付 |

## 二、共享快照

通用快照字段至少包括：

```text
snapshotVersion
targetSnapshotVersion
allianceVersion
revengeHateVersion
gameTime
lastSnapshotRefreshReason
sourceEntityId
```

刷新规则：

- 普通周期刷新由各系统定义。
- 死亡、求生阈值跨线和结盟状态变化按对应事件立即刷新。
- 历史快照只追加，不回写。
- 同一原因只刷新一次，日志记录版本和触发实体。

## 三、战斗状态与回血边界

```text
CombatState:
    isInCombat
    lastCombatActivityTime
    combatExitDelay

Health:
    currentHp
    maxHp
    isAlive
    canRegen
    regenRate

IHealthService:
    EnterAutoRegen(entityId, reason, profileId)
    ExitAutoRegen(entityId, reason)
```

规则：

- 伤害造成或受到时更新 `CombatState`。
- `Health` 负责执行回血，`CombatState` 负责判断是否脱战。
- Boss、玩家和怪物不自行实现回血状态机。
- 环境伤害是否计入战斗由伤害来源配置决定。
- `IHealthService` 只接收回血模式请求，不向调用方暴露 `currentHp` 修改入口。
- `Survive` 自动回血使用独立的 `surviveRegenProfile`，不依赖 `CombatState.isInCombat`，默认不因受击完全停止；该规则只服务 Boss Survive，不改变玩家普通自然回血规则。
- Survive 主动回血在退出 Survive、进入 Rage、死亡或 Boss 行为结束时停止。

## 四、技能通用结构

```text
SkillCard:
    skillId
    targetMode
    castTime
    recoveryTime
    range
    effectRefs

SkillSlotBinding:
    slotId
    skillCard
    skillPoolId
    cooldown
    cooldownStartPolicy
    priority
    triggerPolicy
    maxCharges
```

技能系统负责：

- 单主动施放和施放锁。
- 前摇、提交、恢复、打断。
- 冷却路径和充能。
- 目标解析接口。
- 效果提交。

Boss 只负责：

- 请求哪个 `slotId`。
- 提供状态原因。
- 绑定 Boss 专属 SkillCard 和槽位策略。

## 五、事件与掉落

Common 提供稳定事件端口，不拥有 Endgame 规则：

```text
BossDiedEvent
PlayerEliminatedEvent
AllianceStateChangedEvent
AllianceRevengeAppliedEvent
DemonHeartDroppedEvent
```

掉落由 `ILootDropService` 执行，物品生命周期由 Item 系统负责，胜负由 Endgame 负责。

## 六、相关文档

- 《系统总览与文档索引》
- 《Boss功能开发清单》第 8.4 节
- 《技能系统设计》
- 《Player 与 AI 开发索引》
- 《圣物系统设计》
- 《Endgame 终局系统》
