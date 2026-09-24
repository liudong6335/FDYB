# 《分道扬镳》AI 决策落地执行规格（主文档 v0.7 配套）

> 版本：v0.2（原型阶段 · 决策逻辑的可执行规格）
> 定位：作为《AI 机制开发设计文档 v0.7》第八节的**可执行规格**。主文档给出方向与架构，本规格给出**输入字段、评分公式、阈值取值、输出行为**的完整实现依据，使 Agent 可将决策逻辑直接翻译为 C#。
> 修订说明（v0.2）：完成并入修正——统一决策函数签名与决策频率、`trustScore` / `inferredLabel` 移入记忆、补齐 `PRESS` / `CATCH_UP` 路线映射、加入固定 `seed` 确定性、补充无头数值沙盘与终局评分权重、统一 aggressionScore 参数配置。
> 原则：评分函数 + 阈值（不用行为树/机器学习）；自由决策与策略锁定（`StrategyOverride`）走**同一套决策函数**，仅输入被覆盖；全部阈值外置为配置表，不硬编码。

---

## 一、总体接口：一个函数，三种输入来源

### 1.1 核心函数签名（逻辑描述，非代码）

```text
Decision Decide(
    self   : SelfState,    // 自身状态（允许读取）
    view   : PlayerView,   // 对手公开快照（AI 唯一允许消费的对手信息）
    boss   : BossState,
    world  : WorldState,
    memory : MemoryState,
    config : AiConfig,
    seed   : int
) → Action
```

每 0.2s（5Hz）调用一次，与快照周期独立。函数内部按 **Layer 3 → Layer 2 → Layer 1 → 默认** 的固定优先级依次判断，**命中即返回**，不回溯。所有随机数必须由 `seed` 派生且调用顺序固定，保证同种子可复现。

### 1.2 三种输入来源（统一接口的关键）

| 输入来源 | 数据内容 | 谁提供 |
|---------|---------|-------|
| **A. 真实游戏状态** | 自身血量/战力/位置、Boss 状态、世界状态（心/海平面/时间） | 游戏运行时直接读取 |
| **B. PlayerView（对手快照）** | 对手的粗粒度信息（见 2.2），**不含对手内部路线、意图、trust、背包明细** | 5.6.2 的对手快照评估，按相位刷新 |
| **C. 策略覆盖（StrategyOverride）** | 见第六节，`NONE / CC / DD / DC / CD` | 外部探针（B 层验证用） |

> **统一接口原则**：自由决策与策略锁定走**同一个 `Decide`**。锁定策略不是"另开一个开关模块"，而是**在输入层把 `overrideKill` / `overrideRoute` 强制覆盖**后再进入评分。这样 B 层验证时架构不变，且不依赖"AI 自主分化"。

### 1.3 决策周期与快照周期的解耦

- **决策周期**：固定 0.2s（5Hz），每次都重新评估（生存/事件优先级高，需即时响应）。
- **局势快照周期**：10s，按 `gameTime % 10 == phase` 刷新 `PlayerView` 与趋势采样（见主文档 5.6.1）。
- **事件响应**：被攻击、结盟申请、心出现、Boss 结盟发起 → **立即触发，不走周期**，可打断当前行为。

> 关键：决策是"高频的"，快照是"低频的"。高频决策消费低频快照，**快照过期 ≠ 无响应**（事件通道独立）。这解决了主文档 5.6.1「错峰只制造信息新旧差、不制造响应延迟」的实现口径。

---

## 二、输入数据结构（与主文档第九节快照字段一一对齐）

### 2.1 SelfState（自身状态）

```text
SelfState:
    hp, hpMax                  // 血量、上限
    power                      // 综合战力（含已放置塔 towerPlaced）
    towerStock, towerPlaced    // 背包囤塔数、已放置塔数（必须区分，见 5.3）
    hasRelic                   // 是否持有圣物
    relicType                  // 日之石 / 月之石（影响减伤 50%/40%）
    inEndgame                  // 是否进入终局模式（时间/海平面/Boss 可挑战）
    isUnderdog                 // 是否处于弱势（见 4.1 弱势判定）
    currentAlly                // None / Player / Boss
    allianceTimer              // 当前结盟剩余时间（秒）
    allianceCooldown           // 结盟可用剩余冷却（秒）
    heartProgress              // 心拾取读条进度 0-1（0=未在拾取）
```

### 2.2 PlayerView（对手快照，仅此字段可消费）

```text
PlayerView:
    hpRatio                    // 对手血量比例（0-1，粗粒度，分档：低<0.3/中/高>0.7）
    powerRatio                 // 对手战力 / 自身战力；统一用 gap=(1-powerRatio)/(1+powerRatio)
    bossProgress               // 共享 Boss 进度（按 UI 刷新周期，20-40s 一次）
    killDeltaSinceRefresh      // 上次刷新后对手击杀增量（推断偷杀证据）
    towerStockVisible          // 对手可见囤塔数（分档：无/少1-2/多≥3）
    hasRelic                   // 对手是否持有圣物（布尔）
    allianceState              // None / 与玩家结盟 / 与Boss结盟
```

