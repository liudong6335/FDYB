# 《分道扬镳》Boss 功能开发清单

> 状态：Boss Demo 开发基线
> 用途：第一部分供人快速理解，第二部分供 AI 按任务实施

## 目录

- 第一部分：人读版
- 第二部分：AI 开发文档

---

# 第一部分：人读版

## 一、开发目标

先完成一个可重复运行的 Boss 纵向 Demo，验证以下完整闭环：

```text
Boss 苏醒 → 玩家进入战斗 → Boss 选择目标 → 选择战斗意图 → 移动与攻击
→ 阶段强化 → 技能循环 → 玩家结盟 / 翻脸
→ Boss 死亡 → 恶魔之心争夺 → 分别结算双方胜负
```

当前阶段不做正式出生流程、正式美术、完整成长系统、正式地图和联机。

## 二、Demo 范围

### 2.1 本期开发

- Boss 移动与追击。
- Boss 目标选择和仇恨总分。
- 普攻、三个技能、时间和强化阶段。
- Boss 与玩家结盟、玩家与玩家结盟。
- 翻脸、惩罚仇恨和盟友豁免。
- Boss 死亡、恶魔之心、交付与胜负结算。
- 调试场景、调试面板和结构化日志。

### 2.2 本期暂不开发

- Boss 出生点和苏醒倒计时正式逻辑。
- 正式模型、动画、音效和特效。
- 环境亲和度、双路线和完整掉落。
- 完整宝石、装备和技能书成长。
- 线上联机和网络同步。
- 最终数值平衡。

## 三、七个逻辑维度

### 3.1 移动逻辑

移动逻辑回答“Boss 现在去哪里”，不负责选择攻击目标。

核心流程：

```text
Boss 苏醒 → EntityMemory 启动位置快照采样
有目标 → 接近目标
进入攻击距离 → 停止移动
目标脱离范围 → 请求重新选择目标
没有玩家目标 → 前往最后已知位置
最后已知位置失效 → 前往安全区或战术驻守点
海平面收缩 → 向安全区移动
```

主要完成项：

- EntityMemory 每 5 秒（暂定/需要暴露数值）采样一次存活玩家位置并保存为位置快照；玩家仇恨都为零时，优先追击距离近的玩家。
- NavMesh 追击、停止和朝向。
- BossMovement 只读取最后已知位置和位置快照，不负责采样。
- 进入攻击或仇恨范围后使用实时追击，不受 5 秒采样限制。
- 目标脱离和重新响应。
- 移动日志和调试显示。

### 3.2 战斗行为逻辑

战斗行为逻辑回答“Boss 当前应该进攻、追击、风筝、撤退、重整还是寻求结盟”。

核心意图：

```text
Engage       正常进攻
Chase        追击目标
Kite         保持距离并风筝
Survive      降低风险、脱离集火、等待技能
Regroup      向安全区、召唤物或地形退守
SeekAlliance 判断打不过时尝试与玩家结盟
Recover      回血完成后重新进攻
```

主要完成项：

- 根据生命值、近期承伤、技能冷却、目标数量和距离选择意图。
- 输出移动意图、攻击意图、技能请求和结盟请求。
- Boss 可以战术撤退，但不重置仇恨、不离开战斗区域、不自动回满血。
- 超过最大撤退时间后必须重新进攻或重新判断结盟。
- 所有阈值都暴露，方便不同 Boss 和怪物调整。

### 3.3 攻击逻辑

攻击逻辑回答“打谁、怎么打”。

核心流程：

```text
收集目标 → 计算仇恨总分 → 排序 → 5 秒快照
→ 普攻 → 技能触发 → 阶段强化 → 残血回血
```

仇恨总分：

```text
5 秒真实伤害 + 残血修正 + 圣物仇恨 + 追杀仇恨
```

主要完成项：

- 5秒伤害窗口和残血修正。
- 圣物、结盟追杀和塔仇恨。
- 同分裁决与 5秒目标快照。
- 统一记忆容器：保存目标快照、局势快照、仇恨历史、技能冷却和结盟计时。
- 目标跌破 10% 的立即重选。
- 40m 残血追击。
- 普攻和统一伤害结算。
- 三个技能和技能施放状态机。
- 三阶段、强化层、控制抗性和残血回血。

### 3.4 技能施放逻辑

技能模块回答“是否施放、施放哪个、什么时候施放、效果是什么”。

技能状态：

```text
Idle → Checking → Casting → Resolving → Recovering → Cooldown → Idle
```

主要完成项：

