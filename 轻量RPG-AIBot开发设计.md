# 雾港小镇：轻量 RPG 执行手册

> 目标：使用团结引擎做一个最小可玩 RPG，优先验证 AIBot NPC 对话、发任务、发奖励和记忆。
>
> 目标时长：第一版 20～30 分钟；后续可扩展到 1 小时。
>
> **剧情定稿（2026-09）**：以 `雾港矿镇-完整剧本.md` **v2.0-slice《回声与承诺》** 为准  
> （两段主线：采药 + 矿灯；NPC：林洛 + 阿拓=老王；单结局；40～60 分钟）。  
> 原 2～4 小时《黑曜回声》全量内容降为该文 §14 资料片存档，不进当前开发范围。

---

## 1. MVP 只做什么

### 场景

- `Town`：村镇、药师林洛、铁匠阿拓、森林入口。
- `Forest`：一条路、两只灰狼、一株月光药草。

### 功能

- 玩家移动和交互。
- 一个 NPC 对话。
- 一个主线任务：寻找月光药草。
- 一个可选支线：收集 3 块铁矿。
- 轻量装备：木剑、铁剑、布衣。
- 背包：物品数量、拾取、消耗。
- 存档：单槽位 JSON。
- AIBot：流式对话、任务查询、接受任务、完成任务、发放奖励。

### 第二波体验完善（MVP 跑通后追加，详见 §17）

- 战斗反馈：受击顿帧、伤害飘字、死亡消散。
- 任务目标指引：HUD 当前目标 + 简易方向提示。
- 玩家死亡体验：死亡演出、回镇重生、轻量金币惩罚。
- NPC 互通玩家事迹：一个 NPC 能提及玩家对另一个 NPC 做过的事。
- 撒谎/违约可被戳穿：空手交任务、食言等可被 NPC 记住并吐槽。

### 第一版不做

- 多职业、技能树、复杂属性。
- 开放世界和复杂剧情分支。
- 多人联机。
- 模型直接修改任务、背包或装备。
- 复杂战斗 AI。
- 多小地图 / 寻路网格（指引先用文字 + 门点方向即可）。

---

## 2. 玩家体验流程

```text
进入 Town
  ↓
靠近林洛，按 E
  ↓
通过 AIBot 对话接受任务
  ↓
进入 Forest
  ↓
击败灰狼或绕过灰狼
  ↓
拾取 moon_herb
  ↓
返回 Town
  ↓
再次与林洛对话
  ↓
游戏校验任务并发放金币、药水
  ↓
自动保存
```

只要这条流程跑通，第一版就成功。

---

## 3. 剧情最小设定

> 详细人物关系、两段主线与结局卡见 `雾港矿镇-完整剧本.md` v2.0-slice。

玩家来到雾港小镇，药师林洛的女儿生病，需要森林中的月光药草。林洛请求玩家帮忙，玩家完成任务后获得报酬。

第二幕（剧本 v2.0-slice）：为铁匠阿拓（王拓）找回蓝色矿灯，在浅道听见会复述人话的回声，交还时以「实话 / 谎话」收尾。

林洛记忆点（MVP 三个 + §17 体验完善扩展）：

```text
# MVP（已验证）
player_accepted_quest
player_was_polite
player_returned_with_herb

# §17 撒谎/违约戳穿（游戏侧写 flag，模型只读）
player_tried_turnin_without_herb   # 空手来交任务
player_broke_promise               # 答应后长时间未交 / 明确食言
player_was_rude                    # record_player_choice(rude)

# §17 跨 NPC 事迹（世界旗标，任意 NPC 快照可读）
world_helped_lin_find_herb         # 帮林洛完成采药
world_helped_lin_before            # 是否已对林洛有恩
```

灰狼第一版只作为敌人。后续再增加“交易/帮助灰狼”的分支，不影响基础系统。

---

## 4. 团结引擎工程结构

```text
Assets/
├── Scenes/
│   ├── Boot.scene
│   ├── Town.scene
│   └── Forest.scene
├── Scripts/
│   ├── Bootstrap/GameBootstrap.cs
│   ├── Player/PlayerController.cs
│   ├── Player/PlayerInteractor.cs
│   ├── Dialogue/DialogueBridge.cs
│   ├── Dialogue/NpcInteractable.cs
│   ├── Quest/QuestSystem.cs
│   ├── Inventory/InventorySystem.cs
│   ├── Equipment/EquipmentSystem.cs
│   ├── Save/SaveSystem.cs
│   ├── World/GameContextProvider.cs
│   └── Items/QuestItemPickup.cs
├── Prefabs/
│   ├── Player.prefab
│   ├── NPC_Lin.prefab
│   ├── Enemy_Wolf.prefab
│   └── UI/DialoguePanel.prefab
├── Data/
│   ├── Items/
│   ├── Quests/
│   └── NPC/
└── ThirdParty/ATTRIBUTIONS.md
```

AIBot 包保留在：

```text
Packages/com.aibot.npcagent
```

如果团结引擎不支持本地 UPM 路径，再复制到 `Assets/Plugins/AIBot`，不要修改插件内部逻辑。

### 目录 ↔ 架构层映射（§6.1 定稿后，用于判断代码放哪个文件）

> 上方结构树为原始规划，实际文件以 §15 为准；放新代码 / 审代码时按此表"目录 ↔ 层"对号入座：

| 实际目录（§15 / §16） | §6.1 层 | 说明 |
|---|---|---|
| `Scripts/UI/`（`*Panel.cs`） | View 层 | 只发意图，不调系统改数据 |
| `Scripts/UI/UIManager.cs`、`Scripts/Player/PlayerInteractor.cs`、`Scripts/Dialogue/NpcInteractable.cs` | 意图入口 | 只转发输入，不编流程 |
| `Scripts/Dialogue/QuestToolHost.cs` | AI 侧适配器（意图入口） | 翻译工具调用 → 调 GameSession；流程不落此处 |
| `Scripts/Session/`（GameSession.cs）★ | 流程编排层 | 唯一跨系统编排点：方法体 = 系统调用序列 + Save 收尾；不存数据、不带 UI、不写规则判断 |
| `Scripts/Quest/` `Inventory/` `Equipment/` `Save/` `Enemy/`、`Items/QuestItemPickup.cs` | 系统层 | 各自领域数据 + 规则 + 广播事件 |
| `Scripts/Quest/RewardService.cs` ★ | 系统层（钱包/交易原语） | 金币 + OnGoldChanged + GrantQuestReward；QuestSystem 只留状态机 |
| `Scripts/Bootstrap/`（GameBootstrapper） | AppRoot 组装 | 按依赖顺序挂系统（§6.1 六步模板第②步） |
| `Scripts/Dialogue/GameContextProvider.cs` | 系统层只读投影 | 给 AI 的快照，非独立系统 |
| `Scripts/World/WorldFlagSystem.cs` ★§17 | 系统层（跨 NPC 旗标） | 帮助/违约等世界 flag + 存档 |
| `Scripts/Combat/CombatFeedback.cs` ★§17 | View 层 | 顿帧/飘字/消散，只订阅事件 |
| `Scripts/UI/QuestTrackerView.cs` ★§17 | View 层 | 目标行 + 门点提示 |
| `Resources/Items|Quests/` + ItemData / QuestData | 数据层 | 内容资产，加内容只动这里 |
| `Scripts/LuaLab/` + `StreamingAssets/LuaLab/` | §16 学习沙盒 | 独立实验区，不属于 §6.1 分层，不进 Build Settings 主流程 |

---

## 5. 场景搭建

### Boot.scene

```text
Boot
└── AppRoot
    ├── GameBootstrap
    ├── GameSession
    ├── QuestSystem
    ├── InventorySystem
    ├── EquipmentSystem
    └── SaveSystem
```

`AppRoot` 使用持久化对象，切换场景时不销毁。

### Town.scene

```text
Town
├── PlayerSpawn
├── NPC_Lin
│   ├── NpcAgent
│   ├── NpcInteractable
│   └── GameContextProvider
├── NPC_Tuo
├── ForestGate
└── Canvas
    ├── DialoguePanel
    ├── QuestPanel
    ├── InventoryPanel
    └── StatusText
```

### Forest.scene

```text
Forest
├── PlayerSpawn
├── Wolf_01
├── Wolf_02
├── MoonHerb
│   └── QuestItemPickup
├── IronOre_01
├── IronOre_02
├── IronOre_03
└── TownReturnGate
```