> **断言**：AI 决策代码**只允许读取 `PlayerView`**，禁止直接访问对手的 `SelfState`、路线、意图、背包明细。测试时用断言校验字段集合与 UI 一致（主文档 5.6.2）。

### 2.3 BossState & WorldState

```text
BossState:
    progress                   // 共享苏醒/强化进度 0-100
    awaken                     // 是否已苏醒（可挑战）
    currentEnrageLayer          // 强化层数（每 30s 一层，见 5.4）
    hpRatio                    // Boss 血量比例（终局用）
    allianceWith               // None / Player0 / Player1（Boss 结盟对象）

WorldState:
    gameTime                   // 对局内累计游戏时间（秒，非真实时间）
    seaLevel                   // 海平面高度 / 收缩阶段
    heartState                 // None / 已掉落可争夺 / 被拾取中 / 已交付
    heartPosition              // 心当前位置（争夺目标点）
    timeToEndgame              // 距终局模式的剩余时间
```

### 2.4 MemoryState（短期记忆，主文档 5.1）

```text
MemoryState:
    recentlyBetrayedTimer      // 结盟结束至今秒数（>0 时触发报复倾向）
    recentlyHitByBossTimer     // 最近被 Boss 击中至今秒数
    killStreak                 // 连续杀怪计数（窗口 30s，超时衰减）
    noKillDuration             // 连续不杀怪时长（秒，判定"停手"用）
    lastAllyEndTime            // 上次结盟结束的游戏时间
    progressSamples[ ]         // 进度采样环形缓冲（最近 6 次，用于趋势）
    powerGap                   // 当前战力差 gap=(1-powerRatio)/(1+powerRatio)，正=领先
    trustScore                 // 自己对对手的信任分（0-100，不共享，不在 PlayerView 中）
    inferredLabel              // 克制 / 激进 / 压制 / 合作信号 / 未知（内部评估，非公开 UI）
    aggressionScore            // 当前激进评分（滞回用，见 4.3）
    currentRoute               // 当前路线：RESTRAIN / AGGRESSIVE / PRESS / CATCH_UP
    routeHoldUntil             // 路线锁定到期时间（≥1 个快照周期）
```

---

## 三、Layer 3 · 终局战略层（最高优先级）

### 3.1 进入终局的判定（固定条件，与配置表对齐）

```text
inEndgame = OR(
    timeToEndgame ≤ 0,
    seaLevel ≥ 峰顶决战圈,
    boss.awaken == true
)
```

三项任一满足即切换终局模式，**一旦进入不再退回**（即使海平面回落，原型阶段不处理）。

### 3.2 终局行为评分（Layer 3 内部用「收益打分」选最优，非固定 if/else）

终局不再是简单的 if/else 优先级，**而是对候选行为进行"终局收益评分"**，选最高分。这避免了"心一出现就永远去抢"的脚本感。

```text
EndgameScore(action) = w_gain * 收益期望
                     + w_risk * 安全度
                     + w_progress * 推进度贡献
                     - w_bossThreat * Boss威胁
```

**终局评分权重初始值（v0.2 补齐；各组件先归一化到 0-1）：**

| 权重 | 初始值 | 说明 |
|------|-------|------|
| `w_gain` | 1.0 | 行为收益期望 |
| `w_risk` | 0.6 | 行动安全度 |
| `w_progress` | 0.8 | 对 Boss/心进度的推进 |
| `w_bossThreat` | 0.5 | Boss 强化层与仇恨威胁 |

| 候选行为 | 主要加分项 | 主要扣分项 |
|---------|----------|----------|
| `抢夺心` | 心存在时收益期望极高（w_gain=1.0） | 拾取被打断风险、拾取中无反抗 |
| `击杀圣物持有者` | 抢夺圣物后 solo 收益高 | Boss 仇恨、对手反击 |
| `solo Boss` | 圣物减伤下收益高（w_progress=1.0） | Boss 强化层威胁（w_bossThreat） |
| `结盟（玩家间）` | 结盟期内输出安全（w_risk） | 结盟结束被翻脸风险、时间成本 |
| `结盟（弱者+Boss）` | 摆脱被追杀、争取发育窗口 | Boss 结盟仅 17s，窗口短 |
| `风筝/拖延` | 保命、等结盟冷却 | 进度落后、Boss 持续强化 |
| `放置囤塔` | 战力一次性计入、风筝翻盘 | 放置窗口需安全，否则被打断 |

**终局行为选择流程（伪逻辑）：**

