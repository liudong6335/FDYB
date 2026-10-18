# 《分道扬镳》Boss 功能开发清单

> 状态：Boss Demo 开发基线
> 用途：第一部分供人快速理解，第二部分供编码 Agent 按 Boss 实现规格与任务实施
> 文档边界：本文只负责 Boss 机制、Boss 对接口的消费、Boss 事件发布和掉落请求。
> 相关文档：《系统总览与文档索引》《Common 基础系统》《圣物系统设计》《Endgame 终局系统》《Player 与 AI 开发索引》

## 目录

- 第一部分：人读版
- 第二部分：Boss 实现规格

---

# 第一部分：人读版

## 一、开发目标

先完成一个可重复运行的 Boss 纵向 Demo，验证以下完整闭环：

```text
Boss 苏醒 → 玩家进入战斗 → Boss 选择目标 → 选择战斗意图 → 移动与攻击
→ 阶段强化 → 技能循环 → 玩家结盟 / 翻脸
→ Boss 死亡 → 发布 BossDiedEvent → 请求恶魔之心掉落
```

当前阶段不做正式出生流程、正式美术、完整成长系统、正式地图和联机。

## 二、Demo 范围

### 2.1 本期开发

- Boss 移动与追击。
- Boss 目标选择和仇恨总分。
- 普攻、技能槽施放框架、时间和强化阶段。
- Boss 与玩家结盟、玩家与玩家结盟。
- 翻脸、惩罚仇恨和盟友豁免。
- Boss 死亡事件与恶魔之心掉落请求。
- 调试场景、调试面板和结构化日志。

### 2.2 本期暂不开发

- Boss 出生点和苏醒倒计时正式逻辑。
- 正式模型、动画、音效和特效。
- 环境亲和度、双路线和完整掉落。
- 完整宝石、装备和技能书成长。
- 线上联机和网络同步。
- 最终数值平衡。

## 三、八个逻辑维度

### 3.1 移动逻辑

移动逻辑回答“Boss 现在去哪里”，不负责选择攻击目标。

核心流程：

```text
Boss 苏醒 → EntityMemory 启动位置快照采样
有目标 → 接近目标
进入攻击距离 → 停止移动
Survive → 每秒选择逃跑目标点 → NavMeshAgent 移动到目标
目标脱离范围 → 请求重新选择目标
没有玩家目标 → 前往最后已知位置
最后已知位置失效 → 前往安全区或战术驻守点
海平面收缩 → 向安全区移动
```

主要完成项：

- EntityMemory 每 5 秒（暂定/需要暴露数值）采样一次存活玩家位置并保存为位置快照；玩家仇恨都为零时，优先追击距离近的玩家。
- NavMesh 追击、停止和朝向。
- BossMovement 只读取最后已知位置和位置快照，不负责采样。
- BossTargeting 输出当前目标及 `effectiveChaseRange`；BossMovement 只消费追击距离，不重复计算残血。
- 进入攻击或仇恨范围后使用实时追击，不受 5 秒采样限制。
- Survive 每 1 秒重新选择逃跑目标点，不因 `slot1` 冷却而原地等待。
- 逃跑目标点使用多方向候选评分，综合威胁中心、NavMesh 可落点、战斗边界、安全区和其他敌人位置；实际路线交给 NavMeshAgent。
- 目标脱离和重新响应。
- 移动日志和调试显示。

### 3.2 战斗行为逻辑

战斗行为逻辑回答“Boss 当前应该进攻、追击、风筝、撤退、重整还是寻求结盟”。

核心意图：

```text
Engage       正常进攻
Chase        追击目标
Kite         占位枚举；当前 Demo 禁用
Survive      强行脱离战斗、逃跑并请求第一技能槽
SeekAlliance 判断打不过时尝试与玩家结盟
```

主要完成项：

- 根据生命值、近期承伤、技能冷却、目标数量和距离选择意图。
- 输出移动意图、攻击意图、技能槽请求和结盟请求。
- Boss 不实现普通战术撤退；只有 Survive（求生）会强行脱离战斗并逃跑，并受
  `maxSurviveDuration`、槽位可用性和恢复阈值约束。
- 所有阈值都暴露，方便不同 Boss 和怪物调整。
- Weak 生命低于 `35% × 0.9-1.1`，且 Boss 周围 20m 内非盟友存活玩家的当前生命总和
  大于 Boss 当前生命的 150% 时，优先进入 Survive（求生）。
- Normal 仅在生命低于 10%，且 20m 内非盟友玩家当前生命总和大于 Boss 当前生命的 200% 时
  才进入 Survive；其他情况优先残血追击/击杀。
- 两个逃生与追击条件同时成立时：Weak 优先逃生，Normal 仅在上述紧急条件成立时逃生，
  Rage 永不逃生。
- Weak 求生在生命恢复到 40% 时结束，Normal 求生在生命恢复到 30% 时结束。
- Survive 判断使用独立的 5 秒决策快照；进入后锁存，不因快照间隔内的血量或盟友关系变化
  实时退出，达到对应退出阈值时立即结束。
- `slotId=1` 为 `Disabled` 时不进入 Survive，直接走兜底决策；已进入时立即结束并走同一兜底决策。
- `slotId=1` 为 `Cooldown` 时允许短暂逃跑等待，只在转为 `Ready` 后请求一次；
  超过最大求生时长或无法恢复到退出阈值时强制结束。
- 进入 Survive 时调用 `IHealthService.EnterAutoRegen(RegenReason.Survive)`，由 `Health` 使用 Survive 自动回血配置持续推进生命恢复。
- 退出 Survive、进入 Rage、Boss 死亡或因 slot1 不可用结束时，调用 `IHealthService.ExitAutoRegen(RegenReason.Survive)`。
- Survive 自动回血不要求 Boss 先脱离战斗，默认不因受击完全停止；回血速率和上限由 `Health` 配置决定，Boss 不直接修改生命。
- Rage（暴走）禁止逃跑和结盟，只允许攻击、追击和请求配置为进攻用途的技能槽。

### 3.3 攻击逻辑

攻击逻辑回答“打谁、怎么打”。

核心流程：

```text
收集目标 → 计算仇恨总分 → 排序 → 5 秒快照
→ 普攻 → 技能触发 → 阶段强化 → 残血回血
```

仇恨总分：

```text
5 秒真实伤害 + 残血修正 + relicHate + 追杀仇恨
```

主要完成项：

- 5秒伤害窗口和残血修正。
- 圣物、结盟追杀和塔仇恨。
- 同分裁决与 5秒目标快照。
- 统一记忆容器：保存目标快照、局势快照、仇恨历史、技能冷却和结盟计时。
- 目标跌破 10% 的立即重选。
- 40m 残血追击。
- 普攻和统一伤害结算。
- 技能槽请求、槽位策略和技能施放状态机。
- 三阶段、强化层、控制抗性和残血回血。
- 回血是 `Health` 的被动机制，不创建 Intent；Boss 只通过 `IHealthStateProvider` 读取是否允许回血。
- Rage 和战斗状态对回血的禁用均由 Health/CombatState 配置执行，BossBehaviorController 不直接修改生命。
- 角色实际战力 = 当前生命值总量 × 1 + 当前 DPS × 4 + 当前防御值 × 2
  + 已放置且未销毁塔的当前攻击总和 × 1。
- Boss 读取战力 = 实际战力 × 随机 `0.95-1.05`，随机值在 10 秒策略周期内固定。

### 3.3.1 残血追击业务逻辑

这里的“残血”指玩家目标的生命比例，不是 Boss 自身的残血回血机制。残血同时影响
“仇恨排序”和“追击距离”，但不能绕过盟友、死亡、战斗区域和技能状态。

```text
hpRatio = currentHp / maxHp

isLowHp =
    hpRatio < lowHpModifierStart

lowHpModifier =
    clamp((lowHpModifierStart - hpRatio) / lowHpModifierWindow, 0, 1)
    * lowHpModifierMax

effectiveChaseRange =
    isLowHp ? lowHpChaseRange : normalChaseRange

finalScore =
    damageWindow5s
    + lowHpModifier
    + relicHate
    + revengeHate
```

- `lowHpModifierStart` 暂定 30%，使用严格小于；正好 30% 不进入残血追击。
- `lowHpModifierWindow` 暂定 20%，即从 30% 到 10% 完整计算到封顶。
- `lowHpModifierMax` 暂定 2000；生命比例 ≤ 10% 时固定为 2000。
- `normalChaseRange` 暂定 20m，`lowHpChaseRange` 暂定 40m。
- 残血变化实时更新 `lowHpModifier` 和当前目标的 `effectiveChaseRange`，但常规每 5 秒的目标快照仍不变；
  只有首次跌破 10% 才立即打断并重新排序。
- 当前目标跨入 <30% 时，追击范围可立即扩展至 40m；跨回 ≥30% 时立即收缩至 20m。
- 目标处于残血且距离 ≤ 40m 时，Boss 继续追击；超过 40m 时立即终止该次残血追击并重新选择目标。
- 目标恢复到 30% 以上时，`effectiveChaseRange` 立即回到 20m；若目标已超出 20m，则当前目标失效并按正常规则立即重选。
- 目标成为盟友、死亡、不可选或进入无效区域时，立即从残血追击候选池移除。
- 没有合法玩家目标时，才进入塔目标回退；塔不享受玩家残血修正。
- 残血追击只改变追击距离和仇恨分值，不重置目标快照、不绕过动作锁，也不改变 Boss 强度状态。

### 3.4 技能施放逻辑

Boss 行为层只回答“当前状态下请求哪个技能槽”，不回答具体技能名和技能效果。
技能槽中绑定的 `SkillCard` 才定义效果，后续可以替换、扩展或从随机池抽取。

职责拆分：

```text
SkillCard          只定义技能效果、目标、施法参数和表现引用
SkillSlotBinding   定义实际冷却、优先级、触发策略、斩杀参数及槽位规则
SkillSlotRuntime   保存单个槽位的剩余冷却、充能和就绪状态
技能控制器         接收 slotId，管理全局施放状态、施放锁和打断
```

当前 Demo 使用 3 个技能槽：

| 技能槽 | 当前临时 SkillCard | 基础 CD | 冷却开始时机 | 充能数 | 当前请求状态 |
| --- | --- | --- | --- | --- | --- |
| `slot1` | 地震波 | 20 秒 | `Commit` | 1 | Survive；或 Engage / Chase / Rage 中连续 3 秒未命中敌对玩家 |
| `slot2` | 分身 | 15 秒 | `Commit` | 1 | Engage / Chase / Rage；有合法战斗目标、槽位 Ready；效果条件由技能系统判定 |
| `slot3` | 嗜血狂暴 | 30 秒 | `Commit` | 1 | Engage / Chase / Rage；有合法战斗目标、槽位 Ready；效果条件由技能系统判定；第三槽随机池后置 |

“保命技能”只是玩家体验描述，不是槽位类型、技能类型或系统机制。

当前 Demo 技能请求顺序：

1. `Survive` 时只请求 `slot1`，求生优先于其他技能。
2. `Engage / Chase / Rage` 中连续 3 秒未命中敌对玩家时，`slot1` 作为追击受阻的战术例外，优先于普通战斗技能。
3. 其他战斗情况下，`slot3` 满足条件时优先，其次 `slot2`。
4. `SeekAlliance` 不请求当前配置的三个技能。
5. 没有符合条件的技能时，继续普攻、移动或执行当前战斗意图。

### 3.4.1 当前技能槽 CD 规则

```text
baseCooldown = SkillSlotBinding.cooldown
effectiveCooldown = baseCooldown / skillFrequencyMultiplier
```

- 每个技能槽独立计时；释放一个槽不会改变其他槽的剩余冷却。
- 阶段倍率不修改 `skillFrequencyMultiplier`；该值默认固定为 1.0，实际 CD 仍按上述公式计算。
- 冷却从 `Commit` 提交效果时开始，不从施放开始、前摇结束或恢复结束开始计算。
- 当前三个槽的 `maxCharges = 1`，不使用充能；冷却完成后才恢复 `Ready`。
- `Cooldown` 状态下冷却持续推进；`Disabled` 状态下暂停冷却，不产生请求。
- 从 `Disabled` 恢复后，保留原来的 `cooldownRemaining`，不会自动重置为 0。
- 阶段切换、Rage、结盟和翻脸不自动重置当前剩余 CD。
- 冷却使用游戏时间；暂停菜单、动画和 UI 不阻塞计时。
- 遭遇开始或调试重置时，三个槽统一重置为 `Ready`。

全局施放状态：

```text
Idle → Checking → Casting → Resolving → Recovering → Idle
```

槽位状态：

```text
Ready / Cooldown / Disabled
```

主要完成项：

- 技能包只配置效果、目标、前摇、范围、伤害和表现。
- 技能槽配置绑定哪个技能包、实际冷却、冷却开始时机、优先级、触发条件和斩杀优先参数。
- 同一技能包挂载到不同槽位时，可以分别使用不同冷却。
- Boss 决策只产生 `BossSkillRequest(slotId, stateReason)`，不依赖 `skillId` 或技能名称。
- 完整施放流程：请求 slotId → 读取该槽绑定的 SkillCard → 检查就绪与触发策略
  → 解析目标
  → 开始施放 → 提交效果 → 恢复 → 按槽位策略进入冷却 → 写事件和日志。
