# 《分道扬镳》AI 玩家功能开发文档

> 状态：玩法验证 AI 开发基线
> 用途：第一部分供人快速理解，第二部分供 AI 按任务实施
> 关联文档：《游戏机制总览》《核心机制设计_囚徒困境引擎》《Boss功能开发清单》《Boss属性与行为设计》《Boss战设计_三国演义式三方博弈》《结盟机制设计》《玩家机制设计》《AI决策落地执行规格》《AI数值测试与验收文档》

## 目录

- 第一部分：人读版
- 第二部分：AI 开发文档

---

# 第一部分：人读版

## 一、开发目标

建立一套可控、可复现、可观察的 AI 玩家，用于验证完整玩法：

```text
探索与发育
  → 攻击怪物和对手
  → Boss 苏醒
  → 追击 / 撤退 / 治疗 / 释放技能
  → 玩家结盟 / 与 Boss 结盟
  → 翻脸与追杀
  → 击杀 Boss
  → 争夺并交付恶魔之心
```

本阶段只要求 AI 能验证机制，不要求达到真人高手水平。

## 二、Demo 范围

### 2.1 本期开发

- 读取公开游戏状态。
- 选择移动、攻击、治疗和撤退目标。
- 攻击普通怪物、对手和 Boss。
- 自动使用技能。
- 发起、接受、拒绝和终止结盟。
- 主动翻脸和报复。
- 开箱、拾取物品、放置已准备塔。
- Boss 苏醒前执行回魂契约。
- 结构化记录每次决策和动作。

### 2.2 本期暂不开发

- 机器学习。
- 跨局长期记忆。
- 完整人类操作仿真。
- 复杂地图探索算法。
- 完整经济最优解。
- 网络同步和断线重连。
- 正式 AI 语音或聊天。

## 三、AI 的角色定位

AI 不是正式产品级对手，而是机制验证工具。它必须：

- 可重复：相同 seed 和初始状态得到相同结果。
- 可解释：每次决策都能看到输入和得分。
- 可控：可以锁定为克制、激进、结盟或背叛策略。
- 可替换：后续可以换成人机模式或正式 AI。

## 四、AI 验证等级

### 4.1 Level 0：DummyAI

- 不移动。
- 不攻击。
- 只承受伤害。
- 用于测试 Boss 仇恨、普攻和技能。

### 4.2 Level 1：CombatAI

- 自动选择 Boss 或怪物。
- 自动追击到攻击范围。
- 自动释放普攻和技能。
- 低血自动撤退和治疗。
- 用于验证 Boss 战斗闭环。

### 4.3 Level 2：ValidationAI

- 包含 CombatAI 全部行为。
- 会攻击对手。
- 会开箱、拾取和放置塔。
- 会发起和接受结盟。
- 会翻脸、报复和争夺恶魔之心。
- 用于验证完整对局。

### 4.4 Level 3：UtilityAI

- 在现有 `PlayerBehaviourModel + CharacterCard + IAction` 上扩展。
- 支持人格、路线和策略偏好。
- 用于后续数值调试和正式人机模式。

## 五、AI 基本循环

```text
读取公开信息
  → 更新短期记忆
  → 判断生存 / 事件 / 终局 / 战斗 / 资源
  → 生成一个 AiDecision
  → 交给 AiActionExecutor
  → 调用现有移动、攻击、技能、结盟和物品接口
  → 记录决策、动作和结果
```

AI 不直接修改：

- 生命值。
- 技能冷却。
- 仇恨分数。
- 结盟状态。
- 掉落和背包。

AI 只能调用公开接口发出行动请求。

## 六、主要行为

### 6.1 感知与记忆

- 自己状态。
- 对手公开状态。
- Boss 公开状态。
- 当前时间、海平面和终局状态。
- 最近受到伤害、最近翻脸、最近结盟。
- Boss 进度、状态和强化层。

AI 不得读取对手隐藏背包、内部路线和真实意图。

### 6.2 移动

支持：