```text
IF heartState == 被拾取中 AND 拾取者是 opponent:
    → 立即：打断拾取 / 抢心（事件响应，最高优先）
ELSE IF heartState == 已掉落可争夺 AND self 可到达:
    → action = 抢夺心（评分通常最高，除非自身残血）
ELSE IF hasRelic AND boss.awaken AND hpRatio > 0.3:
    → action = solo Boss（圣物减伤收益覆盖结盟）
ELSE IF allianceCooldown == 0 AND hpRatio < cfg_allianceHpThreshold:
    IF isUnderdog AND boss.allianceWith ≠ self AND deterministicRand(seed, gameTime) < cfg_bossAllyProb:
        → action = 申请"弱者+Boss"结盟
    ELSE:
        → action = 申请"玩家间"结盟
ELSE IF towerStock > 0 AND currentAlly == None:
    → action = 放置囤塔（风筝/翻盘）
ELSE:
    → action = 逼近 Boss（默认终局行为）
```

### 3.3 圣物决策的具体公式（主文档 5.3 的可执行化）

```text
// 持有圣物时的 solo 意愿（0-1）
soloWillingness = sigmoid(
      (hpRatio - cfg_soloHpMin) * 3
    + (1 - boss.hpRatio) * 2          // Boss 越残越想 solo
    - cfg_soloOpponentNearPenalty      // 对手在附近则惩罚
)

IF hasRelic AND boss.awaken:
    IF soloWillingness > 0.6 AND NOT isHunted:
        → 选 solo（不再结盟）
    ELSE:
        → 先保命（生存优先级覆盖）
```

**关键参数（初始猜测，外置配置）：**

| 参数 | 初始值 | 可调区间 | 影响方向 |
|------|-------|---------|---------|
| `cfg_soloHpMin` | 0.3 | 0.2-0.5 | 越低越敢 solo；过低则圣物持有者常送命 → M5 失效 |
| `cfg_soloOpponentNearPenalty` | 1.5 | 1.0-2.5 | 越大越怕对手干扰；过大会让 solo 永不触发 |
| `cfg_allianceHpThreshold` | 0.4 | 0.3-0.6 | 血量低于此值才主动结盟；过高则结盟泛滥 |

---

## 四、Layer 2 · 状态记忆与路线评分

### 4.1 弱势判定（isUnderdog，驱动 M3/M4 与 Boss 结盟）

```text
hpRatio = self.hp / self.hpMax
powerGap = memory.powerGap

isUnderdog = OR(
    hpRatio < 0.35,
    powerGap < -cfg_gapLead,
    NOT self.hasRelic AND view.hasRelic
)
```

三项任一满足即视为弱势。`powerGap` 只由公开可见的 `view.powerRatio` 计算，不读取对手内部状态。

### 4.2 趋势感知的具体公式（主文档 4.1，防"永远该杀"的核心）

```text
// 每 T_sample 秒（=快照周期 10s）采样一次
progressRate = (progressSamples[now] - progressSamples[now - 2]) / (2 * T_sample)
// 用最近两次采样（20s 窗口）的平均增速，平滑单点噪声

IF progressRate < cfg_rateLow:    trend = SAFE
IF progressRate < cfg_rateHigh:   trend = WATCH
ELSE:                             trend = URGENT
```

| 参数 | 初始值 | 可调区间 | 说明 |
|------|-------|---------|------|
| `cfg_rateLow` | 1.5 %/s | 1.0-2.5 | 低于此=安全，可自由发育 |
| `cfg_rateHigh` | 3.5 %/s | 2.5-5.0 | 高于此=紧急，强制停手 |
| `T_sample` | 10 s | 固定=快照周期 | 与 5.6.1 周期一致 |

> **反"永远该杀"机制**：当 `trend == URGENT` 时，资源管理层**强制 `克制`**（见 5.1），即使 `aggressionScore` 很高。这是趋势感知能覆盖激进路线的唯一硬约束。

### 4.3 激进评分 `aggressionScore`（主文档 5.6.3，路线分化的核心）

```text
hpRatio = self.hp / self.hpMax
powerGap = memory.powerGap
baseRisk = 0.5   // 基准风险（中性）

aggressionScore = baseRisk
    + w_killDelta    * view.killDeltaSinceRefresh    // 对手近期偷杀证据
    + w_leadPower    * powerGap                       // 领先→激进，落后→克制
    + w_window       * (1 - timeSinceRefreshRatio)   // 刷新窗口剩余（偷杀可隐藏的诱惑）
    + w_trust        * (50 - memory.trustScore) / 50 // trust 低→激进
    - w_rate         * progressRate / cfg_rateHigh   // Boss 进度风险
    - w_survive      * (1 - hpRatio)                 // 自保需求
    - w_allianceReady * allianceReadyBoost            // 结盟可用时的合作倾向

// 归一化到 [0, 1]
aggressionScore = clamp01(aggressionScore)
```