> 现状注记（2026-09-05）：Town 为 NPC_Lin（NpcAgent+NpcInteractable+QuestToolHost）+ ForestGate；
> Forest 用 `WolfSpawner`（对象池，2 只初始/4 只上限）替代固定 Wolf_01/02；灰狼带数据驱动掉落（iron_ore）。
> NPC_Tuo（铁匠）尚未添加（属内容扩展，Server 需对应 npc 配置）。

第一版允许全部使用 Cube、Capsule 和 Plane 灰盒，不要先花时间制作正式地图。

---

## 6. 系统职责

| 系统 | 只负责什么 |
|---|---|
| `PlayerController` | 移动、攻击、受击、死亡/重生入口 |
| `PlayerInteractor` | 检测附近 NPC、物品和入口 |
| `NpcInteractable` | 打开/关闭 NPC 对话 |
| `NpcAgent` | AIBot 对话、流式输出、工具请求 |
| `DialogueBridge` | 把 AIBot 事件显示到 UI |
| `QuestSystem` | 任务状态和目标推进、当前追踪任务 |
| `InventorySystem` | 物品增删和查询 |
| `EquipmentSystem` | 装备穿戴和属性更新 |
| `RewardService` | 固定奖励发放、交易/扣款原语 |
| `SaveSystem` | JSON 保存和读取 |
| `GameContextProvider` | 提供给 AIBot 的只读状态（含世界旗标/戳穿 flag） |
| `WorldFlagSystem` ★§17 | 跨 NPC 事迹旗标（帮助/违约/无礼）+ 存档 |
| `CombatFeedback` ★§17 | 纯表现：顿帧、飘字、受击闪白、死亡消散 |
| `QuestTrackerView` ★§17 | HUD 当前目标文案 + 门点/目标方向提示 |

核心原则：

```text
AIBot 负责“怎么说”
游戏代码负责“能不能做”
```

---

## 6.1 目标架构（分层 + 事件驱动 + GameSession 收口，2026-09-04 定稿）

> 在 2026-09-03 版基础上定稿：方向不变（分层 MVC 精神 + 事件解耦 + 流程编排收口），
> 补齐 GameSession 边界约束、事件事务语义、现状差距清单与内容/系统扩展协议。

### 定性：本质就是 MVC，只拆开了 Controller

```text
本方案 = MVC 的观察者变体：
  · Controller 拆成两段 → 意图入口（薄：只翻译输入）+ GameSession（只编排流程）→ 防 God Class
  · Model 变化广播事件给 View（观察者），不由 Controller 推给 View
  · View 不碰数据、规则在系统层 —— MVC 精神完整保留

✗ 严格 MVC：所有 View 操作先进一个 Controller → God Class，30 分钟规模反而更糟
✗ MVP：View 接口化 + Presenter 主动驱动 → Unity 动态 UI 下过度设计
✓ 本方案：分层当"抽屉"（代码归属）而不是"调用台阶"（运行路径）；实操只维护 3 个抽屉
```

### 分层总览（全貌图，归置代码用，不是每次操作都走满）

```text
┌─────────────────────────────────────────────────────┐
│  View 层（纯展示 + 发意图）                            │
│    HUDPanel / DialoguePanel / InventoryPanel / QuestPanel │
│    只做：读状态(事件刷新) + 发意图；不碰系统逻辑         │
├─────────────────────────────────────────────────────┤
│  意图入口（薄控制器：只转发不编逻辑）                    │
│    PlayerInteractor / UIManager / NpcInteractable     │
│    QuestItemPickup / QuestToolHost(AI 侧)             │
│    "按E→交互意图"  "点装备→装备意图"  "工具调用→用例调用" │
├─────────────────────────────────────────────────────┤
│  GameSession（流程编排层）★新增核心，唯一跨系统编排点    │
│    每个用例方法 = 按序调系统 + Save 收尾                │
│    不存数据、不带 UI、不写规则 if                       │
├─────────────────────────────────────────────────────┤
│  系统层（数据 + 规则，各自领域管家）                     │
│    QuestSystem / InventorySystem / EquipmentSystem    │
│    RewardService(钱包) / SaveSystem / 拾取·战斗        │
│    WorldFlagSystem(跨NPC旗标) ★§17                   │
├─────────────────────────────────────────────────────┤
│  数据层（纯数据资产 + 存档读写）                        │
│    ItemData / QuestData / NpcProfile / 存档 JSON     │
│    （worldFlags / playerFlags 进存档）★§17           │
└─────────────────────────────────────────────────────┘
```

实操只维护 3 个抽屉；意图入口是已有脚本的"姿态"、数据资产跟随所属系统，均不单独成层：

| 抽屉 | 装什么 | 现在要做的 |
|---|---|---|
| UI 抽屉 | 4 个面板 + QuestTrackerView/CombatFeedback/DeathFade（§17） | 删直调系统的代码，改为发意图 |
| 系统抽屉 | AppRoot 上各系统 | 各自管各自领域，跨域变更交给 Session 编排 |
| GameSession | 跨系统流程 | 新增；全项目流程代码只放这里 |

### 铁律三条

```text
1. 调用只能向下：上层调下层；UI / 意图入口不跨层直调系统
2. 变化只能向上：系统状态变更 → 广播事件 → UI 刷新；禁止轮询、禁止 UI 主动读
3. 流程只有一个家：跨系统的"变更序列"由 GameSession 编排；
   系统间允许只读查询（任务查背包够不够），
   禁止 A 系统写 B 系统的数据，或替 B 系统驱动流程
```

### GameSession 职责与硬约束

```text
方法体只允许 = 系统调用序列 + Save 收尾。例如：

  Session.CompleteQuest(main_001):
      验 Accepted 且持有药草   → 问 QuestSystem / InventorySystem（只读）
      扣药草 + 发金币 + 发药水 → 调 RewardService
      置 Rewarded             → 调 QuestSystem
      Save                    → 调 SaveSystem

硬约束：
  · 不存游戏数据 / 不带 UI / 不写规则判断（"能不能"永远在系统层）
  · 幂等靠系统层状态校验（Rewarded 后再调自然失败），Session 不记额外标记
  · 方法体一旦出现规则判断就拆回系统层 —— 否则 GameSession 就是下一个 God Class
```

### 收录范围（落地顺序）✅ 全部完成（2026-09-05）

```text
第1步（优先）：完成主线-领奖 —— 现状整段在 QuestToolHost.cs:170-207，收口到 Session ✅
第2步：接任务流程、拾取 + 推进任务（QuestItemPickup 只发意图，流程进 Session）✅
第3步（可选）：进门传送、死亡重生 ✅（SceneGate / PlayerController.RespawnRoutine 已有）
```

补充完成（超出原收录范围）：
- 新系统接入模板六步已按文档在 RelationshipSystem（好感度）完整跑通（见下「新系统接入模板」）
- 装备属性接入战斗：`EquipmentSystem.ApplyStatsToPlayer` 真正写回 PlayerController（ATK/DEF 加成、受击减伤）
- 灰狼掉落：`EnemyWolf` 数据驱动掉落（iron_ore），打通支线闭环

### 事件契约与事务语义

现有事件清单 + 增补：

| 事件 | 触发源 | 监听方 |
|---|---|---|
| OnQuestStateChanged(questId, state) | QuestSystem | QuestPanel |
| OnGoldChanged(gold) | 钱包（现暂在 QuestSystem） | HUD（改订阅，删轮询） |
| OnInventoryChanged() | InventorySystem | InventoryPanel |
| OnEquipmentChanged() | EquipmentSystem | InventoryPanel |
| OnPlayerHpChanged(hp, maxHp) ★新增 | PlayerController | HUD |
| OnSaveComplete / OnLoadComplete | SaveSystem | 存档提示 |

```text
补充约定：
  · 订阅用方法组；面板若会被重建，则在 OnDestroy 退订（防重复订阅）
  · 一次业务操作可能触发多个事件（领奖 = 背包 + 金币 + 任务）：
    刷新合并到帧末执行一次，禁止同帧多次全量 Destroy + 重建（对应 §15 已知问题 #2）
  · UI 读状态一律走事件；GameContextProvider 只读快照是给 AI 的例外
```

### 现状差距清单（架构落地 = 清空此表）✅ 已于 2026-09-05 全部清空

