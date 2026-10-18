# 《分道扬镳》一方 AI 玩家功能开发清单

> 版本：v1.1（补全 AI 技能施放、PvP 对抗、治疗、开场采集、经济裁剪、环境规避、圣物、心交付、淘汰与确定性；新增第三部分）
> 状态：AI 玩家 Demo 开发基线
> 用途：第一部分供人快速理解，第二部分供编码 Agent 按 AI 玩家实现规格与任务实施
> 文档边界：本文只负责"AI 接管两名玩家之一"时的感知、快照、记忆、决策、路线、资源管理、生存、结盟申请、翻脸、终局行为和对 Player 系统的操作输出。不重定义 Boss、结盟、Common、Endgame、技能、塔、圣物、环境、物品与玩家死亡规则。
> 结构基线：《Boss功能开发清单》
> 相关文档：《Player 与 AI 开发索引》《AI 机制开发设计文档》《AI 决策落地执行规格》《AI 数值测试与验收文档》《游戏机制总览》《Boss战设计_三国演义式三方博弈》《Boss属性与行为设计》《Boss功能开发清单》《结盟机制设计》《玩家机制设计》《Common 基础系统》《Endgame 终局系统》《技能系统设计》《塔防与塔系统设计》《圣物系统设计》《海岛与海平面上升机制设计》《版本规划》
> 标注约定：`⚠️【文档未明确】` 表示现有设计文档没有给出可执行口径，实现时不得自行拍板；全部登记在第七节。
> 补全约定：`【本文补全建议 · 待策划确认】` 表示现有文档未定义、本文按现有机制推导出的保守方案，可直接指导实现，但冻结前必须由策划确认。

## 目录

- 第一部分：人读版
- 第二部分：AI 玩家实现规格（供编码 Agent）
- 第三部分：补全设计（v1.1 本文新增，待策划确认）

---

# 第一部分：人读版

## 一、开发目标

先完成一个可重复运行的"单方 AI 玩家"纵向 Demo，验证以下完整闭环：

```text
接管一名玩家 → 读取公开信息构建 PlayerView → 每 10 秒更新局势/对手/路线快照
→ 每 0.2 秒按 Layer3→Layer2→Layer1→默认 决策 → 输出 ActionIntent
→ 执行移动/攻击/开箱/囤塔/放塔 → 生存中断 → 玩家间结盟/与 Boss 结盟
→ 翻脸与惩罚响应 → 心争夺 → 淘汰/回魂 → 结算
```

本文的 AI 玩家只消费公开信息和自身状态。它按《Boss功能开发清单》第八、
九、十节的方式落地：一个纯决策函数、一组外置配置、稳定事件通道和可复现日志。

### 1.1 目标运行形态

| 运行形态 | AI 数量 | 说明 |
| --- | --- | --- |
| 单方 AI（本文主目标） | 1 | 一名真人 + 一名 AI，用于新手版单机与好友房补位 |
| 同质 AI 自对弈 | 2 | 两个相同 `Decide` 双开，用于机制验证、稳定性与数值扫描 |

两种形态使用同一套决策函数和同一份配置结构，不维护第二套 AI 逻辑。

> ⚠️【文档未明确】"单方 AI 玩家"是否同时覆盖以上两种形态，现有文档没有统一定位：
> 《AI 机制开发设计文档》定位为"原型阶段 AI vs AI 机制验证专用"，明确不做多人格与角色卡；
> 《版本规划》要求新手版"玩家 + AI 对手"，且 AI 对手可注入人格（保守 / 背叛 / 风筝）
> 并考虑难度分级。本文按"先让单方 AI 能稳定跑完一局"为实现基线，把机制验证、
> 人格与难度分级降为可配置扩展项。该归属需要产品与策划确认。

## 二、Demo 范围

### 2.1 本期开发

- `PlayerView` 公开信息感知、10 秒局势快照、相位错峰和信息延迟。
- `AiMemory` 短期记忆、`trustScore` 对手评估与趋势感知。
- 固定三层优先级决策与生存中断。
- `aggressionScore`、`RESTRAIN / AGGRESSIVE / PRESS / CATCH_UP` 四条路线。
- 克制 / 偷杀、选择性杀怪、开箱、囤塔 / 放塔。
- 终局进入判定、终局收益评分、圣物 solo、心争夺。
- 玩家间结盟申请 / 接受 / 拒绝、与 Boss 结盟申请、翻脸与报复响应。
- 回魂契约决策。
- 输出 `ActionIntent` 并复用 Player 系统的移动、普攻、读条与放置能力。
- `StrategyOverride`（CC / DD / DC / CD）与 AS-1~AS-6 受控场景注入。
- 调试面板、结构化日志、策略探针和固定 seed 复现。

### 2.2 本期暂不开发

- 多人格 / 多角色卡与难度分级。
- 行为树、机器学习、复杂战术与真人手感调优。
- AI 自主商店买卖、宝石合成、装备替换、技能书学习 / 覆盖。
- 网络同步、权威端一致性、断线重连与防作弊。
- Boss 内部快照、仇恨公式、求生阈值和技能槽映射。
- Endgame 胜负结算和恶魔之心生命周期。
- 完整海平面、亲和度双路线与完整塔系统。
- 最终数值平衡。

> ⚠️【文档未明确】以上"暂不开发"中，AI 是否需要自主技能施放、自主经济决策、
> 自主采集任务物品，现有 AI 文档均未给出口径，详见第七节 U-06、U-07、U-08。

## 三、逻辑维度

### 3.1 感知与快照逻辑

感知逻辑回答"AI 能看到什么、信息有多新"，不负责决策。

核心流程：

```text
公开信息源 → PlayerViewBuilder 构建 PlayerView
→ 每 10 秒按相位刷新局势/趋势/对手快照
→ 事件立即响应（被攻击 / 结盟申请 / 心出现 / Boss 结盟发起）
→ 死亡与结盟变化强制刷新
→ Decide 只读取 PlayerView 与自身状态
```

主要完成项：

- `PlayerView` 只包含与 UI 对齐的对手公开信息；对手内部路线、意图、trust、背包明细一律不可读。
- 局势快照周期固定 10 秒；每个 AI 使用 `snapshotPhaseOffset` 固定相位（默认 A 逢 4 / B 逢 9，默认关闭）。
- 相位差用游戏累计时间取模，不使用真实时间；暂停和加速下仍一致。
- S 级对手信息（共享 Boss 进度）按 UI 刷新周期 20-40 秒离散刷新，并带小幅误差。
- 被攻击、结盟申请、心出现、Boss 结盟发起等事件立即响应，不走周期。
- 玩家永久淘汰或 Boss 死亡时，先结束涉及死亡方的结盟并递增 `allianceVersion`，再立即刷新全部活动快照。
- 结盟开始 / 结束 / 翻脸立即更新 `allianceState / allianceTargetId` 并递增 `allianceVersion`，不重掷其他战略字段。
- 快照过期不等于无响应：高频决策消费低频快照，事件通道独立。

### 3.2 状态记忆与对手评估逻辑

状态记忆回答"AI 记得什么"，不负责选行为。

主要完成项：

- `MemoryState` 保存短期标记、计时器、进度采样、`trustScore`、`inferredLabel`、`aggressionScore`、`currentRoute` 和 `routeHoldUntil`。
- 短期标记至少包含：`recentlyBetrayedTimer`、`recentlyHitByBossTimer`、`killStreak`、`noKillDuration`、`lastAllyEndTime`、`powerGap`。
- 趋势采样使用最近两次快照（20 秒窗口）计算 `progressRate`，输出 `SAFE / WATCH / URGENT`。
- `trustScore` 只由公开可推断证据累计：对手偷杀增量、主动申请结盟、结盟期间攻击我方 / 拆我方塔。
- 每个 AI 独立维护 `trustScore`，不共享、不写入 `PlayerView`。
- 连续若干刷新周期无新证据时，`trustScore` 向中性缓慢衰减。

> ⚠️【文档未明确】`trustScore` 存在两套刻度：
> 《AI 机制开发设计文档》8.4 使用 `[0,1]` 与系数 `-0.3/+0.2/-0.5`；
> 《AI 决策落地执行规格》4.4、7.3 使用 `[0,100]`，初始 50、接受阈值 60。
> 两者数值口径互斥，详见第七节 U-10。

### 3.3 终局战略逻辑

终局战略回答"最后阶段优先做什么"，是决策最高层。

进入条件（任一满足，进入后不退回）：

```text
timeToEndgame <= 0
seaLevel >= 峰顶决战圈
boss.awaken == true
```

主要完成项：

- 终局不再使用固定 if/else，而是对候选行为做终局收益评分，取最高分。
- 候选行为至少包含：抢夺心、击杀圣物持有者、solo Boss、玩家间结盟、弱者 + Boss 结盟、风筝 / 拖延、放置囤塔、逼近 Boss。
- 心被对方拾取中时，立即打断 / 抢心（事件响应，最高优先）。
- 心已掉落且可达时优先抢心，除非自身残血。
- 持圣物且 `soloWillingness > 0.6` 且未被追杀时选择 solo，不再结盟。
- `allianceCooldown == 0` 且血量低于结盟阈值时按弱者 / 非弱者选择"弱者 + Boss"或"玩家间"结盟申请。
- `towerStock > 0` 且当前无结盟时放置囤塔。
- 否则默认逼近 Boss。

### 3.4 路线评分逻辑

路线评分回答"当前走克制还是激进、领先还是追赶"，只在战略快照点切换。

主要完成项：

- 弱势判定 `isUnderdog`：血量低于 0.35、`powerGap < -cfg_gapLead`、或自身无圣物而对手持圣物，三者之一成立。
- `powerGap` 只由公开 `powerRatio` 计算，不读取对手内部战力。
- 领先方走 `PRESS`，落后方走 `CATCH_UP`，接近态按 `aggressionScore` 在 `RESTRAIN / AGGRESSIVE` 间选择。
- `aggressionScore` 由基准风险、对手偷杀证据、战力差、刷新窗口剩余、trust 扣分、Boss 进度风险、自保需求、结盟可用加成共同计算。
- 路线切换必须满足滞回和至少一个快照周期锁定，禁止因分数抖动反复横跳。
- 双方输入完全相同时选择同一路线是允许的，不强制一克制一激进。
- 路线只改变资源管理倾向与目标选择，不覆盖事件响应、生存和终局行为。

### 3.5 资源管理逻辑

资源管理回答"发育期杀不杀怪、开不开箱、塔放不放"。

主要完成项：

- 硬约束：`trend == URGENT` 时强制克制，即使 `aggressionScore` 很高。
- 克制路线只杀"战力瓶颈必需怪"（被怪追击反击、下一只怪提供关键升级、无塔且需要塔材料、开局前 5% 进度）；其余不主动杀怪。
- 激进路线杀所有可杀怪，保留生存必需击杀。
- 发育期默认囤塔（`towerStock` 不放置）；结盟期间 `towerStock` 锁定；终局或结盟结束后在安全窗口批量放置。
- 放塔必须由塔系统确认"已解锁（任务结束 +5 分钟）、合法落点、40m 排他与配额"后再执行；AI 不自行判断数量、地形合法性与解锁状态。
- 克制时用开箱、塔材料、回血点等替代收益填充行为，避免原地发呆。
- 开箱读条、采集读条和拾取复用 Player / Item 的通用读条与受击回退规则。

> ⚠️【文档未明确】囤塔 / 放塔的塔配额存在版本差异：
> 《塔防与塔系统设计》为"资源驱动、通常 4 座 / 玩家、保底 2 座"；
> 《游戏机制总览》弱化版为"每名玩家最多 1-2 座防御塔"；
> 《AI 决策落地执行规格》写"塔配额（总）4 座，见附录 A"。
> AI 只能消费塔系统提供的"当前可制造 / 可放置"结果，不得自行写死数量，详见第七节 U-12。

### 3.6 生存与事件响应逻辑

生存与事件响应贯穿所有层，是最高优先级中断。

核心流程：

```text
进入任意决策周期
→ 血量 < 生存阈值：治疗可用则治疗，否则逃跑，无处可逃则申请结盟
→ 事件队列非空：被攻击 / 结盟申请 / 心出现 / Boss 结盟发起 → 立即响应
→ 否则按 Layer3 → Layer2 → Layer1 → 默认 顺序决策
```

主要完成项：

- 生存判断位于所有 Layer 之前，属于中断而非 Layer。
- 生存行为优先级：治疗 → 逃跑 → 申请结盟。
- 被攻击按 `hpRatio` 决定反击或逃跑。
- 结盟申请按 `isUnderdog`、`trustScore`、`hpRatio` 参数化接受 / 拒绝。
- 心出现立即转入终局争夺评分。
- Boss 结盟发起时识别"我成为被追杀方"，切换为风筝 / 拖延 / 抢先削弱 Boss。
- 结盟结束重置 `recentlyBetrayedTimer`，下一决策周期触发报复倾向。

### 3.7 结盟逻辑

结盟逻辑回答"AI 是否申请、是否接受、是否翻脸"，不重定义结盟业务规则。

主要完成项：

- 玩家间结盟：任意一方可申请，玩家之间没有仇恨门槛；每名玩家每局一次申请机会；2-3 秒确认。
- 与 Boss 结盟：只在 Boss 苏醒后生效；申请不经过 Boss 主动概率，Boss 侧按 10 秒局势快照、残血排除和仇恨门槛判定。
- AI 申请与 Boss 结盟时，自身当前对 Boss 的仇恨总分必须归零；伤害仇恨、圣物仇恨和追杀仇恨都会阻止。
- AI 主动申请弱者 + Boss 结盟时，只走申请路径，不模拟 Boss 的 20% 主动概率。
- 结盟期间负面技能不对对手生效，攻击塔不攻击对手，未放置塔锁定不可放置。
- 翻脸判定分两类：结盟中途主动攻击（立即破裂 + 惩罚仇恨），结盟倒计时结束后首次攻击（破裂但不计惩罚）。
- 结盟自然到期、翻脸、玩家淘汰、Boss 死亡时的快照刷新与惩罚仇恨由通用 `AllianceService` 与 Boss 侧规则负责，AI 只消费事件和公开状态。