- 追击敌人。
- 靠近 Boss。
- 远离敌人。
- 撤退到安全区或治疗点。
- 前往宝箱、掉落物或任务目标。
- 前往恶魔之心。
- 前往结盟对象附近。

AI 只设置目的地，不直接修改位置。

### 6.3 战斗

目标分为：

- Boss。
- 对手玩家。
- 普通怪物。
- 恶魔之心争夺者。

目标评分至少考虑：

- 当前生命。
- 距离。
- 是否威胁自己。
- 是否正在拾取恶魔之心。
- 击杀难度。
- 对 Boss 进度的公共代价。

### 6.4 生存

- 低血时治疗或撤退。
- 被多人或高强度攻击时脱离集火。
- 结盟可用时尝试结盟。
- 生存行为优先于普通攻击和资源获取。

### 6.5 技能

- 按优先级和冷却选择技能。
- 目标可击杀时优先释放斩杀技能。
- 生存技能优先于普通输出技能。
- 结盟期间禁止攻击盟友。
- 技能释放失败后等待冷却和下次决策周期。

### 6.6 资源与发育

- 开箱。
- 拾取掉落。
- 选择高价值目标。
- 在安全窗口放置准备好的塔。
- 结盟期间不放置塔。

### 6.7 结盟

支持：

- 玩家 + 玩家结盟。
- 玩家 + Boss 结盟。
- 接受和拒绝结盟申请。
- 结盟期间合作。
- 到期或主动翻脸。
- 翻脸后报复。

主动申请限制：

- 玩家主动申请与 Boss 结盟时，生命比例不得低于 30%；Boss 仇恨不限制申请或接受。
- Boss 主动申请冷却为 60 秒，玩家间主动申请冷却为 120 秒；均从有效申请提交时开始。
- 接受或拒绝他人申请不触发自己的主动申请冷却；无效提交不进入冷却。
- 玩家间结盟不设每局固定次数，由 120 秒冷却限制。
- 玩家间申请被拒绝或超时后，双方 5 秒内不能再次互相申请。
- Boss + 玩家结盟期间以及结束后的 10 秒内，玩家间结盟申请和接受均禁止。
- 所有冷却和封锁使用游戏时间。
- 主动申请资格只由申请者依据自身状态判断；目标角色只处理接受或拒绝，不重复校验发起资格。
- 有效申请提交后进入 3 秒等待期：申请者及归属召唤物、塔停止伤害目标，目标保持完全自由。
- 申请者主动攻击会取消申请；目标主动攻击申请者，或目标归属单位累计造成超过申请者最大生命值 5% 的实际伤害，视为拒绝。

### 6.8 死亡与终局

- Boss 苏醒前死亡时，自动发起回魂申请。
- 作为存活方是否同意回魂，按信任分和宝石条件决定。
- Boss 苏醒后死亡按永久淘汰处理。
- 恶魔之心出现后，争夺、打断、拾取和交付。

## 七、里程碑

| 里程碑 | 内容 | 完成结果 |
| --- | --- | --- |
| A1 | AIView 与调试输出 | 能打印全部公开状态和决策输入 |
| A2 | CombatAI | 能攻击 Boss、追击、撤退和治疗 |
| A3 | 技能与生存 | 能自动释放技能并处理低血 |
| A4 | ValidationAI | 能开箱、攻击对手和争夺目标 |
| A5 | 结盟与终局 | 能结盟、翻脸、抢心并完成对局 |
| A6 | 确定性回放 | 固定 seed 可复现完整对局 |

## 八、验收目标

AI 完成时，必须能够重复完成：

- AI 对 AI 跑完整 Boss 战。
- AI 能击杀至少一种怪物。
- AI 能对 Boss 造成稳定伤害。
- AI 能识别低血并撤退或治疗。
- AI 能成功释放至少一个技能。
- AI 能在满足条件时结盟。
- AI 能翻脸并产生惩罚仇恨。
- AI 能拾取并交付恶魔之心。
- 相同 seed 得到相同 Action 序列。

---

# 第二部分：AI 开发文档

## 九、AI 执行规则