- 目标锁定、范围、效果、召唤或控制均由该槽当前绑定的 SkillCard 和通用技能系统决定。
- 目标在施放开始时锁定；目标死亡、失效或盟友豁免时按技能包的目标模式和槽位打断规则处理。
- 同时只允许一个主动施放。
- 斩杀优先不是 Boss 全局固定规则，而是 `SkillSlotBinding` 的可选策略；是否启用、比较公式、
  优先级和 `lethalTolerance` 待技能设计。当前 Demo 可暂用 `0.1` 作为占位值。
- 死亡和硬控可中断。
- 当前 Demo 槽位配置为地震波、分身、嗜血狂暴；均为临时技能卡，可以替换。
- 技能效果内部的触发条件、技能池和随机规则由技能系统负责；Boss 不新增技能效果判断。
- 技能事件和结构化日志。

### 3.5 结盟逻辑

结盟只在 Boss 苏醒后且场上恰好有 2 名玩家存活时生效。

主要流程：

```text
10 秒局势判断 → 申请 / 主动提议 → 接受或拒绝
→ 盟友豁免 → 限时结束 / 翻脸 → 追杀仇恨
```

主要完成项：

- 绝对门禁：`alivePlayerCount != 2` 时禁止 Boss + 玩家结盟，拒绝申请、停止主动提议并取消待确认请求。
- 任一玩家永久淘汰时，立即结束其参与的所有玩家间结盟和 Boss + 玩家结盟，不进入缓冲、不产生追杀仇恨。
- Boss + 玩家结盟：时长暂定 20 秒；使用上一周期 10 秒局势快照决定是否愿意结盟；
  申请者当前对 Boss 的总仇恨必须为 0，且不处于残血排除状态。
- Boss 主动提议：只在 `BossAllianceAllowed=true` 且未触发 Boss 战力压制时发起，每周期同一玩家只提议一次，
  主动概率上限 20%；玩家申请不经过主动概率。
- 申请失败、被拒或超时后进入 30 秒申请冷却。
- 玩家 + 玩家结盟：时长暂定 45 秒；每名玩家每局一次申请机会；
  发起后 3 秒内确认，否则按拒绝处理。
- 结盟期间盟友不进入对方攻击目标池；玩家间负面技能不生效，正面技能正常生效。
- Boss + 玩家结盟中，盟友造成真实伤害会缩短时长，非盟友造成真实伤害会延长时长；按攻击者归属和游戏秒去重，同一攻击者每秒最多触发一次，最终限制在 `[0, 基础时长]`。
- 翻脸或自然结束时，T0 立即结束关系并递增 `allianceVersion`，刷新目标池、目标快照、5 秒求生快照、
  10 秒策略中的结盟区块和 AI 可见状态；不重掷其他战略随机字段。
- T+3 秒惩罚仇恨生效，增加 5000 追杀仇恨并递增 `revengeHateVersion`，立即刷新目标仇恨快照；
  仇恨在 30 秒内线性消散。
- 玩家 + 玩家结盟中攻击对方会立即破裂；攻击方获得惩罚仇恨，被攻击方获得 3 秒缓冲。
- Boss 进入 Rage 后不允许结盟、不接受申请、不主动提议。
- Boss 死亡后立即结束所有结盟、发布 `BossDiedEvent` 并请求死亡掉落；是否进入心争夺和是否结束对局由 Endgame 判断。死亡导致的结盟结束不进入缓冲、不产生追杀仇恨。

### 3.6 死亡逻辑

死亡逻辑只回答 Boss 如何结束自己的行为，不负责恶魔之心、玩家胜负结算或终局流程。

Boss 死亡流程：

```text
血量归零 → 停止全部 Boss 行为 → 释放动作锁
→ 结束全部结盟并递增 allianceVersion
→ 发布 BossDiedEvent（Boss 死亡事件）
→ 向 ILootDropService 请求死亡掉落
```

玩家死亡：

```text
Player 系统判定玩家永久淘汰
→ 发布 PlayerEliminatedEvent（玩家淘汰事件）
→ Boss 消费事件，结束该玩家参与的全部结盟并递增 allianceVersion
→ 由玩家/对局系统继续处理淘汰状态和后续流程
```

主要完成项：

- 发布 BossDiedEvent（Boss 死亡事件）。
- 停止移动、攻击、技能和结盟。
- 通过通用掉落接口请求 Boss 死亡掉落；恶魔之心配置为必掉 Item。
- 玩家死亡结束其参与的结盟；Boss 死亡结束所有结盟；均不进入追击缓冲、不产生追杀仇恨。
- 死亡事件刷新所有存活观察者的活动快照，历史快照不改写。
- 恶魔之心的拾取、易手、交付和胜负结算不属于 Boss 模块。

### 3.7 出生逻辑

出生逻辑本期暂不开发，只保留配置接口：

```text
出生位置 / 初始阶段 / 是否提前苏醒 / 苏醒原因 / 初始强度
```

移动、攻击和调试生成都必须使用同一份初始状态接口，不能在代码中写死。

### 3.8 系统边界

Boss 战斗模块只输出自己的 Intent（意图）、Request（请求）和 Event（事件）：

- 召唤物生成后，其 AI、生命周期、攻击目标和数值由召唤物系统负责。
- 塔目标由目标系统提供；是否持续拆同一座塔、是否切换目标由塔/目标系统负责，Boss 不保存拆塔承诺。
- 海平面、环境伤害和地图收缩由环境系统负责。
- Boss 死亡后只发布 `BossDiedEvent` 并请求死亡掉落；Boss 不定义获胜者，也不处理恶魔之心和玩家结算。
- 恶魔之心是 Boss 死亡掉落表中的掉落 Item；掉落位置、拾取、易手、交付和 Endgame 由外部系统负责。

## 四、脚本概览

脚本统一分为三类：外部引用脚本、通用脚本、Boss 专属脚本。新增规划标记为“新增”，其余为已有规划或已有脚本。

### 4.1 外部引用脚本

外部引用脚本由对应系统负责实现，Boss 只通过 `8.4` 的接口消费。脚本名和内部结构由所属系统维护，Boss 文档不复制其实现。
当前清单包含 10 项外部契约。

| 外部契约 / 脚本 | 归属系统 | Boss 使用方式 | 主要功能 |
| --- | --- | --- | --- |
| Player 生命周期脚本 / `PlayerEliminatedEvent` 发布端 | Player | 订阅永久淘汰事件 | 发布玩家永久淘汰，触发结盟结束和活动快照刷新 |
| Endgame 控制器 / `EncounterEnded` 发布端 | Endgame | 订阅遭遇结束事件 | 消费 `BossDiedEvent` 和恶魔之心，判断心争夺与最终结束 |
| `ICombatTargetProvider` 实现 | Target / Player | 读取候选目标 | 提供玩家、召唤物、塔的 ID、阵营、位置、存活和可选中状态 |
| `ICombatStatsProvider` 实现 | Stats / Player | 读取战斗属性 | 提供生命、攻击、攻击间隔、防御和属性版本 |
| `ITowerProvider` 实现 | Tower | 读取塔目标和塔攻击 | 提供可攻击塔、有效塔攻击总和及状态变化 |
| `IRelicStateProvider` 实现 | Items / 圣物 | 读取圣物仇恨 | 提供持有状态、`relicHate`、版本和变化事件 |
| `ISummonFactory` 实现 | Summon / World | 请求生成分身 | 接收召唤请求并生成召唤单位 |
| `ILootDropService` 实现 | Items / Loot | 请求死亡掉落 | 在指定位置生成恶魔之心及其他掉落 |
| `ISafeZoneProvider` 实现 | World | 读取安全点 | 提供安全区和战术驻守点 |
| `ICombatAreaProvider` 实现 | World | 读取战斗边界 | 提供战斗区域和允许移动范围 |

### 4.2 通用脚本

通用脚本由 Common 或共享技能框架负责，可供 Boss、玩家、怪物和 AI 复用。
当前清单包含 16 个通用脚本。

| 脚本 | 主要功能 | Boss 侧使用方式 |
| --- | --- | --- |
| `EventBus.cs`（新增） | 发布、订阅、事件队列和稳定分发顺序 | 接收 `PlayerEliminatedEvent`、`EncounterEnded`，发布 Boss 和结盟事件 |
| `EntityMemory.cs` | 伤害历史、位置快照、目标快照、决策快照、计时器和关系版本 | 保存所有 Boss 快照和记忆事实，不保存决策规则 |
| `CombatPowerService.cs` | 实际战力、Boss 感知战力、分项和版本快照 | 结盟、战力压制和目标同分裁决 |
| `AllianceService.cs` | 通用结盟请求、响应、计时、关系过滤、破裂和事件 | 执行 Boss 结盟的通用运行时部分 |
| `Health.cs`（扩展现有） | 当前生命、受伤、死亡、普通回血和执行回血 | 执行 Survive 自动回血，Boss 不直接修改生命 |
| `IHealthStateProvider.cs`（规划名，兼容现有 `IHealthProvider.cs`） | 提供存活、当前生命、最大生命和战斗状态版本 | Boss 只读取 Health 状态 |
| `IHealthService.cs`（新增） | 接收 `EnterAutoRegen`、`ExitAutoRegen` 等回血模式请求 | Boss 进入/退出 Survive 时调用 |
| `CombatState.cs`（新增） | 战斗进入、脱战延迟、战斗状态版本 | 提供普通回血和战斗状态判断 |
| `DamageService.cs`（新增） | 统一伤害入口、防御、护盾、减伤、来源和预览 | Boss 普攻和技能效果统一走该入口 |
| `GameClock.cs`（新增） | 游戏时间和遭遇时间 | Boss 冷却、快照、Rage 和结盟计时 |
| `EncounterRandom.cs`（新增） | encounter seed 驱动的确定性随机 | Boss 策略随机、第三槽随机和目标同分随机 |
| `Skills/SkillCard.cs` | 技能效果、目标、施法、范围和表现引用 | 提供槽位绑定的具体技能卡 |
| `Skills/SkillSlotBinding.cs` | 冷却、冷却开始时机、优先级、触发策略和斩杀参数 | 决定技能效果条件、CD 和优先级 |
| `Skills/SkillSlotRuntime.cs` | 单槽冷却、充能、Ready / Cooldown / Disabled | 提供槽位状态，不选择槽位 |
| `Skills/SkillCastController.cs` | 全局施放状态、施放锁、前摇、提交、恢复和打断 | 接收 Boss 技能请求并执行统一施放流程 |
| `Skills/SkillEffectApplier.cs`（新增） | 校验效果触发条件并应用回血、减速、召唤、增益等效果 | 执行分身和嗜血狂暴的实际效果，Boss 不读取效果状态 |

### 4.3 Boss 专属脚本

当前规划 15 个 Boss 专属脚本。

| 脚本 | 主要功能 | 明确不负责 |
| --- | --- | --- |
| `BossContext.cs`（新增） | 组装 Boss 专属依赖，注入外部 Provider、Common 服务和配置 | 不保存战斗决策，不实现 Provider |
| `BossEventBridge.cs`（新增） | 订阅和取消外部事件，将事件规范化、排序并交给 BossController | 不决定结盟、死亡和快照业务规则 |
| `BossController.cs` | 生命周期、状态迁移、Tick 调度、动作锁释放、死亡结算、事件发布、掉落请求和强制快照刷新编排 | 仇恨公式、技能效果、移动算法 |
| `BossDefinition.cs` | Boss、阶段、技能槽、第三槽随机池和死亡掉落表配置 | 运行时 HP、冷却、目标和拾取 |
| `BossStats.cs` | 基础属性、有效属性和属性修改器 | 阶段切换、当前生命和技能施放 |
| `BossPhaseController.cs` | Weak / Normal / Rage、强化层和倍率 | 当前生命、技能效果和目标选择 |
| `BossMovement.cs` | 消费移动意图，执行追击、停止、朝向、逃跑和安全区移动 | 目标选择、技能施放、战斗决策 |
| `BossRetreatTargetResolver.cs` | Survive 候选点、NavMesh 过滤、威胁评分、目标点滞回和卡住判定 | 路径计算、移动、攻击和仇恨 |
| `BossTargeting.cs` | 候选目标、仇恨总分、残血修正、5 秒快照、同分裁决和重选 | 移动、伤害执行和圣物数值计算 |
| `BossBehaviorController.cs` | 战斗意图、求生、重整、结盟请求和技能槽优先级；根据 `lastPlayerHitTime` 计算 `noPlayerHitElapsed` | 不移动、不攻击、不释放技能、不判断效果条件 |
| `BossCombat.cs` | 普攻、受伤、统一伤害调用、控制抗性、Boss 本体命中事件和 `lastPlayerHitTime` 更新 | 战斗意图、技能调度和目标排序 |
| `BossSkillController.cs` | 接收 Boss 请求，按 `slot3 > slot2` 尝试槽位，接收技能系统结果并输出日志 | 不判断分身、增益、目标过滤或效果持续时间 |
| `BossSkillExecutor.cs` | Boss 侧技能目标解析和上下文适配，调用 `SkillEffectApplier` | 不选择槽位、不决定 CD、不保存效果状态 |
| `BossAllianceController.cs` | 结盟总门禁、10 秒策略快照、Boss 战力压制、主动提议、资格判断和 Boss 侧翻脸结果 | 通用结盟计时、普通伤害仇恨和移动 |
| `BossDebugTools.cs` | HUD、时间快进、强制技能槽触发、仇恨注入、逃跑评分和日志 | 正式战斗决策 |