| # | 现状 | 目标态 | 状态 |
|---|---|---|---|
| 1 | `InventoryPanel.OnEquip/OnUse` 直调 `equipment.Equip`、`player.Heal`、`inventory.Remove` | 面板只发"装备/使用"意图，由入口转发 | ✅ 完成：按钮只发意图 → `UIManager.Request*` → GameSession |
| 2 | `QuestPanel` 反射读私有 `questStates`（QuestPanel.cs:59-63） | `QuestSystem.GetAllQuestIds()`，删反射 | ✅ 完成：加 `GetAllQuestIds()`，删反射 |
| 3 | HUD 由 `UIManager.Update` 每帧轮询金币/HP | HUD 订阅事件；PlayerController 补 HP 事件 | ✅ 完成：`OnPlayerHpChanged`/`OnGoldChanged` 事件订阅，Update 只剩快捷键 |
| 4 | `QuestToolHost` 整段实现领奖事务（编排混在 Dialogue 目录） | 薄适配器：翻译工具调用 → 调 Session → 组回复 | ✅ 完成：QuestToolHost 仅翻译，流程进 GameSession |
| 5 | 金币与扣物/发奖逻辑住在 `QuestSystem.ClaimReward` | 抽 `RewardService`（交易系统前置），QuestSystem 只留状态机 | ✅ 完成：RewardService 钱包+交易原语 |
| 6 | 读档链路断：`LoadAndApply` 无调用点；`Save()` 未带 player → HP 恒存 100 | 启动流程接 LoadAndApply；存档调用统一带 player | ✅ 完成：GameSession 启动读档 + HP 恢复 |
| 7 | 场景对象 `FindObjectOfType` 找系统，有时序风险 | 推广 `sceneLoaded` 广播（沿用 UIManager 模式），引用注入 | ⚠ 部分：AppRoot 系统已 ByRef；QuestItemPickup/GameContextProvider 仍 Find（已知可选项） |
| 8 | 内容硬编码：`moon_herb` / `main_001` / `linFavor=5` | 下沉 QuestData / NpcProfile 数据资产 | ✅ 完成：主任务/目标物品走 QuestData，好感走 RelationshipSystem+NpcProfile |

### 内容扩展协议（加任务 / 加 NPC 不加代码）

```text
QuestData 增加 objectiveType 维度（当前只有 Collect）：
  · Collect：持有 / 上缴目标物品（沿用 TargetItemId / TargetCount）
  · Kill / Talk / Reach：先定义枚举；出现新目标类型才写新推进 handler

NpcProfile（npcId / displayName / 连接配置 / 可用工具白名单 / 初始好感）
  → ScriptableObject 资产，替代散在场景里的配置

规则：加内容 = 加资产文件；只有新目标类型 / 新交互方式才写代码
```

### 新系统接入模板（六步）

```text
以"好感度 RelationshipSystem"为例：
  ① 建系统类：只管 favor 数据 + 规则 + 事件（OnFavorChanged）
  ② 挂 AppRoot：GameBootstrapper.CreateAppRoot 按依赖顺序添加
  ③ 存档：实现 GetSaveData() / LoadFromData()，SaveSystem 收集与恢复（解锁 relationships 字段）
  ④ 定义事件，UI 订阅
  ⑤ AI 可改它 → 注册白名单工具（游戏侧校验，模式见 QuestToolHost）
  ⑥ 接入 GameContextProvider 快照（替换写死的 linFavor=5）

任何新系统一律走此六步，不许另起模式。

---

## 7. 任务、背包和装备数据

### 任务状态

```text
Available → Accepted → Completed → Rewarded
```

### 主线任务

```json
{
  "questId": "main_001",
  "title": "寻找月光药草",
  "state": "Available",
  "targetItemId": "moon_herb",
  "targetCount": 1,
  "rewardGold": 50,
  "rewardItemId": "healing_potion",
  "rewardItemCount": 1
}
```

### 物品

```text
wood_sword       木剑，3 攻击
iron_sword       铁剑，8 攻击
cloth_armor      布衣，2 防御
healing_potion   治疗药水，恢复 30 HP
moon_herb        月光药草，主线任务物品
iron_ore         铁矿，支线任务物品
```

### 完成任务的确定性流程

```csharp
if (quest.State != QuestState.Accepted) return Fail("任务未接受");
if (!inventory.Has("moon_herb", 1)) return Fail("缺少月光药草");