**权重初始值（外置配置）：**

| 权重 | 初始值 | 影响 |
|------|-------|------|
| `w_killDelta` | 0.15 | 对手偷杀证据的敏感度 |
| `w_leadPower` | 0.30 | 领先/落后的分化强度（**主要分化来源**） |
| `w_window` | 0.10 | 刷新窗口剩余时间的诱惑 |
| `w_trust` | 0.10 | 信任扣分的敏感度 |
| `w_rate` | 0.40 | Boss 进度风险的压制力（**必须与 w_leadPower 平衡**，否则永远克制） |
| `w_survive` | 0.50 | 自保需求的压制力（残血时强制克制） |
| `w_allianceReady` | 0.20 | 结盟可用时的合作倾向 |

**路线选择（含滞回，防震荡）：**

```text
IF gameTime < routeHoldUntil:
    → 保持 currentRoute（锁定，至少 1 个快照周期）

IF powerGap > +cfg_gapLead:
    route = PRESS
ELSE IF powerGap < -cfg_gapLead:
    route = CATCH_UP
ELSE:
    route = (aggressionScore > cfg_routeHigh) ? AGGRESSIVE : RESTRAIN

IF route != currentRoute:
    IF 该切换由 powerGap 越界触发
       OR |aggressionScore - 上次评分| > cfg_hysteresis:   // 越过滞回带才切换
        currentRoute = route
        routeHoldUntil = gameTime + T_sample          // 至少锁定一个周期
```

| 参数 | 初始值 | 说明 |
|------|-------|------|
| `cfg_routeHigh` | 0.6 | 激进阈值（>0.6=激进） |
| `cfg_routeLow` | 0.4 | 克制阈值（<0.4=克制），中间为滞回带 |
| `cfg_hysteresis` | 0.08 | 滞回宽度，过小→震荡，过大→僵化 |
| `cfg_gapLead` | 0.25 | 战力差超过 ±阈值 → PRESS / CATCH_UP |

> **关键设计**：`w_leadPower` 与 `w_rate` 的平衡决定"是否永远克制"。若实测接近态 100% 克制（6.9 验收失败），**先调 `w_leadPower` 上调或 `w_rate` 下调**，让领先方有动力偷杀。

### 4.4 对手评估与 trustScore 更新（每刷新周期）

```text
// 每刷新周期（20-40s）更新一次，用增量而非绝对判断
IF view.killDeltaSinceRefresh > cfg_stealThreshold:
    memory.trustScore -= cfg_trustHit     // 发现偷杀 → 扣分
IF 收到 opponent 结盟申请 AND 我方未违约:
    memory.trustScore += cfg_trustCoop    // 合作信号 → 加分
IF opponent 翻脸（中途主动攻击）:
    memory.trustScore = 0                 // 翻脸 → 直接清零

// 自然衰减
memory.trustScore *= (1 - cfg_trustDecay)
memory.trustScore = clamp(memory.trustScore, 0, 100)

// 粗粒度标签（仅用于路线判断，不影响数值计算）
memory.inferredLabel = match memory.trustScore:
    <20 → 激进
    <40 → 压制
    <60 → 未知
    <80 → 合作信号
    else → 克制
```

| 参数 | 初始值 | 说明 |
|------|-------|------|
| `cfg_stealThreshold` | 3 击杀/刷新周期 | 判定"偷杀"的增量阈值 |
| `cfg_trustHit` | 30 | 每次偷杀证据的扣分 |
| `cfg_trustCoop` | 15 | 每次合作信号的加分 |
| `cfg_trustDecay` | 0.05 | 每周期自然衰减率 |
| `cfg_trustInit` | 50 | 初始信任分（中性） |

---

## 五、Layer 1 · 资源管理（克制/激进的具体行为参数化）

### 5.1 克制 vs 偷杀的输出（核心抉择，M1/M2/M8 的事件源）

`currentRoute`（克制/激进，来自 4.3）直接决定资源管理层的**杀怪策略**：

```text
IF trend == URGENT:
    killStrategy = 克制         // 硬约束：进度过快时无论路线都停手

ELSE IF currentRoute == 激进:
    killStrategy = 激进杀怪
    // 激进：杀所有可杀怪，包括自然怪与高价值怪
    // 但保留"生存必需击杀"（被怪追击时的反击不算偷杀）

ELSE:  // 克制
    killStrategy = 选择性杀怪
    // 克制：只杀"战力瓶颈必需怪"（如升级/解锁必需），其余停手
    // 开箱、回血、发育性移动 → 允许
```

**「选择性杀怪」的具体判定（把"基本停手"量化）：**