> ⚠️【文档未明确】以下结盟口径需要补齐：
> 1. "每局一次申请机会"在超时 / 被拒后是否消耗（《结盟机制设计》开放问题 1）；
> 2. AI 如何得知自己是否仍有申请机会，现有 `SelfState` 没有对应字段；
> 3. 结盟期间 AI 应停手、继续打 Boss 还是准备翻脸，决策规格未写；
> 4. "岛中岛任务结束前强制结盟"与"结盟系统只在 Boss 苏醒后生效"两处措辞冲突。
> 详见第七节 U-16、U-17、U-26。

### 3.8 战斗与操作输出逻辑

战斗与操作输出回答"AI 输出什么动作给 Player 系统执行"。

主要完成项：

- `Decide` 只输出 `ActionIntent`，不直接移动、攻击、施法、拾取或放置。
- `ActionIntent` 至少包含移动目标、攻击目标、读条动作、塔操作、结盟动作、优先级和 `reasonCode`。
- 移动、普攻、开箱、采集、拾取、放置与读条由 Player / Item / Tower 系统执行，AI 只发意图。
- 同一次决策最多输出一个主动意图；高优先级事件可以打断当前读条或移动意图。
- 所有意图必须带 `reasonCode`，供日志与机器可读断言使用。

> ⚠️【文档未明确】AI 玩家的技能施放与目标选择没有定义：
> 玩家有天赋槽 + 学习槽两个技能槽（《技能系统设计》），
> 但《AI 决策落地执行规格》的 `ActionIntent` 不含技能施放字段，
> AI 决策层也没有"何时放技能、放哪个槽、如何选目标"的规则。
> 本文把技能施放列为外部依赖与后补项，详见第七节 U-06。

### 3.9 死亡、回魂与淘汰逻辑

死亡逻辑复用《玩家机制设计》，AI 只增加决策部分。

主要完成项：

- Boss 未苏醒时，AI 死亡后满足回魂资格就自动发起回魂申请，不做额外概率判断。
- AI 死亡必须走与真人玩家完全相同的死亡流程：死亡瞬间 Boss 苏醒进度固定推进 60 秒，之后进入 10 秒回魂申请窗口；该推进不因回魂成功撤销。
- AI 作为存活方收到回魂申请时，只使用双方公开的防具宝石状态与自身 `trustScore`。
- 任意一方没有已装备防具宝石时，AI 直接拒绝。
- 双方都有防具宝石且 `trustScore >= cfg_trustRevive` 时接受，否则拒绝。
- 回魂流程、宝石销毁、10 秒窗口、5 秒确认、15-20 秒后 50% 生命复活由 Player / 对局系统执行。
- Boss 苏醒后死亡的 AI 按永久出局处理，不再进入回魂。
- AI 死亡或永久淘汰时立即结束其参与的结盟并刷新活动快照，不进入缓冲、不产生追杀仇恨。

> ⚠️【文档未明确】AI 永久淘汰后的行为未定义：是否停止 Tick、是否作为观察者继续写日志、
> 是否影响 Endgame 的双死判定与结算，现有文档只写了玩家侧规则，详见第七节 U-15。

### 3.10 系统边界

AI 玩家模块只输出自己的 Intent 和动作请求：

- Boss 的目标选择、仇恨、求生、技能槽、掉落请求由 Boss 模块负责；AI 只读公开 `BossState`。
- 结盟的请求 / 响应 / 计时 / 关系过滤 / 破裂与惩罚仇恨由 `AllianceService` 负责；AI 只发申请、接受、拒绝和翻脸意图。
- Endgame 的心状态机、拾取读条、易手、交付和最终结算由 Endgame / Item 负责；AI 只发抢心与交付意图。
- 玩家移动、普攻、技能、读条、背包、装备、宝石由 Player 及相关系统负责；AI 不直接改写这些状态。
- 塔的可制造量、放置合法性、排他检测和配额由塔系统负责；AI 只消费结果并发放置意图。
- 海平面、环境伤害和地图收缩由环境系统负责；AI 只读取 `WorldState.seaLevel`。
- 伤害、防御、护盾、回血由 Common 负责；AI 不自行结算伤害或修改生命。
- 恶魔之心、掉落物与圣物实例由 Item / Loot 负责；AI 不持有实例。

## 四、脚本概览

脚本统一分为三类：外部引用脚本、通用脚本、AI 玩家专属脚本。新增规划标记为"新增"，其余为已有规划或已有脚本。

### 4.1 外部引用脚本

外部引用脚本由对应系统负责实现，AI 只通过第 9.4 节的接口消费。AI 文档不复制其实现。

| 外部契约 / 脚本 | 归属系统 | AI 使用方式 | 主要功能 |
| --- | --- | --- | --- |
| `IAiActionExecutor` 实现 | Player | 输出动作意图 | 执行移动、普攻、读条、放置、结盟与拾取意图 |
| `IPlayerViewSource` 实现 | UI / Info | 构建对手公开快照 | 提供与 HUD 对齐的对手公开信息 |
| `IBossStateProvider` 实现 | Boss | 读取公开 Boss 状态 | 提供 Boss 血量、阶段、强化层、当前目标与结盟对象 |
| `IWorldStateProvider` 实现 | World / Environment | 读取世界公开状态 | 提供海平面、游戏时间、终局剩余时间 |
| `IHeartStateProvider` 实现 | Endgame | 读取恶魔之心状态 | 提供心状态、位置、持有者与拾取进度 |
| `IAllianceService` 实现 | Common | 申请 / 响应 / 查询结盟 | 提供结盟请求、响应、状态、版本与事件 |
| `ICombatPowerProvider` 实现 | Common / Stats | 读取战力分项 | 提供 生命×1 + 当前 DPS×4 + 防御×2 + 已放塔攻击×1 的分项与总战力 |
| `ITowerProvider` 实现 | Tower | 读取塔的可放置状态 | 提供塔材料、可放置点、配额与结盟锁定状态 |
| `IRelicStateProvider` 实现 | Items | 读取圣物与仇恨 | 提供是否持圣物、圣物类型、`relicHate` 与版本 |
| `IHealthStateProvider` / `IHealthService` 实现 | Common | 读生命 / 请求回血模式 | 提供生命、存活、允许回血与回血执行 |
| `IPlayerLifecycle` 实现 | Player | 回魂与淘汰 | 提供回魂资格、申请、响应与永久淘汰事件 |
| `ISkillSlotController` 实现 | Skills | 自身技能槽 | 提供天赋槽 / 学习槽状态与施放入口（本期后补） |
| `ITaskInteractor` 实现 | Quest / Item | 采集与拾取 | 提供任务物品、宝箱与地面拾取交互（边界未明） |

> ⚠️【文档未明确】`ISkillSlotController` 和 `ITaskInteractor` 是 AI 文档没有明确要求、
> 但实现"完整玩家行为"必然需要的接口；本文只登记接口位置，不定义 AI 侧规则。

### 4.2 通用脚本

通用脚本由 Common 或共享技能框架负责，可被 Boss、怪物、玩家和 AI 复用。

| 脚本 | 主要功能 | AI 侧使用方式 |
| --- | --- | --- |
| `EventBus.cs` | 发布、订阅、事件队列和稳定分发顺序 | 接收攻击、结盟、心、淘汰事件，发布 AI 决策日志 |
| `EntityMemory.cs` | 快照、计时器、伤害历史与版本容器 | 承载 AI 快照容器；AI 记忆规则保存在 `AiMemory` |
| `SnapshotService.cs` | 周期快照、相位错峰、强制刷新与版本 | 提供 AI 10 秒局势快照和死亡强制刷新 |
| `CombatPowerService.cs` | 实际战力、分项与版本快照 | 提供 AI 自身战力与对手公开战力 |
| `AllianceService.cs` | 结盟请求、响应、计时、关系过滤、破裂与事件 | 执行 AI 的结盟意图并返回结果 |
| `Health.cs` / `IHealthService.cs` | 生命、受伤、死亡与回血执行 | AI 只读生命、请求回血模式 |
| `CombatState.cs` | 战斗进入、脱战延迟与战斗状态版本 | AI 判断是否处于战斗 |
| `DamageService.cs` | 统一伤害入口、防御、护盾、减伤 | AI 的攻击与技能走统一入口 |
| `GameClock.cs` | 游戏时间和遭遇时间 | AI 决策、快照、路线锁定与结盟计时 |
| `EncounterRandom.cs` | encounter seed 驱动的确定性随机 | AI 感知误差、机会与相位扰动 |
| `Skills/SkillCard.cs` | 技能效果、目标、施法与范围 | AI 自身技能施放（本期后补） |
| `Skills/SkillSlotBinding.cs` | CD、触发策略、优先级 | AI 自身技能槽（本期后补） |
| `Skills/SkillSlotRuntime.cs` | 单槽冷却与就绪状态 | AI 自身技能槽（本期后补） |
| `Skills/SkillCastController.cs` | 全局施放状态与锁 | AI 技能请求执行（本期后补） |

### 4.3 AI 玩家专属脚本

| 脚本 | 主要功能 | 明确不负责 |
| --- | --- | --- |
| `AiContext.cs`（新增） | 组装 AI 依赖，注入 Provider、Common 服务与配置 | 不保存决策规则，不实现 Provider |
| `AiEventBridge.cs`（新增） | 订阅外部事件，规范化并交给 AiController | 不判断结盟、心与死亡业务 |
| `AiController.cs`（新增） | 生命周期、Tick 调度、事件中断、快照触发与意图提交 | 不执行移动、攻击、施法与放置 |
| `AiDefinition.cs`（新增） | 引用 `AiConfig` 与角色 / 人格预设 | 不保存运行时状态 |
| `AiConfig.cs`（新增） | 全部阈值、权重、周期与外置参数 | 不硬编码具体对局逻辑 |
| `AiDecisionService.cs`（新增） | 纯函数 `Decide`，按 Layer3→Layer2→Layer1→默认 返回 Action | 不访问场景对象，不读对手私有状态 |
| `PlayerViewBuilder.cs`（新增） | 从公开信息源构建 `PlayerView` | 不推断对手内部路线与意图 |
| `AiMemory.cs`（新增） | 短期标记、进度采样、trust、路线与锁定计时 | 不保存决策规则 |
| `AiTrendEvaluator.cs`（新增） | 计算 `progressRate` 与 `SAFE / WATCH / URGENT` | 不选择行为 |
| `AiRouteEvaluator.cs`（新增） | `isUnderdog`、`aggressionScore`、路线与滞回 | 不执行资源与战斗 |
| `AiEndgameEvaluator.cs`（新增） | 终局进入判定与终局收益评分 | 不处理心状态机与交付结算 |
| `AiResourceManager.cs`（新增） | 克制 / 偷杀、选择性杀怪、开箱、囤塔 / 放塔 | 不定义掉落、塔属性与配额 |
| `AiSurvivalEvaluator.cs`（新增） | 生存阈值、治疗 / 逃跑 / 结盟中断 | 不执行治疗、移动与回血 |
| `AiAllianceController.cs`（新增） | 结盟申请、接受 / 拒绝、翻脸与 Boss 结盟响应 | 通用结盟计时与关系由 AllianceService 负责 |
| `AiReviveController.cs`（新增） | 回魂申请与响应决策 | 回魂执行由 Player / 对局系统负责 |
| `AiActionExecutor.cs`（新增） | 把 `ActionIntent` 适配为 Player 操作请求 | 不计算决策，不修改战斗数值 |
| `AiDebugTools.cs`（新增） | 调试面板、StrategyOverride、场景注入与日志回放 | 不进入正式决策路径 |

> ⚠️【文档未明确】现有 `Assets/Script/Player/Behaviour` 下已有一套 Utility AI
> （`CharacterCard`、`PlayerBehaviourModel`、`AttackAction`、`KiteAction` 等，
> 见《游戏机制总览》3.4）。它与本文的 `Decide` 纯函数是替换、并存还是作为动作层复用，
> 现有文档没有说明，详见第七节 U-28。

## 五、里程碑

| 里程碑 | 内容 | 完成结果 |
| --- | --- | --- |
| M1 | 契约、感知、记忆、快照与确定性随机 | AI 能构建 PlayerView、每 10 秒刷新快照、按 seed 复现 |
| M2 | 决策主循环、三层优先级、趋势与路线 | AI 能按 0.2 秒决策并在四路线间正确切换、无震荡 |
| M3 | 资源管理、生存与事件响应 | AI 能克制 / 偷杀、开箱、囤塔 / 放塔、残血保命 |
| M4 | 终局与结盟 | AI 能抢心、solo、申请玩家间结盟、申请弱者 + Boss 结盟、翻脸与响应 |
| M5 | 执行适配、回魂与集成调试 | 单方 AI 纵向闭环可重复运行；AI 稳定性前置通过 |
| M6 | 补全设计落地（第三部分） | 技能、PvP、治疗、开场、经济、环境、圣物、心交付与确定性可运行；待策划确认项全部标注 |

## 六、当前需要冻结的数值