inventory.Remove("moon_herb", 1);
player.Gold += 50;
inventory.Add("healing_potion", 1);
quest.State = QuestState.Rewarded;
saveSystem.Save();
return Success();
```

模型不能直接改变上述任何字段。

---

## 8. AIBot 接入

### 8.1 推荐模式

第一版优先用 Server 模式：

```text
团结引擎 → AIBot.Server → LLM + JSON 存储
```

模型 API Key 放在 Server，不放到团结工程。

> ✅ 最终实现 = **Server 模式（game 工具回传）**（2026-09-05 落地并 LLM 全链验证）：
> - 早期曾改走 Local 模式（原因：当时 Server 工具仅 `simulated`，不触达真实游戏状态）；
> - 后 aibot Server 增强出 **game 工具协议**（工具 schema 由客户端经 `CollectLocalToolDescriptors` 上传、
>   Server 按 NPC 配置 `enabledToolIds` 求交、模型调用挂起回传本地真实执行），满足"工具由游戏代码真实执行"；
> - Unity 侧 `NPC_Lin.connectionProfile` 挂 `HerbalistLin_Profile`（Server 连接 + `enableGameTools` + `enabledToolIds` 4 项），
>   SDK 补齐 `AIBotConnectionProfile.enabledToolIds` 字段与占位 DTO 拷贝（见 §15 差异表）；
> - 已通过真实 LLM 全链验证：tools 4 项（get_quest_status/accept_quest/complete_quest/record_player_choice）、
>   好感变化、领奖幂等、T5 同/跨 Session 记忆。

### 8.2 Server 配置

创建 `AIBotConnectionProfile`：

```text
Server Base URL: http://127.0.0.1:5000
Game ID: fogharbor
NPC ID: herbalist_lin
Player ID: player-001
Session ID: demo-session-001
```

> 注：正式游戏使用独立 gameId `fogharbor`（配置在 `D:\Code\aibot\data\games\fogharbor\`），
> 与 aibot 仓库自带的 `default` 测试游戏相互隔离；记忆、会话、日志按 gameId 分别归档。

运行前检查：

```text
GET http://127.0.0.1:5000/api/health
GET http://127.0.0.1:5000/api/ready
```

### 8.3 林洛 NPC 配置

```json
{
  "npcId": "herbalist_lin",
  "displayName": "药师林洛",
  "persona": "温和、认真、关心生病的女儿",
  "enabledToolIds": [
    "get_quest_status",
    "accept_quest",
    "complete_quest",
    "record_player_choice"
  ],
  "fallbackReplies": [
    "抱歉，我现在有些担心女儿，没听清你的话。",
    "等你找到药草，我们再慢慢聊。"
  ]
}
```

### 8.4 游戏状态快照

只传必要状态：

```json
{
  "questId": "main_001",
  "questState": "Accepted",
  "hasMoonHerb": true,
  "gold": 20,
  "items": ["healing_potion", "iron_ore"],
  "linFavor": 5
}
```

不要传 API Key、完整存档、文件路径或设备信息。

### 8.5 工具规则

| 工具 | 游戏代码校验 |
|---|---|
| `get_quest_status` | 返回真实任务状态 |
| `accept_quest` | 任务必须是 `Available` |
| `complete_quest` | 任务必须是 `Accepted` 且拥有药草 |
| `record_player_choice` | 只能写入白名单选择 |

同一个 `requestId` 重试不得重复发放奖励。

---

## 9. 对话 UI

```text
DialoguePanel
├── NpcNameText
├── MessageText
├── InputField
├── SendButton
├── CloseButton
└── StatusText
```

绑定事件：

```csharp
agent.onToken.AddListener(panel.AppendToken);
agent.onReply.AddListener(panel.ShowReply);
agent.onError.AddListener(panel.ShowError);
agent.onBusy.AddListener(panel.ShowBusy);
```

对话状态只显示四种：

```text
Ready / Thinking / Executing / Error
```

第一版不显示 reasoning，不做语音，不做复杂对话树。

---

## 10. 存档

路径：

```text
Application.persistentDataPath/fog_harbor_save.json
```

存档至少包含：

```json
{
  "version": 1,
  "gold": 20,
  "hp": 100,
  "inventory": [
    { "itemId": "wood_sword", "count": 1 }
  ],
  "equipment": {
    "weapon": "wood_sword",
    "armor": "cloth_armor"
  },
  "quests": {
    "main_001": "Accepted"
  },
  "relationships": {
    "herbalist_lin": 5
  }
}
```

保存时使用临时文件：

```text
save.tmp → 校验 JSON → 替换 save.json
```

失败时保留旧存档，并保留一个 `save.bak`。

---

## 11. 免费资源组合

第一版统一使用低多边形风格：

| 用途 | 推荐资源 | 授权/链接 |
|---|---|---|
| 地面、树木、岩石 | [Kenney Nature Kit](https://kenney.nl/assets/nature-kit) | 页面标注 CC0 |
| 地牢/小型场景/图标 | [Kenney Tiny Dungeon](https://kenney.nl/assets/tiny-dungeon) | 页面标注 CC0 |
| 角色、怪物、武器 | [Quaternius Ultimate RPG Pack](https://quaternius.com/packs/ultimaterpg.html) | 页面标注 CC0 |
| 移动、攻击、死亡动作 | [Quaternius Universal Animation Library](https://quaternius.com/packs/universalanimationlibrary.html) | 页面标注 CC0，120+ 动画 |
| 更多 2D/音效/UI | [OpenGameArt](https://opengameart.org/) | 逐个查看许可证 |
| 免费游戏资源筛选 | [itch.io Free Assets](https://itch.io/game-assets/free) | 逐个查看许可证 |

资源处理原则：

- 先用灰盒，系统跑通后再导入正式资源。
- 优先使用 CC0；CC-BY 必须保留署名。
- 所有资源记录在 `Assets/ThirdParty/ATTRIBUTIONS.md`。
- 不混用写实、像素和低模三种风格。

---

## 12. 开发任务清单

> 状态更新于 2026-08-30（第一轮完成 T0~T2 + UI 第一阶段；第二轮完成 T3 对话、T4 任务工具）
> 2026-09-05：T4 拾取推进补齐、T5 记忆全部通过；§6.1 架构/好感度/装备战斗/掉落等已完成（见 §6.1 与 §15）
> 2026-09 追加：**T6 体验完善**五件套已写入方案 §17（战斗反馈/目标指引/死亡体验/NPC互通/撒谎戳穿），待实现

### T0：工程验证

- [x] 创建团结引擎 3D 项目（团结引擎 1.10.1 / Tuanjie 2022.3.62t13，Built-in RP）。
- [x] 导入 AIBot 包（`com.aibot.npcagent`，`file:D:/Code/aibot/Packages/com.aibot.npcagent`）。
- [x] 创建空物体挂载 `NpcAgent`（Town.scene 的 `NPC_Lin`，含 NpcInteractable + QuestToolHost + GameContextProvider）。
- [x] 编译无错误。

### T1：灰盒场景

- [x] 创建 Boot、Town、Forest（`Assets/Scenes/`，已加入 Build Settings，Boot 为入口）。
- [x] 玩家可移动（`PlayerController`：CharacterController + WASD + 攻击 + 受击/回血）。
- [x] 玩家可切换场景（`SceneGate`：按 E 在 Town ↔ Forest 切换；`GameBootstrapper`：任意场景直接 Play 也会自动创建 AppRoot 全部系统）。
- [x] 玩家可按 E 交互（`PlayerInteractor` + `IInteractable` 接口，`CameraFollow` 相机跟随）。
- [x] 地面扩大并用草地材质（`Assets/Materials/GroundGrass.mat`，两场景统一 60×60 米）。

### T2：RPG 基础

- [x] `InventorySystem` 添加/移除/查询（6 个物品定义在 `Assets/Resources/Items/`，`ItemDatabase` 统一查询）。
- [x] `EquipmentSystem` 装备木剑和铁剑（武器/护甲双槽，替换自动退回背包，`EquipmentSaveData`）。
- [x] `QuestSystem` 实现主线状态（Available→Accepted→Completed→Rewarded，`Complete` 与 `ClaimReward` 分离，奖励只发一次；`main_001` 寻找月光药草、`side_001` 收集铁矿定义在 `Assets/Resources/Quests/`）。
- [x] `SaveSystem` 保存和读取（`fog_harbor_save.json`，tmp→校验→替换→bak 防损坏流程，损坏自动回退备份）。
- [x] UI 第一阶段（纯色块）：`UIManager`/`HUDPanel`/`InventoryPanel`/`QuestPanel`/`DialoguePanel`，B 背包 / Q 任务 / ESC 关闭；NotoSansSC 中文字体已配置为 TMP 全局回退。

### T3：对话

- [x] Server `/api/health` 正常（`fogharbor` game 的 `herbalist_lin` 可被识别）。
- [x] 林洛可回复（Server 模式与 Local 模式均验证过真人设回复）。
- [x] 对话面板可以发送和关闭（气泡/富文本 transcript 版，本轮已接入真实 AIBot）。
- [x] Server/Local 错误时显示兜底提示（fallbackReplies + 面板状态灯 Ready/Thinking/Error）。
- [x] 切 Local 模式后直连 LLM 对话正常（修复 AIBot 插件 uploadHandler bug，详见 §15）。

### T4：任务和奖励

- [x] 工具可以接受任务（`accept_quest` 由个人工具宿主执行，校验 Available）。
- [x] 拾取药草后任务目标完成（`GameSession.PickupItem` → `QuestSystem.TryCompleteOnPickup`，拾取/掉落入库自动推进 ✅）。
- [x] 工具可以完成任务（`complete_quest`：校验 Accepted + 囤积药草 → 扣物品 → 发奖励 → Rewarded）。
- [x] 奖励只发放一次（重复 `complete_quest` 被拒，金币不变）。
- [x] 任务完成后自动保存（`SaveSystem.Save()` 已在 `complete_quest` 成功后调用）。

### T5：记忆验证 ✅ 全部通过（2026-09-05，Server 模式真实 LLM 全链）

- [x] 林洛记得玩家是否接受任务。
- [x] 林洛记得玩家是否完成任务。
- [x] 切换 Session 后仍能通过 `playerId` 找到长期记忆（`memoryScope=player_npc` + 后台摘要，
      `memories/{npc}/{player}.json` 生成摘要+facts，新会话正确注入，实测：新会话问"还记得我吗"→
      "你是小勇，你答应过帮我找月光药草"）。
- [x] 重复 requestId 不重复发奖励（Server 挂起轮守卫 + 系统层状态机幂等）。

### T6：体验完善（§17 设计，待实现）

> 目标：在不改架构铁律的前提下，让「打起来有反馈、走起来有方向、死了有代价、NPC 活起来」。
> 落地顺序建议：T6.1 → T6.2 → T6.3 → T6.5 → T6.4（戳穿可先只接林洛；互通要等第二个 NPC 才完整见效）。

- [ ] T6.1 战斗反馈：受击顿帧、伤害飘字、受击闪白强化、灰狼死亡消散；打中/击杀可听音（可选静音开关）。
- [ ] T6.2 任务目标指引：HUD 显示当前追踪任务目标文案；QuestPanel 高亮追踪中任务；按场景门点做简易方向提示（Town→Forest / Forest→Town）。
- [ ] T6.3 玩家死亡体验：死亡淡出演出 + 文案；回 Town 重生并满血；扣除当前金币 10%（下限 0）；死亡后自动存档；重生点用 Town 的 PlayerSpawn。
- [ ] T6.4 撒谎/违约戳穿：空手交任务写 `player_tried_turnin_without_herb`；食言写 `player_broke_promise`；rude 走已有 `record_player_choice`；上述 flag 进 GameContextProvider 快照，林洛人设引导「记得/吐槽」。
- [ ] T6.5 NPC 互通玩家事迹：`WorldFlagSystem`（帮助林洛采药等）+ 存档；快照注入 `worldFlags`；第二 NPC（阿拓）接入后验证「林洛提过你」。

---

## 13. 给团结 AI 的执行方式

一次只让 AI 完成一个脚本或一个小功能。

### 通用提示词

```text
你正在开发《雾港小镇》团结引擎项目。

本次只完成：<一个明确任务>

约束：
1. 不修改 AIBot 插件源码。
2. 不引入新的第三方依赖。
3. 任务、背包、装备和奖励必须由游戏代码确定性控制。
4. 不使用未经确认的团结引擎专属 API。
5. 如果信息不足，先列出假设。
6. 代码必须放进 §6.1 对应的架构层：UI 只发意图不调系统；意图入口只转发不编流程；
   跨系统流程只进 GameSession；规则校验只在系统层。禁止跨层直调
   （如 Panel 直调 System 改数据、工具宿主内实现完整流程）。