## 五、里程碑

| 里程碑 | 内容 | 完成结果 |
| --- | --- | --- |
| M1 | 属性、目标、移动、行为、普攻和强度状态 | Boss 可追击、普攻，并在 Weak / Normal / Rage 间正确切换 |
| M2 | 技能槽框架和暂定技能卡 | Boss 可按状态请求 slotId；各槽独立冷却，替换技能卡不影响 Boss 决策 |
| M3 | 结盟 | Boss 结盟、玩家结盟和翻脸可跑通 |
| M4 | 死亡与掉落 | Boss 能发布死亡事件并请求恶魔之心掉落 |
| M5 | Demo 集成 | 通过现有 Provider 接入塔、圣物、海平面和掉落；只做 Boss 侧适配，不实现外部系统 |

## 六、当前需要冻结的数值

| 项目 | 当前状态 |
| --- | --- |
| Boss 基础属性 | 参考现有文档，待最终确认 |
| Weak / Normal / Rage 默认倍率 | 最大生命、攻击和攻击速度默认使用 0.6 / 1.0 / 2.0；可由 BossPhaseMultiplierConfig 独立覆盖 |
| 阶段固定倍率 | 移速、技能频率和仇恨倍率默认固定为 1.0，不随阶段变化 |
| 强化层倍率 | 每层攻击 / 技能频率倍率和最大层数必须显式配置 |
| 第一技能槽绑定 | 地震波；CD 20 秒 |
| 第二技能槽绑定 | 分身；CD 15 秒 |
| 第三技能槽绑定 | 嗜血狂暴；CD 30 秒；随机池后置 |
| 技能槽冷却规则 | 三槽独立；`Commit` 开始；`maxCharges=1`；禁用时暂停 |
| Boss + 玩家结盟时长 | 暂定 20 秒 |
| 玩家 + 玩家结盟时长 | 暂定 45 秒 |
| 结盟申请冷却 | 暂定 30 秒 |
| 强化层幅度 | 5% 或 10% |
| 虚弱恢复 | 初始 50-60%，每分钟恢复约 10% |
| Weak 求生阈值 | 生命 < 35% × 0.9-1.1，非盟友总生命 > Boss 当前生命 × 150% |
| Normal 求生阈值 | 生命 < 10%，非盟友总生命 > Boss 当前生命 × 200% |
| Weak 求生退出阈值 | 40% |
| Normal 求生退出阈值 | 30% |
| 求生敌我总血量条件 | 20m 内非盟友玩家当前生命总和 > Boss 当前生命 × 150% |
| 求生判断快照 | 每 5 秒（暂定） |
| 最大求生时长 | 15 秒（暂定） |
| 求生强制退出冷却 | 5 秒（暂定） |
| Rage 逃跑 | 禁用 |
| 第一技能槽 | `slotId=1`；地震波；CD 20 秒；Survive 或连续 3 秒未命中敌对玩家时请求 |
| 第二技能槽 | `slotId=2`；分身；CD 15 秒 |
| 第三技能槽 | `slotId=3`；嗜血狂暴；CD 30 秒 |
| 第三槽随机池 | 当前未启用；Demo 阶段固定嗜血狂暴 |
| 圣物接口 | `IRelicStateProvider` 提供 relicHate 与版本；数值和减伤由圣物/伤害系统负责 |
| 玩家定位采样 | 每 5 秒 |
| 最后已知位置有效期 | 暂定 15 秒 |

---

# 第二部分：Boss 实现规格（供编码 Agent）

## 七、编码 Agent 执行规则

1. 按依赖顺序和里程碑开发；任务编号只用于追踪，不代表执行顺序。
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

脚本职责以第 `4.1` 至 `4.3` 节为准。本节只固定目录归属和依赖边界。

### 8.1 外部引用脚本

外部引用脚本由所属系统维护，不放入 `Assets/Script/Boss`：

```text
Player 生命周期 / PlayerEliminatedEvent
Endgame 控制器 / EncounterEnded
ICombatTargetProvider 实现
ICombatStatsProvider 实现
ITowerProvider 实现
IRelicStateProvider 实现
ISummonFactory 实现
ILootDropService 实现
ISafeZoneProvider 实现
ICombatAreaProvider 实现
```

- Boss 只持有接口或稳定事件引用，不直接依赖外部系统实现类。
- 外部脚本的具体文件名和内部结构由各自权威文档决定。
- `BossContext.cs` 负责把这些外部引用注入 Boss 模块。
- `BossEventBridge.cs` 负责外部事件的订阅、退订和规范化。

### 8.2 通用脚本

```text
Assets/Script/Common/
├─ EventBus.cs                         // 新增
├─ EntityMemory.cs
├─ CombatPowerService.cs
├─ AllianceService.cs
├─ Health.cs                           // 扩展现有
├─ IHealthStateProvider.cs             // 规划名，兼容现有 IHealthProvider.cs
├─ IHealthService.cs                   // 新增
├─ CombatState.cs                      // 新增
├─ DamageService.cs                    // 新增
├─ GameClock.cs                        // 新增
├─ EncounterRandom.cs                  // 新增
└─ Skills/
   ├─ SkillCard.cs
   ├─ SkillSlotBinding.cs
   ├─ SkillSlotRuntime.cs
   ├─ SkillCastController.cs
   └─ SkillEffectApplier.cs            // 新增
```

- 通用脚本不得读取 Boss 阶段、仇恨阈值、求生阈值或结盟概率。
- Common 负责事实、状态、计时、伤害、事件和技能执行能力；Boss 负责把业务意图传给这些能力。
- `EventBus.cs` 同时提供发布和订阅，不允许 Boss 直接依赖其他系统的 MonoBehaviour。
- `Health.cs` 负责执行回血；`IHealthService.cs` 只提供回血模式入口。
- `SkillCastController.cs` 负责统一施放；`SkillEffectApplier.cs` 负责效果条件和效果应用。

### 8.3 Boss 专属脚本

```text
Assets/Script/Boss/
├─ BossContext.cs                      // 新增
├─ BossEventBridge.cs                  // 新增
├─ BossController.cs
├─ BossDefinition.cs
├─ BossStats.cs
├─ BossPhaseController.cs
├─ BossMovement.cs
├─ BossRetreatTargetResolver.cs
├─ BossTargeting.cs
├─ BossBehaviorController.cs
├─ BossCombat.cs
├─ BossSkillController.cs
├─ BossSkillExecutor.cs
├─ BossAllianceController.cs
└─ BossDebugTools.cs
```

- `BossController.cs` 统一执行死亡结算、动作锁释放、事件发布、掉落请求和强制快照刷新，不新增独立死亡脚本。
- `BossEventBridge.cs` 只做事件入口和顺序稳定化，不包含 Boss 规则。
- `BossBehaviorController.cs` 只输出 Intent 和槽位优先级，不移动、不攻击、不判断技能效果。
- `BossCombat.cs` 负责更新 Boss 本体命中时间，行为层只读取结果。
- `BossSkillController.cs` 只尝试请求槽位并处理技能系统返回结果，不读取分身或增益状态。
- `BossSkillExecutor.cs` 只做 Boss 上下文和目标解析，效果统一交给 `SkillEffectApplier.cs`。
- `BossAllianceController.cs` 负责 Boss 专属结盟规则，通用请求、计时和关系由 `AllianceService.cs` 承担。

### 8.4 Boss 外部接口契约

> 本节是 Boss 与 Common、Player、Items、World、Endgame 之间的唯一接口契约。
> Boss 只读取输入 Provider，只通过稳定事件和输出端口发布结果。

#### 8.4.1 输入 Provider

| 接口 | 最小职责 | 权威系统 |
| --- | --- | --- |
| `ICombatTargetProvider` | 提供玩家、召唤物、塔的实体 ID、存活、阵营、位置、可选中状态 | Common / Target |
| `ICombatStatsProvider` | 提供当前生命、当前攻击、攻击间隔、防御、属性版本 | Common / Stats |
| `ITowerProvider` | 提供可攻击塔、有效塔攻击总和、塔状态变化 | Tower |
| `ICombatPowerProvider` | 提供实际战力、Boss 感知战力、分项和版本快照；由 CombatPowerService 实现 | Common / Power |
| `IRelicStateProvider` | 提供是否持有圣物、relicHate、仇恨版本和变化事件 | Items / 圣物 |
| `IHealthStateProvider` | 提供存活、当前生命、最大生命、是否允许回血和战斗状态版本 | Common / Health |
| `IHealthService` | 接收 Boss 的自动回血模式请求并交由 Health 执行，不向 Boss 暴露生命修改 | Common / Health |
| `IDamageCalculator` | 统一结算伤害并提供技能伤害预览 | Common / Combat |
| `ISafeZoneProvider` | 提供当前安全点和战术驻守点 | World |
| `ICombatAreaProvider` | 提供战斗区域边界和允许移动范围 | World |
| `ISummonFactory` | 接收召唤请求并生成单位 | Summon / World |
| `ILootDropService` | 接收掉落请求并在指定位置生成 Item | Items / Loot |
| `IEventBus` | 提供发布和订阅能力；Boss 订阅玩家淘汰、遭遇结束，发布 Boss 和结盟事件 | Common / EventBus |
| `IGameClock` | 提供游戏时间和遭遇时间 | Common / Clock |
| `IRandomSource` | 提供 encounter seed 驱动的确定性随机 | Common / Random |

#### 8.4.2 事件契约

**输入事件**

`PlayerEliminatedEvent`

```text
eventId
encounterId
playerEntityId
eliminationGameTime
eliminationReason
killerEntityId
eliminationVersion
```

- 发布者：Player / 对局系统。
- Boss 只消费玩家永久淘汰，不消费普通死亡和回魂申请期事件。
- `killerEntityId` 可以为空，环境淘汰或无法确定来源时允许为空。

`EncounterEnded`

```text
eventId
encounterId
endGameTime
endReason
endgameVersion
```

- 发布者：Endgame 或对局系统。
- Boss 收到后停止全部 Boss 行为，不负责计算最终胜负。
- `EncounterEnded` 与 `PlayerEliminatedEvent` 同帧时，按第 `10.1` 节顺序处理。

**Boss 输出事件**

**BossDiedEvent**

```text
eventId
encounterId
bossEntityId
deathGameTime
deathPosition
killerEntityId
damageSourceId
guaranteedDropIds
dropContextId
finalPhase
enrageLayer
allianceVersion
snapshotVersion
```

- 必需字段：事件、遭遇、Boss、死亡时间、死亡位置和掉落上下文。
- `killerEntityId` 与 `damageSourceId` 至少填写能确定死亡来源的一项；环境死亡允许 killer 为空。
- `finalPhase`、`enrageLayer`、`allianceVersion`、`snapshotVersion` 用于日志、回放和诊断，不参与 Endgame 胜负判断。
- Boss 只发布死亡事实和掉落请求，不定义获胜者。
- 恶魔之心是 `guaranteedDropIds` 中配置的掉落 Item。
- Boss 不处理恶魔之心拾取、易手和交付。

**结盟事件**

```text
AllianceStateChangedEvent
AllianceEndedEvent
AllianceRevengeAppliedEvent
```

事件只携带状态、参与者、版本和时间；Endgame 和 AI 自行决定是否消费。

#### 8.4.3 输出掉落

```text
LootDropRequest:
    sourceEntityId
    deathPosition
    guaranteedDropIds
    dropContextId
    ownerId
```

- `BossDiedEvent` 中的掉落字段是死亡事实和审计信息。
- 实际生成由后续 `LootDropRequest` 执行。
- 两处数据必须来自同一份 `BossDefinition` 死亡掉落表。
- 调用顺序：Boss 死亡结算 → 发布 `BossDiedEvent` → 提交 `LootDropRequest` → Item 系统发布恶魔之心生成事件。
- Boss 不持有恶魔之心实例。

#### 8.4.4 Fake 与测试

- 所有 Provider 必须可 Fake。
- 固定 seed 下事件顺序和版本必须一致。
- Boss 单元测试不得依赖完整场景或正式 Endgame。
- 跨系统集成测试只验证事件、ID 和版本契约，不验证其他系统的内部实现。
- 模块禁止使用 `FindObjectOfType`、全局单例或 `UnityEngine.Random`。
- 所有事件使用稳定实体 ID 排序，确保相同 seed 可复现。

## 九、核心状态与数据契约

### 9.1 Boss 生命周期状态

生命周期状态只描述 Boss 当前处于生存流程的哪个阶段，不描述强度：

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
任意非 Dead 状态 → Dead
```

### 9.2 Boss 运行时状态

```text
currentHp
encounterStartTime
encounterTime
lifecycleState
phaseState
enrageLayer
weaknessStrength
currentTarget
movementState
behaviorState:
    currentIntent
    surviveSlot1RequestPending
    surviveElapsed
    surviveForcedExitPending
    surviveForcedExitCooldownRemaining
    retreatDestination