1. 按任务编号顺序开发，不一次性实现全部行为。
2. AI 只能读取公开信息。
3. AI 只输出 `AiDecision`，不得直接修改游戏状态。
4. 所有行动通过公开游戏接口执行。
5. 决策周期固定，建议 `0.2s`，可配置。
6. 所有随机数由 seed 派生。
7. 所有评分、阈值和动作参数外置。
8. 生存和事件响应可以打断普通决策周期。
9. 决策日志必须记录候选动作、得分和最终原因。
10. 相同 seed、输入和初始状态下结果必须可复现。

## 十、复用现有代码

| 现有模块 | 用途 |
| --- | --- |
| `PlayerMove` | 移动执行 |
| `PlayerCombat` | 选目标、追击和普攻 |
| `PlayerHealth` / `Health` | 生命、治疗和死亡 |
| `PlayerInputController` | 仅真玩家输入，AI 模式下禁用 |
| `PlayerBehaviourModel` | 行为模型宿主和现有动作评分框架 |
| `CharacterCard` | 人格和路线参数 |
| `GameContext` | 现有环境快照 |
| `EntityMemory` | 通用记忆、快照和计时 |
| `IAction` | 现有动作接口 |

当前不另起一套与 `PlayerBehaviourModel` 完全平行的 AI 框架。

## 十一、目录与脚本职责

```text
Assets/Scripts/AI/
├─ AiPlayerController.cs
├─ AiPlayerDefinition.cs
├─ AiViewBuilder.cs
├─ AiDecisionMaker.cs
├─ AiActionExecutor.cs
├─ AiMemoryState.cs
└─ AiDebugPanel.cs
```

| 脚本 | 核心职责 | 主要入口 | 明确不负责 |
| --- | --- | --- | --- |
| `AiPlayerController.cs` | AI 生命周期、模块初始化、Tick 和当前动作状态 | `Initialize`、`Tick`、`ResetAI` | 评分公式、移动执行、伤害执行 |
| `AiPlayerDefinition.cs` | 决策周期、阈值、权重、技能策略和行为开关 | ScriptableObject 字段 | 运行时状态 |
| `AiViewBuilder.cs` | 读取公开状态并构建 `AiView` | `BuildView` | 做决策、修改状态 |
| `AiDecisionMaker.cs` | 纯决策函数；输出 `AiDecision` | `Decide` | 不移动、不攻击、不放技能 |
| `AiActionExecutor.cs` | 将 `AiDecision` 映射到现有移动、战斗、技能、结盟和物品接口 | `Execute` | 不重新评分 |
| `AiMemoryState.cs` | 信任分、路线、最近结盟、最近受伤和趋势采样 | `Read`、`Write` | 不执行行为 |
| `AiDebugPanel.cs` | 显示决策输入、候选得分、最终动作和失败原因 | 调试命令、HUD | 正式战斗决策 |

## 十二、数据契约

### 12.1 AiView

```text
self
opponentView
bossView
worldView
memoryView
```

### 12.2 SelfState

```text
entityId
hp
hpMax
hpRatio
power
bossHateTotal
selfRelicHate
alivePlayerCount
position
hasRelic
relicType
skills
skillCooldowns
towerStock
towerPlaced
inventorySummary
allianceState
allianceTimer
hasPendingAllianceRequest
allianceRequestRole
allianceRequestTargetId
allianceRequestRestrictionUntil
bossAllianceRequestCooldownUntil
playerAllianceRequestCooldownUntil
bossAlliancePvpBlockedUntil
playerAllianceNegotiationBlockedUntil
canRequestRevive
```

### 12.3 OpponentView

```text
hpRatioBand
powerRatio
position
visibleTowerCount
hasRelic
allianceState
isPickingHeart
lastKnownPosition
```

### 12.4 BossView

```text
awaken
state
phaseState
hpRatio
currentTarget
enrageLayer
allianceWith
progress
```

### 12.5 WorldView

```text
gameTime
seaLevel
safeArea
heartState
heartPosition
objectives
```

### 12.6 AiMemoryState