请输出：
1. 完整脚本。
2. 挂载到哪个 GameObject。
3. Inspector 需要填写什么。
4. 3 个手动测试步骤。
```

### 推荐 AI 任务顺序

```text
1. PlayerController
2. PlayerInteractor
3. InventorySystem
4. QuestSystem
5. SaveSystem
6. NpcInteractable
7. DialogueBridge
8. GameContextProvider
9. accept_quest 工具
10. complete_quest 工具
```

每完成一个脚本，都先编译、运行和手动验证，再让 AI 写下一个。

---

## 14. 最终验收

```text
新建游戏
→ 与林洛对话
→ 接受任务
→ 进入森林
→ 拾取月光药草
→ 返回林洛
→ 获得 50 金币和 1 瓶药水
→ 保存
→ 重启游戏
→ 任务、背包、装备和 NPC 记忆正确
```

验收失败时，按顺序排查：

```text
编译 → 场景交互 → 本地任务 → 背包奖励 → 存档 → Server → AIBot 工具 → 记忆
```

不要在基础流程未通过前增加新 NPC、新地图或新剧情。

---

## 15. 第一轮实现进度（2026-08-30）

> 含第二轮补充：T3 对话接入（Local 模式）+ T4 任务工具接入。

### 已完成的工程文件（相对本方案的差异已标注）

```text
Assets/
├── Scenes/
│   ├── Boot.scene      ✓ 已建（AppRoot + 全部系统）
│   ├── Town.scene      ✓ 已建（Ground/Light/Player/Camera/ForestGate/NPC_Lin）
│   └── Forest.scene    ✓ 已建（Ground/Light/Player/Camera/TownReturnGate/MoonHerb 占位）
├── Scripts/
│   ├── Bootstrap/
│   │   ├── GameBootstrap.cs       ✓ 持久化 + 加载首场景
│   │   └── GameBootstrapper.cs    ★ 新增：任意场景启动自动建 AppRoot
│   ├── Player/
│   │   ├── PlayerController.cs    ✓ 移动/攻击/受击/回血/SetHp
│   │   ├── PlayerInteractor.cs    ✓ 近距离交互 + UI 提示
│   │   ├── CameraFollow.cs        ★ 新增：相机跟随
│   │   └── IInteractable.cs       ★ 新增：交互接口
│   ├── World/
│   │   └── SceneGate.cs           ★ 新增：场景入口（替代方案中未列的入口实现）
│   ├── Items/
│   │   ├── ItemData.cs            ★ 新增：物品定义 ScriptableObject
│   │   └── ItemDatabase.cs        ★ 新增：物品查询库（Resources.LoadAll）
│   ├── Inventory/InventorySystem.cs    ✓
│   ├── Equipment/EquipmentSystem.cs    ✓
│   ├── Quest/
│   │   ├── QuestState.cs          ★ 新增：状态枚举
│   │   ├── QuestData.cs           ★ 新增：任务定义 ScriptableObject
│   │   ├── QuestSystem.cs         ✓
│   │   └── RewardService.cs       ★ 新增：钱包 + 交易原语（金币/OnGoldChanged/GrantQuestReward）
│   ├── Session/GameSession.cs    ★ 新增（§6.1 流程编排层）：唯一跨系统编排点（CompleteQuest/AcceptQuest/PickupItem/Equip/Use/Drop/RecordPlayerChoice + 启动读档）
│   ├── Relationship/             ★ 新增（§6.1 六步模板示例）
│   │   ├── RelationshipSystem.cs ★ 新增：好感度系统（favor + OnFavorChanged + 白名单校验 + 存档）
│   │   └── NpcProfile.cs         ★ 新增：NPC 好感数据资产（npcId/initialFavor/白名单 choices）
│   ├── Enemy/
│   │   ├── EnemyWolf.cs          ★ 新增→增强：灰狼状态机 + 数据驱动掉落（iron_ore）
│   │   └── WolfSpawner.cs        ★ 新增：对象池刷怪器
│   ├── Save/SaveSystem.cs         ✓
│   ├── Dialogue/                  ★ 第二轮新增目录
│   │   ├── NpcInteractable.cs     ★ 新增：按 E 打开对话；首次对话前注册任务工具 + 绑定游戏快照
│   │   ├── GameContextProvider.cs ★ 新增：实现 IGameContext，提供只读游戏状态快照（§8.4；主任务/好感已下沉 QuestData/RelationshipSystem）
│   │   └── QuestToolHost.cs       ★ 新增：注册 get_quest_status / accept_quest / complete_quest / record_player_choice 四个真实工具（薄适配器 → Session）
│   └── UI/
│       ├── UIHelper.cs            ★ 新增：UI 元素工厂
│       ├── UIManager.cs           ★ 新增：Canvas + 面板管理 + 快捷键 + ShowDialogue(agent) + 意图转发（RequestEquip/Use/Drop）
│       ├── HUDPanel.cs            ★ 新增：HP/金币/交互提示（事件驱动 + 任务完成 toast）
│       ├── InventoryPanel.cs      ★ 新增：背包面板（按钮只发意图）
│       ├── QuestPanel.cs          ★ 新增：任务面板（GetAllQuestIds，无反射）
│       └── DialoguePanel.cs       ★ 新增→已升级：对话面板（气泡版 → 富文本 transcript 版，接真实 AIBot）
├── Materials/
│   └── GroundGrass.mat            ★ 新增：草地材质（Town/Forest 地板统一）
├── Resources/
│   ├── Items/                     ★ 数据：6 个 ItemData（wood_sword/iron_sword/cloth_armor/healing_potion/moon_herb/iron_ore）
│   ├── Quests/                    ★ 数据：2 个 QuestData（main_001/side_001）
│   └── NPCs/HerbalistLin_NpcProfile.asset ★ 数据：好感度 NpcProfile（initialFavor=5，白名单 polite/care_child/rude）
├── Data/NPC/HerbalistLin_Profile.asset  ★ 数据：Server 连接 Profile（NPC_Lin 挂载，enableGameTools + enabledToolIds 4 项）
├── Prefabs/Enemy_Wolf.prefab      ★ 数据：灰狼预制体（EnemyWolf 掉落默认 iron_ore 60%）
└── Codely/Fonts/                  ★ 中文字体：NotoSansSC-Regular Dynamic（重建后 TMP 全局回退；旧 Static 资产因 atlas 空洞弃用）
```

标注说明：`✓` = 方案中原有项已实现；`★ 新增` = 实现过程中新增的文件（不在原方案第 4 节结构中）。

### 与方案的实现差异

| 方案原文 | 实际实现 |
|---|---|
| `Data/Items/` 放物品资产 | 移到 `Resources/Items/`（保证 `ItemDatabase` 可运行时加载） |
| `Data/Quests/` 放任务资产 | 移到 `Resources/Quests/`（同上） |
| 场景切换仅靠 Boot 引导 | 增加 `GameBootstrapper`：从 Town/Forest 直接 Play 也会自动初始化全部系统 |
| 交互检测仅写"检测 NPC/物品/入口" | 统一走 `IInteractable` 接口 + `SceneGate` 实现场景切换 |
| UI 面板（§9） | 先做纯色块版本；`DialoguePanel` 已升级为富文本 transcript 版并接入真实 AIBot |
| 中文 UI 文本 | NotoSansSC 已配置为 TMP 全局回退（`Assets/Codely/Fonts/`） |
| 玩家 HP 存取 | `PlayerController` 已加 `Heal()`/`SetHp()`，`SaveSystem` 已序列化 ✅ 恢复已挂钩（GameSession 启动读档 + `SetHp`） |
| §8.1 第一版优先 Server 模式 | ✅ **最终走 Server 模式（game 工具回传）**：早期曾切 Local（当时 Server 工具仅 simulated），Server 增强 game 工具协议后切回。`NPC_Lin.connectionProfile` 挂 `HerbalistLin_Profile`（enableGameTools + enabledToolIds：get_quest_status/accept_quest/complete_quest/record_player_choice）。SDK 补两行：`AIBotConnectionProfile` 加 `enabledToolIds` 字段；`NpcAgent.LoadConfig` 占位 DTO 拷贝。工具 schema 客户端上传 → Server 与 NPC 配置求交 → 挂起回传本地执行 |
| AIBot 插件（不改源码原则） | **违反两次（必要修复）**：① `UnityWebRequestBackend.uploadHandler` null bug（早期 Local 修复）② `AIBotConnectionProfile`/`NpcAgent` 加 `enabledToolIds` 上传链路（game 工具必需）。均在 `Packages/com.aibot.npcagent`（file: 本地包，升级会丢修复） |
| §8.3 `enabledToolIds` 含 `record_player_choice` | `record_player_choice` 尚未实现（Local 模式启用未注册工具会打警告，不影响对话） |
| AIBot 插件（不改源码原则） | **违反一次（必要修复）**：`com.aibot.npcagent/Runtime/Unity/UnityWebRequestBackend.cs` 的 `UnityWebRequest(url,"POST",handler,null)` uploadHandler 传 null，序列化 body 不上传 → 网关报 "Model is not supported"。已加 `req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body))` 修复（只改一行，不碰逻辑） |

### 已验证的测试点

```text
第一轮（T0~T2 + UI）：
1. Boot → 自动跳转 Town，AppRoot DontDestroyOnLoad 生效
2. InventorySystem: Add/Has/GetCount/Remove 全部通过；6 物品定义加载 ✓
3. EquipmentSystem: 装备/替换/脱下；旧装备自动退回背包；非装备物品拒绝 ✓
4. QuestSystem: 接受→拾取→完成→领奖；重复接受/重复领奖被拒绝；药水+50金正确发放 ✓
5. SaveSystem: 存档 JSON 完整；读档恢复背包/装备/任务/金币；二次存档生成 .bak；损坏回退备份 ✓
6. UI: HUD(HP/金币/提示) / 背包(B) / 任务(Q) / 对话面板均创建成功；中文字体正常 ✓
7. 从 Town.scene 直接 Play 也能看到 HUD 并按 B 打开背包 ✓（修复：自动引导）