skillSlots[slotId].state
skillSlots[slotId].cooldownRemaining
lastPlayerHitTime
noPlayerHitElapsed
allianceState
allianceId
allianceTargetId
allianceVersion
allianceRequestState
entityMemory
isDead
```

`lifecycleState` 使用 9.1 的 `Dormant / Awakening / Combat / Alliance / Dead`；`phaseState` 使用 9.3 的 `Weak / Normal / Rage`。

`behaviorState` 由 `BossBehaviorController` 拥有，`BossController` 只持有引用；`surviveSnapshot`、`combatMemory` 等快照数据保存在 `EntityMemory`，不重复放入 `behaviorState`。

### 9.3 Boss 强度状态

Boss 强度状态只有三种，与生命周期状态、强化层相互独立：

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
encounterTime >= rageAtTime → Rage
Weak 在 rageAtTime 仍未恢复 → Rage
Rage 不回到其他状态
```

说明：

- `Weak` 表示初始强度低于 100%，按 `weaknessStrength` 恢复。
- `Normal` 表示使用标准属性和倍率。
- `Rage` 表示到达 `rageAtTime` 后锁定为暴走状态，不再回到其他强度状态；进入 Rage 后不逃跑、不结盟，只执行攻击、追击并请求配置为进攻用途的技能槽。
- `encounterTime = gameTime - encounterStartTime`，`encounterStartTime` 以 Boss 苏醒为起点。
- `rageAtTime` 暂定 480 秒（苏醒后 8 分钟）；原 `phaseTimes = [300s, 480s]` 删除，避免与三状态和强化层产生三套时间口径。
- `enrageLayer` 仍按 `enrageInterval = 30s` 独立累计，不负责切换 `phaseState`。

状态输出：

```text
damageMultiplier
maxHealthMultiplier
attackSpeedMultiplier
moveSpeedMultiplier
skillFrequencyMultiplier
hateMultiplier
```

默认阶段倍率：

```text
Weak   → 0.6
Normal → 1.0
Rage   → 2.0
```

上述数值只作用于最大生命、攻击和攻击速度。移速、技能频率和仇恨倍率固定为 1.0，不随阶段变化。

`maxHealthMultiplier` 修改最大生命，当前生命不因阶段切换回填；阶段切换时只按新的最大生命做上限钳位。

阶段倍率与强化层按以下顺序叠加：

```text
enrageMultiplier =
    1 + min(enrageLayer, maxEnrageLayers) * enrageStep

effectiveStat =
    baseStat
    * phaseStatMultiplier(stat)
    * enrageStatMultiplier(stat)
```

- `phaseStatMultiplier(stat)` 来自 Weak / Normal / Rage：仅最大生命、攻击和攻击速度使用 0.6 / 1.0 / 2.0，其他属性为 1.0。
- `enrageStatMultiplier(stat)` 只来自 `enrageLayer`；默认作用于攻击和技能频率，其他属性不叠加强化层倍率，除非显式配置。
- 强化层不因 Weak → Normal 或 Normal → Rage 重置，进入 Rage 后继续叠加。
- 当前生命值和最大生命值不因阶段或强化层变化而回填。
- 最大生命、攻击和攻击速度使用同一叠加顺序；移速、技能频率和仇恨的阶段倍率固定为 1.0。

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
SeekAlliance
```

意图输入：

```text
hpRatio
recentDamage
incomingDamageRate
nearbyEnemyCount
nearbyEnemyHpTotal
currentTarget
distanceToTarget
skillSlots[slotId].state
skillSlots[slotId].cooldownRemaining
lastPlayerHitTime
noPlayerHitElapsed
surviveSlot1RequestPending
surviveElapsed
surviveForcedExitPending
surviveForcedExitCooldownRemaining
phaseState
isWeak
allianceAvailable
combatMemory
surviveSnapshot
```

意图输出：

```text
movementIntent
attackIntent
skillSlotRequest
allianceRequest
retreatDestination
intentDuration
```

核心参数：

```text
behaviorDecisionInterval
surviveSnapshotInterval
surviveEnemyRadius
weakSurviveEnemyHpMultiplier
normalSurviveEnemyHpMultiplier
weakSurviveEnterHpRatio
weakSurviveExitHpRatio
normalSurviveEnterHpRatio
normalSurviveExitHpRatio
surviveHpNoiseMin
surviveHpNoiseMax
maxSurviveDuration
surviveForcedExitCooldown
escapeTargetInterval
escapeDirectionCandidateCount
escapeStuckWindow
escapeStuckProgressRatio
escapeMinProgressDistance
escapePathPendingTimeout
escapeNoPathTimeout
escapeDestinationMinMove
escapeScoreHysteresis
targetAcquireRange
normalChaseRange
lowHpChaseRange
attackRange
kiteEnabled
kiteDistance
seekAllianceHpThreshold
seekAllianceEnemyCount
```

意图优先级固定为：

| 顺序 | Intent（意图） | 进入条件 | 退出条件 |
| --- | --- | --- | --- |
| 1 | Survive（求生） | Weak：快照生命 <35% × `0.9-1.1`，且 20m 内非盟友玩家总生命 > Boss 当前生命 ×150%；Normal：生命 <10%，且非盟友玩家总生命 > Boss 当前生命 ×200%；`slot1State != Disabled`；Rage：禁用 | Weak 生命达到 40%；Normal 生命达到 30%；`maxSurviveDuration` 到期；slot1 变为 Disabled；进入 Rage / 死亡 |
| 2 | SeekAlliance（寻求结盟） | `BossAllianceController` 输出 `ShouldSeekAlliance=true` | 结盟成功、机会消失或进入更高优先级意图 |
| 3 | Chase（追击） | 当前目标有效但不在攻击距离 | 进入攻击距离或目标失效 |
| 4 | Engage（进攻） | 默认状态 | 被更高优先级意图覆盖 |

- 每帧只输出一个 Intent（意图）。
- `intentMinDuration（意图最短持续时间）` 防止同一帧反复切换。
- 切换 Intent 必须满足新意图进入条件和旧意图退出条件。
- 更高优先级意图可以立即抢占；同优先级意图使用 `intentSwitchHysteresis（意图切换滞回）`。
- 当前 Demo 中 `kiteEnabled=false`，Kite 不进入本优先级表、不参与行为裁决，仅保留枚举占位。

Survive（求生）完整规则：

```text
nearbyEnemyHpTotal =
    sum(currentHp of alive players within surviveEnemyRadius)
    excluding allies

weakSurviveCondition =
    hpRatio < weakSurviveEnterHpRatio
    AND nearbyEnemyHpTotal > currentHp * weakSurviveEnemyHpMultiplier

normalSurviveCondition =
    hpRatio < normalSurviveEnterHpRatio
    AND nearbyEnemyHpTotal > currentHp * normalSurviveEnemyHpMultiplier

shouldEnterSurvive =
    phaseState != Rage
    AND slot1State != Disabled
    AND (
        (phaseState == Weak AND weakSurviveCondition)
        OR
        (phaseState == Normal AND normalSurviveCondition)
    )

surviveExitHpRatio =
    Weak   → 40%
    Normal → 30%
    Rage   → 不进入
```

- 求生判断使用独立的 5 秒决策快照；`weakSurviveEnterHpRatio`、`nearbyEnemyHpTotal`
  和盟友集合在同一快照周期内固定，不实时重算。
- 未进入 Survive 时，每个快照重新判断一次；进入后锁存，直到达到 `surviveExitHpRatio`。
- `slotId=1` 为 `Disabled` 时禁止进入 Survive，也不得持续轮询请求该槽。
- `slotId=1` 为 `Cooldown` 时允许进入等待，但只在状态变为 `Ready` 后请求一次；
  若被不可打断动作阻塞，则保留 pending 请求，待动作锁释放后再尝试，不进行每帧轮询。
- 因 `Disabled` 无法进入 Survive 时，同样执行 `SeekAlliance → Engage / Chase` 兜底，不进入撤退循环。
- `surviveEnemyRadius` 暂定 20m；只统计非盟友存活玩家，盟友、召唤物和塔不计入。
- `weakSurviveEnemyHpMultiplier` 暂定 150%，`normalSurviveEnemyHpMultiplier` 暂定 200%；
  只使用玩家和 Boss 的当前生命值。
- `normalSurviveEnterHpRatio` 固定为 10%，使用严格小于，不加入随机浮动。
- `surviveHpNoiseMin / Max` 暂定 `0.9 / 1.1`，仅用于 Weak 进入阈值；Normal 固定为 10%，不随机。
- 进入 Survive 后立即取消 Chase、Engage、Kite 和普通攻击意图。
- Survive 期间只执行逃跑移动并请求第一技能槽 `slotId=1`。
- `slotId=1` 只是第一技能槽；“保命技能”是体验描述，不是机制标签。
- 第一槽实际执行什么效果，由该槽当前绑定的 `SkillCard` 和 `SkillSlotBinding` 决定。
- 如果当前技能施放锁不可打断，先等待锁释放，再进入逃跑和第一技能槽请求。
- 逃跑目标点由 `BossRetreatTargetResolver` 每 `escapeTargetInterval` 秒重新评估，默认 1 秒；移动受阻或路径失效时立即重选。
- 威胁中心按近期真实伤害、`lowHpModifier` 和距离权重计算；逃跑基础方向为远离加权威胁中心。
- Survive 中威胁位置使用 `trackInCombatRealtime` 或局部高频采样；NavMesh 落点和障碍碰撞使用实时世界状态。
- 候选方向默认为 16 个，必须过滤战斗区域外、NavMesh 落点失败、危险地形或无法拉开威胁距离的方向。
- 候选评分：

```text
escapeScore =
    威胁距离增量
    + 远离威胁方向夹角
    + 安全区接近度
    - 敌人火力覆盖
    - 地形风险
    - 当前移动方向反转惩罚
```

- 不使用自定义 A* 或逐候选多段路径计算；只选择 NavMesh 上的目标点，实际路线由 `NavMeshAgent` 处理。
- 选择评分最高的有效目标点；直线优先不代表忽略其他敌人的包夹方向。
- 安全区是兜底和高权重候选，不是唯一逃跑目标。
- 只有以下情况调用 `SetDestination`：当前路径失效或 `PathPartial`、目标点变化 ≥ `escapeDestinationMinMove`、新目标评分优势 ≥ `escapeScoreHysteresis`。
- 若目标点变化小于 `escapeDestinationMinMove` 或评分优势不足，保持原目标，避免每秒覆盖 NavMesh 路径。
- 卡住判断使用 `escapeStuckWindow` 窗口内的有效路径进度，而不是只看总位移：

```text
expectedProgress =
    min(agent.speed, remainingDistance / escapeStuckWindow)
    * escapeStuckWindow

requiredProgress =
    max(
        escapeMinProgressDistance,
        expectedProgress * escapeStuckProgressRatio
    )

actualPathProgress =
    previousDistanceToNextCorner - currentDistanceToNextCorner

isStuck =
    actualPathProgress < requiredProgress
    OR pathStatus == PathPartial
    OR pathPendingElapsed >= escapePathPendingTimeout
```

- `escapeStuckWindow` 暂定 1 秒，`escapeStuckProgressRatio` 暂定 25%，`escapeMinProgressDistance` 暂定 0.5m，`escapePathPendingTimeout` 暂定 0.5 秒。
- 第一次卡住时立即重新选择目标；再次卡住时尝试左右绕行或转向安全区。
- `noEscapePathElapsed` 持续累计，不能因每分钟一次的目标重选而清零；只有当前目标有效、路径完整且实际路径进度达到要求时才清零。
- 连续 `escapeNoPathTimeout` 秒（暂定 2.5 秒）没有有效前进进度时，标记 `noEscapePath` 并强制结束 Survive，按普通兜底选择结盟或战斗。
- Weak 生命达到 40%、Normal 生命达到 30% 时立即退出 Survive，并重新进入常规意图裁决。
- 正常达到退出阈值时清空 `surviveElapsed` 和 `surviveForcedExitPending`。
- 进入 Rage 时立即取消 Survive，不等待恢复阈值。
- 进入 Survive 时开始累计 `surviveElapsed`；达到 `maxSurviveDuration` 且生命仍未达到退出阈值时，
  标记 `surviveForcedExitPending`，在当前不可打断动作结束后强制结束 Survive。
- Survive 期间若 `slotId=1` 变为 `Disabled`，在当前不可打断动作结束后立即结束 Survive。
- 因槽位不可用或最大时长强制退出后：

  1. 设置 `surviveForcedExitCooldown`，期间不重新进入 Survive。
  2. 若 `BossAllianceCanProceed=true`，优先进入 `SeekAlliance`。
  3. 否则进入 `Engage` 或 `Chase`，恢复正常战斗。

- 若强制退出等待期间当前动作完成并恢复到退出阈值，正常退出优先，不启动强制退出冷却。
- 强制退出只在冷却结束且 `slotId=1` 恢复为 `Ready` 后，才允许在下一次求生快照重新进入 Survive。
- `slotId=1` 为 `Disabled` 时禁止持续调用 `TryRequestSlot(1)`；不得用每帧请求代替状态兜底。

求生与残血追击同时合法时的状态裁决：

```text
if shouldEnterSurvive AND lowHpPursuitEligible:
    Weak   → Survive
    Normal → Survive
    Rage   → 不成立（Rage 禁止 Survive）