| 项目 | 当前状态 |
| --- | --- |
| AI 决策频率 | 0.2 秒（5Hz）；⚠️《AI 机制开发设计文档》8.3 写"每 0.5s 或即时"，需统一 |
| 局势快照周期 | 10 秒 |
| 相位错峰 | A 逢 4 / B 逢 9；默认关闭 |
| 共享进度信息延迟 | 20-40 秒刷新 |
| 生存阈值 `cfg_survivalHp` | 0.25 |
| 结盟血量阈值 `cfg_allianceHpThreshold` | 0.40 |
| 接受结盟信任阈值 `cfg_trustAccept` | 60 |
| 接受回魂信任阈值 `cfg_trustRevive` | 60 |
| Boss 结盟概率 `cfg_bossAllyProb` | 0.50（0.3-0.7）；⚠️语义双重，见 U-34 |
| 圣物 solo 最低血量 `cfg_soloHpMin` | 0.30 |
| 圣物 solo 对手邻近惩罚 `cfg_soloOpponentNearPenalty` | 1.5 |
| 趋势低阈值 `cfg_rateLow` | 1.5 %/s |
| 趋势高阈值 `cfg_rateHigh` | 3.5 %/s |
| 激进阈值 `cfg_routeHigh` / 克制阈值 `cfg_routeLow` | 0.6 / 0.4 |
| 路线滞回 `cfg_hysteresis` | 0.08 |
| 战力差阈值 `cfg_gapLead` | 0.25 |
| aggressionScore 权重 | `w_killDelta=0.15`、`w_leadPower=0.30`、`w_window=0.10`、`w_trust=0.10`、`w_rate=0.40`、`w_survive=0.50`、`w_allianceReady=0.20` |
| 终局评分权重 | `w_gain=1.0`、`w_risk=0.6`、`w_progress=0.8`、`w_bossThreat=0.5` |
| trust 初始 / 刻度 | 初始 50，范围 0-100；⚠️与主文档 `[0,1]` 口径冲突 |
| trust 更新 | `cfg_stealThreshold=3`、`cfg_trustHit=30`、`cfg_trustCoop=15`、`cfg_trustDecay=0.05` |
| 塔配额 | 4 座 / 玩家、保底 2 座；⚠️与《游戏机制总览》弱化版 1-2 座冲突、与 +5 分钟解锁关系未定，见 U-12、U-37 |
| 心拾取读条 | 10 秒；受击回退、移动重置；回退步长未定 |
| 玩家间结盟时长 | 默认 45 秒（Boss 清单冻结值）；AI 候选 35 秒，见 U-35 |
| 弱者 + Boss 结盟时长 | 默认 20 秒（Boss 清单冻结值）；AI 候选 17 秒，见 U-35 |
| 结盟确认 / 冷却 | 2-3 秒 / 30 秒 |
| 结盟结束惩罚 | 3 秒缓冲后 +5000，30 秒线性归零 |
| 回魂窗口 / 确认 / 复活 | 10 秒 / 5 秒 / 15-20 秒，50% 生命 |
| 种子组规模 | 每场景 30-50 局 + holdout |
| 接受门禁 | 双方胜率 `[35%,65%]`；双输率 `<=25%`；M6 易手 / 打断 `>10%`；击杀者交付率 `<70%` |
| AI 稳定性 | 有效局率 `>=90%`；卡死 / 寻路失败 `<=2%` |
| 完成判据 | 锁定策略收益满足 `T > R > P > S` |

## 七、文档未明确项与待决清单

> 本节是本清单的强制组成部分。以下条目在现有文档中无法明确，
> 相关任务必须保持可配置、可关闭，并在实现前登记决策，不得自行拍板。

| 编号 | 未明确内容 | 涉及文档 | 影响 | 建议处理 |
| --- | --- | --- | --- | --- |
| U-01 | AI 玩家的产品目标归属：机制验证 AI vs 新手版对手 AI | 《AI 机制开发设计文档》1.1 / 《版本规划》二 | 决定是否做人格、难度、教学行为 | 先定单方 AI 实现基线；机制验证与人格分开关 |
| U-02 | "一方 AI 玩家"是否覆盖 1 真人 + 1 AI 与 AI vs AI 两种形态 | 《版本规划》二 / 《AI 机制开发设计文档》1.2 | 决定控制器数量与权威端关系 | 同一 `Decide` 双开；形态由外部编排决定 |
| U-03 | 新手版"与 Boss 结盟 60%"的简化边界 | 《版本规划》八.6 | 决定 AI 侧结盟接口是否需简化分支 | Boss 侧系统开关裁剪；AI 接口按完整规则设计 |
| U-04 | AI 难度分级（简单 / 中等 / 困难） | 《版本规划》八.1 | 决定 `AiConfig` 是否需要难度层 | 本期只保留配置分层能力，不实现难度 |
| U-05 | 好友房缺人是否机器人补位 | 《版本规划》八.2 | 决定是否需要权威端同构 AI | 本期不做网络权威端 AI |
| U-06 | AI 玩家技能施放：何时放、放哪个槽、如何选目标 | 《技能系统设计》二 / 《AI 决策落地执行规格》2.2、9 | 玩家有双技能槽，AI 缺对应动作 | 新增技能施放子模块；决策规格需补字段 |
| U-07 | AI 经济决策：商店买卖、宝石合成、装备替换、技能书学习 | 《游戏机制总览》六 / 《装备与宝石系统设计》 | 影响成长与终局战力，决定 AI 是否"残疾" | 本期走 Player 通用接口，AI 自主经济后补 |
| U-08 | AI 采集任务物品与岛中岛开场行为 | 《任务物品生命周期设计》/ 《岛中岛开场设计》 | Opening 阶段 AI 可能发呆或卡关 | 需补 Opening 行为与采集决策 |
| U-09 | `PlayerView` 字段集合两份文档不一致 | 《AI 机制开发设计文档》8.2 / 《AI 决策落地执行规格》2.2 | 直接决定信息边界与防作弊口径 | 以《AI 决策落地执行规格》为准，统一字段命名 |
| U-10 | `trustScore` 刻度与系数不一致：`[0,1]` vs `[0,100]` | 《AI 机制开发设计文档》8.4 / 《AI 决策落地执行规格》4.4、7.3 | 阈值 60 与系数 0.3 无法同时成立 | 统一为 `[0,100]`，回改主文档系数 |
| U-11 | 决策频率不一致：0.2s vs 0.5s | 《AI 决策落地执行规格》1.1 / 《AI 机制开发设计文档》8.3 | 影响反应速度、事件响应与性能 | 以 0.2s 为准，事件走即时通道 |
| U-12 | 塔配额版本冲突：4 座 / 保底 2 座 vs 1-2 座 | 《塔防与塔系统设计》二 / 《游戏机制总览》7.6 / 《AI 决策落地执行规格》5.2 | 囤塔与终局风筝强度 | AI 只消费塔系统结果，不写死数量 |
| U-13 | 心拾取读条回退步长与打断数值 | 《Boss战设计》十 / 《AI 数值测试与验收文档》4.4 | 影响 AI 抢心与打断行为 | 属 Endgame 规则，需补 |
| U-14 | AI 如何打断对手抢心：普攻 / 技能 / 其他 | 《AI 决策落地执行规格》3.2 | 决定"打断"是否可实现 | 需明确打断手段与归属系统 |
| U-15 | AI 永久淘汰后是否停 Tick、是否观察者写入 | 《玩家机制设计》1.5 / 《AI 数值测试与验收文档》4.6 | 影响 Endgame 双死与稳定性 | 需明确 AI 出局后状态 |
| U-16 | 结盟期间 AI 的行为约束（停手 / 打 Boss / 准备翻脸） | 《结盟机制设计》五 / 《AI 决策落地执行规格》三 | AI 可能在结盟期做出违规动作 | 需补结盟期行为模板 |
| U-17 | 玩家间结盟"每局一次申请机会"的消耗口径与 AI 可见状态 | 《结盟机制设计》十.1 | AI 无法判断是否还能申请 | 需补字段与消耗规则 |
| U-18 | 非终局阶段 AI 是否主动申请玩家间结盟 | 《AI 决策落地执行规格》3.2、7.2 | 结盟可能只在终局触发 | 需明确各阶段结盟发起条件 |
| U-19 | `cfg_gapLead` 同时用于弱势判定与路线判定是否合理 | 《AI 决策落地执行规格》4.1、4.3 | 一个参数控制两种语义 | 建议拆为 `cfg_underdogGap` 与 `cfg_routeGap` |
| U-20 | 与 Boss 结盟后 AI 是否视为转阵营、是否需避免攻击 Boss | 《结盟机制设计》二.1 / 《Boss功能开发清单》3.5 | 盟友攻击 Boss 会缩短结盟 | 需明确 AI 是否知道时长调整规则 |
| U-21 | 圣物获取 / 抢夺的动作与版本差异 | 《圣物系统设计》/ 《版本规划》四 | 规格有"击杀持有者"但无拾取动作 | 需补圣物取得与抢夺行为 |
| U-22 | AI 对海平面 / 环境伤害的规避与转高地行为 | 《海岛与海平面上升机制设计》/ 《AI 数值测试与验收文档》A-6 | AI 可能被淹死或卡在死区 | 需补环境规避规则 |
| U-23 | 无头沙盘与 Unity 场景的参数回灌口径 | 《AI 机制开发设计文档》8.7 | 沙盘结论无法落地 | 需定义沙盘输出到 `AiConfig` 的映射 |
| U-24 | AI 随机 seed 是否等同 encounter seed、是否需要个体 salt | 《AI 决策落地执行规格》1.1 / 《Common 基础系统》 | 两个 AI 可能随机序列相同 | 需明确 seed 派生规则 |
| U-25 | 机器可读事件断言字段命名未冻结 | 《AI 机制开发设计文档》6.5 / 《AI 数值测试与验收文档》九 | 验收脚本无法统一解析 | 需补事件字段清单 |
| U-26 | "岛中岛结束前强制结盟"与"结盟只在 Boss 苏醒后生效"冲突 | 《结盟机制设计》一 / 《游戏机制总览》4.1 | 开场阶段关系语义不清 | 需区分"开场止戈"与"Boss 战结盟" |
| U-27 | 目标版本时长不同（10 / 15 / 22-25 分钟） | 《版本规划》一 / 《游戏机制总览》1.2 | `timeToEndgame` 与海平面参数不同 | 按版本给 `AiConfig` 与场景配置 |
| U-28 | 现有 Utility AI 资产与新 `Decide` 纯函数的关系 | 《游戏机制总览》3.4 / `Assets/Script/Player/Behaviour` | 可能出现两套 AI 并存 | 需明确替换 / 并存 / 复用动作层 |
| U-29 | AI 底层移动、寻路、读条是否复用 Player 组件 | 《AI 决策落地执行规格》9 / 《游戏机制总览》3.1 | 决定执行层实现量 | 建议复用 Player，AI 只发 ActionIntent |
| U-30 | AI 持心后的交付动作与目标点 | 《Endgame 终局系统》三 / 《游戏机制总览》1.4 | 心争夺可能无法收尾 | 需补交付行为与目标点 |
| U-31 | AI 何时攻击对手玩家（压制 / 打残不打死 / 击杀背叛） | 《核心机制设计_囚徒困境引擎》二 / 《Boss战设计》四 | 玩家间对抗是囚徒困境核心，但决策层只在翻脸、圣物抢夺和终局给出 PvP 目标；Action 有 Attack 却没有 PvP 决策 | 需补 "骚扰 / 打残 / 击杀" 三档 PvP 行为与触发条件 |
| U-32 | AI 的治疗手段与优先级 | 《游戏机制总览》3.1、3.2、6.3 / 《道具系统设计》 | 玩家没有自身治疗技能，`Heal` 需要落到药水、阿蛮回血点或营地回血；三者可达性与优先级未定 | 需明确治疗手段清单与选择规则 |
| U-33 | 攻击对手（未击杀）是否推进 Boss 苏醒 | 《游戏机制总览》十一.15 / 《核心机制设计》二 | 决定 AI 能否从共享进度推断对手 PvP 行为，直接影响偷杀证据与收益判断 | 需先冻结"攻击 / 打残 / 击杀"各自的进度权重 |
| U-34 | `cfg_bossAllyProb` 语义双重 | 《AI 决策落地执行规格》3.2 / 附录 A | 同一参数既表示"AI 是否发起弱者+Boss 申请"，又被写成"Boss 响应概率"；与 Boss 侧 20% 主动概率混淆 | 需拆为 AI 申请概率与 Boss 响应概率，或明确只保留一侧 |
| U-35 | 结盟时长口径 AI 文档与 Boss 清单不一致 | 《AI 机制开发设计文档》附录 A / 《Boss功能开发清单》十.6 | AI 取 17s / 35s，Boss 清单冻结 20s / 45s | 需统一，或明确新手版与原型版分别取值 |
| U-36 | 加速技能数值口径冲突 | 《游戏机制总览》3.1 / 《技能系统设计》二 | Q 加速一处为 30s CD，一处为 20s CD；AI 技能施放子模块无法确定上限 | 需统一技能数值来源后再接入 U-06 |
| U-37 | 防御塔解锁时间与 AI 放置资格 | 《游戏机制总览》7.6 / 《塔防与塔系统设计》二 | 塔在任务结束 +5 分钟才解锁；AI 若只读配额会尝试提前放塔 | 放置合法性统一由塔系统判定，AI 只读可放置结果 |

---

# 第二部分：AI 玩家实现规格（供编码 Agent）

## 八、编码 Agent 执行规则