第二轮（T3~T4）：
8. NPC_Lin 靠近检测（"按 E 与林洛对话"提示 + SphereCollider 触发器）✓
9. 对话面板打开/发送/显示消息 ✓（修复 Mask→RectMask2D 后真实截图可见）
10. Server 模式对话成功（fogharbor game + herbalist_lin 人设回复）✓
11. Local 模式直连 LLM 成功（修复插件 uploadHandler bug 后）✓
12. 任务工具直接调用全通过：get_quest_status / accept_quest / complete_quest；
    缺药草拒绝、重复领奖拒绝（金币不变）、发奖后自动存档 ✓

第三轮（2026-09-05：§6.1 架构 + Server 回归 + T5）：
13. §6.1 差距 1-7 全清（GameSession/RewardService/事件化/去反射），差距 8 好感度系统六步模板跑通 ✓
14. 装备属性真实接入战斗：木剑 ATK+3、铁剑 +8、布衣 DEF+2 减伤（8 伤害→6），存档/读档恢复 ✓
15. 灰狼掉落 iron_ore（60%）→ 支线 side_001 收集 3 铁矿自动 Completed ✓
16. 任务完成即时提示（HUD toast，走 OnQuestStateChanged 事件）✓
17. Server 模式 LLM 全链回归：record_player_choice 两端放行后 4 工具全通、好感变化、领奖幂等 ✓
18. T5 记忆：同 session 复述全程 + 跨 Session（player_npc 摘要）认出"小勇/承诺" ✓
19. 完整验收回归：新游戏→打狼→采药→领奖→存档→真重启读档全恢复（含 HP）✓
20. TMP 中文 NRE 修复：NotoSansSC 重建为 Dynamic 多图集 + 预烘字符（见已知问题 5）✓
```

### 已知问题（2026-08-30 走查 + 2026-09-05 更新）

1. **QuestPanel 用反射读取 QuestSystem 私有字段**（`Scripts/UI/QuestPanel.cs:59-63`）
   - ✅ 已修复（2026-09-05）：`QuestSystem.GetAllQuestIds()` 公开接口替代反射，`QuestPanel.Refresh` 正常调用。

2. **背包/任务面板列表每帧刷新时全量 Destroy + 重建**（`InventoryPanel.Refresh` / `QuestPanel.Refresh`）
   - 未处理（文档标注 UI2 美术阶段重构；MVP 物品/任务量小，无感知）。→ §6.1 事件事务语义

3. **`NpcAgent` 的 `gameContextProvider` 在运行时被脚本赋值**（`NpcInteractable.Interact()` 中）
   - 保留：运行时 AddComponent + 赋值（未改为 Inspector 拖拽）。功能正常。（→ §6.1 差距清单 #7 部分保留项）

4. **AIBot 插件改动属外部包修改**
   - `Packages/com.aibot.npcagent`（file: 本地包）：① `UnityWebRequestBackend.uploadHandler` bug 修复
     ② `AIBotConnectionProfile.enabledToolIds` + `NpcAgent.LoadConfig` 占位 DTO 拷贝（game 工具上传链路）。
   - 升级/重装该包会丢失修复，需留意。

5. **TMP 中文运行时加字 NRE（2026-09-05 已修复）**
   - 现象：toast 首次出现未烘焙字符（如「标」「成」）时 `TMP_FontAsset.SetupNewAtlasTexture` 抛 NullReferenceException。
   - 根因：旧 `NotoSansSC-Regular SDF`（Static）`m_AtlasTextures` 数组第 2 页为 NULL（glyph 全在页 0），
     运行时动态加字对空页索引越界。
   - 修复：用 `TMP_FontAsset.CreateFontAsset(otf, 90, 8, GlyphRenderMode.SDFAA, 2048, 2048, Dynamic, 多图集)` 重建
     `NotoSansSC-Regular Dynamic.asset`，预烘 154 常用 UI 字符，`TMP_Settings` 全局回退 + `LiberationSans` 局部回退改指新资产。
   - 备注：旧 Static 资产保留未删（已无回退指向）；Tuanjie TMP 3.0.9 的 `GlyphRenderMode` 在
     `UnityEngine.TextCore.LowLevel` 命名空间。

### 下一轮待做（已退役 → 统一由 §6.1「现状差距清单」管理）

> 2026-09-05 更新：下表旧项 C/D/E/F 均已验收或闭环，仅 G（UI2 美术）仍是独立待办。
> 体验完善五件套（战斗反馈/目标指引/死亡/互通/戳穿）见 **§17、T6**，不在本退役表内。

| 旧清单项 | 现状与归宿 |
|---|---|
| A 加 GameSession 流程编排层 | ✅ 已落地（`Scripts/Session/GameSession.cs`） |
| B 拾取系统（拾取 + 推进任务） | ✅ 已落地（`GameSession.PickupItem` + `TryCompleteOnPickup`） |
| C 敌人系统（灰狼接场景） | ✅ 已落地 + 掉落（`EnemyWolf` 数据驱动 iron_ore） |
| D T5 AIBot 记忆验证 | ✅ 全部通过（同/跨 Session、幂等，见 §12 T5） |
| E record_player_choice 工具 | ✅ 已落地（好感度白名单工具，两端 enabledToolIds 放行） |
| F UI 已知问题修复（反射/重建） | ✅ 反射已修；列表重建留 UI2（见已知问题 2） |
| G UI 2 正式美术素材 | 仍待办（美术替换，与架构无关） |
| H 体验完善五件套 | → **§17 + §12 T6**（战斗反馈 / 目标指引 / 死亡体验 / NPC 互通 / 撒谎戳穿） |

---

## 16. Lua / xLua 学习沙盒（LuaLab，2026-09-04 追加）

> 目标：以本项目为载体练习 Lua 与 xLua 热更技能，**不改造主游戏**。
> 原则：练习与游戏用目录/场景彻底隔离 —— 练完删掉 `LuaLab` 相关文件，游戏还原如初。
> 本沙盒是"学习区"，不是 6.1 架构的一部分；若将来确定把行为正式 Lua 化，先在 §6.1 补一节"行为脚本层"取得架构名分，不许先斩后奏。

### 16.1 设计原则

```text
· 主游戏（Town/Forest/Quest/AIBot/正式系统）一行不碰，全部保持 C#
· 练习对象 = LuaLab 场景里的"新增物"，可自由碰 LuaEnvManager 与 Unity 基础 API
· 用 Lua"新增"行为变体，不"改写"已跑通的 EnemyWolf（避免回归，且演示效果更强）
· 一条管线打通：C# 壳 + .lua 行为 → 热替换 → 行为即时变，零重编译
```

### 16.2 目录布局

```text
Assets/Scripts/LuaLab/
  ├── LuaEnvManager.cs     全局唯一 LuaEnv；封装 require / 热重载（练习核心）
  ├── LuaBehaviour.cs      通用壳：挂任意 GameObject + 填 .lua 名，
  │                        每帧调 Lua 的 update(self, dt)；把能力方法暴露给 Lua 调
  └── LuaProbe_Color.cs    (可选)对象 A 用，纯视觉演示
