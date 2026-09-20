# 雾港小镇（FogHarbor）

一个基于**团结引擎（Tuanjie Engine）**的轻量单机 RPG 演示项目，核心目标是验证「**AIBot 驱动的 NPC 对话 / 发任务 / 发奖励 / 长期记忆**」在真实游戏循环中的落地方式。

玩家在村镇「雾港」与 NPC 对话接取任务，前往森林采集月光药草、清剿灰狼，回镇交付领取奖励。NPC 的对话由大语言模型（LLM）实时驱动，任务的「能不能做」由游戏代码裁决（LLM 只负责表达与决策，游戏负责校验与执行）。

---

## 引擎与依赖

| 项 | 版本 / 说明 |
| --- | --- |
| 引擎 | 团结引擎 **1.10.1**（内核 Unity `2022.3.62t13`） |
| 渲染管线 | **URP** `14.2.0-t1`（Linear 色彩空间，High Fidelity 资产） |
| 中文文字 | TextMeshPro `3.0.9` + Noto Sans SC（动态字体，全局回退） |
| 场景扩展名 | `.scene`（团结引擎格式，非 Unity 的 `.unity`） |
| 命名空间 | `FogHarbor.*` |

### ⚠️ 外部依赖（未随仓库分发）

`Packages/manifest.json` 中有两项依赖指向**本机绝对路径 / 外部工程**，直接克隆无法开箱运行，需要自行准备：

1. **AIBot NPC Agent SDK** — `com.aibot.npcagent`，来自独立工程 `D:/Code/aibot`（同时也是 AIBot Server 所在）。
   路径为本机绝对路径，换机器需改 `manifest.json` 指向你本地的 SDK。
2. **TJGenerators 生图工具** — `cn.tuanjie.ai.generators`，指向 Codely CLI 的本地扩展目录（已被 `.gitignore` 忽略）。

此外，NPC 对话当前运行在 **Server 模式**：需要另行启动 `AIBot.Server`（默认 `127.0.0.1:5000`），并通过环境变量注入 LLM 密钥（`AIBOT_LLM_KEY`）。Unity 侧通过 Connection Profile 声明可用工具集。

---

## 快速开始

1. 用**团结引擎 1.10.1**打开 `rpg/` 目录（该目录才是 Unity 工程根，仓库根不是）。
2. 准备好上述 AIBot SDK 与 Server，并修正 `rpg/Packages/manifest.json` 中的本地依赖路径。
3. 启动 AIBot Server 并注入 LLM 密钥。
4. 打开并运行构建列表首个场景 `Assets/Scenes/Boot.scene`，然后进入 `Town`。

> 存档为单槽位 JSON，位于 Unity 的持久化数据目录；重新开始请清空存档。

---

## 场景与玩法流程

构建列表（Build Settings）：

| 顺序 | 场景 | 内容 |
| --- | --- | --- |
| 1 | `Boot` | 引导场景，挂载 `AppRoot`（各系统单例 + 跨场景常驻），内嵌 `GameUI` 实例 |
| 2 | `Town` | 村镇：主街三栋建筑（铁匠铺 / 石屋 / 药师小屋）、重铺石板路网、外圈林带环绕的 120×120 大地图、森林入口 |
| 3 | `Forest` | 森林：月光药草、铁矿、灰狼刷新区、石板小径与成片林木、地标立石 |

主流程：

```text
进入 Town → 靠近林洛按 E 对话（LLM）→ 接受任务
  → 进入 Forest → 击败/绕过灰狼 → 采集 moon_herb
  → 返回 Town → 再次对话交付 → 游戏校验并发奖（金币 / 药水）→ 自动存档
```

---

## 已实现功能

- **玩家控制**：走 / 跑（Shift）、跳跃（含土狼时间与输入缓冲）、采集动作定身与 Gather 动画；鼠标视角跟随。
- **战斗动作**：拔剑 / 收剑过渡（Pro Sword and Shield 动作包）、三段攻击连段、攻击仅驱动上半身（`PlayerUpperBody.mask`，行走攻击两不误）、手持武器外观随装备切换（`PlayerWeaponVisual`）。
- **输入管理**：`GameInput` 统一屏蔽闸门——面板打开时屏蔽交互 / 攻击键，防止误触发；`GameCursor` 管理游标。
- **交互**：`E` 键交互（`IInteractable`），采集拾取、对话触发。
- **AIBot 对话**：流式 LLM 对话，NPC 具备游戏状态快照（剧情阶段、任务进度、目标物、好感度），字段与 Server 端模拟状态对齐；对话框支持 NPC 头像。
- **任务系统**：主线「寻找月光药草」+ 支线「收集铁矿」，状态机推进、拾取自动推进目标、发奖幂等。
- **AIBot 工具链**：`get_quest_status` / `accept_quest` / `complete_quest` / `record_player_choice`，由游戏端真实执行。
- **好感度 / 关系**：`RelationshipSystem` 记录玩家选择、按白名单增减好感，随存档持久化，跨 Session 长期记忆（`player_npc` 摘要）。
- **背包 / 装备**：物品数量、拾取、消耗、**卸下装备**；木剑 / 石剑 / 铁剑（均有手持模型），装备属性真实接入战斗（攻击加成、防御减伤）。
- **战斗**：灰狼（巡逻 / 追击 / 攻击 / 死亡状态机），伤害结算对齐动画咬合帧（前摇 0.36s），受击击退可打断咬击前摇（抢刀机制）。
- **存档**：单槽位 JSON，启动自动读档，恢复任务 / 背包 / 装备 / 好感 / 玩家状态。
- **UI**：HUD（金币 / HP / 当前目标 / 伤害飘字）、屏幕侧边任务追踪面板、对话 / 背包 / 任务 / 装备面板；UI 支持「场景实例 → Prefab（`Resources/UI/GameUI`）→ 代码构建」三级来源，可由编辑器菜单 `Tools/雾港小镇/烘焙 UI 预制体` 生成；中文 TMP 动态字体。