1. 按依赖顺序和里程碑开发；任务编号只用于追踪，不代表执行顺序。
2. 每个任务必须先满足依赖，再修改代码。
3. 不得自行改变游戏规则、数值口径或脚本职责。
4. 第七节的每一项未明确项，实现时必须保持可配置或可关闭，并在交付说明中标记。
5. 所有数值放入 `AiConfig`，不硬编码在 `MonoBehaviour` 中。
6. 决策逻辑不得依赖 Animator、UI、特效或摄像机是否就绪。
7. 每个任务结束时必须满足该任务的验收条件。
8. 不创建只搬移字段、没有独立状态的辅助脚本。
9. 单个脚本超过约 400 行，或出现独立变化原因时再拆分。
10. 每个核心决策、路线切换、结盟动作和快照刷新必须写入结构化日志。
11. `Decide` 内部禁止使用 `UnityEngine.Random`、`FindObjectOfType`、全局单例或服务定位器。
12. 同种子、同输入必须同输出；所有随机只允许由 `seed` 派生且调用顺序固定。

## 九、目录与脚本职责

脚本职责以第 4.1 至 4.3 节为准。本节只固定目录归属和依赖边界。

### 9.1 外部引用脚本

外部引用脚本由所属系统维护，不放入 `Assets/Script/AI`：

```text
Player / IAiActionExecutor
UI / IPlayerViewSource
Boss / IBossStateProvider
World / IWorldStateProvider
Endgame / IHeartStateProvider
Common / IAllianceService
Common / ICombatPowerProvider
Tower / ITowerProvider
Items / IRelicStateProvider
Common / IHealthStateProvider / IHealthService
Player / IPlayerLifecycle
Skills / ISkillSlotController            // 本期后补
Quest / ITaskInteractor                  // 边界未明
```

- AI 只持有接口或稳定事件引用，不直接依赖外部系统实现类。
- 外部脚本的具体文件名和内部结构由各自权威文档决定。
- `AiContext.cs` 负责把这些外部引用注入 AI 模块。
- `AiEventBridge.cs` 负责外部事件的订阅、退订和规范化。

### 9.2 通用脚本

```text
Assets/Script/Common/
├─ EventBus.cs
├─ EntityMemory.cs
├─ SnapshotService.cs
├─ CombatPowerService.cs
├─ AllianceService.cs
├─ Health.cs
├─ IHealthStateProvider.cs
├─ IHealthService.cs
├─ CombatState.cs
├─ DamageService.cs
├─ GameClock.cs
├─ EncounterRandom.cs
└─ Skills/
   ├─ SkillCard.cs
   ├─ SkillSlotBinding.cs
   ├─ SkillSlotRuntime.cs
   └─ SkillCastController.cs
```

- 通用脚本不得读取 AI 路线、trust 阈值、资源配额或结盟业务判定。
- Common 负责事实、状态、计时、伤害、事件和技能执行能力；AI 负责把业务意图传给这些能力。
- `SnapshotService.cs` 提供周期、相位错峰、强制刷新和版本；AI 只提供自己的 `snapshotProfile`。
- `EncounterRandom.cs` 提供 encounter seed；AI 不得自行创建随机源。

### 9.3 AI 玩家专属脚本

```text
Assets/Script/AI/
├─ AiContext.cs
├─ AiEventBridge.cs
├─ AiController.cs
├─ AiDefinition.cs
├─ AiConfig.cs
├─ AiDecisionService.cs
├─ PlayerViewBuilder.cs
├─ AiMemory.cs
├─ AiTrendEvaluator.cs
├─ AiRouteEvaluator.cs
├─ AiEndgameEvaluator.cs
├─ AiResourceManager.cs
├─ AiSurvivalEvaluator.cs
├─ AiAllianceController.cs
├─ AiReviveController.cs
├─ AiActionExecutor.cs
└─ AiDebugTools.cs
```

- `AiController.cs` 负责 Tick 调度、事件中断、快照触发、意图提交和死亡后停止，不新增独立生命周期脚本。
- `AiEventBridge.cs` 只做事件入口和顺序稳定化，不包含 AI 决策规则。
- `AiDecisionService.cs` 是唯一决策入口，必须保持纯函数；不得访问场景对象。
- `PlayerViewBuilder.cs` 只消费公开信息；不得读取对手 `SelfState`、路线、意图、背包明细。
- `AiRouteEvaluator.cs` 只计算路线与滞回，不执行资源行为。
- `AiEndgameEvaluator.cs` 只做终局评分，不处理心状态机和交付结算。
- `AiAllianceController.cs` 负责 AI 侧结盟决策，通用请求、计时和关系由 `AllianceService.cs` 承担。
- `AiActionExecutor.cs` 只做意图到 Player 操作的适配，不参与决策。

> ⚠️【文档未明确】现有 `Assets/Script/Player/Behaviour` 的 Utility AI 资产
> 与 `Assets/Script/AI` 新模块的关系未定（U-28）。实现前必须先确认
> 是替换旧模型、保留旧模型做动作层，还是并存并由场景开关选择。

### 9.4 AI 外部接口契约

> 本节是 AI 玩家与 Common、Player、Boss、Items、World、Endgame 之间的唯一接口契约。
> AI 只读取输入 Provider，只通过 `ActionIntent` 和稳定事件发布结果。

#### 9.4.1 输入 Provider

| 接口 | 最小职责 | 权威系统 |
| --- | --- | --- |
| `IPlayerViewSource` | 提供与 HUD 对齐的对手公开信息 | UI / Info |
| `IBossStateProvider` | 提供 Boss 进度、苏醒、强化层、血量、结盟对象与版本 | Boss |
| `IWorldStateProvider` | 提供游戏时间、海平面、终局剩余时间 | World / Environment |
| `IHeartStateProvider` | 提供心状态、位置、持有者与拾取进度 | Endgame |
| `IAllianceService` | 提供结盟申请、响应、状态、版本与事件 | Common / Alliance |
| `ICombatPowerProvider` | 提供生命、DPS、防御、已放塔攻击与总战力 | Common / Power |
| `ITowerProvider` | 提供塔材料、可放置点、配额与结盟锁定状态 | Tower |
| `IRelicStateProvider` | 提供是否持圣物、类型、`relicHate` 与版本 | Items |
| `IHealthStateProvider` | 提供存活、当前生命、最大生命与允许回血 | Common / Health |
| `IHealthService` | 接收回血模式请求，不暴露生命修改 | Common / Health |
| `IPlayerLifecycle` | 提供回魂资格、申请、响应与永久淘汰事件 | Player |
| `ISkillSlotController` | 提供自身技能槽状态与施放入口（本期后补） | Skills |
| `ITaskInteractor` | 提供任务物品、宝箱与地面拾取交互（边界未明） | Quest / Item |
| `IEventBus` | 提供发布和订阅能力 | Common / EventBus |
| `IGameClock` | 提供游戏时间和遭遇时间 | Common / Clock |
| `IRandomSource` | 提供 encounter seed 驱动的确定性随机 | Common / Random |

#### 9.4.2 输入事件

`PlayerAttackedEvent`

```text
eventId
encounterId
victimEntityId
attackerEntityId
damageAmount
damageSourceId
attackGameTime
```

- 发布者：Player / Common 伤害系统。
- AI 只消费与自身相关或公开可见的攻击事件。

`AllianceRequestedEvent` / `AllianceStateChangedEvent` / `AllianceEndedEvent` / `AllianceRevengeAppliedEvent`

```text
eventId
encounterId
allianceId
allianceType
participantIds
state
allianceVersion
startGameTime
endGameTime
reason
revengeHateVersion
```

- 发布者：Common / AllianceService。
- AI 消费申请、开始、结束、翻脸与惩罚仇恨；不重新定义结盟业务规则。

`DemonHeartDroppedEvent` / `DemonHeartPickedUpEvent` / `DemonHeartPickupInterruptedEvent` / `DemonHeartDeliveredEvent`

```text
eventId
encounterId
heartInstanceId
holderEntityId
position
progress
endgameVersion
```

- 发布者：Endgame / Item。
- AI 只消费公开心状态与位置，不处理心状态机。

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
- AI 收到后按死亡强制刷新规则刷新活动快照。

`BossDiedEvent`

```text
eventId
encounterId
bossEntityId
deathGameTime
deathPosition
killerEntityId
finalPhase
enrageLayer
allianceVersion
snapshotVersion
```

- 发布者：Boss。
- AI 收到后结束涉及 Boss 的结盟视图、刷新全部活动快照并进入终局心争夺。

#### 9.4.3 输出动作

```text
ActionIntent:
    movementTarget
    attackTarget
    channelAction            // 开箱 / 采集 / 拾取心 / 其他读条
    towerAction              // hold / place
    allianceAction          // apply / accept / reject / none
    priority
    reasonCode
```

- AI 不输出伤害、治疗、掉落或状态效果结果，只输出动作意图。
- 同一次决策最多输出一个主动意图；高优先级事件可以打断当前读条或移动意图。
- 技能施放字段本期缺失；接入前必须先解决 U-06，不得自行扩展字段语义。

#### 9.4.4 Fake 与测试

- 所有 Provider 必须可 Fake。
- 固定 seed 下决策输出、事件顺序和快照版本必须一致。
- AI 单元测试不得依赖完整场景或正式 Endgame。
- 跨系统集成测试只验证事件、ID 与版本契约，不验证其他系统的内部实现。
- 模块禁止使用 `FindObjectOfType`、全局单例或 `UnityEngine.Random`。
- AI 决策测试必须断言"只消费 `PlayerView`"，不得直接访问对手私有状态。

## 十、核心状态与数据契约

### 10.1 决策分层与优先级

```text
生存中断（最高）
→ 事件响应
→ Layer 3 终局战略
→ Layer 2 状态记忆 / 路线
→ Layer 1 资源管理
→ 默认（移动、巡逻、补刀）
```

规则：

- 高层命中即返回，不回溯到低层。
- 生存中断在每个决策周期最先判断，覆盖所有层。
- 事件响应可以打断当前行为，但生存中断优先于事件响应。
- 终局进入后不再退回；终局行为内部使用收益评分，不使用固定 if/else。

### 10.2 SelfState（自身状态）

```text
SelfState:
    entityId
    hp, hpMax
    power
    towerStock, towerPlaced
    hasRelic
    relicType
    inEndgame
    isUnderdog
    currentAlly                 // None / Player / Boss
    allianceVersion
    allianceTimer
    allianceCooldown
    playerAllianceAttemptAvailable   // ⚠️ 现有文档缺字段，见 U-17
    heartProgress
```

### 10.3 PlayerView（对手快照）

```text
PlayerView:
    hpRatio                    // 分档：低 <0.3 / 中 / 高 >0.7
    powerRatio                 // 对手战力 / 自身战力
    bossProgress
    killDeltaSinceRefresh
    towerStockVisible          // 分档：无 / 少 1-2 / 多 >=3
    hasRelic
    allianceState              // None / 与玩家结盟 / 与 Boss 结盟
```

规则：

- AI 决策代码只允许读取 `PlayerView`，禁止直接访问对手 `SelfState`、路线、意图、背包明细。
- `PlayerView` 的刷新节奏必须与 UI 一致；测试时断言字段集合与 UI 一致。
- `powerRatio` 的统一换算：`gap = (1 - powerRatio) / (1 + powerRatio)`。

> ⚠️【文档未明确】《AI 机制开发设计文档》8.2 与《AI 决策落地执行规格》2.2
> 的字段集合和命名不一致，见 U-09。实现以本节字段为准，但需先确认
> `selfHp/oppHp` 与 `hpRatio` 的取舍。

### 10.4 BossState 与 WorldState

```text
BossState:
    progress
    awaken
    currentEnrageLayer
    hpRatio
    allianceWith               // None / Player0 / Player1
    allianceVersion

WorldState:
    gameTime
    seaLevel
    heartState                 // None / 已掉落可争夺 / 被拾取中 / 已交付
    heartPosition
    timeToEndgame
```

### 10.5 MemoryState（短期记忆）

```text
MemoryState:
    recentlyBetrayedTimer
    recentlyHitByBossTimer
    killStreak
    noKillDuration
    lastAllyEndTime
    progressSamples[]
    powerGap
    trustScore                 // 0-100；⚠️与主文档 [0,1] 冲突，见 U-10
    inferredLabel              // 克制 / 激进 / 压制 / 合作信号 / 未知
    aggressionScore
    currentRoute               // RESTRAIN / AGGRESSIVE / PRESS / CATCH_UP
    routeHoldUntil
```

### 10.6 AiConfig

```text
AiConfig:
    decisionInterval            // 0.2s
    samplePeriod                // 10s
    snapshotPhaseOffset         // A 逢 4 / B 逢 9；默认关
    survivalHp                  // 0.25
    allianceHpThreshold         // 0.40
    trustAccept                 // 60
    trustRevive                 // 60
    bossAllyProb                // 0.50
    soloHpMin                   // 0.30
    soloOpponentNearPenalty     // 1.5
    rateLow / rateHigh          // 1.5 / 3.5 %/s
    routeHigh / routeLow        // 0.6 / 0.4
    hysteresis                  // 0.08
    gapLead                     // 0.25；⚠️语义重叠，见 U-19
    aggressionWeights           // w1-w7
    endgameWeights              // w_gain / w_risk / w_progress / w_bossThreat
    trustStealThreshold         // 3
    trustHit / trustCoop / trustDecay / trustInit   // 30 / 15 / 0.05 / 50
    towerPlacementPolicy        // 消费塔系统结果，不写死数量
    killWeightConfig            // 由游戏规则提供，AI 不保存
```

规则：

- 全部参数外置为 ScriptableObject 或 JSON，修改无需改代码。
- 每个参数必须有默认值、合法区间与越界告警。
- 日志记录本次决策使用的关键参数值。

### 10.7 ActionIntent 与 Action 枚举

```text
Action:
    Move
    Attack
    Flee
    Heal
    UseItem
    UseSkill
    OpenChest
    Collect
    PickupHeart
    DeliverHeart
    PickupRelic
    HoldTower
    PlaceTower
    ApplyAlliance
    AcceptAlliance
    RejectAlliance
    Betray
    Harass
    Cripple
    KillOpponent
    RetreatToSafeZone
    ApproachBoss
    Idle
```