Assets/StreamingAssets/LuaLab/    .lua 文件外置，改完即替换（最像热更）
  ├── color_beacon.lua
  ├── patrol_ai.lua
  └── damage_formula.lua
Assets/Scenes/LuaLab.unity        独立练习场景，不进 Build Settings 主流程
```

关键设计：**`LuaBehaviour` 通用壳** —— Inspector 填 `luaScriptName`，负责加载 Lua、
每帧喂 `self/dt`、暴露能力方法；以后练新行为 = 写一个 .lua + 挂壳配名字，C# 几乎不再写。

### 16.3 练习对象（由易到难）

| 对象 | 场景里是什么 | C# 壳做什么 | .lua 做什么 | 练到的 xLua 点 |
|---|---|---|---|---|
| A. 变色信标 | 一个 Cube | 每帧把 Renderer 交给 Lua；提供 SetColor() | 每 2 秒变一种颜色 + Log | LuaEnv / require / Lua 调 C# |
| **B. 巡逻假人（主打）** | 胶囊假人 + CharacterController | 提供 MoveTo/SetSpeed/读取玩家距离 | 状态机：patrol(两点往返) → 玩家近 → chase | 双向互调 + 状态机在 Lua + 委托缓存/热重载 |
| C. 伤害公式桩（可选） | 木桩 + 按 E | 调 Lua 函数并显示返回值 | calc(attack, luck) → 伤害数值 | LuaFunction.Call 返回值/多返回值 |

### 16.4 验收标准（热替换"秀肌肉"时刻）

```text
进 Play → 假人按 Lua 巡逻 → 改 patrol_ai.lua 的巡逻距离/索敌范围
→ 触发热重载 → 假人走位立刻变化，游戏进程不重启、代码不重编译
```

> ⚠ 新手必踩的坑：C# 侧若把 Lua 函数缓存成委托字段，热重载
> （`package.loaded['patrol_ai'] = nil` + `require`）后必须**重新拉取委托并重新绑定**，
> 否则改的 Lua 不生效、游戏纹丝不动。热重载验收必须包含"重绑"这一步。

### 16.5 明确不碰的清单（守护 §6.1）

```text
✗ 系统层（Quest/Inventory/Equipment/SaveSystem）—— 铁律：C#，不脚本化
✗ 正式敌人 EnemyWolf / WolfSpawner（已跑通，B 是新增假人，不碰它）
✗ QuestToolHost / GameContextProvider / AIBot —— Lua 练习与之无关
✗ UIManager 及正式面板 —— LuaLab 自建最小文本显示即可
✗ GameSession —— 系统编排层，不进练习范围
```

### 16.6 落地步骤（每步独立验证）

```text
第0步  验证 xLua 能否在团结引擎编译/运行（最高风险，先做这个）
第1步  LuaEnvManager + hello.lua：控制台出现 Hello from xLua → 环境通
第2步  对象 A 变色信标：Lua 调 C# SetColor → 双向连通
第3步  对象 B 巡逻假人：Lua 状态机跑起来
第4步  热替换验收（§16.4）：改 lua → 重载 → 行为即时变
第5步  （可选）对象 C 伤害公式桩
```

### 16.7 技术风险与对策

| 风险 | 影响 | 对策 |
|---|---|---|
| xLua 与团结引擎兼容性 | 最高，编译可能不过 | 第0步先单独验证；xLua 上游长期未大维护，团结(Tuanjie 2022.3)可能需要社区 fork 或源码自编译 |
| 平台兼容 | Editor(mono) 大概率可跑；**Android/IL2CPP 打包可能失败** | 练习阶段只跑 Editor；打包兼容性记账，到打包期再验（风险只是推迟，没消失） |
| 委托类型 codegen | `Global.Get<委托>` 若该委托类型没标 `[CSharpCallLua]` 并 Generate Code，运行时报错（不是编译报错） | 首次配置跑通 XLua 代码生成，不用反射模式糊弄 |
| 热重载不生效 | 改了 Lua 没反应 | 重载后重新拉取委托并重绑（§16.4 警示） |
| 每帧 Global.Get 查找 | 性能反模式 | 加载时取一次缓存成字段，每帧只 Invoke 缓存 |
| 练习污染主架构 | 悄悄给 6.1 开口子 | 全部关进 LuaLab；真 Lua 化先补架构名分（见节首原则） |

> 注：`XLua.Hotfix`（运行时给现有 C# 方法打补丁）需要构建期注入，与"Lua 驱动 + 重载脚本"
> 是两套机制，**不进本沙盒核心练习**，以后当进阶专题单独玩。

---

## 17. 体验完善设计（2026-09 追加）

> 前提：MVP 主链路（T0～T5）已跑通；本章只补「玩起来」和「NPC 活起来」，
> **不改 §6.1 铁律**：表现层纯展示，flag 由游戏代码写，模型只读快照、不能直接改 flag。
>
> 对应任务清单：§12 T6。

### 17.1 战斗反馈

**目标**：攻击命中立刻有「打中了」的感觉；死亡不再只是物体消失。

| 反馈 | 规格 | 观察源 |
|---|---|---|
| 受击顿帧 hitstop | 命中时 `Time.timeScale=0.03~0.05`，持续 0.05s，再恢复 | 玩家攻击命中 / 灰狼咬中玩家 |
| 伤害飘字 | 世界坐标弹出数字，上飘 0.6s 淡出；暴击/装备加成可同色区分 | `OnEnemyDamaged(dmg, worldPos)` / 玩家受击 |
| 受击闪白 | 已有灰狼闪色，保留并保证 1 次攻击只闪 1 次 | EnemyWolf |
| 死亡消散 | 缩放/下沉 + 半透明渐隐 0.4s 后回收对象池；禁止瞬间 SetActive(false) | EnemyWolf.OnDeath |
| 击杀提示（可选） | HUD toast「灰狼被击败」；可关 | OnEnemyDied |

**事件契约（View 只订阅，不反向改数据）**：

```text
OnEnemyDamaged(int amount, Vector3 worldPos)   EnemyWolf / 战斗命中处 → CombatFeedback
OnEnemyDied(EnemyWolf wolf)                    EnemyWolf → CombatFeedback / WolfSpawner
OnPlayerDamaged(int effectiveDamage)           PlayerController → CombatFeedback（飘字可选）
```

**架构落点**：

- 新脚本 `Scripts/Combat/CombatFeedback.cs`：纯 View（对象池飘字 + 顿帧 + 死亡消散协程），挂场景或 AppRoot 均可，**不进 GameSession**。
- `EnemyWolf.TakeDamage` / 死亡路径只负责「发事件 + 触发表现协程入口」，掉落仍走现有 `GameSession.PickupItem`。
- 数值（顿帧时长、飘字存活）全部 SerializeField，不写死魔法数。

**验收**：

1. 木剑打灰狼：有顿帧 + 飘「8」左右数字 + 闪白。
2. 灰狼死亡：渐隐后再从 Spawner 复用，不闪一下就没。
3. 玩家挨咬：血条下降同时可选飘字，不打断移动输入超过顿帧时长。

### 17.2 任务目标指引

**目标**：玩家接任务后知道「现在该去哪」，森林里不迷路。

**第一版只做两层，不做小地图**：

| 层 | 内容 | 说明 |
|---|---|---|
| L1 文案 | HUD 固定一行「当前目标：…」 | 来自追踪任务的目标描述 / 进度 |
| L2 门点方向 | 需要换场景时，屏幕边缘或门点上方显示简易指示（箭头/光柱/文字「森林入口 →」） | 只对 `SceneGate` 生效，不做逐物品寻路 |

**数据扩展（QuestData，加内容不动代码逻辑）**：

```text
objectiveHint        例："在森林深处寻找月光药草（0/1）"
targetScene          例：Forest / Town / 空=当前场景
hintGateId           例：ForestGate（可选，驱动 L2）
```

**系统职责**：

```text
QuestSystem
  + TrackedQuestId（默认主线；Accept 时设为该任务）
  + GetTrackedObjectiveText() → 「标题 + hint + 进度」

