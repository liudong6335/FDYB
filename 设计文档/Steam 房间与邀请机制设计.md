# Steam 房间与邀请机制设计

> 版本：v0.1（设计稿）
> 平台：Steam / PC
> 范围：好友挑战 + 房间码
> 关联文档：《分道扬镳PC.xlsx》

## 一、设计目标

《分道扬镳》采用 1v1 私约模式，不依赖系统撮合匹配。玩家通过两种方式进入同一局游戏：

1. 好友挑战：从 Steam 好友列表直接邀请，对方接受后进入房间。
2. 房间码：房主创建房间后生成房间码，任何人输入房间码即可加入。

核心原则：

- Steam Lobby 承担“房间”角色，负责成员、邀请、加入和房间状态。
- 对局数据通过 Steam P2P / Steam Datagram Relay 传输，不单独搭建游戏服务器。
- 房主（Host）作为权威端运行完整游戏模拟，客机（Client）上传输入、接收状态快照。
- 房间码只是人类可读的加入入口，真正标识房间的是 Steam Lobby ID。

## 二、总体架构

```
玩家 A（房主）                 Steam                  玩家 B（客机）
     |                          |                          |
     |--- 创建 Lobby ---------->|                          |
     |--- 邀请 / 房间码 ------->|---- 邀请弹窗 / 房间码 -->|
     |<-- 对方已加入 -----------|<-- 请求加入 Lobby -------|
     |                          |                          |
     |------- Steam P2P / Datagram Relay 对局数据 ---------|
```

模块划分：

- 客户端：Steam 初始化、好友/大厅、房间码、网络传输、游戏状态机。
- Steam 服务：Lobby 列表、成员、邀请、覆盖层、P2P 中继。
- 可选项：后续若房间码搜索遇到规模瓶颈，可增加轻量后端把 `room_code -> lobby_id` 映射起来。

## 三、房间生命周期

```
创建房间 -> 等待加入 -> 双方准备 -> 加载对局 -> 游戏进行 -> 结算 -> 重赛/解散
```

### 3.1 状态定义

| 状态 | 说明 | 进入条件 | 退出条件 |
| --- | --- | --- | --- |
| Waiting | 房间已创建，等待对手 | 房主建房 | 对手加入 |
| Ready | 双方已进入，等待确认 | 客机加入 | 双方都准备 |
| Loading | 场景加载 | 双方准备 | 双方加载完成 |
| Playing | 对局进行中 | 双方握手成功 | 达成结算条件 |
| Finished | 对局已结算 | 产生胜负 | 重赛或解散 |
| Closed | 房间已关闭 | 房主解散/超时 | 无 |

### 3.2 房间超时规则

- `Waiting` 状态持续超过 10 分钟无玩家加入，自动关闭。
- 房间码有效期默认 30 分钟，超时后房间不再可被房间码搜索到。
- `Ready` 状态超过 5 分钟未准备，房主可解散。
- 对局中掉线重连窗口默认 60 秒。

## 四、好友挑战流程

1. 房主创建 Lobby，类型为 `FriendsOnly` 或 `Invisible`，房间不会出现在公开列表。
2. 房主在好友列表中选择目标玩家。
3. 客户端调用 Steam API 发送邀请：
   - `InviteUserToLobby(lobbyID, friendSteamID)`
   - 或打开 Steam 覆盖层邀请框 `ActivateGameOverlayInviteDialog(lobbyID)`
4. 好友收到 Steam 邀请，在覆盖层或好友消息中点击“加入”。
5. 客机收到 `LobbyInvite_t` 回调，调用 `JoinLobby(lobbyID)`。
6. 双方都进入 Lobby 后，进入 `Ready` 状态。

说明：

- Steam 原生好友邀请不依赖我们自己的好友系统。
- 如果对方未安装游戏或不在线，Steam 覆盖层会显示为不可邀请状态。
- 邀请弹窗结束后，通过 `LobbyChatUpdate_t` 或成员列表变化感知对方加入。

## 五、房间码加入流程

1. 房主创建 `Public` Lobby，生成唯一房间码并写入 Lobby 数据：
   - `room_code = "7KXQ2M"`
2. 客人在主界面输入房间码。
3. 客户端调用 Steam Lobby 搜索：
   - `AddRequestLobbyStringFilter("room_code", code, k_ELobbyComparisonEqual)`
   - `RequestLobbyList()`
4. 收到 `LobbyMatchList_t` 后，取第一个匹配的 Lobby。
5. 客户端调用 `JoinLobby(lobbyID)`。
6. 房主在成员加入事件中校验身份；不认识的玩家可踢出。

### 5.1 房间码搜索的边界

- Steam 公共 Lobby 才能被房间码搜索到。
- `FriendsOnly` / `Invisible` 房间不能通过房间码搜索。
- Steam 的 Lobby 列表搜索有数量和时效性限制，v1 版本在低并发下可用。
- 如果后续房间数量增大、搜索不稳定，建议增加轻量后端保存 `room_code -> lobby_id` 映射，客户端输入房间码时直接查后端。

### 5.2 陌生人权限

- v1 默认允许“持有房间码即可加入”，由房主手动踢出不认识的人。
- 若需要更严格，可增加“房主确认加入”或“邀请口令”，未通过校验的玩家在加入后立刻被踢出。

## 六、房间码规则