规则：

- `ActionIntent.reasonCode` 必须稳定、可枚举、可断言。
- `Idle` 只允许作为无合法目标时的显式兜底，且必须写日志。
- 不得用 `Action` 枚举扩展未定义的玩家规则。
- `UseSkill`、`Harass / Cripple / KillOpponent`、`PickupRelic`、`DeliverHeart`、`RetreatToSafeZone` 的具体规则见第三部分补全设计；其中 `Harass / Cripple / KillOpponent` 与 `UseSkill` 在策划确认前保持可关闭。

### 10.8 决策主循环

```text
Decide(self, view, boss, world, memory, config, seed) → Action
```

- 每 0.2 秒调用一次；事件通道独立于周期。
- 内部按 Layer 3 → Layer 2 → Layer 1 → 默认 依次判断，命中即返回。
- 所有随机由 `seed` 派生且调用顺序固定。
- 同种子、同输入必须同输出。

事件级：

```text
1. view = PlayerViewBuilder.Build()
2. hpRatio < survivalHp → Heal / Flee / ApplyAlliance（最高优先级）
3. eventQueue 非空 → 立即响应
4. 否则取当前 route 对应的 ActionIntent
```

战略级（只在快照点）：

```text
if gameTime % samplePeriod == 当前 AI 相位:
    1. 更新 SAFE / WATCH / URGENT
    2. 更新对手评估与 trustScore
    3. routeHoldUntil 已结束 → 重新计算路线（带滞回）
    4. 记录 routeChanged 日志
```

### 10.9 趋势与路线

趋势：

```text
progressRate = (samples[now] - samples[now-2]) / (2 * T_sample)
progressRate < cfg_rateLow   → SAFE
progressRate < cfg_rateHigh  → WATCH
else                         → URGENT
```

弱势：

```text
isUnderdog = hpRatio < 0.35
           OR powerGap < -cfg_gapLead
           OR (NOT self.hasRelic AND view.hasRelic)
```

激进评分：

```text
aggressionScore = clamp01(
    0.5
  + w_killDelta * view.killDeltaSinceRefresh
  + w_leadPower * powerGap
  + w_window * (1 - timeSinceRefreshRatio)
  + w_trust * (50 - trustScore) / 50
  - w_rate * progressRate / cfg_rateHigh
  - w_survive * (1 - hpRatio)
  - w_allianceReady * allianceReadyBoost
)
```

路线：

```text
powerGap > +cfg_gapLead → PRESS
powerGap < -cfg_gapLead → CATCH_UP
else aggressionScore > routeHigh → AGGRESSIVE
else                             → RESTRAIN
```

- 路线锁定至少一个快照周期；越界切换或超过滞回宽度才允许换向。
- `URGENT` 时强制克制，覆盖 `AGGRESSIVE`。
- 路线模板只改变资源倾向与目标选择，不覆盖事件响应、生存和终局。

### 10.10 trustScore

```text
IF view.killDeltaSinceRefresh > cfg_stealThreshold: trustScore -= cfg_trustHit
IF 收到对手结盟申请且我方未违约:                    trustScore += cfg_trustCoop
IF 对手翻脸（中途主动攻击）:                        trustScore = 0
trustScore *= (1 - cfg_trustDecay)
trustScore = clamp(trustScore, 0, 100)

inferredLabel:
    <20 激进 / <40 压制 / <60 未知 / <80 合作信号 / else 克制
```

> ⚠️【文档未明确】《AI 机制开发设计文档》8.4 使用 `[0,1]` 与
> `-0.3/+0.2/-0.5` 系数，与本节阈值 60 冲突，见 U-10。

### 10.11 终局评分

```text
EndgameScore(action) = w_gain * 收益期望
                     + w_risk * 安全度
                     + w_progress * 推进度贡献
                     - w_bossThreat * Boss威胁
```

候选行为：抢夺心、击杀圣物持有者、solo Boss、玩家间结盟、弱者 + Boss 结盟、风筝 / 拖延、放置囤塔、逼近 Boss。

solo 意愿：

```text
soloWillingness = sigmoid(
      (hpRatio - cfg_soloHpMin) * 3
    + (1 - boss.hpRatio) * 2
    - cfg_soloOpponentNearPenalty
)
IF hasRelic AND boss.awaken AND soloWillingness > 0.6 AND NOT isHunted → solo
```

### 10.12 结盟与回魂决策

接受结盟：

```text
acceptAlliance = isUnderdog
              OR trustScore > cfg_trustAccept
              OR hpRatio < cfg_allianceHpThreshold
```

弱者 + Boss 结盟申请（终局）：

```text
isUnderdog AND boss.allianceWith != self
AND deterministicRand(seed, gameTime) < cfg_bossAllyProb
→ ApplyAlliance(Boss)
```

回魂：

```text
死亡方：满足资格 → 自动发起回魂申请
存活方：任一方无已装备防具宝石 → 拒绝
        双方都有且 trustScore >= cfg_trustRevive → 接受
        否则拒绝
```

> ⚠️【文档未明确】弱者 + Boss 结盟申请被拒后是否进入冷却、AI 如何得知
> 申请机会与冷却、结盟期行为约束，见 U-16、U-17、U-20。

## 十一、裁决规则

### 11.1 统一优先级

| 优先级 | 事件 | 处理原则 |
| --- | --- | --- |
| 1 | EncounterEnded | AI 停止全部主动行为 |
| 2 | AI 永久淘汰 / Boss 死亡 | 结束相关结盟视图，强制刷新活动快照 |
| 3 | 生存阈值跨线 | 治疗 / 逃跑 / 申请结盟，覆盖所有层 |
| 4 | 心被对方拾取中 | 立即打断 / 抢心 |
| 5 | 被攻击 / 结盟申请 / Boss 结盟发起 | 事件响应，可打断当前行为 |
| 6 | 结盟状态变化 / 惩罚仇恨生效 | 立即更新 `allianceVersion`，刷新结盟相关决策 |
| 7 | 终局行为评分 | 最高层战略决策 |
| 8 | 路线切换 | 只在战略快照点且满足滞回 |
| 9 | 资源管理 | 克制 / 偷杀、开箱、囤塔 / 放塔 |
| 10 | 默认行为 | 移动、巡逻、补刀 |

规则：

- 高优先级事件处理完成后，低优先级行为才可使用同一帧。
- 低优先级行为不能覆盖高优先级结果。
- 死亡事件必须先完成结盟视图修改与快照刷新，同帧后续决策只读取刷新后的结果。
- 同一帧多个同优先级事件使用稳定顺序：实体 ID → 距离 → 事件序号。
- 所有裁决结果必须写入日志，包含事件、目标、实际参数和裁决原因。

### 11.2 快照与事件

| 事件 | 是否立即生效 | 处理 |
| --- | --- | --- |
| 玩家永久淘汰 | 是 | 结束涉及死亡方结盟 → 递增 allianceVersion → 强制刷新活动快照 |
| Boss 死亡 | 是 | 结束涉及 Boss 结盟 → 递增 allianceVersion → 强制刷新 → 进入终局 |
| 结盟开始 / 结束 / 翻脸 | 是 | 立即更新 allianceState / allianceTargetId，递增 allianceVersion |
| 惩罚仇恨生效 | 是 | T+3 刷新目标相关决策输入 |
| 生存阈值跨线 | 是 | 立即中断当前行为，进入生存决策 |
| 心状态变化 | 是 | 立即进入终局争夺响应 |
| 普通伤害变化 | 否 | 等下一快照 |
| 常规路线切换 | 否 | 等战略快照点 |

规则：

- 强制刷新只执行一次，原因写入日志；不得在同一死亡事件中重复刷新。
- 历史快照只追加，不回写。
- 快照版本与 `allianceVersion` 不一致时，结盟相关判断以实时版本为准。

### 11.3 时序与动作锁

| 锁 | 持有者 | 期间禁止 |
| --- | --- | --- |
| 读条锁 | 当前开箱 / 采集 / 拾取心 | 新读条、可选移动、攻击 |
| 攻击锁 | 当前普攻 | 新普攻，不阻止读条取消 |
| 移动锁 | 当前寻路 | 其他移动意图覆盖 |
| 结盟状态锁 | 结盟系统 | 盟友攻击、盟友过滤穿透 |
| 淘汰锁 | 永久出局 | 所有主动行为 |

规则：

- 同一时间只能存在一个主动意图执行。
- 读条被打断时按对应通用规则处理，AI 不得自行定义归零或回退数值。
- 生存中断可以取消非心拾取读条；心拾取读条按 Endgame 规则处理。
- 死亡立即释放全部动作锁并停止 Tick。

### 11.4 门槛窗口

```text
下限包含，上限不包含
[下限, 上限)
时间阈值使用 >=
```

主要门槛：

| 门槛 | 规则 | 配置字段 |
| --- | --- | --- |
| 生存中断 | 生命比例 < 0.25 | `survivalHp` |
| 弱势血量 | 生命比例 < 0.35 | `underdogHpRatio` |
| 弱势战力差 | `powerGap < -0.25` | `gapLead` |
| 弱势圣物 | 自身无圣物且对手持圣物 | 无 |
| 趋势安全 / 警戒 / 紧急 | 1.5 / 3.5 %/s | `rateLow`、`rateHigh` |
| 激进 / 克制 | 0.6 / 0.4，中间滞回 | `routeHigh`、`routeLow` |
| 路线锁定 | >= 1 个快照周期 | `routeHoldUntil` |
| 囤塔解锁 | 非结盟且（终局或结盟结束） | `currentAlly`、`inEndgame` |
| 结盟接受 | isUnderdog / trust > 60 / hp < 0.40 | `trustAccept`、`allianceHpThreshold` |
| 回魂接受 | trust >= 60 且双方有防具宝石 | `trustRevive` |
| 终局进入 | timeToEndgame <= 0 或 seaLevel >= 峰顶或 boss.awaken | `inEndgame` |
| 心拾取 | 10 秒读条；受击回退；移动重置 | Endgame 提供，回退值未定 |

规则：

- 门槛只在跨过边界时触发一次。
- 阈值两侧必须提供滞回值，避免同一帧反复触发。
- 实际值统一从 `AiConfig` 或 `MemoryState` 读取，不能在代码内写死。

### 11.5 数值暂定值

| 项目 | 暂定值 | 状态 |
| --- | --- | --- |
| AI 决策间隔 | 0.2s | 以规格为准；主文档 0.5s 待统一 |
| 局势快照 | 10s | 暂定 |
| 相位错峰 | A 逢 4 / B 逢 9 | 默认关闭 |
| 共享进度刷新 | 20-40s | 暂定 |
| 生存阈值 | 0.25 | 暂定 |
| 结盟血量阈值 | 0.40 | 暂定 |
| trust 初始 / 接受 / 回魂 | 50 / 60 / 60 | 暂定 |
| Boss 结盟响应概率 | 0.50 | 暂定 |
| 圣物 solo 最低血量 | 0.30 | 暂定 |
| 圣物 solo 邻近惩罚 | 1.5 | 暂定 |
| 趋势阈值 | 1.5 / 3.5 %/s | 暂定 |
| 路线阈值 | 0.6 / 0.4 | 暂定 |
| 滞回 | 0.08 | 暂定 |
| 战力差阈值 | 0.25 | 暂定；语义重叠待拆 |
| trust 证据阈值 | 3 击杀 / 周期 | 暂定 |
| trust 扣分 / 加分 / 衰减 | 30 / 15 / 0.05 | 暂定 |
| 玩家间结盟时长 | 默认 45s；AI 候选 35s | 见 U-35 / 15.2 |
| 与 Boss 结盟时长 | 默认 20s；AI 候选 17s | 见 U-35 / 15.2 |
| 结盟确认 / 冷却 | 3s / 30s | 暂定 |
| 结盟惩罚仇恨 | 3s 缓冲后 +5000，30s 归零 | 暂定 |
| 回魂窗口 / 确认 / 复活 | 10s / 5s / 15-20s | 属玩家机制 |
| 回魂生命 | 50% | 属玩家机制 |
| 心拾取读条 | 10s | 属 Endgame |
| 塔配额 | 4 座 / 保底 2 座 | 冲突，见 U-12 |
| 种子组 | 30-50 局 | 暂定 |
| 双输率门禁 | <=25% | 暂定 |
| M6 易手 / 打断 | >10% | 暂定 |
| 击杀者交付率 | <70% | 暂定 |
| 有效局率 | >=90% | 暂定 |
| 卡死 / 寻路失败率 | <=2% | 暂定 |

### 11.6 数值配置检查

开工前必须完成：

- 所有决策阈值、权重、周期都进入 `AiConfig`。
- 没有配置时使用默认值，并输出警告。
- 配置值非法时禁止运行 AI 默认逻辑。
- 日志记录本次决策使用的实际值。
- 修改配置后无需修改脚本。
- 第七节未明确项对应的参数必须可关闭或可回退，不能阻塞基线决策。

## 十二、开发任务

执行顺序按 M0 → M1 → M2 → M3 → M4 → M5。同一阶段内按表格从上到下执行，编号只用于追踪。