```text
trustScore
currentRoute
routeHoldUntil
recentlyBetrayedTimer
recentlyHitByBossTimer
recentDamage
progressSamples
powerGap
lastAllianceEndTime
```

### 12.7 AiDecision

```text
action
primaryTarget
moveDestination
skillId
allianceTarget
objective
score
reason
```

## 十三、决策优先级

```text
1. 对局已结束
2. AI 永久淘汰
3. 生存事件
4. 立即事件响应
5. 恶魔之心终局
6. Boss 战
7. 玩家对抗
8. 资源与发育
9. 默认探索
```

规则：

- 高优先级命中后立即返回。
- 事件响应不走周期，可以打断普通行为。
- 事件响应只改变决策输入，不由 View 层直接执行动作。

## 十四、行为模块

### 14.1 生存

```text
IF hpRatio < survivalHp:
    优先治疗
    否则撤退
    否则申请结盟
```

### 14.2 战斗

```text
+ 威胁度
+ 击杀收益
+ 目标价值
+ 距离分
- 公共 Boss 代价
- 自身风险
```

### 14.3 技能

```text
事件：可击杀目标
  → 立即尝试斩杀技能

普通周期：
  → 生存技能
  → 控制技能
  → 输出技能
  → 默认普攻
```

### 14.4 结盟

接受玩家结盟：

```text
+ isUnderdog
+ trustScore
+ 当前低血
- 最近被该玩家攻击
```

申请与 Boss 结盟：

```text
Boss 已苏醒
AND 场上恰好 2 名玩家存活
AND Boss 非 Rage
AND hpRatio >= 0.30
AND Boss 主动申请冷却已结束
AND 当前没有结盟或待确认申请
```

申请玩家间结盟：

```text
Boss 已苏醒
AND 双方均存活
AND 当前没有结盟或待确认申请
AND 不在 Boss + 玩家结盟期间
AND Boss 结盟结束后的 10 秒封锁已结束
AND 玩家间主动申请冷却已结束
AND 双方 5 秒协商锁已结束
```

接受回魂：

```text
+ 双方均有防具宝石
+ trustScore >= cfg_trustRevive
```

### 14.5 恶魔之心

```text
对手正在拾取
  → 打断 / 攻击拾取者

心可拾取
  → 前往并拾取

已持有心
  → 交付
```

## 十五、行为仲裁

同一帧发生多个请求时：

1. 对局结束。
2. AI 永久淘汰。
3. 生存事件。
4. 被攻击和结盟申请。
5. 心争夺。
6. 技能施放。
7. 普通攻击。
8. 移动和目标切换。

日志包含：

- 时间。
- 当前 AI 等级。
- 当前 action。
- 候选动作和分数。
- 最终目标。
- 失败原因。

## 十六、开发任务

### 16.1 Core

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| AICORE-01 | 定义 AiPlayerDefinition | 无 | 所有行为开关和阈值可在 Inspector 配置 |
| AICORE-02 | 实现 AiPlayerController 生命周期与 Tick | AICORE-01 | 可固定周期调用决策 |
| AICORE-03 | 实现 seed 与确定性随机 | AICORE-02 | 相同 seed 结果一致 |
| AICORE-04 | 接入 EntityMemory | AICORE-02 | 信任、路线和计时可保存 |

### 16.2 View

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| AIVIEW-01 | 构建 SelfState | AICORE-02 | 血量、技能、塔和结盟状态可读取 |
| AIVIEW-02 | 构建 OpponentView | AICORE-02 | 只包含公开字段 |
| AIVIEW-03 | 构建 BossView | AICORE-02 | 状态、生命、目标和强化层可读取 |
| AIVIEW-04 | 构建 WorldView | AICORE-02 | 时间、海平面和心状态可读取 |
| AIVIEW-05 | 禁止隐藏信息 | AIVIEW-02 | 测试断言不能读取对手背包和内部路线 |