```

- `lowHpPursuitEligible` 仍要求目标生命 <30%、非盟友、存活且距离 ≤40m。
- Weak 同时满足时优先逃生。
- Normal 只有在 `<10%` 且敌我生命比 `>200%` 的紧急条件下才会进入上面的逃生分支；
  其他情况 `shouldEnterSurvive=false`，优先残血追击/击杀。
- 若只有一个分支合法，直接选择该合法分支。

Rage（暴走）完整规则：

- 不进入 Survive、Kite 或 SeekAlliance。
- 不结盟、不接受结盟申请、不主动提议。
- 只进入 Engage 和 Chase。
- 继续使用攻击、普攻，并请求配置为进攻用途的技能槽。
- 不进入残血回血姿态。
- Rage 状态只请求配置允许在 Rage 下触发的技能槽，不按固定 slotId 禁用某一槽。

职责边界：

- BossBehaviorController 只输出意图，不直接修改位置、生命、技能冷却或仇恨。
- 移动、攻击、技能和结盟模块分别消费对应意图。
- Boss 不实现普通战术撤退；Survive 只由 Weak 40%、Normal 30%、`maxSurviveDuration`、slot1 不可用、Rage 或死亡结束。
### 9.5 Boss 使用的通用记忆与快照

`EntityMemory`、快照存储、版本号、容量和通用计时器由《Common 基础系统》负责。本文只定义 Boss 如何消费：

| 快照 | Boss 用途 | 周期 / 刷新 |
| --- | --- | --- |
| 目标快照 | 锁定当前目标、同分裁决 | 5 秒；死亡、不可选、超范围、跌破 10% 可立即刷新 |
| 求生快照 | Survive 进入条件 | 5 秒；求生阈值跨线立即刷新 |
| 策略快照 | 结盟总门禁、战力压制和 Boss 决策 | 10 秒；只刷新结盟区块，不重掷随机数 |
| 位置快照 | 最后已知位置和逃跑目标计算 | 由 Common 位置采样器提供 |

规则：

- Boss 只保存快照版本、刷新原因和游戏时间，不定义通用存储字段、容量和序列化实现。
- 死亡事件先完成死亡与结盟结算，再刷新所有存活观察者的活动快照；历史快照不改写。
- `allianceState` 是实时版本化子状态；变化时立即递增 `allianceVersion`，10 秒快照只记录版本。
- Boss 的求生阈值和仇恨规则属于 Boss；通用记忆机制属于 Common。
- 详细字段、Inspector 参数和日志实现见《Common 基础系统》。
### 9.6 Boss 技能槽使用契约

Boss 不拥有通用技能框架。`SkillCard`、`SkillSlotBinding`、`SkillSlotRuntime`、全局施放状态、冷却、打断和效果提交由《技能系统设计》与《Common 基础系统》负责。

Boss 侧只冻结以下内容：

| 技能槽 | 当前临时 SkillCard | 基础 CD | 冷却开始时机 | 充能数 | 请求条件 |
| --- | --- | --- | --- | --- | --- |
| `slot1` | 地震波 | 20 秒 | `Commit` | 1 | Survive；或 Engage / Chase / Rage 中连续 3 秒未命中敌对玩家 |
| `slot2` | 分身 | 15 秒 | `Commit` | 1 | Engage / Chase / Rage；有合法战斗目标、槽位 Ready；具体效果触发条件由技能系统判定 |
| `slot3` | 嗜血狂暴 | 30 秒 | `Commit` | 1 | Engage / Chase / Rage；有合法战斗目标、槽位 Ready；具体效果触发条件由技能系统判定；随机池后置 |

当前 Demo 请求裁决固定为：

| 顺序 | 条件 | 请求 |
| --- | --- | --- |
| 1 | 进入 Survive | `slot1` |
| 2 | Engage / Chase / Rage，有合法敌对玩家目标，连续 3 秒未命中 | `slot1` |
| 3 | Engage / Chase / Rage，有合法战斗目标，`slot3` Ready 且技能系统接受施放请求 | `slot3` |
| 4 | Engage / Chase / Rage，有合法战斗目标，`slot2` Ready 且技能系统接受施放请求 | `slot2` |
| 5 | 其他情况 | 不请求技能，继续普攻、移动或执行当前意图 |

规则：

- Boss 行为层只输出 `slotId` 和状态原因，不依赖技能名、`skillId` 或具体效果类型。
- `lastPlayerHitTime` 只在 Boss 本体命中的敌对玩家时刷新；被护盾吸收但攻击已命中的情况同样刷新。分身、召唤物、塔和非敌对目标不计入。
- Boss 获得第一个合法敌对玩家目标时开始计算 `noPlayerHitElapsed`；命中玩家时清零。
- 没有合法敌对玩家目标时，`noPlayerHitElapsed` 暂停；重新进入一段有效战斗时从 0 开始。
- 3 秒未命中判定只用于 `Engage / Chase / Rage`，不用于 `Survive` 或 `SeekAlliance`。
- `Survive` 的 `slot1` 请求优先级最高，可以抢占普通战斗技能；3 秒未命中的 `slot1` 请求优先于 `slot2 / slot3`。
- `slot2 / slot3` 的效果触发条件，例如“无存活分身”“增益未生效”、目标过滤和距离检查，由 `SkillSlotBinding.triggerPolicy` 与技能系统负责，Boss 不读取或维护这些效果状态。
- Boss 按 `slot3`、`slot2` 的顺序尝试请求；当前槽因效果触发条件被拒绝时，可在同一次行为决策继续尝试下一槽，但最多只能有一个请求被接受。
- `slot2 / slot3` 只在 `Engage / Chase / Rage` 请求；当前 Demo 在 `SeekAlliance` 不请求这三个技能。
- `Ready` 只表示技能槽可请求，不代表无条件释放。只有存在合法目标、没有全局施法锁且满足触发条件时，才在下一个行为决策点请求。
- 每次行为决策最多提交一个 `slotId`；请求被接受后不重复提交，请求被拒绝、阻塞或目标失效时本次丢弃，下个行为决策点重新判断。
- 普通战斗槽不建立请求队列；只有 Survive 的 `slot1` 请求允许保留 pending，等待不可打断动作结束后尝试一次。
- 各槽 CD 独立，阶段倍率不修改 `skillFrequencyMultiplier`；该值默认固定为 1.0，实际 CD 仍按公式换算。
- `Cooldown` 持续推进；`Disabled` 暂停冷却；阶段、Rage 和结盟变化不自动重置 CD。
- 第一技能槽是普通槽位，不是保命类型；“保命技能”只是体验描述。
- 第三槽随机池的池 ID、抽取时机和权重由技能配置负责，Boss 只请求 `slotId=3`。
- 替换槽内 SkillCard 不得要求修改 Boss 行为状态机。

Boss 控制抗性参数：

```text
finalControlDuration = baseControlDuration * bossControlDurationMultiplier
finalSlowStrength = baseSlowStrength * bossSlowStrengthMultiplier
```

- `BossControlResistanceProfile` 属于 Boss 配置。
- 通用状态效果系统负责叠加、刷新、免疫和最终时长计算。
### 9.7 仇恨分值

```text
hpRatio
isLowHp
damageWindow5s
lowHpModifier
relicHate
revengeHate
towerHate
effectiveChaseRange
finalScore
```

规则：

- 只统计真实伤害。
- 5 秒窗口外伤害失效。
- 塔不参与玩家目标竞争。
- 排序：总分 → 血量低 → Boss 感知战力低 → 距离近。
- 所有玩家仇恨都为零时，直接在存活玩家中按距离从近到远选择。
- 残血计算：

```text
isLowHp = hpRatio < lowHpModifierStart

lowHpModifier =
    clamp((lowHpModifierStart - hpRatio) / lowHpModifierWindow, 0, 1)
    * lowHpModifierMax

effectiveChaseRange =
    isLowHp ? lowHpChaseRange : normalChaseRange

finalScore =
    damageWindow5s + lowHpModifier + relicHate + revengeHate
```

- `relicHate` 由 `IRelicStateProvider.GetBossHateModifier(entityId)` 提供，BossTargeting 只消费最终值。
- 圣物 ID、基础仇恨、衰减时长和版本变化由圣物系统负责；BossDefinition 不保存日之石/月之石常数。
- `IRelicStateProvider` 的变化事件必须触发目标快照或仇恨分项刷新，具体刷新范围由圣物版本决定。
- `lowHpModifierStart` 暂定 0.30，`lowHpModifierWindow` 暂定 0.20，`lowHpModifierMax` 暂定 2000。
- 生命比例 ≤ 0.10 时 `lowHpModifier = 2000`；正好 0.30 时修正为 0、追击距离为 20m。
- `lowHpModifier` 和当前目标的 `effectiveChaseRange` 可随血量实时变化，目标快照本身不被普通血量变化打断。
- 生命比例恢复到 0.30 以上后，`effectiveChaseRange` 立即恢复 20m。
- 残血目标距离 > 40m 时不允许继续残血追击，必须立即重新选择目标。
- 候选目标死亡、成为盟友、不可选或离开战斗区域时，不参与残血追击排序。
- 普通变化不打断快照。
- 目标跌破 10% 可以立即打断快照。
- Boss 结盟资格不保存“是否攻击过”标记，直接读取申请时的 `finalScore`：

```text
BossAllianceEligible =
    alivePlayerCount == 2
    AND phaseState != Rage
    AND finalScore <= allianceHateThreshold
    AND hpRatio >= allianceMinHpRatio
```

- `alivePlayerCount` 必须恰好为 2 且 `phaseState != Rage`；只剩 1 名或 0 名存活玩家时，
  任何玩家都不能与 Boss 结盟。
- `allianceHateThreshold` 暂定 `0`；伤害仇恨、圣物仇恨、追杀仇恨等仍存在时不能与 Boss 结盟，对应分项消散后自动恢复资格。
- 残血排除使用独立的 `allianceMinHpRatio`，避免只依赖仇恨分值。

### 9.7.1 目标、追击与攻击距离

三类距离必须独立配置，不能复用同一个“仇恨范围”：

| 距离 | 作用 | 初始规则 |
| --- | --- | --- |
| `targetAcquireRange` | 判断目标是否能进入候选池 | 暂定 20m；只负责获取，不负责移动 |
| `normalChaseRange` | 普通目标允许追踪的范围 | 暂定 20m |
| `lowHpChaseRange` | 残血目标允许继续追踪的范围 | 暂定 40m |
| `attackRange` | 普攻实际可命中距离 | 默认 2m，可由普通攻击配置覆盖 |

- `targetAcquireRange` 不因残血自动扩展；已在目标池或有效位置记忆中的低血量目标才可使用 40m 残血追击。
- `effectiveChaseRange` 只在 `normalChaseRange` 和 `lowHpChaseRange` 之间切换。
- `ChaseRange` 只控制移动，不等于攻击距离；进入 ChaseRange 不保证能普攻或施放技能。
- 每个技能仍使用自身 `SkillCard.range`，不得用 ChaseRange 代替技能范围。
- 目标超出 `effectiveChaseRange`、死亡、不可选或成为盟友时立即退出当前追击。

### 9.8 Boss 战力读取

战力计算公式、属性快照、噪声随机和版本化缓存由《Common 基础系统》中的 `CombatPowerService` / `ICombatPowerProvider` 负责。

Boss 只读取：

- `actualPower`
- `bossPerceivedPower`
- 快照版本和采样时间
- 四项分项和属性变化版本

规则：

- Boss 不自行计算生命、DPS、防御或塔攻击战力。
- 同一次 10 秒策略快照使用同一个 `bossPerceivedPower` 版本。
- `bossPerceivedPower` 由 Provider 返回；Boss 只用于结盟、同分裁决和战力压制。
- 圣物、塔和装备变化通过 Provider 版本触发目标或策略快照刷新。
### 9.9 Boss 结盟战力压制规则

“双杀”只作为玩家体验描述，不作为独立系统功能或脚本。

系统内部由 `BossAllianceController（Boss 结盟控制器）` 计算 `BossPowerAdvantage（Boss 战力压制）`：

```text
bossPowerLimit = bossCombatPower * 0.70

BossAllianceAllowed =
    alivePlayerCount == 2
    AND phaseState != Rage

BossPowerAdvantage =
    BossAllianceAllowed
    AND playerAPerceivedPower < bossPowerLimit
    AND playerBPerceivedPower < bossPowerLimit

BossAllianceCanProceed =
    BossAllianceAllowed
    AND NOT BossPowerAdvantage