QuestTrackerView（UI 抽屉，订阅 OnQuestStateChanged / OnInventoryChanged）
  刷新 HUD 目标行；目标场景 ≠ 当前场景时显示门点提示

SceneGate
  已是 IInteractable；L2 可选：Gate 方向指示由 TrackerView 按目标场景找对应 Gate
```

**验收**：

1. 接主线后 HUD 出现「寻找月光药草」。
2. 站在 Town 未进森林时，有「前往森林」类提示。
3. 拾取药草后目标文案变为「回去找林洛」（或 hint 更新）。
4. 任务 Rewarded 后目标行隐藏或显示「无追踪任务」。

### 17.3 玩家死亡体验

**现状**（`PlayerController.RespawnRoutine`）：toast → 1.5s → LoadScene(Town)，无惩罚、无演出、重生点未显式绑定。

**目标态**：

```text
HP ≤ 0
  → 禁止输入 + 死亡淡出（黑屏 0.4~0.6s）+ 文案「你在雾里倒下了…」
  → GameSession.RespawnPlayer()：
       扣金币 = max(0, floor(gold * 0.10))     ← RewardService 原语，规则在系统层
       加载 Town
       绑定 PlayerSpawn 满血恢复
       Save
  → HUD toast「你在镇口醒来，遗失了 N 金币」
```

**架构落点**：

| 项 | 归属 | 约束 |
|---|---|---|
| 死亡判定 | PlayerController | `currentHp<=0` 时只发 `OnPlayerDied`，不直接 LoadScene（改掉现在直接开协程切场景） |
| 编排 | GameSession.RespawnPlayer | 调 RewardService 扣金 → 加载场景 → SetHp(max) → Save |
| 扣金规则 | RewardService.DenyGold(percent) 或 TrySpend | Session 不写 `if` 业务分支以外的数值拼装；比例常量可配置 |
| 演出 | UI（淡出遮罩）+ CombatFeedback 或 DeathFadeView | 纯表现 |

**刻意不做**：尸体掉落装备、永久死亡、复杂跑尸。轻惩罚即可，避免打断 20～30 分钟节奏。

**验收**：

1. 被狼咬死 → 黑屏文案 → 回 Town 满血。
2. 死前金币 100 → 死后 90；金币 5 → 死后 0（不出现负数）。
3. 死亡扣金后存档，重启读档金币一致。
4. 死亡不会丢任务/背包/装备。

### 17.4 NPC 之间会互相提玩家

**目标**：玩家帮过 A，再和 B 聊时 B 会提到；记忆从「单机 NPC」变成「镇子里的事」。

**机制：世界旗标 WorldFlagSystem（游戏侧权威）**

```text
世界旗标（跨 NPC 共享，进存档 worldFlags）
  world_helped_lin_find_herb     主线领奖成功时写入
  world_helped_lin_before        帮助类旗标合并视图（可派生）
  （预留）world_helped_tuo_smith / world_angered_wolf_pack ...

规则：
  · 只有 GameSession 在确定性流程成功点写入（领奖、关键选择）
  · 模型 / 工具不能直接写 worldFlags；record_player_choice 只写 Relationship 白名单
  · GameContextProvider 快照注入：
      "worldFlags": ["world_helped_lin_find_herb"]
      "otherNpcNotes": "林洛记得你帮她采过药。"   ← 由数据模板生成，不是 LLM 编
```

**呈现方式（两档，先做档 A）**：

| 档 | 做法 | 成本 |
|---|---|---|
| A 快照引导（推荐先做） | 阿拓的 snapshot 含 worldFlags + 简短中文 note；其 persona/系统提示写「若 note 非空，可在自然对话中提及，勿生硬点名」 | 几乎零新对话 UI |
| B 工具（可选） | 注册只读工具 `get_town_rumors`，NPC 主动「打听」 | 依赖 Server 工具白名单 |

**第二 NPC 接入时必配**：

```text
NpcProfile / 连接配置：
  npcId=blacksmith_tuo
  可读 worldFlags 白名单（只读，不写）
  mentionPolicy：有 world_helped_lin_find_herb → 允许提及林洛
```

**验收**：

1. 先完成林洛主线领奖 → worldFlags 出现在存档 JSON。
2. 与阿拓对话，不主动提林洛时，阿拓有机会说出「听说你帮林药师找了药草」类语句。
3. 新开角色（无旗标）时，阿拓不会无中生有提「帮过林洛」。
4. 旗标只在领奖成功后出现；失败/未完成不出现。

### 17.5 撒谎 / 违约可被戳穿

**目标**：玩家「空手交差」「答应了却摆烂」「态度恶劣」会被记住，对话里有代价感——仍是轻量，不做复杂道德系统。

**游戏侧写 flag（模型只读）**：

| Flag | 写入时机 | 写入点 |
|---|---|---|
| `player_tried_turnin_without_herb` | `complete_quest` 校验失败且原因是「已 Accepted 但缺目标物品」 | QuestToolHost 失败分支 → 经 Session 记录 → WorldFlag/Relationship |
| `player_broke_promise` | ① 玩家明确选择「我做不了/反悔」（record_player_choice 白名单 `break_promise`）② 可选：Accepted 后现实时间超过阈值仍无进展再对话（阈值可配置，默认关） | RelationshipSystem + WorldFlag |
| `player_was_rude` | 已有 `record_player_choice(rude)` | RelationshipSystem（已存在） |

**快照字段（GameContextProvider 扩展）**：

```json
{
  "questState": "Accepted",
  "hasQuestTarget": false,
  "playerFlags": [
    "player_tried_turnin_without_herb",
    "player_was_rude"
  ],
  "linFavor": 2
}
```

**林洛人设引导（Server persona / 系统提示，不写死台词树）**：

```text
若 playerFlags 含 player_tried_turnin_without_herb：
  可温和指出「你空手回来过」，但不要辱骂；仍以任务推进为主。
若 player_broke_promise 或 favor 很低：
  语气变谨慎，可拒绝额外支线闲聊；不阻断主任务完成（游戏校验通过即可交）。
若 player_was_polite：
  可保持现在的温和语气。
```

**硬约束**：

```text
· 模型不能凭对话「自己判定」玩家撒谎并改任务；必须游戏已写入 flag。
· 戳穿只影响语气与可选支线态度，不直接改金币/物品/任务状态。
· 连续 fail 的 complete_quest 不重复堆叠同一 flag（幂等 Set，不是 List++）。
```

**验收**：

1. 接任务后空手对话要求交差 → 工具失败 → flag 写入 → 林洛后续可提「药草呢？」。
2. 选择 rude → favor 下降，语气变差，但不导致任务无法完成。
3. 真正交上药草领奖后，可另写 `player_returned_with_herb`，戳穿类对话应减弱（persona 引导）。
4. 存档重启后 flags 仍在，跨 Session 记忆与 flag 一致。

### 17.6 架构与数据落点汇总

```text
新增
  Scripts/Combat/CombatFeedback.cs      View：顿帧/飘字/消散
  Scripts/UI/QuestTrackerView.cs        View：目标行 + 门点提示
  Scripts/World/WorldFlagSystem.cs      系统：跨 NPC 旗标 + 存档
  （可选）Scripts/UI/DeathFadeView.cs   View：死亡淡出

修改（只加不拆层）
  EnemyWolf / PlayerController          补战斗/死亡事件
  GameSession                           RespawnPlayer；领奖成功写 world flag
  QuestToolHost                         complete_quest 失败原因区分并记 flag
  GameContextProvider                   worldFlags / playerFlags / 提示 note
  QuestData                             objectiveHint / targetScene / hintGateId
  QuestSystem                           TrackedQuestId + 目标文案查询
  RewardService                         扣金原语（死亡惩罚用）
  SaveSystem                            新增 worldFlags、playerFlags 字段（version+1 兼容）

不改
  AIBot 插件内部逻辑
  §6.1 三层抽屉与铁律
  模型不可直改任务/背包/装备/flag
```

### 17.7 落地顺序与依赖

```text
T6.1 战斗反馈          无依赖，纯表现，最先做
T6.2 目标指引          依赖 QuestData 字段扩展
T6.3 死亡体验          依赖 RewardService 扣金 + Session 编排
T6.5 世界旗标          可先做旗标与存档；完整「互提」等阿拓
T6.4 撒谎/违约戳穿     依赖旗标/Relationship + QuestToolHost 失败分支
```

一次只做一小项，每项按 §13 通用提示词约束执行，做完手动验收再进入下一项。