```text
mustKill = OR(
    isHuntedByMonster,              // 被怪追击，反击必需
    nextKillGrantsPowerUp,          // 下一只怪提供关键升级
    towerStock == 0 AND needTower,  // 无塔且需要塔材料（发育必需）
    bossProgress < 5 AND earlyGame  // 开局前 5% 进度可自由发育（避免 M8 僵局）
)

IF 克制 AND NOT mustKill:
    → 不主动杀怪（停手，计入 noKillDuration）
```

> **M8 防僵局机制**：开局 `bossProgress < 5` 时允许自由发育（否则双方都不杀 → 真死锁）。配合 Boss 随时间强化（每 30s 一层），极端合作局仍会被迫收束。

### 5.2 囤塔 vs 放塔（主文档 5.3，驱动 M7）

```text
// 发育期：默认囤塔（不放置）
IF NOT inEndgame AND currentAlly == None:
    → 拾取塔材料时放入 towerStock（不立即放置）

// 结盟锁定：结盟期间不可放置
IF currentAlly != None:
    → towerStock 锁定，任何放置指令无效

// 终局 / 结盟后：一次性放置（放塔风筝）
IF (inEndgame OR currentAlly == None) AND towerStock > 0:
    IF 处于安全窗口（opponent 远离 OR 结盟刚结束）:
        → 批量放置 towerStock 中的所有塔
        → towerPlaced += towerStock; towerStock = 0
```

| 参数 | 初始值 | 说明 |
|------|-------|------|
| 塔配额（总） | 4 座 | 见附录 A |
| 结盟锁定 | true | 结盟期间不可放置（核心机制） |

> **M7 事件源**：只有当「发育期囤塔 → 结盟锁定 → 结盟结束解锁 → 终局批量放置 → 后续翻盘」完整发生，`tower_stock_placed_post_alliance → subsequent_win` 的断言（主文档 6.5 M7）才能触发。

### 5.3 开箱 vs 狩猎（克制的替代收益）

克制路线下 AI 不能只"站着不动"，需有**替代发育行为**：

```text
IF 克制 AND NOT mustKill:
    candidate = argmax(nearbyObjective):
        score(宝箱)   = 宝箱价值 * (1 + 距离衰减)
        score(塔材料) = 塔材料价值 * 囤塔需求度
        score(回血点) = 回血紧迫度
        score(怪物)   = 0   // 克制时怪物优先级最低
    → moveTo(candidate)
```

这保证了克制路线下 AI 仍有"有意义的行动"，避免"双方都不杀 → 原地发呆"的观感。

---

## 六、StrategyOverride：策略锁定（与自由决策统一接口）

### 6.1 覆盖点（仅两处，最小化侵入）

策略锁定**不在行为层做 if/else**，而是**在输入层覆盖两个变量**：

```text
overrideKill:     克制 → 强制 killStrategy = 克制（除 survival 必需）
                  偷杀 → 强制 killStrategy = 激进杀怪（无视 trend URGENT 外的约束）
                  无    → 走 4.3/5.1 自由评分

overrideRoute:    克制 → aggressionScore 强制 = 0（恒走克制路线）
                  激进 → aggressionScore 强制 = 1（恒走激进路线）
                  无    → 走 4.3 自由评分
```

**注意**：`survival`（血量极低时的逃跑/治疗）**不受覆盖影响**——即使锁定偷杀，AI 残血时仍会保命（否则 DD 会恒双输，破坏 P>S 的真实性）。这是"够用"与"真实"的平衡。

### 6.2 四种锁定策略（对应主文档 6.2.3）

| 策略 | A 的覆盖 | B 的覆盖 | 说明 |
|------|---------|---------|------|
| **CC** | `overrideKill=克制, overrideRoute=克制` | 同 | 双方基本停手 |
| **DD** | `overrideKill=偷杀, overrideRoute=激进` | 同 | 双方激进杀怪 |
| **DC** | 偷杀/激进 | 克制/克制 | A 偷杀、B 克制 |
| **CD** | 克制/克制 | 偷杀/激进 | 互换（对称性校验） |

**实现接口（逻辑描述）：**

```text
struct StrategyOverride:
    mode: NONE | CC | DD | DC | CD
    apply(self, opponent):
        IF mode == NONE: return
        // 根据 self 是 Player0 还是 Player1，查表得到覆盖值
        (overrideKill, overrideRoute) = LOOKUP[mode][self.index]
        // 覆盖发生在评分之前
```

> **与快照错峰的关系**：`StrategyOverride` 是**整局锁定**的（B 层验证用），不走快照相位，不受 `snapshotPhaseOffset` 影响。快照错峰只影响**自由决策**时的 PlayerView 新鲜度，不影响锁定策略。因此"错峰失效"不会污染 B 层的成立性证明（主文档 5.6.1 / 第六节）。

---

## 七、生存与事件响应（贯穿所有层，最高优先级）

### 7.1 生存阈值（任何层都被覆盖）