```

规则：

- `ShouldSeekAlliance` 是 `BossAllianceController` 按《结盟机制设计》第三节判定流程输出的结果；本文件不重复定义正向结盟条件，Boss 行为层只消费该布尔结果。
- `BossAllianceAllowed` 是结盟的绝对总门禁，必须为 `true` 才允许申请、主动提议或接受申请。
- `alivePlayerCount != 2` 时立即设置 `BossAllianceAllowed = false`、`ShouldSeekAlliance = false`。
- 单人残局不得创建新的结盟请求；已有 Boss + 玩家结盟立即终止，不产生追杀仇恨。
- 单人残局或 Rage 状态下，`BossPowerAdvantage` 不参与实际决策。
- 使用 `BossPerceivedPower（Boss 感知战力）`，不是玩家实际战力。
- `BossPerceivedPower = 玩家实际战力 × random(0.95, 1.05)`。
- 随机倍率在每次 10 秒结盟策略快照内固定。
- 必须使用严格小于 `<`；正好等于 70% 不构成战力压制。
- `BossPowerAdvantage = true` 时，本周期 `ShouldSeekAlliance（是否寻求结盟）= false`，拒绝申请并停止主动提议。
- 只有 `BossAllianceCanProceed = true` 时，才继续检查仇恨、残血、申请冷却和结盟资格。
- `BossPowerAdvantage = false` 只表示没有触发战力压制，不能替代 `BossAllianceAllowed`。
- `phaseState == Rage` 时直接禁止结盟，不进入 `BossPowerAdvantage` 判断。
- 少于两名存活玩家时禁止所有 Boss + 玩家结盟，不得把 `BossPowerAdvantage = false` 解释为可结盟。

### 9.10 结盟时长调整规则

Boss + 玩家结盟期间，只对 Boss 实际受到的真实伤害事件执行时长调整：

```text
gameSecondBucket = floor(gameTime)

adjustmentKey =
    allianceId
    + attackerOwnerId
    + gameSecondBucket

delta =
    attackerOwner is ally ? -allyAttackDurationPenalty : +enemyAttackDurationBonus

remainingAllianceTime =
    clamp(remainingAllianceTime + delta, 0, baseAllianceDuration)
```

硬约束：

- `attackerOwnerId` 必须是玩家或玩家拥有的召唤物 / 塔的最终归属 ID，多个召唤物不能各算一次。
- 每个 `(allianceId, attackerOwnerId, gameSecondBucket)` 最多处理一个有效时长调整事件。
- 同一攻击者同一游戏秒内即使产生 100 次命中或 100 次 DoT 跳伤，也只计算第一次有效真实伤害。
- 后续事件记录为 `rate_limited`，不修改时长、不写新的计时结果。
- 只有扣除防御、减伤后真实伤害大于 0 的事件有效；未命中、完全被护盾吸收、环境伤害和玩家间伤害不触发。
- 同一帧多个事件按事件序号稳定排序，第一个有效事件生效。
- `allianceId` 变化时重新建立独立计数；旧结盟的秒桶不能影响新结盟。
- 盟友事件 `delta=-1s`，非盟友事件 `delta=+1s`，当前 Demo 不按伤害量缩放。
- 时长先应用本次 delta，再钳位；达到 0 时立即进入结盟结束流程，同一瞬间后续事件不再修改已经结束的结盟。
- 非盟友延长不能超过基础时长；已经等于基础时长时仍记录该秒已处理，但不重复累加。
- 所有调整和忽略事件必须记录 `allianceId`、`attackerOwnerId`、`gameSecondBucket`、`eventId`、`delta`、`before/after` 和 `rate_limited` 原因。

### 9.11 结盟结束快照刷新

正常到期和翻脸统一使用 `AllianceEndedEvent`，只通过 `reason` 区分：

```text
NaturalExpiry
Betrayal
Death
Rage
NoValidOpponent
```

T0 结盟结束：

1. 立即结束关系，并递增 `allianceVersion`。
2. 前盟友立即从盟友池移除，可以重新进入目标池。
3. 立即刷新目标快照并递增 `targetSnapshotVersion`。
4. 立即刷新 5 秒求生快照，重新计算 20m 内非盟友总生命。
5. 只刷新 10 秒策略快照中的结盟区块，不重掷战力随机数和其他战略字段。
6. 立即更新 AI 可见状态、结盟 UI 和调试显示。
7. 历史快照与 Replay 不改写。

T+3 秒惩罚仇恨生效：

1. 发布 `AllianceRevengeAppliedEvent`。
2. 对原盟友增加 5000 `revengeHate`，并开始 30 秒线性消散。
3. 递增 `revengeHateVersion`。
4. 立即刷新目标仇恨快照并重新排序，不等待下一个 5 秒目标快照。
5. 不再次递增 `allianceVersion`，因为关系已经在 T0 结束。

死亡导致的结盟结束不进入 3 秒缓冲，也不产生惩罚仇恨，直接执行死亡快照刷新流程。

### 9.12 BossDiedEvent 发布

`BossDiedEvent` 的完整字段、掉落请求格式和订阅方边界见本文第 8.4 节。

Boss 的发布义务：

- 先停止全部 Boss 行为并结束全部结盟。
- 递增 `allianceVersion`，发布稳定的事件 ID 和实体 ID。
- 携带 death position、killer、damage source、final phase、enrage layer 和掉落上下文。
- 发布事件后向 `ILootDropService` 提交死亡掉落请求。
- 不定义玩家获胜者，不处理恶魔之心生命周期，不等待 Endgame 结果。
## 十、裁决规则

本节用于明确两个或更多系统在同一帧冲突时由谁生效。所有裁决必须可记录、可复现，并带固定优先级。

### 10.1 统一优先级

同一帧出现多个事件时，按以下顺序处理：

| 优先级 | 事件 | 处理原则 |
| --- | --- | --- |
| 1 | EncounterEnded | Boss 停止所有战斗逻辑；终局规则由外部系统负责 |
| 2 | Boss 死亡 | 停止 Boss 行为；结束所有结盟并递增 allianceVersion；发布死亡事件后刷新所有活动快照 |
| 3 | 玩家永久淘汰 | 结束该玩家参与的所有结盟；递增 allianceVersion；刷新目标池及所有存活观察者活动快照 |
| 4 | RageTransition | 立即取消 Survive，并按 Rage 规则阻断结盟请求 |
| 5 | 硬控与死亡打断 | 立即打断可打断技能和普通攻击 |
| 6 | 结盟状态变化 / 惩罚仇恨生效 | 开始/结束递增 allianceVersion；T+3 惩罚递增 revengeHateVersion；均立即刷新目标过滤 |
| 7 | SurviveThresholdCrossed | 立即刷新/结束 5 秒求生快照，再进入本轮决策 |
| 8 | 目标快照失效 | 按 10.2 判断是否允许换目标 |
| 9 | 技能结算 | 处理当前技能的伤害、召唤或控制 |
| 10 | 普通攻击 | 只有无技能施放且目标有效时执行 |
| 11 | 移动 | 最后消费当前目标与动作锁 |

规则：

- 高优先级事件处理完成后，低优先级事件才可使用同一帧。
- 低优先级事件不能覆盖高优先级事件的结果。
- 死亡事件先完成死亡/结盟状态修改，再统一刷新活动快照；同帧决策只能读取刷新后的结果。
- RageTransition 必须先取消 Survive，再处理普通战斗和技能请求。
- 行为层产出的攻击、移动、技能和结盟意图可以在当帧消费，也可以在下一帧消费；不要求所有模块同帧完成，但同一个意图只能被消费一次并写入日志。
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
| 结盟关系结束 | 是 | 前盟友重新进入目标池，立即刷新并递增 targetSnapshotVersion |
| 惩罚仇恨生效 | 是 | 增加 revengeHate 后立即重新排序 |
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

- 单体技能：取消技能效果，按该技能槽的 `cooldownStartPolicy` 决定是否进入冷却。
- 范围、自身或原地技能：按施放开始时锁定的位置执行。
- 普通攻击：取消本次攻击，等待下一攻击间隔。

全局死亡强制刷新：

| 事件 | 处理顺序 | 刷新范围 |
| --- | --- | --- |
| 玩家永久淘汰 | 结算淘汰 → 结束相关结盟并递增 allianceVersion → 更新目标池 → 强制刷新 | 目标、求生、策略、战力、位置有效性和 AI 可见活动快照 |
| Boss 死亡 | 停止 Boss 行为 → 结束全部结盟并递增 allianceVersion → 发布 BossDiedEvent → 强制刷新 | 所有存活观察者的活动快照；Boss 自身记录最后快照后停止 Tick |

- 强制刷新只执行一次，原因写入日志；不得在同一死亡事件中重复刷新。
- 强制刷新后，同帧后续决策必须使用新快照，不能继续使用死亡前状态。
- Boss 死亡后的刷新主要用于终局和 AI 玩家响应，不代表 Boss 复活或恢复决策。
- 只刷新活动快照；历史快照和 Replay 数据不可被死亡事件改写。

求生生命阈值强制刷新：

| 事件 | 条件 | 刷新范围 |
| --- | --- | --- |
| `BossSurviveEnterThresholdCrossed` | `previousHp >= enterThreshold` 且 `currentHp < enterThreshold` | 5 秒求生快照、Boss 当前生命、威胁位置、盟友集合、slot1State |
| `BossSurviveExitThresholdCrossed` | `previousHp < exitThreshold` 且 `currentHp >= exitThreshold` | 立即结束 Survive，刷新普通战斗输入 |

- Weak 使用当前 5 秒周期已确定的随机进入阈值，不重新随机。
- Normal 固定使用 10% 进入阈值。
- 刷新后只有满足对应敌我生命条件且 `slot1State != Disabled` 才进入 Survive。
- 该刷新不等于死亡刷新，不重掷 10 秒战略快照和战力随机数。

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
- 结盟状态变化可以递增 `allianceVersion` 并立即更新目标池，但不能重掷战略快照或直接修改技能槽冷却。
- Boss 死亡立即释放全部动作锁并终止后续裁决。
- 技能槽冷却只由 `SkillSlotRuntime.StartCooldown` 启动，开始时机由 `cooldownStartPolicy` 决定：
  `CastStart`、`Commit` 或 `PostRecovery`。

### 10.4 同分排序

仇恨总分比较使用配置的 `scoreEpsilon`，默认暂定 `0.01`。

排序顺序：

```text
总分高
→ 生命比例低
→ Boss 感知战力低
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
| 残血修正计算窗口 | 30% → 10%，线性增加到封顶 | `lowHpModifierWindow` |
| 残血修正封顶 | 生命比例 ≤ 10% | `lowHpModifierCap` |
| 残血修正上限 | 最大仇恨加分 2000 | `lowHpModifierMax` |
| 快照立即打断 | 从 ≥ 11% 跌到 < 10% | `snapshotBreakHp` |
| 打断重新武装 | 回到 ≥ 11% 才可再次触发 | `snapshotRearmHp` |
| 残血追击范围 | < 30% 时 20m → 40m | `lowHpChaseRange` |
| 残血追击终止 | 距离 > 40m 或恢复到 ≥ 30% 后超出 20m | `normalChaseRange`、`lowHpChaseRange` |
| 进入暴走 | 遭遇时间 >= 480s | `rageAtTime`、`encounterTime` |
| 强化层 | 每累计 30s 增加一层 | `enrageInterval` |
| 结盟快照 | 每累计 10s 刷新一次 | `decisionSnapshotInterval` |
| 求生快照 | 每累计 5s 刷新一次 | `surviveSnapshotInterval` |
| 求生进入阈值跨线 | 首次向下跨越时立即刷新，不等 5s | `weakSurviveEnterHpRatio`、`normalSurviveEnterHpRatio` |
| 求生退出阈值跨线 | 达到退出阈值立即结束 Survive | `weakSurviveExitHpRatio`、`normalSurviveExitHpRatio` |
| 求生最大持续 | 达到 `maxSurviveDuration` 仍未到退出阈值则强制结束 | `maxSurviveDuration` |
| 求生强制退出冷却 | 强制退出后 5s 内不得重新进入 | `surviveForcedExitCooldown` |
| 求生槽位兜底 | `slot1State == Disabled` 不进入；`slot1State == Cooldown` 可等待 | `slot1State` |
| Weak 求生进入 | 生命比例 < 35% × 0.9-1.1，且 20m 内非盟友玩家生命和 > Boss 当前生命 × 150% | `weakSurviveEnterHpRatio`、`surviveEnemyRadius`、`weakSurviveEnemyHpMultiplier` |
| Normal 求生进入 | 生命比例 < 10%，且 20m 内非盟友玩家生命和 > Boss 当前生命 × 200% | `normalSurviveEnterHpRatio`、`surviveEnemyRadius`、`normalSurviveEnemyHpMultiplier` |
| Weak 求生退出 | 生命比例 ≥ 40% | `weakSurviveExitHpRatio` |
| Normal 求生退出 | 生命比例 ≥ 30% | `normalSurviveExitHpRatio` |
| 威胁评估 | 每累计 10s 计算一次 | `threatEvaluationInterval` |
| Boss 结盟总门禁 | 存活玩家数 == 2 且非 Rage | `alivePlayerCount`、`phaseState` |
| Boss 结盟资格 | 总门禁为真，且当前总仇恨 <= 0、生命比例 >= 30% | `BossAllianceAllowed`、`allianceHateThreshold`、`allianceMinHpRatio` |
| 斩杀窗口 | 仅当槽位启用斩杀策略时生效；Demo 暂按目标生命 <= 预期伤害 × 1.1，正式规则待技能设计 | `lethalPriorityEnabled`、`lethalTolerance` |
| Boss 战力读取浮动 | 角色实际战力 × 0.95-1.05 | `bossPowerNoise` |

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
| 普通追击范围 | 20m；`normalChaseRange` | 暂定 |
| 残血追击范围 | 40m；`lowHpChaseRange` | 暂定 |
| 目标获取范围 | 20m；`targetAcquireRange` | 暂定 |
| 普通攻击距离 | 2m；`attackRange` | 默认值，可配置 |
| Kite 启用 | `false` | Demo 占位 |
| 目标快照 | 5 秒 | 暂定 |
| 求生快照 | 5 秒 | 暂定 |
| 逃跑目标重选 | 1 秒或受阻/路径失效时 | 暂定 |
| 逃跑候选方向数 | 16 | 暂定 |
| 逃跑目标最小位移 | 2m | 暂定 |
| 逃跑评分滞回 | 0.15 | 暂定 |
| 逃跑卡住窗口 | 1 秒 | 暂定 |
| 逃跑卡住进度比例 | 预期进度的 25% | 暂定 |
| 逃跑最低有效进度 | 0.5m | 暂定 |
| 路径 Pending 超时 | 0.5 秒 | 暂定 |
| 逃跑无有效路径超时 | 2.5 秒累计无有效进度 | 暂定 |
| 最大求生时长 | 15 秒 | 暂定 |
| 求生强制退出冷却 | 5 秒 | 暂定 |
| 求生敌人半径 | 20m | 暂定 |
| Weak 求生进入阈值 | `35% × 0.9-1.1` | 暂定 |
| Normal 求生进入阈值 | `10%`，固定不随机 | 暂定 |
| Weak 求生敌我生命比 | 敌人总生命 > Boss 生命 × 150% | 暂定 |
| Normal 求生敌我生命比 | 敌人总生命 > Boss 生命 × 200% | 暂定 |
| Weak 求生退出阈值 | 40% | 暂定 |
| Normal 求生退出阈值 | 30% | 暂定 |
| 策略快照 | 10 秒 | 暂定 |
| 结盟状态快照 | 实时版本子字段；变化时立即递增 `allianceVersion`，10 秒快照保存版本号 | 暂定 |
| 残血修正启动 | 30% | 暂定 |
| 残血修正封顶 | 10% | 暂定 |
| 残血修正窗口 | 20% | 暂定 |
| 残血修正上限 | 2000 | 暂定 |
| 追杀仇恨 | 5000 | 暂定 |
| 追杀仇恨消散 | 30 秒 | 暂定 |
| 结盟结束缓冲 | 3 秒 | 暂定 |
| 结盟结束 T0 刷新 | 立即刷新 allianceVersion、目标、求生、策略结盟区块和 AI 视图 | 暂定 |
| 惩罚仇恨 T+3 刷新 | 立即刷新 targetSnapshotVersion 和 revengeHateVersion | 暂定 |
| Boss + 玩家结盟时长 | 20 秒 | 暂定 |
| 玩家 + 玩家结盟时长 | 45 秒 | 暂定 |
| 结盟确认超时 | 3 秒 | 暂定 |
| 结盟申请失败冷却 | 30 秒 | 暂定 |
| 盟友攻击 Boss 调整 | 每个 `(allianceId, attackerOwnerId, gameSecondBucket)` 最多 -1 秒 | 暂定 |
| 非盟友攻击 Boss 调整 | 每个 `(allianceId, attackerOwnerId, gameSecondBucket)` 最多 +1 秒 | 暂定 |
| 时长调整去重 | 同一归属者同秒多次伤害/DoT/召唤物命中只计首次有效事件 | 暂定 |
| Boss 结盟最低生命比例 | 30% | 暂定 |
| 斩杀容差 | 10% | 占位，启用条件和规则待技能设计 |
| Boss 战力读取浮动 | ±5% | 暂定 |
| 技能槽冷却开始时机 | Commit | 暂定 |
| Boss 结盟战力压制阈值 | 70% | 暂定 |
| Boss 感知玩家战力浮动 | ±5% | 暂定 |
| 塔仇恨倍率 | 80% | 暂定 |
| 同分容差 | 0.01 | 暂定 |
| 10% 打断滞回 | 11% 重新武装 | 暂定 |
| `slot1` 地震波 CD | 20 秒 | 暂定 |
| `slot2` 分身 CD | 15 秒 | 暂定 |
| `slot3` 嗜血狂暴 CD | 30 秒 | 未启用随机池 |
| 技能槽冷却开始 | `Commit` | 暂定 |
| 技能槽充能数 | 1 | 暂定 |
| Boss 最大生命 / 攻击 / 攻击速度倍率 | Weak / Normal / Rage 默认 0.6 / 1.0 / 2.0 | 默认值，可独立覆盖 |
| Boss 移速 / 技能频率 / 仇恨倍率 | 固定 1.0 | 不随阶段变化 |