- 技能数据配置：冷却、优先级、前摇、范围、目标和效果参数。
- 触发条件判断。
- 目标解析。
- 同时只允许一个主动施放，技能有CD时间(CD时间需要暴露数值）。
- 玩家血量值不高于技能伤害的10%，技能能击杀玩家立马释放。
- 死亡和硬控可中断。
- 鲜血收割、魔潮召唤、锁链囚笼。
- 技能事件和结构化日志。

### 3.5 结盟逻辑

结盟只在 Boss 苏醒后生效。

主要流程：

```text
10 秒局势判断 → 申请 / 主动提议 → 接受或拒绝
→ 盟友豁免 → 限时结束 / 翻脸 → 追杀仇恨
```

主要完成项：

- Boss + 玩家结盟。
- 玩家 + 玩家结盟。
- 避免攻击过 Boss 的玩家与 Boss 结盟。
- 残血排除和主动概率。
- 结盟期间行为限制。
- 翻脸、3 秒缓冲、5000 惩罚仇恨。
- 死亡不新增结盟解除判定。

### 3.6 死亡逻辑

死亡逻辑回答 Boss 如何结束，以及玩家死亡如何影响对局。

Boss 死亡流程：

```text
血量归零 → 停止行为 → 恶魔之心掉落
→ 10 秒拾取 → 被打断 / 易手 → 交付 → 分别结算胜负
```

玩家死亡：

```text
Boss 苏醒后死亡 → 永久淘汰 → 结算淘汰掉落
→ 另一方继续 solo 或交付恶魔之心
```

主要完成项：

- Boss 死亡事件。
- 恶魔之心掉落和拾取。
- 受击回退、死亡掉落和易手。
- 交付过场。
- 系统分别结算每名玩家胜负。
- 双方均失败时在统计层记为双输。

### 3.7 出生逻辑

出生逻辑本期暂不开发，只保留配置接口：

```text
出生位置 / 初始阶段 / 是否提前苏醒 / 苏醒原因 / 初始强度
```

移动、攻击和调试生成都必须使用同一份初始状态接口，不能在代码中写死。

## 四、脚本概览

当前规划 13 个 Boss 专属脚本和 1 个通用记忆组件：

| 脚本 | 主要功能 |
| --- | --- |
| `EntityMemory.cs` | 通用记忆、位置快照、计时、关系数据及 Inspector 参数配置 | |
|
| BossController.cs | 生命周期、状态迁移、模块调度 |
| `BossBehaviorController.cs` | 战斗意图、追击、风筝、保命、重整和结盟请求 |
| `BossDefinition.cs` | Boss、阶段和技能配置 |
| BossStats.cs | 基础属性、有效属性和属性修改器 |
| BossPhaseController.cs | 虚弱、正常、暴走状态、强化层和倍率 |
| `BossMovement.cs` | 接收行为意图，读取位置快照，执行追击、停止、移动和安全区行为 |
| `BossTargeting.cs` | 目标、仇恨、快照和同分裁决 |
| `BossCombat.cs` | 普攻、受伤、伤害、控制和回血 |
| `BossSkillController.cs` | 技能状态机、触发、冷却和打断 |
| `BossSkillExecutor.cs` | 技能目标和三个技能效果 |
| `BossAllianceController.cs` | 结盟、豁免、翻脸和结束 |
| `BossEndgameController.cs` | Boss 死亡、恶魔之心和胜负 |
| `BossDebugTools.cs` | 调试、强制触发和日志 |

## 五、里程碑

| 里程碑 | 内容 | 完成结果 |
| --- | --- | --- |
| M1 | 移动、行为、目标、普攻 | Boss 可根据战斗意图追击并攻击测试目标 |
| M2 | 技能和阶段 | 三个技能、第二 / 第三阶段和强化层可用 |
| M3 | 结盟 | Boss 结盟、玩家结盟和翻脸可跑通 |
| M4 | 终局 | Boss 死亡、掉心、交付和结算可完成 |
| M5 | Demo 整合 | 接塔、圣物、海平面并完成可玩闭环 |

## 六、当前需要冻结的数值

| 项目 | 当前状态 |
| --- | --- |
| Boss 基础属性 | 参考现有文档，待最终确认 |
| 三个技能数值 | 伤害、范围、冷却待定 |
| 强化层幅度 | 5% 或 10% |
| 虚弱恢复 | 初始 50-60%，每分钟恢复约 10% |
| 圣物效果 | 减伤版本与增伤版本尚未统一 |
| 塔的数量 | 当前文档存在多套口径 |
| 玩家定位采样 | 每 5 秒 |
| 最后已知位置有效期 | 暂定 15 秒 |
| 恶魔之心 | 掉落位置、存在时间和拾取细节待定 |

---

# 第二部分：AI 开发文档

## 七、AI 执行规则

1. 按任务编号顺序开发，不跨阶段一次性实现全部功能。
2. 每个任务必须先满足依赖，再修改代码。
3. 不得自行改变游戏规则、数值口径或脚本职责。
4. 发现设计冲突时停止相关实现，并在交付说明中标记阻塞项。
5. 所有数值放入配置，不硬编码在 `MonoBehaviour` 中。
6. 战斗逻辑不得依赖 Animator、UI 或特效是否播放。
7. 每个任务结束时必须满足该任务的验收条件。
8. 不创建只搬移字段、没有独立状态的辅助脚本。
9. 单个脚本超过约 400 行，或出现独立变化原因时再拆分。
10. 每个核心状态变化必须写入结构化日志。

## 八、目录与脚本职责

```text
Assets/Scripts/Common/
└─ EntityMemory.cs

Assets/Scripts/Boss/
├─ BossController.cs
├─ BossDefinition.cs
├─ BossStats.cs
├─ BossPhaseController.cs
├─ BossMovement.cs
├─ BossTargeting.cs
├─ BossBehaviorController.cs
├─ BossCombat.cs
├─ BossSkillController.cs
├─ BossSkillExecutor.cs
├─ BossAllianceController.cs
├─ BossEndgameController.cs
└─ BossDebugTools.cs
```

| 脚本 | 核心职责 | 主要入口 | 明确不负责 |
| --- | --- | --- | --- |
| `EntityMemory.cs` | 通用记忆组件；保存伤害历史、位置快照、目标快照、决策快照、计时器和实体关系 | `SamplePositionSnapshot`、`GetLatestPosition`、`RegisterDamage`、`TickMemory`、`StoreSnapshot`、`SetTimer` | 不选择目标、不执行技能、不控制移动 |
| BossController.cs | 生命周期、状态迁移、BossRuntimeState 容器和 Tick 调度 | Initialize、Tick、ChangeState | 仇恨公式、技能效果、移动算法 |
| `BossDefinition.cs` | 基础属性、阶段数据、技能数据配置 | 数据字段 | 运行时 HP、冷却、目标 |
| `BossStats.cs` | 基础属性、有效属性和属性修改器 | `GetDamage`、`GetMoveSpeed`、`ApplyModifier` | 状态切换、当前生命、技能施放 |
| `BossPhaseController.cs` | 虚弱、正常、暴走状态；属性倍率、技能频率倍率、仇恨倍率和强化层 | `ApplyState`、`TickState`、`GetMultipliers` | 当前生命、技能效果、目标选择 |
| `BossMovement.cs` | 接收 BossBehaviorController 的移动意图，读取 EntityMemory 位置快照，执行追击、停止、朝向和撤退移动 | `Tick`、`ApplyMovementIntent`、`LockMovement`、`GetDestination` | 战斗意图判断、位置采样、记忆存储、目标选择、技能施放 |
| `BossTargeting.cs` | 候选目标、仇恨总分、5 秒目标快照、同分裁决和重选 | `RegisterDamage`、`TickSnapshot`、`GetCurrentTarget` | 移动、伤害执行 |
| `BossBehaviorController.cs` | 根据生命、承伤、技能冷却、目标数量和距离选择战斗意图 | `TickIntent`、`GetCurrentIntent`、`RequestSkill`、`RequestAlliance` | 不移动、不攻击、不释放技能 |
| `BossCombat.cs` | 接收攻击意图，执行普攻、攻击间隔、受伤、统一伤害、控制和回血 | `ApplyAttackIntent`、`ApplyDamage`、`TickRegen` | 战斗意图判断、技能调度、目标排序 |
| `BossSkillController.cs` | 接收技能请求，管理技能状态机、触发、优先级、冷却、施放锁和打断 | `Tick`、`TryStartCast`、`TickCast`、`InterruptCast` | 战斗意图判断、具体技能效果、普通仇恨 |
| `BossSkillExecutor.cs` | 技能目标解析和三个技能效果 | `ResolveTargets`、`Execute` | 何时施放、全局冷却 |
| `BossAllianceController.cs` | 接收结盟请求，管理 10 秒策略快照、申请、响应、盟友豁免、翻脸和结束 | `TickStrategy`、`Request`、`Respond`、`Break` | 战斗意图判断、普通伤害仇恨、移动 |
| `BossEndgameController.cs` | Boss 死亡、恶魔之心、拾取、易手、交付和结算 | `OnBossDead`、`TickHeart`、`ResolveResult` | 存活阶段战斗 |
| `BossDebugTools.cs` | HUD、时间快进、强制技能、仇恨注入和日志 | 调试命令 | 正式战斗决策 |

## 九、核心状态与数据契约

### 9.1 Boss 状态

```text
Dormant
Awakening
Combat
Alliance
Dead
```

允许迁移：

```text
Dormant → Awakening
Awakening → Combat
Combat ↔ Alliance
Combat → Dead
Alliance → Dead
```

### 9.2 Boss 运行时状态

```text
currentHp
currentPhase
enrageLayer
weaknessStrength
currentTarget
movementState
skillState
allianceState
entityMemory
isDead
```

### 9.3 Boss 属性状态

Boss 属性状态只有三种：

```text
Weak
Normal
Rage
```

转换规则：

```text
正常苏醒 → Normal
提前苏醒 / 对手淘汰苏醒 → Weak
Weak 强度恢复到 100% → Normal
到达暴走时间 → Rage
Weak 在暴走时间仍未恢复 → Rage
Rage 不回到其他状态
```

状态输出：

```text
damageMultiplier
moveSpeedMultiplier
skillFrequencyMultiplier
hateMultiplier
```

职责：

- BossPhaseController 只负责状态和倍率。
- BossStats 消费倍率并计算有效属性。
- BossTargeting 消费仇恨倍率。
- BossSkillController 消费技能频率倍率。
- BossBehaviorController 可以读取状态，但不能修改状态。
### 9.4 Boss 战斗意图

```text
Engage
Chase
Kite
Survive
Regroup
SeekAlliance
Recover
```

意图输入：

```text
hpRatio
recentDamage
incomingDamageRate
nearbyEnemyCount
currentTarget
distanceToTarget
skillCooldowns
bossPhase
isWeak
allianceAvailable
combatMemory
```

意图输出：

```text
movementIntent
attackIntent
skillRequest
allianceRequest
retreatDestination
intentDuration
```

核心参数：

```text
behaviorDecisionInterval
retreatHpThreshold
returnHpThreshold
maxRetreatTime
retreatDistance
kiteDistance
regroupRadius
seekAllianceHpThreshold
seekAllianceEnemyCount
```

职责边界：

- BossBehaviorController 只输出意图，不直接修改位置、生命、技能冷却或仇恨。
- 移动、攻击、技能和结盟模块分别消费对应意图。
- Boss 可以战术撤退，但不能重置仇恨、离开战斗区域或自动回满血。
- 超过 `maxRetreatTime` 后必须重新进入 Engage 或重新判断结盟。
### 9.5 通用记忆与快照

`EntityMemory` 是通用 MonoBehaviour，放在 `Assets/Scripts/Common/EntityMemory.cs`，直接挂载到 Boss、怪物或 AI 角色的根节点。每个模块的开关、窗口、容量、阈值和衰减参数都通过 `[SerializeField]` 暴露在 Inspector 中。

生命周期：

```text
角色 Awake
  → 控制器获取 EntityMemory 组件引用
  → EntityMemory 初始化运行时记忆

角色 Update 或 EntityMemory.Update
  → TickMemory(deltaTime)

角色 Reset
  → ClearAll() 或按模块 clearOnDisable
```

角色接入方式：

```text
BossController / MonsterController / AICharacterController
  ├─ [SerializeField] EntityMemory entityMemory
  └─ Awake 时在字段为空时 GetComponent<EntityMemory>()

模块引用：
  BossTargeting.Initialize(entityMemory);
  BossAllianceController.Initialize(entityMemory);
  BossSkillController.Initialize(entityMemory);
```

- 每个角色预制体各自挂载一个 `EntityMemory`。
- 相同类型的角色可以使用同一个预制体，通过 Prefab Variant 调整参数。
- 不同类型的角色直接复制组件，修改 Inspector 参数。
- 控制器只在 `Awake` 获取一次引用，其他模块不重复 `GetComponent`。
- `EntityMemory` 自己保存运行时状态，不需要额外配置资产。
- 模块开关和数值都可以在各自角色的 Inspector 中直接调整。

通用数据：

```text
entityId
profileId
damageHistory
targetSnapshots
decisionSnapshots
namedTimers
entityRelations
playerPositionMemory
lastKnownPositions
positionSource
snapshotVersion
lastSnapshotGameTime
```

模块开关与参数：

| 模块 | 开关 | 暴露参数 |
| --- | --- | --- |
| 伤害历史 | `enableDamageMemory` | `damageWindow`、`maxDamageRecords`、`clearOnDisable` |
| 目标快照 | `enableTargetSnapshot` | `targetSnapshotInterval`、`targetSnapshotCapacity`、`targetInvalidationHpRatio`、`clearOnDisable` |
| 决策快照 | `enableDecisionSnapshot` | `decisionSnapshotInterval`、`decisionSnapshotCapacity`、`clearOnDisable` |
| 命名计时器 | `enableNamedTimers` | `timerPrecision`、`timerCapacity`、`defaultDecayMode`、`clearOnDisable` |
| 实体关系 | `enableRelationMemory` | `relationCapacity`、`relationDecayRate`、`relationMinValue`、`relationMaxValue`、`clearOnDisable` |
| 位置记忆 | `enablePositionMemory` | `positionSourceProvider`、`playerPositionSampleInterval`、`positionMemoryCapacity`、`lastKnownPositionExpire`、`trackInCombatRealtime`、`arrivalRadius`、`sampleOnEnable`、`clearOnDisable` |
位置源接口：

```text
IPositionSource:
    GetPositionSamples()
        → entityId
        → position
        → isAlive
        → gameTime
```

- BossController 提供玩家位置源，EntityMemory 负责采样和保存。
- 其他角色可以提供玩家、敌人或其他追踪目标的位置源。
- BossMovement 只读取 EntityMemory 的位置快照，不持有位置源。
| 快照版本 | `enableSnapshotVersioning` | `initialVersion`、`incrementOnDecision`、`incrementOnTargetChange` |
| 记忆日志 | `enableMemoryLogging` | `memoryLogCapacity`、`logSnapshotWrites`、`logTimerChanges`、`logRelationChanges` |

配置规则：

- 每个模块必须可单独启停。
- 关闭模块时，不记录、不 Tick、不分配该模块的运行时容量。
- `clearOnDisable` 控制关闭时是否清空已有数据。
- 所有时间窗口、容量、阈值和衰减参数必须暴露，不得写死在逻辑脚本中。
- 默认配置必须给出可用值，新建实体无需配置即可运行。
- 配置必须支持 Inspector 编辑，并允许后续扩展为共享配置资产。

| 记忆内容 | Boss 用途 | 其他实体用途 | 维护者 |
| --- | --- | --- | --- |
| 伤害历史 | 5 秒真实伤害 | 仇恨、威胁度 | `BossTargeting` / 怪物 AI |
| 目标快照 | 5 秒锁定当前目标 | 锁定追击目标 | `BossTargeting` / 怪物 AI |
| 决策快照 | 10 秒结盟与战术判断 | AI 路线和意图判断 | `BossAllianceController` / AI |
| 命名计时器 | 圣物仇恨、追杀仇恨 | 技能冷却、警觉、脱战 | 对应模块写入 |
| 实体关系 | 结盟、翻脸、追杀 | 信任、敌意、归属 | `BossAllianceController` / AI |

统一规则：

- EntityMemory 只保存事实、快照和计时，不决定打法。
- 每个模块独立启停，模块关闭后不得继续产生运行时开销。
- 所有时间窗口、容量和阈值必须外部可调。
- 具体模块决定何时写入、读取或失效。
- 5 秒目标快照和 10 秒策略快照相互独立。
- 策略快照在周期内不重算，事件响应可以立即执行，但不能提前刷新策略快照。
- 目标死亡、脱离范围或首次跌破 10% 允许打断目标快照。
- 所有计时使用游戏时间，不因 UI、动画或暂停菜单停止。
- 快照必须带版本号和生成时间，日志可以复现每次决策输入。
- 使用 `List + key` 查询，避免 Unity 对 `Dictionary` 的序列化限制。

### 9.6 技能施放状态

```text
Idle
Checking
Casting
Resolving
Recovering
Cooldown
```

每个技能运行时必须记录：

```text
skillId
cooldownRemaining
isAvailable
activeCastPhase
castRemaining
lockedTargets
interrupted
```

技能配置必须暴露：

```text
cooldown
priority
castTime
recoveryTime
range
damage
targetMode
lethalDamageRatio
```

斩杀优先规则：

```text
IF targetHp <= skillDamage * lethalDamageRatio:
    → 忽略普通技能优先级，立即尝试释放该技能
```

- `lethalDamageRatio` 必须在 Inspector 暴露，人读版当前口径暂定为 `0.1`。
- 只有目标有效、技能冷却完成且当前没有不可打断施放时，斩杀技能才能抢占。
- 斩杀优先仍然受 Boss 死亡、硬控和动作锁裁决。

### 9.7 仇恨分值

```text
damageWindow5s
lowHpModifier
relicHate
revengeHate
towerHate
finalScore
```

规则：

- 只统计真实伤害。
- 5 秒窗口外伤害失效。
- 塔不参与玩家目标竞争。
- 排序：总分 → 血量低 → 战力低 → 距离近。
- 所有玩家仇恨都为零时，直接在存活玩家中按距离从近到远选择。
- 普通变化不打断快照。
- 目标跌破 10% 可以立即打断快照。

## 十、裁决规则

本节用于明确两个或更多系统在同一帧冲突时由谁生效。所有裁决必须可记录、可复现，并带固定优先级。

### 10.1 统一优先级

同一帧出现多个事件时，按以下顺序处理：

| 优先级 | 事件 | 处理原则 |
| --- | --- | --- |
| 1 | 对局已经结束 | 停止所有战斗逻辑，只保留结算 |
| 2 | Boss 死亡 | 最高战斗事件，停止移动、攻击、技能和结盟 |
| 3 | 玩家永久淘汰 | 先结算淘汰状态，再刷新目标池 |
| 4 | 硬控与死亡打断 | 立即打断可打断技能和普通攻击 |
| 5 | 结盟状态变化 | 立即更新盟友豁免和目标池 |
| 6 | 目标快照失效 | 按 10.2 判断是否允许换目标 |
| 7 | 技能结算 | 处理当前技能的伤害、召唤或控制 |
| 8 | 普通攻击 | 只有无技能施放且目标有效时执行 |
| 9 | 移动 | 最后消费当前目标与动作锁 |

规则：

- 高优先级事件处理完成后，低优先级事件才可使用同一帧。
- 低优先级事件不能覆盖高优先级事件的结果。
- 同一帧多个同优先级事件使用稳定顺序：实体 ID → 距离 → 事件序号。
- 所有裁决结果必须写入日志，包含事件、目标和裁决原因。

### 10.2 快照打断

5 秒目标快照只有在以下情况可以立即打断：

| 打断原因 | 是否立即生效 | 处理 |
| --- | --- | --- |
| 当前目标死亡 | 是 | 清空目标，立即重选 |
| 当前目标不可选 | 是 | 清空目标，立即重选 |
| 当前目标超出仇恨范围 | 是 | 清空目标，立即重选 |
| 目标首次跌破 10% | 是 | 触发残血上限，立即重选 |
| 其他目标总分反超 | 否 | 等下一快照 |
| 当前目标血量变化但未跌破 10% | 否 | 等下一快照 |
| 普通伤害变化 | 否 | 等下一快照 |

同一帧存在多个打断原因时，裁决顺序为：

```text
目标死亡
→ 目标不可选
→ 超出范围
→ 首次跌破 10%
```

技能施放期间发生快照打断：

- 单体技能：取消技能效果，施放结束并进入冷却。
- 范围、自身或原地技能：按施放开始时锁定的位置执行。
- 普通攻击：取消本次攻击，等待下一攻击间隔。

### 10.3 时序抢锁

系统使用以下动作锁：

| 锁 | 持有者 | 期间禁止 |
| --- | --- | --- |
| 技能施放锁 | 当前技能 | 新技能、普通攻击、可选移动锁 |
| 普通攻击锁 | 当前普攻 | 新普攻，不阻止技能 |
| 移动锁 | 技能或控制 | 搜索、回防、追击、转向覆盖 |
| 结盟状态锁 | 结盟系统 | 盟友攻击和盟友仇恨 |
| 死亡锁 | Boss 死亡 | 所有战斗行为 |

规则：

- 同一时间只能存在一个技能施放锁。
- 技能施放时，普通攻击不得抢占。
- 技能完成或打断后，至少等下一帧才能开始新技能。
- 结盟状态变化可以立即更新目标池，但不能直接修改技能冷却。
- Boss 死亡立即释放全部动作锁并终止后续裁决。
- 冷却在技能成功结算或合法打断后立即开始，不等待动画结束。

### 10.4 同分排序

仇恨总分比较使用配置的 `scoreEpsilon`，默认暂定 `0.01`。

排序顺序：

```text
总分高
→ 生命比例低
→ 战力低
→ 距离近
→ 实体 ID 小
```

规则：

- 两名玩家总分差绝对值小于 `scoreEpsilon` 视为同分。
- 塔不进入玩家同分排序。
- 同一候选对象在同帧只能出现一次。
- 目标不可选时不参与排序。
- 排序结果必须在调试日志中记录每个分项。

### 10.5 门槛窗口

所有阈值统一使用以下约定：

```text
下限包含，上限不包含
[下限, 上限)
时间阈值使用 >=
```

主要门槛：

| 门槛 | 暂定规则 | 配置字段 |
| --- | --- | --- |
| 残血修正启动 | 生命比例 < 30% | `lowHpModifierStart` |
| 残血修正封顶 | 生命比例 ≤ 10% | `lowHpModifierCap` |
| 快照立即打断 | 从 ≥ 11% 跌到 < 10% | `snapshotBreakHp` |
| 打断重新武装 | 回到 ≥ 11% 才可再次触发 | `snapshotRearmHp` |
| 残血追击范围 | < 30% 时 20m → 40m | `lowHpChaseRange` |
| 阶段切换 | 游戏时间 >= 300s / 480s | `phaseTimes` |
| 强化层 | 每累计 30s 增加一层 | `enrageInterval` |
| 结盟快照 | 每累计 10s 刷新一次 | `decisionSnapshotInterval` |

规则：

- 门槛只在跨过边界时触发一次。
- 阈值两侧必须提供滞回值，避免同一帧反复触发。
- 达到上限时不重复触发。
- 实际值统一从 `EntityMemory` 或模块配置读取。

### 10.6 数值暂定值

以下数值用于 Demo 首版，可调整但不允许未配置：

| 项目 | 暂定值 | 状态 |
| --- | --- | --- |
| Boss 生命 | 10000 | 暂定 |
| Boss 攻击 | 300 / 2 秒 | 暂定 |
| Boss 防御 | 100 | 暂定 |
| Boss 移速 | 3m/s | 暂定 |
| Boss 仇恨范围 | 20m | 暂定 |
| 残血追击范围 | 40m | 暂定 |
| 目标快照 | 5 秒 | 暂定 |
| 策略快照 | 10 秒 | 暂定 |
| 残血修正启动 | 30% | 暂定 |
| 残血修正封顶 | 10% | 暂定 |
| 残血修正上限 | 2000 | 暂定 |
| 圣物仇恨 | 日 5000 / 月 4000 | 暂定 |
| 圣物仇恨消散 | 120 秒 | 暂定 |
| 追杀仇恨 | 5000 | 暂定 |
| 追杀仇恨消散 | 30 秒 | 暂定 |
| 结盟结束缓冲 | 3 秒 | 暂定 |
| 塔仇恨倍率 | 80% | 暂定 |
| 同分容差 | 0.01 | 暂定 |
| 10% 打断滞回 | 11% 重新武装 | 暂定 |

### 10.7 数值配置检查

开工前必须完成：

- 所有裁决阈值都进入配置文件或 Inspector。
- 没有配置时使用默认值，并输出警告。
- 配置值非法时禁止运行 Boss 默认逻辑。
- 日志记录本次裁决使用的实际值。
- 修改配置后无需修改脚本。
## 十一、开发任务

### 11.1 Core

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| CORE-01 | 定义 BossState 和合法迁移 | 无 | 非法迁移被拒绝并记录 |
| CORE-02 | 实现 BossController 初始化与 Tick | CORE-01 | 子模块按固定顺序初始化 |
| CORE-03 | 定义 BossDefinition、阶段和技能数据 | 无 | 数值可配置，运行时不修改 SO |
| CORE-04 | 实现 BossStats 基础与有效属性 | CORE-03 | 属性修改器和有效值可读取 |
| CORE-05 | 实现残血回血 | CORE-04 | 20% / 10% 边界正确 |
| CORE-06 | 实现可挂载 EntityMemory 组件 | CORE-02 | 可直接挂载角色并完整保存运行时记忆 |
| CORE-07 | 实现记忆模块开关与参数暴露 | CORE-06 | 每个模块可独立启停，全部窗口、容量和阈值可在 Inspector 调整 |
| CORE-08 | 实现通用目标快照 | CORE-06 | 周期、锁定和版本信息可读取 |
| CORE-09 | 实现通用决策快照 | CORE-06 | Boss 结盟和 AI 路线可使用同一存储 |
| CORE-10 | 实现通用命名计时器 | CORE-06 | 仇恨、技能冷却和结盟计时可统一衰减 |
| CORE-11 | 实现快照失效与容量规则 | CORE-08、CORE-09 | 合法打断、周期刷新和记录上限生效 |
| CORE-12 | 输出通用记忆日志 | CORE-08～CORE-11 | 可回放目标、决策和计时变化 |
| CORE-14 | 实现 BossPhaseController 三状态 | CORE-04 | 虚弱、正常、暴走可切换并输出倍率 |
| CORE-13 | 实现位置快照模块与采样器 | CORE-06 | EntityMemory 按配置周期调用 IPositionSource，保存位置快照 |

### 11.2 Movement

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| MOVE-01 | 读取位置快照并生成移动目的 | CORE-13 | BossMovement 只消费 EntityMemory 的最新位置快照，不自行采样 |
| MOVE-02 | 最后已知位置移动 | MOVE-01 | 没有有效目标时前往最后已知位置，不做随机漫游 |
| MOVE-03 | 追击、停止与朝向 | MOVE-01 | 目标有效时追击，攻击距离内停止并面向目标 |
| MOVE-04 | 目标脱离与重新搜索 | MOVE-02 | 超出范围后保留最后位置，并请求重新选择目标 |
| MOVE-05 | 风筝检测 | MOVE-03 | 长时间无法接近进入风筝状态 |
| MOVE-06 | 塔与安全区移动 | MOVE-02 | 无玩家时靠近塔；最后位置失效时回到安全区 |

### 11.3 Targeting

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| TARGET-01 | 候选目标收集 | CORE-02 | 玩家、塔、召唤物可进入候选池 |
| TARGET-02 | 5 秒真实伤害窗口 | COMBAT-01 | 过期伤害自动清理 |
| TARGET-03 | 残血修正 | TARGET-02 | 30% 起加分，10% 封顶 |
| TARGET-04 | 圣物与追杀仇恨 | TARGET-02 | 分项独立计时并可叠加 |
| TARGET-05 | 塔仇恨 | TARGET-02 | 无玩家目标时按 80% 生效 |
| TARGET-06 | 同分裁决 | TARGET-01 | 按规则稳定排序 |
| TARGET-07 | 5 秒快照与 10% 打断 | TARGET-06 | 普通变化不换目标，跌破 10% 可换 |
| TARGET-08 | 40m 残血追击 | TARGET-07 | 30% 以下翻倍，恢复后回到 20m |
| TARGET-09 | 零仇恨目标选择 | TARGET-06 | 所有玩家仇恨为零时选择距离最近的存活玩家 |

### 11.4 Behavior

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| BEHAVIOR-01 | 定义战斗意图枚举与决策输入 | TARGET-07 | 七种意图可切换，输入字段完整 |
| BEHAVIOR-02 | Engage / Chase 意图 | BEHAVIOR-01 | 目标有效时正常进攻或追击 |
| BEHAVIOR-03 | Kite 意图 | BEHAVIOR-01 | 技能冷却或近身压力下保持距离 |
| BEHAVIOR-04 | Survive / Regroup 意图 | BEHAVIOR-01 | 低血量和集火时可战术撤退到安全位置 |
| BEHAVIOR-05 | SeekAlliance 意图 | BEHAVIOR-01、ALLY-01 | 判断打不过时向结盟模块发出请求 |
| BEHAVIOR-06 | Recover 意图 | BEHAVIOR-04 | 恢复到返回阈值后重新进攻 |
| BEHAVIOR-07 | 撤退限制与意图日志 | BEHAVIOR-04～BEHAVIOR-06 | 不重置仇恨、不离开战斗区域、不无限撤退 |

### 11.5 Combat

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| COMBAT-01 | 统一受伤入口 | CORE-04 | 外部伤害只能通过该入口进入 |
| COMBAT-02 | 防御和减伤计算 | COMBAT-01 | 防御、圣物占位可配置 |
| COMBAT-03 | 普攻与攻击间隔 | TARGET-07 | 300 伤害 / 2 秒可配置 |
| COMBAT-04 | 控制抗性 | COMBAT-01 | 减速、眩晕规则可配置 |
| COMBAT-05 | 伤害事件输出 | COMBAT-01 | 每次伤害写入仇恨和日志 |

### 11.6 Skills

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| SKILL-01 | 技能施放状态机 | CORE-02 | 六种施放状态可切换 |
| SKILL-02 | 技能优先级与冷却 | SKILL-01 | 多技能可用时按优先级选择 |
| SKILL-03 | 技能触发条件 | SKILL-01 | 残血、被风筝、双人围攻可独立判断 |
| SKILL-04 | 技能目标解析 | SKILL-01 | 单体、范围、双目标均可解析 |
| SKILL-05 | 施放锁与打断 | SKILL-01 | 单主动施放，死亡和硬控中断 |
| SKILL-06 | 鲜血收割 | SKILL-02～SKILL-05 | 最低生命目标高伤并标记 |
| SKILL-07 | 魔潮召唤 | SKILL-02～SKILL-05 | 召唤 2-3 只幽灵，Boss 继续行动 |
| SKILL-08 | 锁链囚笼 | SKILL-02～SKILL-05 | 双目标减速、定身或拉近 |
| SKILL-09 | 技能日志 | SKILL-01 | 记录候选、条件、目标、冷却和打断 |
| SKILL-10 | 斩杀优先施放 | SKILL-02～SKILL-05 | 目标 HP ≤ 技能伤害 × lethalDamageRatio 时忽略普通优先级立即施放，参数可在 Inspector 调整 |

### 11.7 Alliance

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| ALLY-01 | 10 秒局势判断快照 | CORE-02 | 申请使用上一周期结果 |
| ALLY-02 | 未攻击 Boss 门槛 | ALLY-01 | 攻击过 Boss 的玩家不可结盟 |
| ALLY-03 | 残血排除 | ALLY-01 | 残血目标不可与 Boss 结盟 |
| ALLY-04 | Boss 主动提议 | ALLY-01 | 上限 20%，同周期不重复 |
| ALLY-05 | 玩家申请与响应 | ALLY-01 | 申请、接受、拒绝和超时完整 |
| ALLY-06 | 盟友豁免 | ALLY-05 | 盟友不进入目标池 |
| ALLY-07 | 翻脸 | ALLY-05 | 立即破裂并触发惩罚仇恨 |
| ALLY-08 | 结盟结束 | ALLY-07 | 3 秒缓冲后进入 5000 追杀仇恨 |
| ALLY-09 | 死亡关系 | ALLY-08 | 不新增死亡解除判定，按倒计时结束 |

### 11.8 Endgame

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| END-01 | Boss 死亡事件 | CORE-01 | 停止全部 Boss 行为 |
| END-02 | 恶魔之心掉落 | END-01 | 死亡后心出现在地面 |
| END-03 | 10 秒拾取读条 | END-02 | 受击回退，主动操作重置 |
| END-04 | 易手与死亡掉落 | END-03 | 持有者淘汰时心立即掉落 |
| END-05 | 交付流程 | END-03 | 交付触发终局结束 |
| END-06 | 单玩家结算 | END-05 | 系统分别输出每名玩家胜负 |
| END-07 | 双方失败 | END-06 | 双方分别失败，统计层记为双输 |

### 11.9 Spawn

出生逻辑本期不实现。只保留 `BossSpawnContext` 占位：

```text
spawnPosition
initialState
initialPhase
earlyAwaken
awakenReason
initialStrength
```

### 11.10 Debug

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| DEBUG-01 | 调试 Boss 生成 | CORE-02 | 不依赖正式出生流程即可生成 |
| DEBUG-02 | 仇恨与技能显示 | TARGET-08、SKILL-09 | 可查看目标和技能状态 |
| DEBUG-03 | 强制技能与时间快进 | SKILL-09 | 可快速验证阶段和技能 |
| DEBUG-04 | 结构化日志 | CORE-02 | 状态、目标、技能、结盟、终局可回放 |
| DEBUG-05 | 确定性 seed | CORE-02 | 相同输入和 seed 可复现 |

## 十二、系统不变量

1. 死亡后 Boss 不再选择目标、移动、普攻或施放技能。
2. 同一时间只能有一个主动技能施放。
3. 仇恨快照未被合法打断时，不得切换当前目标。
4. 塔不参与玩家目标竞争。
5. 结盟只提供目标过滤和仇恨加分，不直接修改移动。
6. 技能效果通过统一伤害、状态或召唤接口产生结果。
7. 任何数值变化都必须有配置来源。
8. 所有状态迁移必须有日志。
9. 出生逻辑未实现前，调试生成必须复用同一份运行时初始化入口。
10. 通用记忆不包含决策规则，只保存事实、快照和计时。
11. BossBehaviorController 只输出战斗意图，不直接执行移动、攻击、技能或结盟。

## 十三、完成定义

Boss Demo 完成时，以下流程必须可重复运行：

```text
调试生成 Boss
→ 进入战斗
→ 选择战斗意图
→ 选择并追击目标
→ 普攻和释放三个技能
→ 进入时间阶段和强化层
→ 玩家结盟或翻脸
→ Boss 死亡
→ 恶魔之心争夺
→ 分别结算双方胜负
```

验收要求：

- 无非法状态迁移。
- 无永久死锁。
- 不依赖正式出生逻辑。
- 不依赖正式动画和 UI。
- 每个阶段均有日志。
- 固定 seed 可复现战斗事件。