- 长度：6 位字符。
- 字符集：去掉容易混淆的字符，如 `0/O`、`1/I/L`，使用 32 字符集。
- 生成：创建房间时随机生成，并写入 Lobby 数据。
- 冲突：写入后查询一次，若已存在则重新生成。
- 有效期：默认 30 分钟，超时后不可再通过房间码加入。
- 显示：房间界面显示大号房间码，提供“复制”按钮。
- 深链：Steam 客户端可附带 `steam://joinlobby/<appid>/<lobbyid>/<ownerid>`，用于聊天窗口快速加入。

## 七、Lobby 数据契约

建议写入 Steam Lobby 的元数据字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| room_code | string | 6 位房间码 |
| host_steamid | string | 房主 SteamID |
| mode | string | 标准/快速/自定义 |
| map | string | 地图 ID |
| version | string | 客户端版本号 |
| state | string | 当前房间状态 |
| created_at | int | 创建时间戳 |
| room_expire_at | int | 房间码过期时间戳 |
| spectators_allowed | bool | 是否允许观战 |

## 八、网络连接与同步

### 8.1 连接建立

1. 双方加入同一 Lobby 后，通过成员列表获取对方 SteamID。
2. 房主创建 P2P 监听：
   - `CreateListenSocketP2P`
3. 客机连接房主：
   - `ConnectP2P`
4. NAT 与中继由 Steam Datagram Relay 自动处理。

### 8.2 权威模型

- 房主运行完整游戏模拟：护送、幽灵、宝箱、掉落、Boss、恶魔之心。
- 客机负责表现层：本地预测、插值、UI。
- 客机固定频率上传输入；房主固定频率下发状态快照。
- 建议：输入频率 30-60 Hz，快照频率 20-30 Hz。
- 客机根据快照做位置校正，网络抖动时使用插值平滑。

### 8.3 版本校验

- 双方加入后校验 `version` 字段。
- 版本不一致时禁止开局，提示更新客户端。

## 九、断线重连

### 9.1 客机掉线

- 房主保留当前对局状态和 60 秒内快照。
- 客机通过 SteamID 识别身份，重新加入原 Lobby 后请求恢复。
- 房主下发最近快照，双方继续对局。
- 超过 60 秒未恢复，按弃权处理，对方获胜。

### 9.2 房主掉线

- v1 方案：直接结束对局，视为房主方失败；房间解散。
- 可选增强：支持房主转移和断线重连，但会增加状态同步复杂度，建议后续版本再做。

## 十、对局流程衔接

1. 双方准备后，房主检查 Lobby 数据版本和模式。
2. 双方同时加载同一场景。
3. 加载完成后客户端上报 `loading_ready`，房主确认双方就绪后发送 `game_start`。
4. 对局中保持 Lobby 存活，用于掉线重连和观战。
5. 结算时按《分道扬镳PC.xlsx》规则产生胜负。
6. 结算界面提供“再来一局”，复用当前 Lobby 回到 `Ready` 状态。

## 十一、Unity 实现模块

建议按以下模块拆分：

| 模块 | 职责 |
| --- | --- |
| SteamManager | Steam 初始化、App ID、覆盖层 |
| SteamLobbyService | 创建/加入/离开 Lobby、成员监听、Lobby 数据读写 |
| RoomCodeService | 房间码生成、校验、搜索 |
| MatchSession | 房间状态机、状态迁移、超时 |
| NetworkManager | P2P 连接、输入上传、快照下发 |
| GameModeController | 加载、开局、结算、重赛 |
| LobbyUI | 创建房间、好友邀请、房间码输入、准备界面 |

### 11.1 事件流建议

- 创建房间成功：`LobbyCreated_t -> MatchSession.CreateRoom()`
- 加入房间成功：`LobbyEnter_t -> MatchSession.OnJoinedRoom()`
- 成员变化：`LobbyChatUpdate_t -> 更新玩家列表`
- 收到邀请：`LobbyInvite_t -> 弹出加入提示`
- 搜索房间码结果：`LobbyMatchList_t -> JoinLobby`
- 网络连接建立/断开：`SteamNetConnectionStatusChangedCallback_t -> 更新对局状态`

## 十二、技术选型建议

账号、好友、Lobby、邀请、覆盖层使用 Steamworks.NET。

网络层有两套可行方案：

1. 推荐：Unity Netcode for GameObjects + Steam P2P Transport
   - 开发速度快，房间/场景切换与 RPC 框架完善。
   - 适合后续增加观战、房间管理等功能。
2. 备选：Mirror + Steam Transport
   - 生态成熟，社区案例多。
   - 如果团队更熟悉 Mirror 可选。
3. 轻量方案：直接使用 Steam Datagram Relay API 手写输入/快照层
   - 1v1 足够，但需要自己处理序列化、重连和状态同步。

建议第一版先用框架方案把房间流程和完整对局跑通，后续再评估是否替换为轻量实现。

## 十三、待确认问题

1. 好友挑战是否只允许 Steam 好友，还是好友列表之外也可以输入房间码加入？
2. 房间码是否默认对陌生人开放，还是需要房主逐人确认？
3. 私人房间是否保留胜负记录/段位，还是完全无排名？
4. 是否支持观战位？一个房间固定 2 人还是支持观众？
5. 房主掉线 v1 直接判负是否可接受？
6. 是否支持自定义规则房间（快速模式、纯 Boss 模式）？