```text
IF hpRatio < cfg_survivalHp:
    → action = 逃跑 / 治疗 / 申请结盟（按优先级：治疗可用→治疗；否则逃跑；无处可逃→结盟）
    // 此判断在任何 Layer 之前，属于"中断"
```

| 参数 | 初始值 | 说明 |
|------|-------|------|
| `cfg_survivalHp` | 0.25 | 低于此值强制保命 |

### 7.2 事件响应（立即触发，不走周期）

| 事件 | 响应行为 | 是否可打断当前 |
|------|---------|-------------|
| 被攻击 | 反击 / 逃跑（按 hpRatio） | 是 |
| 结盟申请 | 接受/拒绝（按 isUnderdog 与 trust） | 是 |
| 心出现 | 立即争夺（见 3.2） | 是 |
| Boss 结盟发起 | 识别"我成被追杀方"→ 风筝/拖延/抢先削弱 Boss（主文档 5.4） | 是 |
| 结盟结束 | 触发报复倾向（`recentlyBetrayedTimer` 重置） | 否（下一决策周期生效） |

**结盟接受逻辑（参数化）：**

```text
acceptAlliance = OR(
    isUnderdog,                              // 弱势必接受
    memory.trustScore > cfg_trustAccept,     // trust 高则接受
    hpRatio < cfg_allianceHpThreshold        // 残血则接受
)
```

| 参数 | 初始值 | 说明 |
|------|-------|------|
| `cfg_trustAccept` | 60 | trust 高于此值接受结盟 |

### 7.3 回魂契约决策（初版）

- AI 死亡后，只要满足回魂资格，就自动发起回魂申请，不做额外概率判断。
- AI 作为存活方收到回魂申请时，只使用双方已公开的防具宝石状态与自身 `trustScore`。
- 任意一方没有已装备防具宝石时，AI 直接拒绝。
- 双方都有防具宝石且 `trustScore >= cfg_trustRevive` 时，AI 接受回魂。
- `trustScore < cfg_trustRevive` 时，AI 拒绝回魂。
- 本阶段不加入隐藏库存、装备价值、Boss 进度或人格差异的额外判断，保持条件简单可复现。

| 参数 | 初始值 | 说明 |
|------|-------|------|
| `cfg_trustRevive` | 60 | trust 高于或等于此值时接受回魂 |

---

## 八、参数配置表（外置，非硬编码）

**全部参数集中在一个配置表**（JSON/CSV），修改无需改代码。下表为完整清单（与附录 A 合并，去重）：

### 8.1 决策参数

| 参数 | 初始值 | 区间 | 所属系统 |
|------|-------|------|---------|
| `cfg_survivalHp` | 0.25 | 0.15-0.40 | 生存 |
| `cfg_soloHpMin` | 0.30 | 0.20-0.50 | 圣物 solo |
| `cfg_soloOpponentNearPenalty` | 1.5 | 1.0-2.5 | 圣物 solo |
| cfg_trustRevive | 60 | 存活 AI 接受回魂的最低信任分 |
| `cfg_allianceHpThreshold` | 0.40 | 0.30-0.60 | 结盟 |
| `cfg_trustAccept` | 60 | 40-80 | 结盟 |
| `cfg_bossAllyProb` | 0.50 | 0.3-0.7 | Boss 结盟响应 |
| `cfg_rateLow` | 1.5 %/s | 1.0-2.5 | 趋势感知 |
| `cfg_rateHigh` | 3.5 %/s | 2.5-5.0 | 趋势感知 |
| `T_sample` | 10 s | 固定 | 快照周期 |
| `w_leadPower` | 0.30 | 0.1-0.6 | 路线分化 |
| `w_rate` | 0.40 | 0.2-0.7 | 路线分化 |
| `w_survive` | 0.50 | 0.3-0.8 | 路线分化 |
| `w_killDelta` | 0.15 | 0.05-0.3 | 路线分化 |
| `w_window` | 0.10 | 0.0-0.2 | 路线分化 |
| `w_trust` | 0.10 | 0.0-0.3 | 路线分化 |
| `w_allianceReady` | 0.20 | 0.0-0.4 | 路线分化 |
| `w_gain` | 1.0 | 0.5-1.0 | 终局评分 |
| `w_risk` | 0.6 | 0.3-1.0 | 终局评分 |
| `w_progress` | 0.8 | 0.5-1.0 | 终局评分 |
| `w_bossThreat` | 0.5 | 0.2-1.0 | 终局评分 |
| `cfg_routeHigh` | 0.6 | 0.5-0.75 | 路线滞回 |
| `cfg_routeLow` | 0.4 | 0.25-0.5 | 路线滞回 |
| `cfg_hysteresis` | 0.08 | 0.03-0.15 | 路线滞回 |
| `cfg_gapLead` | 0.25 | 0.15-0.4 | 路线分化 |
| `cfg_stealThreshold` | 3 | 1-5 | trust |
| `cfg_trustHit` | 30 | 20-50 | trust |
| `cfg_trustCoop` | 15 | 10-30 | trust |
| `cfg_trustDecay` | 0.05 | 0.02-0.1 | trust |
| `cfg_trustInit` | 50 | 30-70 | trust |