### 12.1 M0：AI 契约与核心初始化

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| AI-CORE-01 | 定义 `AiContext` 与外部依赖引用 | 无 | 接口可注入，Fake 可实现；不用单例或服务定位器 |
| AI-CORE-02 | 定义 `AiDefinition` 与 `AiConfig` | 无 | 参数可配置，运行时不修改 SO；缺配置有默认值与告警 |
| AI-CORE-03 | 定义 `SelfState / PlayerView / BossState / WorldState / MemoryState / ActionIntent` 数据契约 | 无 | 字段命名冻结；`PlayerView` 只含公开信息 |
| AI-CORE-04 | 实现 `AiController` 初始化与 Tick | AI-CORE-01、AI-CORE-02 | 0.2s 决策调度可确定复现；同一意图不重复执行 |
| AI-CORE-05 | 实现 `AiEventBridge` 事件入口 | AI-CORE-01、EventBus（外部） | 订阅/退订稳定，事件按统一顺序交给 AiController |
| AI-CORE-06 | 接入确定性与 encounter seed | EncounterRandom（外部） | 同 seed 同输入同输出；禁止全局随机源 |
| AI-CORE-07 | 实现结构化决策日志与策略探针骨架 | AI-CORE-04 | 每条决策含 time / seed / ai / situation / route / trust / action / reasonCode |

外部前置，不属于 AI 开发：

```text
EventBus / SnapshotService
GameClock / EncounterRandom
IPlayerViewSource
```

### 12.2 M1：感知、快照与记忆

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| AI-PERCEPT-01 | 实现 `PlayerViewBuilder` | IPlayerViewSource | 字段集合与 UI 一致；决策代码只读 PlayerView |
| AI-PERCEPT-02 | 实现 10 秒局势快照与相位错峰 | SnapshotService、AI-CORE-02 | 相位用游戏时间取模；默认关闭；暂停和加速下一致 |
| AI-PERCEPT-03 | 实现共享进度离散刷新与信息延迟 | AI-PERCEPT-01、AI-CORE-06 | 20-40s 刷新、小幅误差可复现 |
| AI-PERCEPT-04 | 实现死亡与关系强制刷新 | EventBus、AI-PERCEPT-02 | 死亡先结算结盟并递增 allianceVersion，再同帧刷新 |
| AI-MEM-01 | 实现 `AiMemory` 短期标记与计时器 | AI-CORE-03 | 标记与文档 5.1 一一对应，可按时间衰减 |
| AI-MEM-02 | 实现 `trustScore` 更新 | AI-MEM-01 | 只消费公开证据；clamp 与衰减正确；刻度口径先解决 U-10 |
| AI-MEM-03 | 实现趋势采样与 SAFE / WATCH / URGENT | AI-MEM-01 | 20s 窗口平均增速；URGENT 能强制克制 |

外部前置，不属于 AI 开发：

```text
SnapshotService / EntityMemory
UI 公开信息字段
Boss 公开状态 Provider
```

### 12.3 M2：决策主循环、路线与终局

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| AI-DECIDE-01 | 实现 `Decide` 纯函数与三层优先级 | AI-CORE-03、AI-MEM-01 | 命中即返回；同 seed 同输入同输出；不访问场景对象 |
| AI-DECIDE-02 | 实现生存中断 | AI-DECIDE-01 | hp < 0.25 时治疗 / 逃跑 / 结盟，覆盖所有层 |
| AI-DECIDE-03 | 实现事件响应通道 | AI-DECIDE-01、AiEventBridge | 被攻击 / 结盟申请 / 心出现 / Boss 结盟发起立即响应 |
| AI-ROUTE-01 | 实现 isUnderdog 与 powerGap | AI-PERCEPT-01、AI-MEM-01 | 三项判定正确；不读对手内部战力 |
| AI-ROUTE-02 | 实现 `aggressionScore` | AI-ROUTE-01、AI-MEM-03 | 权重来自配置；URGENT 能压过激进 |
| AI-ROUTE-03 | 实现四路线与滞回 | AI-ROUTE-02 | 只在快照点切换；锁定 >= 1 周期；无抖动 |
| AI-ROUTE-04 | 实现路线模板行为约束 | AI-ROUTE-03 | PRESS / CATCH_UP / RESTRAIN / AGGRESSIVE 只改资源倾向 |
| AI-END-01 | 实现终局进入判定 | WorldState、BossState | 三条件任一满足即进入，进入后不退回 |
| AI-END-02 | 实现终局收益评分 | AI-END-01 | 候选行为评分可配置；心被抢时立即响应 |
| AI-END-03 | 实现圣物 solo 决策 | AI-END-02、IRelicStateProvider | 持圣物且意愿 > 0.6 且未被追杀时 solo |

外部前置，不属于 AI 开发：

```text
IHeartStateProvider
IWorldStateProvider
IBossStateProvider
```

### 12.4 M3：资源管理

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| AI-RES-01 | 实现克制 / 偷杀 killStrategy | AI-ROUTE-03、AI-MEM-03 | URGENT 强制克制；StrategyOverride 可覆盖且生存不受影响 |
| AI-RES-02 | 实现选择性杀怪判定 | AI-RES-01 | mustKill 四条件正确；开局 5% 进度可发育 |
| AI-RES-03 | 实现囤塔 / 放塔 | AI-RES-01、ITowerProvider | 发育期囤塔；结盟期锁定；解锁、落点、排他与配额由 ITowerProvider 判定；终局安全窗口批量放置 |
| AI-RES-04 | 实现开箱 vs 狩猎目标选择 | AI-RES-01、ITaskInteractor | 克制时有替代收益；不原地发呆 |
| AI-RES-05 | 实现心争夺与打断意图 | AI-END-02、IHeartStateProvider | 心掉落可争夺；对方拾取中可打断（手段待 U-14） |
| AI-RES-06 | 实现基础采集 / 地面拾取（待边界确认） | ITaskInteractor | 仅登记未明确项，不定义 AI 采集规则；保持可关闭 |
| AI-RES-07 | 实现对手压制 / 打残 / 击杀三档行为（待 U-31） | AI-RES-01 | 规格未明确前只保留接口占位，不实现默认 PvP 决策 |

外部前置，不属于 AI 开发：

```text
ITowerProvider
ITaskInteractor
Endgame 心状态机
```

### 12.5 M4：结盟、翻脸与回魂

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| AI-ALLY-01 | 实现玩家间结盟申请 / 接受 / 拒绝 | IAllianceService、AI-DECIDE-02 | 接受条件参数化；申请机会字段缺口先解决 U-17 |
| AI-ALLY-02 | 实现弱者 + Boss 结盟申请 | IAllianceService、IBossStateProvider | 只走申请路径；不模拟 Boss 主动概率 |
| AI-ALLY-03 | 实现 Boss 结盟事件响应 | AI-ALLY-02、AiEventBridge | 识别被追杀方并切换风筝 / 拖延 / 削弱 Boss |
| AI-ALLY-04 | 实现翻脸与报复响应 | AI-ALLY-01、AI-MEM-01 | 区分中途翻脸与到期后翻脸；重置 recentlyBetrayedTimer |
| AI-ALLY-05 | 实现结盟期行为约束（待 U-16） | AI-ALLY-01、AI-RES-03 | 保持可关闭；不违反盟友过滤与塔锁定 |
| AI-REVIVE-01 | 实现死亡方自动回魂申请 | IPlayerLifecycle | 满足资格即申请，无额外概率；死亡瞬间 Boss 苏醒 +60s 由 IPlayerLifecycle 统一执行，AI 不另写分支 |
| AI-REVIVE-02 | 实现存活方回魂响应 | AI-REVIVE-01、AI-MEM-02 | 缺防具宝石直接拒绝；trust >= 60 接受 |

外部前置，不属于 AI 开发：

```text
AllianceService / 结盟事件
Player 回魂执行与淘汰事件
```

### 12.6 M5：执行适配、集成与调试

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| AI-EXEC-01 | 实现 `AiActionExecutor` 意图适配 | IAiActionExecutor（外部） | 移动 / 攻击 / 读条 / 塔 / 结盟意图可执行；AI 不直接改状态 |
| AI-EXEC-02 | 接入 AI 技能施放（待 U-06） | ISkillSlotController（外部） | 规格补齐前只登记接口，不实现默认决策 |
| AI-DEBUG-01 | 实现 StrategyOverride | AI-RES-01、AI-ROUTE-03 | CC / DD / DC / CD 整局锁定；生存不受覆盖 |
| AI-DEBUG-02 | 实现 AS-1~AS-6 场景注入 | AI-DEBUG-01 | 只偏移初始状态，不改 AI 逻辑；独立种子组 |
| AI-DEBUG-03 | 实现调试面板与日志回放 | AI-DECIDE-01、AI-CORE-07 | 可查看 route / trust / trend / action / event；固定 seed 可回放 |
| AI-TEST-01 | AI 稳定性前置 | AI-EXEC-01、AI-DEBUG-03 | 有效局率 >=90%；卡死 / 寻路失败 <=2%；无非法状态迁移 |
| AI-INTEGRATION-01 | 单方 AI 纵向闭环 | AI-EXEC-01、AI-ALLY-05、AI-REVIVE-02、AI-RES-05 | 固定 seed 可重复跑完"发育 → 结盟 → 翻脸 → 心争夺 → 结算" |

## 十三、系统不变量

1. AI 决策只能读取自身状态与 `PlayerView` 公开信息，不得读取对手内部状态。
2. `Decide` 必须是纯函数；同种子、同输入必须同输出。
3. 所有随机必须来自 encounter seed 派生，不得使用全局随机源。
4. 生存中断优先级高于所有 Layer，事件响应高于常规决策。
5. 终局进入后不再退回；终局行为使用评分而非固定 if/else。
6. 路线只在战略快照点切换，且至少锁定一个快照周期。
7. `URGENT` 进度风险必须能强制克制，覆盖激进路线。
8. 克制不等于不行动，必须有开箱 / 塔材料 / 回血等替代收益。
9. 结盟期间未放置塔锁定不可放置；结盟结束后解锁。
10. AI 只发结盟意图，不重新定义结盟时长、仇恨与关系规则。
11. AI 只发心争夺意图，不处理心状态机与交付结算。
12. AI 永久淘汰或 Boss 死亡时，必须先结束涉及死亡方的结盟视图并递增 `allianceVersion`，再强制刷新活动快照。
13. 死亡导致的结盟结束不进入缓冲、不产生追杀仇恨。
14. AI 死亡与回魂规则完全复用《玩家机制设计》，不新增第二套口径。
15. 快照版本与 `allianceVersion` 不一致时，结盟相关判断以实时版本为准。
16. 所有数值变化必须有配置来源。
17. 所有状态迁移、路线切换和结盟动作必须有日志。
18. AI 不得修改生命、伤害、掉落、塔和物品状态，只能请求对应系统执行。
19. `StrategyOverride` 只覆盖 kill / route 两个输入，不覆盖生存中断。
20. Boss 的求生阈值、仇恨公式、战力压制和技能槽映射不得被 AI 复用为自身默认值。
21. AI 使用自己的 `snapshotProfile` 与输入集合；Boss 快照机制只作为架构参考。
22. 第七节未明确项必须保持可配置或可关闭，不得自行拍板。
23. 现有 Utility AI 与新 `Decide` 纯函数的归属未定前，不得同时驱动同一个角色。
24. AI 技能施放规格未补齐前，不得默认施放技能或扩展 `ActionIntent` 字段语义。
25. 塔配额、心拾取回退等未明确数值只能消费外部系统结果，AI 不得写死。
26. AI 死亡必须走与真人玩家完全相同的死亡流程：Boss 苏醒进度 +60 秒、10 秒回魂窗口和淘汰判定均由 Player / 对局系统统一执行，AI 不得走单独分支。
27. 放塔资格（解锁时间、落点、40m 排他、配额）只由塔系统判定，AI 只读可放置结果，不自行计算。
28. 对手 PvP 行为（压制 / 打残 / 击杀）在 U-31、U-33 明确前保持占位，不进入默认决策。

## 十四、完成定义

AI 玩家 Demo 完成时，以下流程必须可重复运行：

```text
接管一名玩家
→ 构建 PlayerView 并每 10 秒刷新快照
→ 按 0.2 秒决策
→ 发育 / 克制 / 偷杀 / 开箱 / 囤塔
→ 残血生存中断
→ 玩家间结盟或与 Boss 结盟
→ 翻脸与惩罚响应
→ 终局抢心与 solo
→ 淘汰 / 回魂
→ 结算
```

验收要求：

- 无非法状态迁移。
- 无永久死锁、无原地发呆。
- 同种子、同输入可复现。
- AI 只消费公开信息，测试断言通过。
- 每个阶段均有结构化日志与策略探针。
- 有效局率 >=90%，卡死 / 寻路失败 <=2%。
- 第七节未明确项全部登记并保持可配置 / 可关闭。

---

# 第三部分：补全设计（v1.1 本文新增，待策划确认）

> 本部分用于补全第七节登记、但现有文档没有给出可执行口径的条目。
> 每项结论都标注 `【本文补全建议 · 待策划确认】`，可直接指导实现，冻结前必须由策划确认。
> 与现有文档冲突时，以本部分第 15.2 节的统一口径为准；本部分不重定义 Boss、Endgame、塔与物品的内部规则。

## 十五、补全总览与统一口径

### 15.1 补全状态总览