### 16.3 Decision

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| AIDECIDE-01 | 定义 AiDecision | AIVIEW-01～AIVIEW-04 | 输出动作、目标、目的地和原因 |
| AIDECIDE-02 | 实现生存决策 | AIDECIDE-01 | 低血可治疗、撤退或结盟 |
| AIDECIDE-03 | 实现战斗决策 | AIDECIDE-01 | Boss、玩家和怪物可进入候选 |
| AIDECIDE-04 | 实现资源决策 | AIDECIDE-01 | 开箱、拾取和塔目标可触发 |
| AIDECIDE-05 | 实现终局决策 | AIDECIDE-01 | 心出现后可打断、争夺和交付 |

### 16.4 Execute

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| AIEXEC-01 | 移动执行 | AIDECIDE-01 | 调用 PlayerMove 设置目的地 |
| AIEXEC-02 | 攻击执行 | AIDECIDE-01 | 调用 PlayerCombat 设置目标 |
| AIEXEC-03 | 技能执行 | AIDECIDE-01 | 调用技能公开接口 |
| AIEXEC-04 | 结盟执行 | AIDECIDE-01 | 发起、接受、拒绝和翻脸 |
| AIEXEC-05 | 物品执行 | AIDECIDE-01 | 治疗、开箱和拾取 |
| AIEXEC-06 | 心执行 | AIDECIDE-01 | 拾取、打断和交付 |

### 16.5 Alliance

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| AIALLY-01 | 计算信任分 | AIVIEW-02 | 偷杀、合作和翻脸可改变 trust |
| AIALLY-02 | 申请玩家结盟 | AIEXEC-04 | 有效主动申请后进入 120 秒冷却；被拒/超时后双方进入 5 秒协商锁 |
| AIALLY-03 | 申请 Boss 结盟 | AIEXEC-04 | 生命比例 >=30%、非 Rage、双人存活且 60 秒冷却结束时申请；Boss 仇恨不设门槛；3 秒等待期内申请者及归属单位停止攻击目标 |
| AIALLY-04 | 接受或拒绝 | AIALLY-01 | 结果符合配置阈值；响应不触发自己的主动申请冷却 |
| AIALLY-05 | 翻脸和报复 | AIALLY-01 | 区分中途翻脸与到期后攻击；Boss 结盟期间及结束后 10 秒内禁止玩家间结盟 |

### 16.6 Debug

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| AIDEBUG-01 | 决策面板 | AIDECIDE-01 | 显示候选、得分和最终动作 |
| AIDEBUG-02 | 强制动作 | AIEXEC-01～AIEXEC-06 | 可手动触发移动、攻击和技能 |
| AIDEBUG-03 | 状态可视化 | AIVIEW-01～AIVIEW-04 | 显示 AIView 全部字段 |
| AIDEBUG-04 | 决策日志 | AIDECIDE-01 | JSON / CSV 可回放 |
| AIDEBUG-05 | 固定 seed 回放 | AICORE-03 | 重新运行得到相同事件序列 |

## 十七、系统不变量

1. AI 不修改隐藏对手数据。
2. AI 不直接修改生命、背包、掉落或地图状态。
3. AI 每次只产生一个最终决策。
4. 生存和事件响应优先级最高。
5. 结盟期间不攻击盟友。
6. 同一帧多个动作只能有一个主动作。
7. 所有动作都必须有日志和原因。
8. 所有阈值必须可从 AiPlayerDefinition 调整。
9. 相同 seed 和输入必须产生相同结果。

## 十八、完成定义

AI 玩家 Demo 完成时，必须可以重复执行：

```text
生成 AI
→ 探索并选择目标
→ 攻击怪物 / 对手 / Boss
→ 低血撤退或治疗
→ 释放技能
→ 发起或接受结盟
→ 到期翻脸或主动背叛
→ 击杀 Boss
→ 争夺恶魔之心
→ 完成交付
→ 输出双方独立胜负
```

最终验收：

- 无非法状态迁移。
- 无永久卡死。
- 不依赖正式动画和 HUD。
- AI 对 AI 可以跑完整局。
- 固定 seed 可以复现。
- 决策日志可以解释每个动作。

---

*本文档仅覆盖玩法验证 AI。正式人机、多人格和网络同步 AI 在后续版本中扩展。*