### 8.2 与附录 A 的对齐（机制参数，本文档不重定义）

> 以下参数属于**游戏机制层**（非 AI 决策层），由游戏运行时提供，AI 只读。此处仅列出 AI 依赖的字段，确保接口对齐：

- 共享击杀权重阈值（100）、玩家击杀权重倍数（4）、个人权重→恶魔分身（65%）
- 结盟时长：玩家间 35s、弱者+Boss 17s、确认 2-3s、冷却 30s
- 圣物减伤：日 50% / 月 40%
- 心拾取读条 10s、受击回退、移动重置
- 塔配额 4 / 保底 2、结盟锁定
- 单局目标 22-25 分钟、海平面速度倒推
- 局势快照：周期 10s、相位差开关（默认关）、A 逢 4 / B 逢 9
- 接近判定阈值（相对战力差 X%，待实测）、路线切换冷却 ≥1 周期

---

## 九、输出行为枚举（Action）

决策函数的返回值，与事件日志字段一一对应（主文档第九节）：

```text
enum Action:
    // 移动/定位
    MOVE_TO_SAFETY          // 逃跑
    MOVE_TO_HEAL            // 治疗点
    MOVE_TO_OBJECTIVE       // 宝箱/塔材料/回血点（克制替代收益）
    APPROACH_BOSS           // 逼近 Boss

    // 战斗
    KILL_MONSTER            // 主动杀怪（激进）
    KILL_SURVIVAL           // 生存必需击杀（反击，克制下也允许）
    ATTACK_OPPONENT         // 攻击对手
    INTERRUPT_HEART         // 打断对手拾取心

    // 发育/塔
    OPEN_CHEST              // 开箱
    PLACE_TOWER             // 放置囤塔

    // 结盟
    REQUEST_ALLIANCE_PLAYER // 申请玩家间结盟
    REQUEST_ALLIANCE_BOSS   // 申请弱者+Boss 结盟
    ACCEPT_ALLIANCE         // 接受结盟
    BREAK_ALLIANCE_MIDWAY   // 结盟中途翻脸（惩罚仇恨）

    // 终局专属
    CONTEST_HEART           // 争夺心
    SOLO_BOSS               // 独自挑战 Boss
    KITE_BOSS               // 风筝/拖延 Boss
```

---

## 十、与主文档验收的对接

### 10.1 自检三道门（第十一节）的可执行化

| 检查 | 操作 | 通过的量化标准 |
|------|-------------------|
| 参数敏感性 | 杀怪权重 3→10 | `trend` 进入 URGENT 的频率 ↑，**且**克制路线占比 ↑（若占比不变说明趋势感知未生效） |
| 结盟触发 | 己方 hp 20% + Boss 进度 85% | `isUnderdog == true` AND `acceptAlliance == true` 至少一次 |
| 翻脸行为 | 两 AI 结盟后等待结束 | `recentlyBetrayedTimer > 0` 的一方在结束后 3s 内发起 `ATTACK_OPPONENT` |

**可选两项（圣物/囤塔）：**

| 检查 | 通过标准 |
|------|---------|
| 圣物行为 | `hasRelic == true` 时 `soloWillingness > 0.6` 至少一次（说明 AI 倾向 solo） |
| 囤塔行为 | `towerStock > 0` 在终局/结盟后转化为 `towerPlaced` 至少一次 |

### 10.2 6.9 接近态分化验收的数据来源

第九节快照字段新增以下可观测项（供 6.9 验收使用）：

- `aggressionScore` 每周期记录（可绘曲线）
- `currentRoute` 切换时间戳
- 每次路线切换时的 `(powerGap, progressRate, trustScore)` 三元组

**验收标准（量化）：**

1. 接近判定时（`|powerGap| < cfg_gapLead` 且 `killDelta==0` 且 `trend==SAFE`）的双方路线组合，**克制-克制比例 < 100%**（建议 < 80%）；
2. 拉开态（`powerGap > cfg_gapLead` 或 `< -cfg_gapLead`）**路线不同至少 50%**（PRESS vs CATCH_UP）；
3. 单方激进分支（一方 AGGRESSIVE、一方 RESTRAIN）在**自然对局种子组中至少出现 3 局**（对应主文档 6.9「若干局」）。

若不满足 → 调 `w_leadPower / w_rate / cfg_routeHigh`（见 4.3 末尾），**不要改评分结构**。

---

## 十一、实现顺序建议（Agent 可执行清单）

按以下顺序实现，**每步跑通再进下一步**，便于定位问题：