| 编号 | 主题 | 补全位置 | 状态 |
| --- | --- | --- | --- |
| U-06 | AI 技能施放 | 17 | 【本文补全建议 · 待策划确认】 |
| U-07 | AI 经济与成长 | 21 | 【本文补全建议 · 待策划确认】 |
| U-08 | 开场与采集 | 20 | 【本文补全建议 · 待策划确认】 |
| U-09 | PlayerView 字段口径 | 15.2 | 【本文统一口径 · 待策划确认】 |
| U-10 | trustScore 刻度 | 15.2 | 【本文统一口径 · 待策划确认】 |
| U-11 | 决策频率 | 15.2 | 【本文统一口径 · 待策划确认】 |
| U-13 | 心拾取回退数值 | 23.2 | 仍待 Endgame 冻结 |
| U-14 | 心争夺打断手段 | 23.1 | 【本文补全建议 · 待策划确认】 |
| U-15 | 淘汰后行为 | 24.1 | 【本文补全建议 · 待策划确认】 |
| U-16 | 结盟期行为约束 | 16.3 | 【本文补全建议 · 待策划确认】 |
| U-17 | 结盟申请机会消耗 | 16.4 | 仍待策划冻结 |
| U-18 | 非终局结盟发起 | 16.2 | 【本文补全建议 · 待策划确认】 |
| U-19 | cfg_gapLead 语义重叠 | 15.2 | 【本文统一口径 · 待策划确认】 |
| U-20 | 与 Boss 结盟后的行为 | 16.3 | 【本文补全建议 · 待策划确认】 |
| U-21 | 圣物获取与保护 | 22.2 | 【本文补全建议 · 待策划确认】 |
| U-22 | 海平面规避 | 22.1 | 【本文补全建议 · 待策划确认】 |
| U-23 | 沙盘参数回灌 | 24.3 | 【本文补全建议 · 待策划确认】 |
| U-24 | AI seed 派生 | 24.2 | 【本文补全建议 · 待策划确认】 |
| U-25 | 机器可读日志字段 | 24.4 | 【本文补全建议 · 待策划确认】 |
| U-26 | 开场止戈 vs 结盟 | 16.4 / 20 | 【本文统一口径 · 待策划确认】 |
| U-27 | 版本时长差异 | 15.2 | 【本文统一口径 · 待策划确认】 |
| U-28 | 旧 Utility AI 归属 | 15.2 | 【本文统一口径 · 待策划确认】 |
| U-29 | 底层操作复用 | 15.2 | 【本文统一口径 · 待策划确认】 |
| U-30 | 心交付动作 | 23.3 | 【本文补全建议 · 待策划确认】 |
| U-31 | PvP 对抗 | 18 | 【本文补全建议 · 待策划确认】 |
| U-32 | 治疗与回血 | 19 | 【本文补全建议 · 待策划确认】 |
| U-33 | 攻击对手是否推 Boss | 18.4 | 默认按"不推进"，待冻结 |
| U-34 | cfg_bossAllyProb 语义 | 15.2 | 【本文统一口径 · 待策划确认】 |
| U-35 | 结盟时长口径 | 15.2 | 【本文统一口径 · 待策划确认】 |
| U-36 | 加速技能数值 | 15.2 | 【本文统一口径 · 待策划确认】 |
| U-37 | 塔解锁与放置资格 | 15.2 | 【本文统一口径 · 待策划确认】 |
| U-01~U-05 | 产品目标、形态、版本简化、难度、补位 | 15.2 | 仍待产品与策划确认 |
| U-12 | 塔配额版本冲突 | 15.2 / 22.2 | AI 只消费塔系统结果 |

### 15.2 冲突统一口径

> 【本文补全建议 · 待策划确认】以下条目用于消除现有文档之间的冲突。
> 未列入的条目仍按第七节保持待决。

| 冲突 | 统一口径 | 依据 |
| --- | --- | --- |
| U-09 `PlayerView` 字段 | 以《AI 决策落地执行规格》2.2 为准：`hpRatio / powerRatio / bossProgress / killDeltaSinceRefresh / towerStockVisible / hasRelic / allianceState`；主文档的自视角字段作为构建端映射，不进入决策函数 | 规格为可执行依据 |
| U-10 `trustScore` 刻度 | 统一 `[0,100]`，初始 50，接受 / 回魂阈值 60；主文档 `[0,1]` 系数按 ×100 换算为 -30 / +20 / -50 | 与规格阈值一致 |
| U-11 决策频率 | 统一 0.2s（5Hz）；事件走即时通道，不受决策周期限制 | 规格 1.1 |
| U-19 `cfg_gapLead` | 拆为 `cfg_underdogGap` 与 `cfg_routeGap`，初始都取 0.25，允许独立调参 | 语义分离，避免一个参数控制两件事 |
| U-27 版本时长 | AI 只读取 `WorldState.timeToEndgame`；新手版 10 分钟、首发进阶版 15 分钟、完整版 22-25 分钟分别由场景与 `AiConfig` 注入，不在 AI 代码中写死 | 版本规划 |
| U-28 旧 Utility AI | 旧 `Player/Behaviour` 保留为动作层与调试资产，`Assets/Script/AI` 的 `Decide` 为正式决策层；同一角色只允许一个决策层驱动 | 避免双 AI 并存 |
| U-29 底层操作 | AI 复用 Player 的移动、寻路、普攻、读条、拾取、放置与技能施放；AI 只发 `ActionIntent` | 游戏机制总览 3.1 |
| U-34 `cfg_bossAllyProb` | 拆为 AI 侧 `aiBossAllyApplyProb`（默认 0.5，仅表示 AI 是否发起申请）与 Boss 侧 `bossAllyAcceptProb`；AI 不模拟 Boss 的 20% 主动概率 | 结盟机制设计二.1 |
| U-35 结盟时长 | AI 侧读取 `AllianceService` 的当前时长；默认按 Boss 清单冻结值：与 Boss 20s、玩家间 45s；AI 附录的 17s / 35s 作为待实测候选 | Boss 清单十.6 |
| U-36 加速技能 | 以《技能系统设计》为准：加速 3 秒 +50% 移速、CD 20s；《游戏机制总览》的 30s 作为旧实现待迁移 | 技能系统设计二 |
| U-37 塔放置 | AI 只读取塔系统的"已解锁 / 合法落点 / 40m 排他 / 配额"结果；任务结束 +5 分钟解锁由塔系统判定 | 游戏机制总览 7.6 |

## 十六、结盟补全（U-16 ~ U-20、U-26）

### 16.1 结盟申请与响应

【本文补全建议 · 待策划确认】

```text
申请玩家间结盟：
    isUnderdog OR hpRatio < cfg_allianceHpThreshold OR trustScore < cfg_trustAccept
    → ApplyAlliance(Player)

响应玩家间结盟申请：
    acceptAlliance = isUnderdog
                  OR trustScore > cfg_trustAccept
                  OR hpRatio < cfg_allianceHpThreshold

申请与 Boss 结盟（弱者路径）：
    当前对 Boss 仇恨总分 == 0
    AND hpRatio >= allianceMinHpRatio
    AND boss.allianceWith != self
    AND deterministicRand(seed, gameTime) < aiBossAllyApplyProb
    → ApplyAlliance(Boss)
```

- 玩家间结盟申请每局一次机会；申请失败、被拒或超时的消耗口径仍以 U-17 为准。
- 与 Boss 结盟只走申请路径；Boss 是否同意由其 `BossAllianceController` 决定，AI 不读取其内部判定过程。
- 申请失败后是否进入 30 秒冷却由 `AllianceService` 返回状态决定。

### 16.2 非终局结盟发起

【本文补全建议 · 待策划确认】

| 阶段 | AI 是否主动申请 | 条件 |
| --- | --- | --- |
| Opening | 否 | 开场为强制止戈，不启用结盟系统 |
| Exploration（Boss 未苏醒） | 否 | 结盟只在 Boss 苏醒后生效 |
| Boss 苏醒后、非终局 | 是 | `isUnderdog` 且 `trustScore > cfg_trustAccept`，或自身血量低于阈值且需要合作 |
| 终局 | 是 | 按终局收益评分选择玩家间或弱者 + Boss |

- 非终局主动申请排除 `trend == URGENT` 且 `aggressionScore` 高的情形。
- 结盟冷却未结束时不得重复申请。
- 申请失败后进入 30 秒冷却（沿用结盟机制设计）。

### 16.3 结盟期行为约束

【本文补全建议 · 待策划确认】

- 玩家间结盟：共同目标为击杀 Boss；AI 继续攻击 Boss 与怪物，不攻击盟友；保留换弹 / 走位 / 囤塔。
- 与 Boss 结盟：AI 不攻击 Boss；Boss 目标过滤由 Boss 侧负责；AI 优先输出剩余玩家。
- 结盟期间不对盟友使用负面技能，攻击塔不攻击盟友；未放置塔锁定。
- 结盟剩余时间低于 `allianceWarnTime`（建议 5s）时，AI 进入"翻脸准备"：保留爆发技能、拉开与盟友距离、优先抢占心点或 Boss 尾刀位。
- 与 Boss 结盟的 AI 不主动攻击 Boss，避免缩短时长；该约束来自玩家对规则的可读性，不要求 AI 知道精确 -1s 公式。
- 结盟自然到期或翻脸时，AI 立即：
  1. 结束盟友过滤；
  2. 进入 `recentlyBetrayedTimer`；
  3. 按 `isUnderdog` 决定追击原盟友或脱离；
  4. 结盟期锁定的塔在结束后才可放置。

### 16.4 开场止戈与结盟的关系

【本文统一口径 · 待策划确认】

- 岛中岛任务结束前的"不可互攻"是**阶段状态（Opening）**，不是结盟系统。
- 结盟系统只在 Boss 苏醒后启用；Opening 阶段 AI 不申请、不接受、不表演结盟 UI。
- 任务结束进入 Exploration 后，PvP 解锁，但结盟仍需等 Boss 苏醒（或按版本开关跳过）。
- 现有文档中"任务结束前处于结盟状态"的措辞统一改为"开场止戈"。

## 十七、AI 技能施放补全（U-06、U-36）

### 17.1 槽位与职责

【本文补全建议 · 待策划确认】

| AI 槽位 | 玩家槽位 | 说明 |
| --- | --- | --- |
| `slotId=1` | 天赋槽（Q） | 角色天赋技能，不可更换 |
| `slotId=2` | 学习槽（E） | 技能书技能；本期由预设注入，不做局内学习 |

职责拆分沿用 Common：

```text
SkillCard          定义效果、目标、施法、范围与表现
SkillSlotBinding   定义 CD、冷却开始时机、触发策略、优先级
SkillSlotRuntime   保存单槽冷却与就绪状态
SkillCastController 管理全局施放、动作锁与打断
AI 决策层           只输出 SkillRequest(slotId, stateReason)
```

- AI 不读取技能效果内部状态，不保存分身、增益或持续时间。
- AI 不轮询冷却；`Disabled` 不请求，`Cooldown` 不每帧请求，转为 `Ready` 后请求一次。
- 每 0.2 秒决策最多输出一个技能请求。

### 17.2 技能请求优先级

【本文补全建议 · 待策划确认】

```text
1. 生存：治疗 / 护盾 / 解控类技能，hpRatio < cfg_survivalHp
2. 逃生：加速 / 位移类技能，处于深水、被怪追击或需要脱离 PvP
3. 打断：控制 / 减速类技能，对手正在拾取心 / 开箱 / 交付
4. 战斗：伤害 / 增益类技能，存在合法目标（Boss / 对手 / 怪物）
5. 否则不请求
```

| 参数 | 建议值 | 说明 |
| --- | --- | --- |
| `skillRequestHpHigh` | 0.50 | 血量低于此值时优先保命技能 |
| `skillCombatMinTargetHpRatio` | 0.20 | 目标血量过低时不再浪费高价值技能 |
| `skillWasteGuard` | 0.30 | 目标预计承伤低于技能收益的该比例时跳过 |
| `skillRequestCooldown` | 0.5s | 同一槽位两次请求的最小间隔 |

### 17.3 战斗技能目标选择

【本文补全建议 · 待策划确认】

- 目标优先级：当前 PvP 目标 > Boss > 高价值怪物 > 普通怪物。
- 范围技能：优先选择能同时覆盖当前攻击目标与至少 1 个额外敌对目标的落点。
- 单体技能：优先残血目标，但受 `skillCombatMinTargetHpRatio` 保护，避免对将死目标浪费。
- 阈值、技能分类与触发条件全部外置；技能名不进入 AI 代码。
- 技能数值冲突按 15.2 的 U-36 口径处理。

## 十八、PvP 对抗补全（U-31、U-33）

### 18.1 三档 PvP 行为

【本文补全建议 · 待策划确认】

| 行为 | 触发 | 目标 | 退出 |
| --- | --- | --- | --- |
| `Harass` 骚扰 | 对手在资源点 / 开箱 / 拾取心，且自身无更高优先行为 | 打断对手节奏，不追求击杀 | 超出 `pvpHarassLeash` 或对手脱离 3s |
| `Cripple` 打残 | 对手 `hpRatio < 0.50` 且 `powerGap > cfg_routeGap` | 压制到 `pvpCrippleStopHp` 后停手 | 对手死亡、盟友化、超出 `pvpCrippleLeash` 或自身 `hpRatio < survivalHp` |
| `KillOpponent` 击杀 | 见 18.3 | 使对手永久出局或掉落心 | 完成、目标消失或风险超阈值 |

```text
pvpHarassLeash      = 12m   // 建议
pvpCrippleLeash     = 20m   // 建议
pvpCrippleStopHp    = 0.30  // 建议
pvpKillLeash        = 20m   // 建议
```

- PvP 目标必须是非盟友、存活、可见且在 leash 内；盟友过滤以实时 `allianceVersion` 为准。
- 受害者是当前被 Boss 追击的目标时，Harass 价值提高；但不得让 Boss 目标脱离自身视野。
- 对手开箱 / 拾取心时，Harass 可以直接打断读条。

### 18.2 打残不打死

【本文补全建议 · 待策划确认】

- `Cripple` 到达 `pvpCrippleStopHp` 后立即停手并回归资源路线。
- 只有 18.3 的击杀条件成立时才允许越过停手线。
- 停手后不追击、不守尸；若对手回血，可在下一快照点重新评估。
- 该规则直接对应《核心机制设计》"打残但不打死"的设计意图。

### 18.3 击杀条件

【本文补全建议 · 待策划确认】