### 10.7 数值配置检查

开工前必须完成：

- 所有裁决阈值都进入配置文件或 Inspector。
- 没有配置时使用默认值，并输出警告。
- 配置值非法时禁止运行 Boss 默认逻辑。
- 日志记录本次裁决使用的实际值。
- 修改配置后无需修改脚本。
## 十一、开发任务

执行顺序按 M0 → M1 → M2 → M3 → M4 → M5。同一阶段内按表格从上到下执行，编号只用于追踪。

### 11.1 M0：Boss 契约与核心初始化

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| CORE-15 | 定义 BossContext 与外部依赖引用 | 无 | 落为 `BossContext.cs`；接口可注入，Fake 可实现；不重新定义接口内部或外部系统实现 |
| CORE-01 | 定义 BossLifecycleState 和合法迁移 | 无 | 非法迁移被拒绝并记录 |
| CORE-03 | 定义 BossDefinition、强度状态和技能槽配置 | 无 | 数值可配置，运行时不修改 SO |
| CORE-02 | 实现 BossController 初始化与 Tick | CORE-01、CORE-15、CORE-16（外部） | 子模块初始化可确定复现；意图允许当帧或下一帧消费，但同一意图不得重复执行 |
| CORE-18 | 实现 BossEventBridge 外部事件入口 | CORE-15、CORE-17（外部） | 订阅/退订稳定事件，把 `PlayerEliminatedEvent`、`EncounterEnded` 按统一顺序交给 BossController；不决定结盟或死亡规则 |

外部前置，不属于 Boss 开发：

```text
CORE-06 / CORE-07   EntityMemory 与配置
CORE-08 / CORE-11   目标快照与失效规则
CORE-09 / CORE-12   决策快照与记忆日志
CORE-10             通用命名计时器
CORE-13             位置快照与采样器
CORE-16             SkillCard / SkillSlotBinding / SkillSlotRuntime / SkillCastController 契约
CORE-17             EventBus 发布、订阅和事件队列
DEBUG-05            确定性 seed 与随机源
```

### 11.2 M1：Boss 属性、目标、移动与行为

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| CORE-04 | 实现 BossStats 基础与有效属性 | CORE-03 | 属性修改器和有效值可读取 |
| CORE-14 | 实现 BossPhaseController 三状态 | CORE-04 | Weak / Normal / Rage 可切换；最大生命、攻击和攻击速度倍率默认 0.6/1.0/2.0，可独立覆盖；移速、技能频率和仇恨倍率固定 1.0；阶段倍率与 enrageLayer 按乘法顺序叠加 |
| COMBAT-05 | Boss 伤害事件适配 | COMBAT-01（外部）、CORE-15 | 将真实伤害写入 Boss 仇恨、快照和日志 |
| TARGET-01 | Boss 候选目标收集 | CORE-02、CORE-15 | 按 Provider 返回玩家、塔、召唤物的阵营和可选中状态 |
| TARGET-02 | 5 秒真实伤害窗口消费 | COMBAT-05 | 过期伤害自动清理，目标排序使用窗口结果 |
| TARGET-03 | 残血修正计算 | TARGET-02 | `hpRatio < 0.30` 时按 `(0.30-hpRatio)/0.20 × 2000` 计算；≤ 0.10 封顶 2000；正好 0.30 为 0 |
| TARGET-04 | 圣物与追杀仇恨 | TARGET-02、IRelicStateProvider | relicHate 从接口读取并可叠加；追杀仇恨由 Boss 计时；不在 Boss 模块保存圣物常数 |
| TARGET-05 | 塔仇恨 | TARGET-02 | 无玩家目标时按 80% 生效，塔不进入玩家竞争 |
| TARGET-06 | 同分裁决 | TARGET-01、POWER-04（外部） | 按规则稳定排序，战力项使用 Boss 感知值 |
| TARGET-07 | 5 秒快照与 10% 打断 | TARGET-06、CORE-08（外部） | 普通变化不换目标，跌破 10% 可换 |
| TARGET-08 | 残血有效追击距离 | TARGET-07 | 生命 < 30% 时 `effectiveChaseRange=40m`，否则 20m；> 40m 立即放弃残血追击；恢复到 ≥ 30% 后若超出 20m 立即重选目标 |
| TARGET-09 | 零仇恨目标选择 | TARGET-06 | 所有玩家仇恨为零时选择距离最近的存活玩家 |
| COMBAT-03 | Boss 普攻与攻击间隔 | TARGET-07 | 300 伤害 / 2 秒可配置 |
| COMBAT-04 | Boss 控制抗性配置 | COMBAT-01（外部） | Boss 只提供抗性倍率；状态效果系统负责持续时间和免疫计算 |
| MOVE-01 | 读取位置快照并生成移动目的 | CORE-13（外部）、TARGET-08 | BossMovement 只消费位置快照和 `effectiveChaseRange`，不自行采样或重算残血 |
| MOVE-02 | 最后已知位置移动 | MOVE-01 | 没有有效目标时前往最后已知位置，不做随机漫游 |
| MOVE-03 | 追击、停止与朝向 | MOVE-01 | 目标有效时按有效追击距离追击，攻击距离内停止并面向目标 |
| MOVE-04 | 目标脱离与重新搜索 | MOVE-02 | 超出有效追击范围后保留最后位置，并请求重新选择目标 |
| MOVE-05 | Kite 占位检测 | MOVE-03 | 当前 `kiteEnabled=false`，不进入生产裁决 |
| MOVE-06 | 塔与安全区移动 | MOVE-02、ITowerProvider、ISafeZoneProvider、ICombatAreaProvider | 无玩家时靠近塔；最后位置失效时回到安全区，并限制在战斗区域边界内 |
| BEHAVIOR-01 | 定义战斗意图枚举与决策输入 | TARGET-07 | 四种有效意图按固定优先级切换；Kite 为禁用占位 |
| BEHAVIOR-02 | Engage / Chase 意图 | BEHAVIOR-01 | 目标有效时按 `effectiveChaseRange` 正常进攻或追击；不自行计算残血距离 |
| BEHAVIOR-04 | Survive（求生）意图 | BEHAVIOR-01、CORE-11（外部） | Weak 按 `<35%×0.9-1.1` 且 150% 敌我生命比优先逃生；Normal 仅在 `<10%` 且 200% 时逃生，否则优先残血击杀；Rage 禁用；slot1 不可用时按兜底规则处理 |
| BEHAVIOR-07 | 求生退出与意图日志 | BEHAVIOR-04 | Weak / Normal 达到退出阈值、`maxSurviveDuration` 到期或 slot1 不可用时退出；不重置仇恨、不离开战斗区域、不实现普通战术撤退 |
| BEHAVIOR-08 | 逃跑目标点选择 | BEHAVIOR-04、CORE-13（外部）、ICombatAreaProvider、ISafeZoneProvider | 每 1 秒或受阻时评估候选点；用 NavMesh.SamplePosition 过滤；2m/0.15 滞回后提交 SetDestination；按有效路径进度判断卡住，累计 2.5 秒无有效进度则强制退出 |
| CORE-05 | 接入被动回血状态 | CORE-04、IHealthStateProvider、IHealthService | Boss 进入 Survive 时调用 Health 自动回血模式，退出时停止；Rage、死亡和 slot1 不可用都会停止该模式；Boss 不修改生命 |
| COMBAT-06 | Boss 本体命中时间与 3 秒未命中计时 | COMBAT-01（外部）、BEHAVIOR-01 | `BossCombat` 在 Boss 本体命中敌对玩家时更新 `lastPlayerHitTime`；`BossBehaviorController` 根据它计算 `noPlayerHitElapsed`；分身、召唤物和塔不更新 |
| DEBUG-01 | 调试 Boss 生成 | CORE-02、CORE-15 | 不依赖正式出生流程即可生成 |
| DEBUG-04 | Boss 结构化日志 | CORE-02 | 状态、目标、技能、结盟和 BossDiedEvent 可回放 |

外部前置，不属于 Boss 开发：