1. **SelfState / WorldState 读取**：先能打印自身血量、Boss 进度、游戏时间。跑 1 局空逻辑，确认数据非空。
2. **趋势感知（4.2）+ 硬约束**：实现 `progressSamples` 缓冲与 `trend` 判定。自检：`cfg_rateLow` 调极低时 AI 应普遍停手。
3. **激进评分（4.3）无权重版本**：先令所有权重=0.5（纯中性），确认评分在 [0,1] 且不震荡（滞回生效）。
4. **路线选择 + 囤塔（5.2）**：确认发育期 `towerStock` 累积、结盟锁定生效。
5. **圣物决策（3.3）+ 终局切换（3.1）**：注入 `hasRelic=true`，确认 `soloWillingness` 计算正确。
6. **事件响应（7.2）+ 生存**：人工制造"血量 20% + 结盟申请"，确认立即响应。
7. **PlayerView（2.2）+ trust（4.4）**：先用手动注入的对手数据（不走快照），确认 `inferredLabel` 更新。
8. **无头数值沙盘**：先只实现成长曲线、Boss 苏醒阈值、C/D 收益结算，跑 CC/DD/DC/CD，验证 B 层排序是否成立。
9. **StrategyOverride（第六节）**：在 Unity 场景整局锁定 CC/DD/DC/CD，复验 B 层收益矩阵。
10. **快照错峰（主文档 5.6.1）**：接入 `gameTime % 10 == phase`，A 逢 4 / B 逢 9，验证 6.9。
11. **参数扫描**：按第七节四步法 + 附录 A，调出 v1 初始数值包。

> **关键**：第 8 步（无头沙盘）先于第 9 步（Unity 策略锁定），第 9 步先于第 10 步（快照错峰）。B 层成立性证明不依赖错峰，先把"机制成立"验证掉，再加分化机制更稳妥。

---

## 附录 A · 与主文档接口对照表（防漂移）

| 主文档位置 | 本文档对应 | 状态 |
|----------|----------|------|
| 5.1 短期记忆字段 | 2.4 MemoryState | ✅ 对齐 |
| 5.3 囤塔/放塔逻辑 | 5.2 | ✅ 细化 |
| 5.4 Boss 第三方 | 3.2 / 7.2 | ✅ 纳入评分 |
| 5.5 决策伪逻辑 | 第三~七节 | ✅ 参数化 |
| 5.6.1 快照相位 | 1.3 / 第六节 | ✅ 明确不影响锁定策略 |
| 5.6.2 PlayerView | 2.2 | ✅ 字段固化 |
| 5.6.3 路线评分 | 4.3 | ✅ 加权重与滞回 |
| 6.2 效用函数 U | （属游戏层，不在决策内） | ➖ 不涉及 |
| 6.5 事件断言字段 | 第九节 Action 枚举 | ✅ 对齐 |
| 6.9 接近态分化 | 第十节 10.2 | ✅ 量化标准 |
| 附录 A 参数表 | 第八节 8.1 | ✅ 合并去重 |

---

## 附录 B · 风险与已知缺口

1. **权重平衡是核心风险**：`w_leadPower` vs `w_rate` 决定"是否永远克制"。若 6.9 验收失败，**只调这两个权重与 `cfg_routeHigh`**，不要改结构。
2. **生存覆盖与锁定策略的冲突**：锁定偷杀时 AI 残血仍要保命（7.1 不受覆盖）。若 DD 出现"残血仍硬杀导致必输"，这是**预期行为**（体现 P>S 的真实性）；但若 DD 双输率 =100%，需检查是否生存逻辑失效。
3. **trustScore 的初始值**：配置表已给 `cfg_trustInit = 50`（中性）。若初始 = 100（完全信任），前 30s 双方会过度合作，影响早期数据。
4. **「选择性杀怪」的 mustKill 判定可能过松**：若实测克制路线仍疯狂杀怪（`noKillDuration` 始终短），收紧 `mustKill` 条件（如移除"塔材料"例外）。
5. **终局收益评分的权重（`w_gain/w_risk/w_progress/w_bossThreat`）**：v0.2 已在 3.2 与第八节配置表给出初始值（1.0 / 0.6 / 0.8 / 0.5），实现第 5 步后仍需按实测结果校准，校准理由记入修改审计。

---

*本文档仅覆盖原型阶段决策逻辑的可执行规格。多人格、真人手感、网络同步由《AI 机制开发设计文档 v0.7》的关联文档在项目中后期承接。*

*修订记录：v0.1 初稿——将 v0.5 第五、八节的架构方向转化为评分函数、参数化行为与配置表，含完整字段结构、权重初始值、StrategyOverride 统一接口、实现顺序与验收量化标准；v0.2 并入主文档 v0.7 前修正——统一接口签名与 seed、trustScore 移入记忆、补齐 PRESS/CATCH_UP、补充无头沙盘与终局权重。*
