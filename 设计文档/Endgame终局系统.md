# Endgame 终局系统

> 定位：消费 Boss 死亡、恶魔之心和玩家淘汰事件，决定心争夺阶段和对局是否结束。
> 原则：Endgame 不控制 Boss 行为，也不定义圣物原始属性。

## 一、进入终局

```text
BossDiedEvent
→ ILootDropService 生成恶魔之心
→ DemonHeartDroppedEvent
→ Endgame 进入心争夺阶段
```

规则：

- 恶魔之心是 Boss 死亡必掉 Item。
- Boss 只请求掉落，不创建心实例。
- 恶魔之心生成失败属于掉落系统异常，Endgame 记录错误并停止进入心争夺阶段。

## 二、恶魔之心状态

```text
Available
PickedUp
Moving
PickupInterrupted
Dropped
Delivered
Resolved
```

事件：

```text
DemonHeartDroppedEvent
DemonHeartPickedUpEvent
DemonHeartPickupInterruptedEvent
DemonHeartDroppedEvent
DemonHeartDeliveredEvent
EndgameResolvedEvent
```

## 三、拾取与交付

- 拾取读条、受击回退、移动打断和死亡掉落属于 Endgame/Item 协同规则。
- 持心者可以交付；交付完成后产生唯一获胜方。
- 如果双方都死亡，Endgame 立即结束对局，不需要等待恶魔之心交付。
- 如果 Boss 死亡后仍有存活玩家，游戏继续心争夺阶段。

## 四、终止条件

| 条件 | 结果 |
| --- | --- |
| 恶魔之心完成交付 | 交付方获胜 |
| 双方玩家均死亡 | 双方失败，统计层双输 |
| 持心者死亡 | 心回到 `Dropped`，继续争夺 |
| 无合法拾取者 | Endgame 按失败/双输规则结算 |

## 五、责任边界

| 系统 | 负责 |
| --- | --- |
| Boss | 死亡、事件发布、掉落请求 |
| Items/Loot | 恶魔之心实例、掉落位置、拾取组件 |
| Endgame | 心状态机、交付、双死和最终结束判断 |
| Player | 玩家淘汰、回魂和参与权 |
| UI | 心进度、持有者、交付表现 |

## 六、相关文档

- 《系统总览与文档索引》
- 《Common 基础系统》
- 《Boss功能开发清单》第 8.4 节
- 《道具系统设计》