```text
COMBAT-01 / COMBAT-02   统一伤害入口、防御与减伤
POWER-01 / POWER-02     战斗属性和战力计算
POWER-03 / POWER-04     Boss 感知战力和战力快照
IHealthService / CombatState / Health  自动回血与战斗状态
```
### 11.3 M2：Boss 技能槽集成

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| SKILL-03 | Boss 状态到技能槽的请求 | SKILL-02（外部） | Boss 只输出 slotId 和状态原因；请求只发生在 Survive 或 Engage / Chase / Rage；不依赖技能名、skillId 或固定技能顺序 |
| SKILL-06 | Boss Demo 技能卡接入 | SKILL-11、SKILL-15（外部） | 地震波、分身、嗜血狂暴绑定 1/2/3 槽；嗜血狂暴按 8 秒 +50% 移速和普攻攻速实现；替换技能卡不修改 Boss 决策代码 |
| SKILL-07 | 分身召唤适配 | SKILL-11、ISummonFactory | 分身通过 SummonRequest 生成；生命 20%、攻击 50%、持续 8 秒，可被击杀 |
| SKILL-08 | 地震波减速适配 | SKILL-11 | 40m 范围敌人移速和攻速 -30%，持续 5 秒；盟友排除 |
| SKILL-09 | Boss 技能决策日志 | SKILL-01（外部） | 记录 slotId、状态原因、候选、冷却和打断 |
| SKILL-10 | Boss 槽位优先与可选斩杀策略 | SKILL-03、SKILL-04（外部）、SKILL-11 | Demo 固定 Survive / 3 秒未命中的 slot1 例外；其余战斗按 slot3 > slot2；效果触发条件由 SkillSlotBinding 裁决，Boss 不读取分身或增益活动状态 |
| SKILL-11 | Boss 技能执行闭环 | SKILL-01～SKILL-05（外部）、SKILL-09 | 从 Boss 槽位请求到冷却启动完整跑通 |
| SKILL-12 | Boss 第一技能槽请求 | SKILL-02（外部）、BEHAVIOR-04 | Survive 或连续 3 秒未命中敌对玩家时请求 slotId=1；Disabled 禁止轮询；Cooldown 转 Ready 后请求一次 |
| SKILL-13 | Boss 技能槽独立 CD | SKILL-02（外部） | slot1/2/3 分别使用 20/15/30 秒；Commit 开始；maxCharges=1；Disabled 暂停 |
| SKILL-14 | Boss 战斗技能使用裁决 | SKILL-03、SKILL-09 | Engage / Chase / Rage 中每次决策最多接受一个请求；slot3 优先于 slot2；技能系统因效果条件拒绝时可尝试下一槽；SeekAlliance 不请求当前战斗技能 |

外部前置，不属于 Boss 开发：

```text
SKILL-01 全局技能施放状态机
SKILL-02 技能槽就绪、优先级与冷却
SKILL-04 通用目标解析
SKILL-05 通用施放锁与打断
SKILL-15 SkillEffectApplier 效果条件检查与效果应用
BEHAVIOR-03 Kite 占位配置（当前禁用）
```

### 11.4 M3：Boss 侧结盟

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| ALLY-01 | 10 秒局势判断快照 | CORE-09（外部）、ALLY-00（外部）、POWER-04（外部） | 申请使用上一周期结果，包含固定 Boss 感知战力 |
| ALLY-13 | 结盟状态实时版本覆盖 | ALLY-00（外部）、ALLY-01 | 结盟开始/结束/翻脸立即更新 allianceState 并递增 allianceVersion；目标过滤立即生效 |
| ALLY-12 | 结盟总门禁、战力压制与决策 | ALLY-01、POWER-04（外部） | 存活玩家数必须恰好为 2；单人残局拒绝申请、停止提议并结束现有结盟；Rage 禁止结盟 |
| ALLY-02 | Boss 结盟仇恨门槛 | TARGET-04、ALLY-01 | 当前 finalScore <= 0 才能申请与 Boss 结盟 |
| ALLY-03 | Boss 残血排除 | ALLY-01 | 生命比例低于 allianceMinHpRatio 时不可与 Boss 结盟 |
| ALLY-04 | Boss 主动提议 | ALLY-12 | 存活玩家数必须为 2；上限 20%；同周期同一玩家不重复；单人残局不提议 |
| ALLY-05 | 玩家申请与 Boss 响应 | ALLY-03、ALLY-04、ALLY-12 | 申请、接受、拒绝、超时和 30 秒冷却完整；单人残局直接拒绝 |
| ALLY-10 | Boss 结盟时长与事件调整 | ALLY-05 | 20 秒基础时长；按 `(allianceId, attackerOwnerId, gameSecondBucket)` 去重；钳位 [0, 20s] |
| ALLY-08 | Boss 结盟结束与目标快照刷新 | ALLY-07（外部） | T0 刷新目标/求生/策略区块；T+3 增加 5000 追杀仇恨并刷新目标排序 |
| ALLY-09 | Boss 死亡与玩家淘汰关系 | ALLY-08 | 任一玩家死亡结束其参与结盟；Boss 死亡结束全部结盟；均不进入缓冲、不产生追杀仇恨 |
| BEHAVIOR-05 | Boss SeekAlliance 意图 | BEHAVIOR-01、ALLY-12 | 读取 BossAllianceController 的唯一结盟决策并输出意图 |

外部前置，不属于 Boss 开发：

```text
ALLY-00 通用 AllianceService
ALLY-06 通用盟友豁免
ALLY-07 通用翻脸状态
ALLY-11 玩家 + 玩家结盟
```

### 11.5 M4：Boss 死亡与掉落请求

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| END-01 | Boss 死亡事件 | CORE-01、ALLY-00（外部） | 由 `BossController` 停止全部 Boss 行为、释放动作锁、结束结盟、发布完整 BossDiedEvent 并触发活动快照刷新；不改写历史快照 |
| DROP-01 | Boss 死亡掉落请求 | END-01、ILootDropService | 由 `BossController` 在死亡结算后通过通用掉落接口请求必掉恶魔之心；Boss 不处理拾取、交付和结算 |
### 11.6 M5：生成与集成调试

出生逻辑本期不实现。只保留 `BossSpawnContext` 占位：

```text
spawnPosition
initialLifecycleState
initialPhaseState
earlyAwaken
awakenReason
initialStrength
```

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| DEBUG-02 | 仇恨、战力、技能与逃跑路径显示 | TARGET-08、BEHAVIOR-08、SKILL-09、POWER-04 | 可查看目标、仇恨、实际/感知战力、各技能槽状态和逃跑目标评分 |
| DEBUG-03 | 强制技能槽与时间快进 | SKILL-09、ALLY-09 | 可按 slotId 强制触发并快速验证阶段、技能槽和结盟 |
| INTEGRATION-01 | Demo 纵向闭环 | DROP-01、ALLY-09、SKILL-11 | 固定 seed 可重复跑完整 Boss Demo |

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
12. SkillCard 不保存冷却和运行时状态；实际冷却、优先级和触发策略属于 SkillSlotBinding，剩余状态属于 SkillSlotRuntime。
13. Boss 生命周期状态、Weak / Normal / Rage 强度状态和 enrageLayer 强化层互相独立。
14. Boss 结盟资格只读取当前仇恨总分和生命比例，不保存“从未攻击过”标记。
15. 所有随机决策必须使用 encounter seed，不得使用全局随机源。
16. 实际战力只由 CombatPowerService 计算，UI 和 Boss 模块不得各自重算。
17. Boss 的战力比较和同分裁决只读取当前策略快照中的 bossPerceivedPower。
18. “双杀”只作为体验概念，不作为系统功能、脚本或正式输出字段。
19. BossAllianceController 使用战斗力 70% 阈值计算 BossPowerAdvantage。
20. BossPowerAdvantage=true 时，本周期不得结盟、不得接受申请、不得主动提议。
21. 召唤技能只产生 SummonRequest，不实现召唤物 AI、生命周期或数值。
22. Boss 不保存拆塔承诺；塔目标取舍由目标/塔系统负责。
23. 战斗区域边界由环境/地图系统提供，BossMovement 只遵守边界。
24. Boss 只发布死亡事件和掉落请求，不结算 BossVictory，也不处理恶魔之心生命周期。
25. Survive 使用独立 5 秒快照，只把非盟友存活玩家的当前生命计入敌我生命比。
26. Weak / Normal 求生分别在生命达到 40% / 30% 时结束，Rage 不进入 Survive。
27. Boss 攻击流程输出 slotId 和状态原因，不依赖固定技能名、skillId 或技能效果类型。
28. slot1 只是第一技能槽，不定义特殊槽位类型；各槽的实际 SkillCard 和触发规则由配置决定。
29. 第三槽若启用随机池，必须使用 encounter seed；抽取结果不得改变状态到 slotId 的请求契约。
30. Boss + 玩家结盟要求场上恰好 2 名玩家存活；只剩 1 名或 0 名玩家时绝不结盟。
31. `BossPowerAdvantage=false` 不代表结盟可用；必须先通过 `BossAllianceAllowed=true`。
32. 玩家永久淘汰或 Boss 死亡必须立即结束相关结盟，并在状态结算后刷新所有存活观察者的活动快照；历史快照不改写。
33. 决策快照机制可由 AI 玩家复用，但 AI 使用自己的快照周期和输入，不继承 Boss 专属阈值。
34. 残血修正和有效追击距离只由 BossTargeting 计算，BossMovement 和 BossBehaviorController 不重复计算。
35. 残血追击必须满足 `hpRatio < 30%`；距离 > 40m 或恢复后超出 20m 时必须立即退出该次追击。
36. `slot1State == Disabled` 时不进入 Survive；已在 Survive 中则立即结束并走兜底决策。
37. `slot1State == Cooldown` 时不得每帧轮询；只在转为 Ready 后请求一次。
38. Survive 达到 `maxSurviveDuration` 仍未到退出阈值时强制结束，并进入强制退出冷却。
39. Demo 槽位固定为 slot1 地震波、slot2 分身、slot3 嗜血狂暴；三者 CD 分别为 20 / 15 / 30 秒。
40. 技能槽 CD 独立使用游戏时间，`Commit` 开始，`maxCharges=1`，`Disabled` 时暂停。
41. 结盟状态是实时版本化子状态；变化时立即更新 allianceState 并递增 allianceVersion，不重掷整个 10 秒快照。
42. 快照版本与实时 allianceVersion 不一致时，盟友过滤和攻击合法性以实时版本为准。
43. Weak 同时满足求生和残血追击时优先逃生；Normal 仅在 `<10%` 且 200% 敌我生命比时逃生，否则优先击杀；Rage 永不进入 Survive。
44. Boss 首次向下跨越求生进入阈值或向上跨越退出阈值时，立即刷新/结束求生快照，不等待 5 秒周期。
45. Survive 逃跑目标点必须通过候选评分和 NavMesh 落点校验，不得因 slot1 Cooldown 原地等待；只有目标明显变化或路径失效时才 SetDestination；累计 2.5 秒无有效路径进度必须强制退出。
46. 结盟时长调整按联盟、攻击者归属和游戏秒去重；同一秒内的伤害事件、DoT 跳数和多个召唤物命中不得重复叠加。
47. 结盟正常到期或翻脸时，T0 立即刷新关系相关活动快照；T+3 惩罚仇恨生效时立即刷新目标仇恨快照，不重掷无关战略字段。
48. Boss 死亡只发布事件并请求通用掉落；Boss 不定义获胜者，不处理恶魔之心生命周期或 Endgame 结果。
49. 普通被动回血由 Health/CombatState 执行，不是 Intent；Survive 自动回血是 Boss 通过 `IHealthService` 请求的独立恢复模式，同样不进入 Intent。
50. Survive 的 slot1 请求是最高优先级；Engage / Chase / Rage 中连续 3 秒未命中敌对玩家时，slot1 作为追击受阻的战术例外，优先于普通战斗技能。
51. `lastPlayerHitTime` 只由 Boss 本体对敌对玩家的命中刷新；`BossBehaviorController` 根据它计算 `noPlayerHitElapsed`；分身、召唤物、塔、非敌对目标和未命中攻击不计入，被护盾吸收但已命中的攻击计入。
52. 普通战斗技能只在 Engage / Chase / Rage 请求，固定优先 slot3，其次 slot2；SeekAlliance 不请求当前战斗技能。
53. Ready 只代表可请求；每次行为决策最多提交一个 slotId，普通战斗槽不排队，请求失败或阻塞时下个决策点重新判断。
54. Survive 自动回血通过 `IHealthService` 调用 Health 执行，不依赖普通脱战回血，默认不因受击完全停止；退出 Survive、Rage、死亡或 slot1 不可用时必须停止。
55. 技能效果触发条件属于技能系统；Boss 不读取分身、增益、目标过滤或效果持续时间，只消费技能请求结果。
56. `BossEventBridge.cs` 是外部事件的唯一入口；其他 Boss 脚本不得直接订阅外部系统事件。
57. `BossController.cs` 统一负责死亡结算、动作锁释放、事件发布、掉落请求和强制快照刷新，不新增独立死亡脚本。
58. `BossContext.cs` 是 Boss 依赖唯一组装点；Boss 模块不得使用单例或服务定位器获取外部依赖。
59. `SkillEffectApplier.cs` 负责技能效果条件和效果应用；`BossSkillController.cs` 不读取效果活动状态。

## 十三、完成定义

Boss Demo 完成时，以下流程必须可重复运行：

```text
调试生成 Boss
→ 进入战斗
→ 选择战斗意图
→ 选择并追击目标
→ 普攻并按状态请求技能槽
→ 进入暴走状态和强化层
→ 玩家结盟或翻脸
→ Boss 死亡
→ 发布 BossDiedEvent
→ 请求死亡掉落恶魔之心
```

验收要求：

- 无非法状态迁移。
- 无永久死锁。
- 不依赖正式出生逻辑。
- 不依赖正式动画和 UI。
- 每个阶段均有日志。
- 固定 seed 可复现战斗事件。