---

## 目录结构

```text
rpg/                         # ← 团结引擎工程根（用引擎打开这一层）
├── Assets/
│   ├── Scenes/              # Boot / Town / Forest
│   ├── Scripts/             # 全部 C#，按系统分模块
│   │   ├── Bootstrap/       # AppRoot 引导与系统装配
│   │   ├── Session/         # GameSession：跨系统流程层（唯一编排入口）
│   │   ├── Player/          # 控制器、相机跟随、交互、输入屏蔽、武器外观
│   │   ├── Dialogue/        # NPC 交互、AIBot 上下文与工具宿主
│   │   ├── Quest/           # 任务状态机、奖励服务
│   │   ├── Inventory/ Equipment/ Items/
│   │   ├── Relationship/    # 好感度与 NPC 档案（含头像）
│   │   ├── Enemy/ UI/ Save/ World/
│   ├── Editor/              # 编辑器工具（UI 预制体烘焙菜单）
│   ├── Data/ Resources/     # ScriptableObject 数据（任务 / 物品 / NPC 档案）与 UI Prefab
│   ├── Prefabs/             # Player / 敌人预制体
│   ├── Art/                 # 项目自有美术（建筑 / 角色 / 道具 / 材质 / UI 图标与头像）
│   ├── Settings/            # URP 管线与渲染器资产
│   └── ThirdParty/          # 第三方资源（见下）
├── Packages/                # URP、TMP 等依赖清单
└── ProjectSettings/
```

**架构约定**：UI 只发意图，`GameSession` 是唯一跨系统流程层（负责编排各系统调用 + 存档收尾），各系统（背包 / 装备 / 任务 / 奖励 / 好感）相互解耦。

---

## 第三方资源与授权

美术资源主要来自以下免费 / CC0 资源包，均位于 `rpg/Assets/ThirdParty/`：

| 资源包 | 用途 |
| --- | --- |
| **Mixamo（Adobe）** | 玩家角色 `Character_Rigged` 与 NPC 药师林洛的模型、骨骼与动画 |
| **Pro Sword and Shield Pack** | 玩家拔剑 / 收剑 / 挥剑攻击动画（`Art/Characters/Player/Animations/`） |
| **Quaternius · Universal Animation Library** | 玩家通用动画（跳跃、舞蹈等） |
| **Quaternius · Modular Outfits Fantasy** | 模块化角色服装（ peasant / ranger 等，供外观换装） |
| **Quaternius · Zombie Apocalypse Kit** | 灰狼模型（低模德牧）与动画 |
| **Kenney · Nature Kit** | 路面石板（`path_stone`）、街边原木 |
| **Kenney · Blocky Characters** | 早期占位角色（已由 Mixamo 角色替换） |
| **Polytope Studio · Low Poly Environment** | 树木 / 花草 / 灌木 / 蘑菇 / 岩石等地物 |
| **SimpleNaturePack** | 树桩等少量地物 |
| **Free Viking Pack** | 镇内维京高脚屋（装饰建筑） |
| **Alebardium · Bloodlines UI** | UI 素材 |

项目自有美术（AI 生成与整理）位于 `rpg/Assets/Art/`，包含建筑（铁匠铺 / 石屋 / 药师小屋）、武器模型（木剑 / 石剑 / 铁剑 / 铁矿）、UI 图标与 NPC 头像、路面材质等。

具体署名与许可信息见 [`rpg/Assets/ThirdParty/ATTRIBUTIONS.md`](rpg/Assets/ThirdParty/ATTRIBUTIONS.md)。

---

## 说明

- **策划 / 设计 / 剧本文档不随仓库分发**（保留在作者本地），因此本仓库只包含可运行的工程与代码。
- `rpg/CODELY.md`（AI 辅助开发日志）、`.codelyignore`、`.com-unity-codely.json`、`.vsconfig` 等协作 / IDE 配置文件仅保留在本地，不入库。
- 开发过程截图与录屏（`rpg/screenshots/`）、原始美术源文件（`art/`）也已在 `.gitignore` 中排除。
- 仓库根目录下的 `_rpg_*` 备份目录、`Library/`、`obj/`、IDE 工程文件等均已通过 `.gitignore` 排除。