```text
KillOpponent 允许当：
    (对手持有恶魔之心)
    OR (处于终局决战圈且对手是交付竞争者)
    OR (对手 hpRatio < 0.15 且预计 2s 内可击杀)
    OR (对手正在击杀我方且我方无法脱离)
```

### 18.4 击杀收益与 Boss 代价

【本文补全建议 · 待策划确认】

```text
pvpKillScore = 预期收益（对手出局 / 夺心）
             - bossAwakenPenalty（击杀固定推进 Boss 苏醒 60s）
             - 反杀风险
             - 装备掉落与仇恨升级风险
```

- 击杀对手固定推进 Boss 苏醒进度 60 秒，是明确规则，AI 必须计入。
- 攻击对手（未击杀）是否推进 Boss 苏醒仍以 U-33 为准；**默认按不推进处理**，只统计玩家击杀事件。
- U-33 冻结后，只需调整 `pvpDamageProgressWeight`，不修改行为结构。

## 十九、治疗与回血补全（U-32）

【本文补全建议 · 待策划确认】

治疗优先级：

```text
1. 自身治疗 / 护盾类技能 Ready
2. 背包药水可用且 hpRatio < cfg_potionThreshold
3. 前往最近可用回血点（阿蛮 / 营地），要求可达且路径不在深水
4. 无处可回时：申请结盟或继续逃跑
```

| 参数 | 建议值 | 说明 |
| --- | --- | --- |
| `cfg_potionThreshold` | 0.50 | 与现有 `Combat_potionThreshold` 兼容 |
| `healPointSearchRadius` | 30m | 回血点搜索半径 |
| `healPointMinHpGain` | 0.10 | 低于此收益不专程前往 |
| `deepWaterTreatAsUrgent` | true | 深水 30HP/s 视为紧急 |

- 药水使用走 Player 背包接口，AI 只发 `UseItem` 意图。
- 回血点使用可以被战斗打断；AI 需保留逃跑与结盟兜底。
- 加速 / 位移技能优先用于脱离深水或持续伤害区域。

## 二十、开场与采集补全（U-08、U-26）

### 20.1 Opening 阶段行为

【本文补全建议 · 待策划确认】

| 行为 | 规则 |
| --- | --- |
| 参战 | 攻击幽灵 / 牛怪波次与深渊幽灵将军，参与守点 |
| PvP | 不攻击对手玩家，不参与任何 PvP |
| 结盟 | 不申请、不接受；开场止戈由阶段状态提供 |
| 采集 | 采集任务物品与湖心岛宝箱，优先级低于守点 |
| 移动 | 跟随守点区域，不主动离开任务区 |

### 20.2 Exploration 阶段行为

【本文补全建议 · 待策划确认】

- 任务完成后切换到资源路线：开箱、打怪、囤塔、争夺资源点。
- 优先完成剩余任务物品采集；无任务时按路线模板选择开箱 / 狩猎。
- 进入 Boss 苏醒倒计时后，按 `SAFE / WATCH / URGENT` 收敛资源行为。

### 20.3 采集与拾取

【本文补全建议 · 待策划确认】

- 任务必需物品 > 宝箱 > 塔材料 > 普通掉落。
- 采集 / 开箱读条受击回退与移动取消使用通用读条规则，AI 不自定义数值。
- 采集目标由 `ITaskInteractor` 提供；不可达目标在下一决策周期重评。

## 二十一、经济与成长补全（U-07）

【本文补全建议 · 待策划确认】

本期 AI 经济行为裁剪为"消费结果 + 最小拾取"：

| 行为 | 本期处理 |
| --- | --- |
| 商店购买 / 出售 | 不实现；补位模式默认关闭商店 |
| 宝石合成 / 镶嵌 | 不实现；消费玩家系统的自动合成与自动镶嵌结果 |
| 装备替换 | 不实现；消费自动穿戴结果 |
| 技能书学习 | 不实现；学习槽由 `AiDefinition` 预设注入 |
| 掉落拾取 | 实现：按价值与背包空间拾取 |
| 药水 | 实现：治疗流程按 19 节使用 |

```text
lootValuePriority = 任务物品 > 圣物 / 恶魔之心 > 技能书 > 塔材料 > 装备 / 宝石 > 药水 > 金币
```

- 若后续需要 AI 自主经济，必须先补策划口径，不修改 `Decide` 主结构。

## 二十二、环境与圣物补全（U-22、U-21）

### 22.1 海平面与环境规避

【本文补全建议 · 待策划确认】

| 状态 | AI 行为 |
| --- | --- |
| 淹没前 30s 预警 | 向安全区 / 高地移动，放弃低价值目标 |
| 浅水减速 | 使用加速 / 位移技能撤离 |
| 深水 30HP/s | 按紧急处理，优先级接近生存中断 |
| 心在低地 | 按风险评分决定抢夺或等待；深水区默认不进入 |
| 未完成区域 60s 缓冲 | 缓冲期内优先完成手头读条，缓冲结束前撤离 |

- 安全区、可站立点与路径由 `ISafeZoneProvider` / `IWorldStateProvider` 提供，AI 不自行计算地形。
- 与环境相关的阈值全部外置。

### 22.2 圣物获取与保护

【本文补全建议 · 待策划确认】

- 按版本启用：新手版无圣物；首发进阶版为地图拾取一次性减伤；完整版为合成 / 掉落 / 替换。
- 无圣物：优先抢夺 / 击杀持有者；通过 `playerView.hasRelic` 与 `IRelicStateProvider` 获取公开信息。
- 有圣物：Boss 残血且自身血线健康时优先 solo；避免无意义 PvP 以防掉落。
- 圣物仇恨未归零时不能与 Boss 结盟；AI 需把 `relicHate` 状态纳入结盟判断。
- 圣物拾取、销毁、掉落与替换由 Items 系统执行，AI 只发拾取 / 交付意图。

## 二十三、心争夺与交付补全（U-14、U-30、U-13）

### 23.1 抢夺与打断

【本文补全建议 · 待策划确认】

| 情况 | AI 行为 |
| --- | --- |
| 心 `Available` 且可达 | 前往并开始 10s 拾取读条 |
| 对手拾取中 | 使用普攻或伤害 / 控制类技能打断；优先打断而不是抢读条 |
| 自身拾取中、被攻击 | 读条按 Endgame 规则回退；读条不足以完成时转入自保 |
| 人多 / 被 Boss 追击 | 按终局风险评分决定是否放弃当前读条 |

- 打断手段默认普攻 + 伤害 / 控制类技能；精确规则待 Endgame 与技能系统冻结。
- `heartProgress` 由 Endgame 提供，AI 不自行推进或回退。

### 23.2 心拾取读条回退

- 读条时长 10s、受击回退、移动重置由 Endgame / Item 负责。
- 回退步长、叠加与上限仍待 Endgame 冻结（U-13）；AI 只读取 `heartProgress` 与事件。

### 23.3 持心与交付

【本文补全建议 · 待策划确认】

```text
持有心 → 向交付点 / 阿蛮移动
      → 被追杀时优先自保，必要时放弃直线路径
      → 到达交付点且读条完成 → 交付
      → 死亡则心回到 Dropped，继续争夺
```

- 交付目标点由 Endgame 提供；AI 不持有一个心实例。
- 心拾取与交付期间不主动 PvP，除非对手直接阻止交付。
- 尾刀化保护使用 M6 验收，不新增 AI 专属规则。

## 二十四、淘汰、确定性与测试补全（U-15、U-24、U-23、U-25）

### 24.1 永久淘汰后的 AI

【本文补全建议 · 待策划确认】

- 回魂申请期：AI 停止主动行为，等待存活方响应。
- 回魂成功：15-20 秒后在死亡地点以 50% 生命恢复，清空临时状态，继续决策。
- 永久淘汰：停止 `AiController.Tick` 与决策日志，保留快照 / 事件日志用于验收。
- 淘汰的 AI 不参与心争夺、不写入新的决策，不阻塞 Endgame。
- 双方都永久淘汰时，由 Endgame 按双输结算。

### 24.2 AI 随机 seed 派生

【本文补全建议 · 待策划确认】

```text
aiSeed = Hash(encounterSeed, playerIndex)
```

- 两个 AI 使用不同 `playerIndex`，随机序列互不干扰。
- 随机只用于感知误差、机会、相位扰动与同分裁决，不用于关键决策。
- 同 seed、同输入必须同输出；重放与加速校验必须通过。

### 24.3 无头沙盘参数回灌

【本文补全建议 · 待策划确认】

| 沙盘输出 | 回灌字段 | 说明 |
| --- | --- | --- |
| 击杀权重阈值 | 游戏规则 / 怪物配置 | AI 不保存 |
| 击杀权重 | 游戏规则 / 怪物配置 | AI 不保存 |
| Boss 成长曲线 | Boss 配置 | AI 不保存 |
| 收益矩阵结论 | `AiConfig` 建议值 | 用于校准 `aggressionWeights` |
| holdout 结果 | 修改审计 | 记录参数、原因、样本量 |

- 沙盘只负责收益结构与快速调参；最终数值必须在 Unity 场景内回归确认。
- 沙盘结论回灌后的参数必须经 holdout 种子复验。

### 24.4 机器可读日志字段

【本文补全建议 · 待策划确认】

```text
decisionLog:
    gameTime
    encounterSeed
    aiSeed
    playerIndex
    situation            // SAFE / WATCH / URGENT
    route                // RESTRAIN / AGGRESSIVE / PRESS / CATCH_UP
    trustScore
    oppInference
    action
    reasonCode
    targetId
    value
```

- `reasonCode` 必须稳定、可枚举，供 A/B 层断言自动生成。
- 事件断言字段（如 `kill_count_p0`、`alliance_form`、`underdog_win`）命名必须先冻结，再写验收脚本（U-25 仍待测试文档确认）。
- 所有关键事件带时间戳、实体 ID 与版本号。

## 二十五、补全项对应的开发任务（M6）

| 编号 | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| AI-SKILL-01 | 实现 AI 技能槽请求 | 17.1、ISkillSlotController、SKILL-02（外部） | 只输出 slotId 与 stateReason；不读效果内部状态；不轮询 CD |
| AI-SKILL-02 | 实现技能请求优先级与目标选择 | AI-SKILL-01、17.2 | 生存 → 逃生 → 打断 → 战斗 的顺序正确；技能分类与阈值外置 |
| AI-PVP-01 | 实现 Harass / Cripple / KillOpponent | 18.1、AI-RES-01 | 三档触发、leash、停手阈值正确；盟友过滤以实时版本为准 |
| AI-PVP-02 | 实现打残与击杀收益评估 | AI-PVP-01、18.3、18.4 | 打残到 `pvpCrippleStopHp` 停手；击杀计入 Boss +60s 代价 |
| AI-HEAL-01 | 实现治疗来源选择 | 19、IHealthStateProvider、背包接口 | 技能 → 药水 → 回血点 → 逃跑 / 结盟 顺序正确；路径避开深水 |
| AI-OPEN-01 | 实现 Opening 守点行为 | 20.1、ITaskInteractor | 不 PvP、不申请结盟；参与守点与采集；进入 Exploration 后切换 |
| AI-ECON-01 | 实现最小拾取与消费结果 | 21 | 商店 / 合成 / 换装不实现；按价值拾取；消费自动穿戴与自动合成结果 |
| AI-ENV-01 | 实现海平面规避 | 22.1、ISafeZoneProvider、IWorldStateProvider | 预警期撤离；深水按紧急处理；不进入不可站立区域 |
| AI-RELIC-01 | 实现圣物获取与保护 | 22.2、IRelicStateProvider | 无圣物抢 / 击杀持有者；有圣物避免无意义 PvP 并 solo |
| AI-HEART-01 | 实现抢心、打断与交付 | 23、IHeartStateProvider、Endgame | 10s 读条、受击回退、移动重置由 Endgame 执行；可打断对手；可交付 |
| AI-ELIM-01 | 实现淘汰后停 Tick | 24.1、IPlayerLifecycle | 回魂期等待；回魂成功继续；永久淘汰停止决策且不阻塞 Endgame |
| AI-TEST-02 | 实现 seed 派生与确定性校验 | 24.2 | 1x vs 8x、同 seed 重放结果一致；两个 AI 序列独立 |
| AI-TEST-03 | 实现无头沙盘回灌与 holdout | 24.3、AI-TEST-01 | 沙盘结论可映射到 AiConfig；holdout 通过后才允许冻结 |
| AI-TEST-04 | 实现机器可读日志与断言 | 24.4、AI-DEBUG-03 | 决策日志字段完整；A/B 层断言可自动生成 |

## 二十六、补全后的完成定义

补全完成后，AI 玩家 Demo 还须满足：

- AI 能按技能优先级请求天赋槽与学习槽，且不依赖技能名。
- AI 能对对手执行骚扰 / 打残 / 击杀三档行为，并把击杀的 Boss 苏醒代价计入决策。
- AI 在残血时能按技能 → 药水 → 回血点 → 结盟 / 逃跑的顺序自保。
- AI 在 Opening 阶段只守点与采集，不参与 PvP 与结盟。
- AI 能规避海平面与深水，按版本处理圣物。
- AI 能完成抢心、打断与交付，并正确处理淘汰后状态。
- AI 的随机数来自 `Hash(encounterSeed, playerIndex)`，两个 AI 互不干扰且可复现。
- 沙盘结论可回灌 `AiConfig`，并经 holdout 复验。
- 决策日志字段冻结，A/B 层断言可自动生成。

---

*关联文档：《Player 与 AI 开发索引》《AI 机制开发设计文档》《AI 决策落地执行规格》《AI 数值测试与验收文档》《游戏机制总览》《Boss功能开发清单》《结盟机制设计》《玩家机制设计》《Common 基础系统》《Endgame 终局系统》《版本规划》*
