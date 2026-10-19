# 雾港小镇（Fog Harbor）

![雾港小镇游戏封面](readme-assets/fogharbor-cover.png)

雾港小镇是一款使用团结引擎（Tuanjie Engine）制作的第三人称 RPG 原型。玩家在小镇和森林之间探索、与 NPC 对话、完成任务、采集资源并与灰狼战斗。NPC 对话接入外部 AIBot Server；任务状态、物品、战斗和存档由游戏代码管理。

## 当前工程信息

| 项目 | 当前配置 |
| --- | --- |
| 引擎 | 团结引擎 1.10.1（Unity 内核 2022.3.62t13） |
| 渲染 | URP 14.2.0-t1，Linear 色彩空间 |
| 场景格式 | 团结引擎 `.scene` |
| 命名空间 | `FogHarbor.*` |
| Unity 工程目录 | `rpg/` |

## 打开与运行

1. 安装团结引擎 1.10.1，并打开仓库中的 `rpg/` 目录。
2. 准备下方所列的外部依赖，确保 Unity Package Manager 能解析 `rpg/Packages/manifest.json`。
3. 需要使用 AI NPC 对话时，另行启动 AIBot Server，并按其说明配置模型服务。
4. 在 Build Settings 中从 `Assets/Scenes/Title.scene` 启动游戏。

当前 Build Settings 的场景顺序是：

| 顺序 | 场景 | 用途 |
| --- | --- | --- |
| 1 | `Title` | 标题菜单：继续旅程、新旅程、设置、退出 |
| 2 | `Boot` | 初始化游戏系统并进入应恢复的游戏场景 |
| 3 | `Town` | 雾港小镇 |
| 4 | `Forest` | 森林探索区域 |

`LuaLab.scene` 是实验场景，没有加入当前 Build Settings。直接运行 `Boot.scene` 可用于编辑器内调试；正常游玩从标题菜单开始。继续旅程会恢复到存档中的小镇或森林；开启新旅程前，游戏会把旧存档移入持久化数据目录下的 `JourneyArchives/`。

## 玩法与操作

- **移动与探索**：WASD 移动，鼠标控制视角，按住 Shift 奔跑，Space 跳跃。
- **交互**：靠近 NPC 或可交互物后按 E。
- **战斗与采集**：鼠标左键攻击；按 R 拔刀或收刀。装备的武器决定攻击或采集行为。
- **界面**：B 打开背包，Q 打开任务面板，Esc 关闭当前面板。
- **鼠标**：按 Alt 切换鼠标锁定状态。
- **开发工具**：在 Unity Editor 或 Development Build 中按 F2 打开 GM 面板；该入口在普通 Release Build 中关闭。

主线围绕寻找月光药草展开；支线涉及收集铁矿。小镇包含药师林洛和铁匠阿拓等 NPC，森林中有采集物、矿石和灰狼。

## 已实现系统

- 第三人称移动、奔跑、跳跃、相机跟随、交互输入屏蔽。
- NPC 对话、游戏状态上下文、任务工具调用和好感度记录。
- 主线与支线任务、物品拾取、背包、装备属性和武器外观。
- 灰狼巡逻、追击、攻击与死亡流程。
- HUD、任务追踪、对话、背包、任务、装备、死亡与标题界面。
- 单槽位 JSON 自动存档，记录位置、任务、物品、装备、关系和玩家状态。
- 仅供开发和测试使用的 GM 面板，可检查或操作部分游戏状态。

### NPC 对话服务

NPC 对话当前使用 **Server 模式**，不是离线内置模型。项目需要单独运行 AIBot Server（默认地址 `127.0.0.1:5000`），并按 Server 的配置方式安全提供 `AIBOT_LLM_KEY`。具体模型、密钥和 Server 设置不由本仓库管理。没有连接 Server 时，普通移动、探索和战斗可用于本地测试，但 AI 对话不可用。

## 外部依赖与可移植性

`rpg/Packages/manifest.json` 当前包含一项本机路径依赖：

- `com.aibot.npcagent` 指向 `D:/Code/aibot/Packages/com.aibot.npcagent`。

这个目录不随本仓库提供。因此，**从干净克隆无法直接还原完整工程**。其他机器需要先取得兼容版本的 AIBot SDK，再配置可访问的包来源。AIBot Server 也需要单独部署或取得。

项目不再依赖 TJGenerators；此前生成并导入的游戏资源仍保留在 `rpg/Assets/`。`cn.tuanjie.codely.bridge` 仍作为编辑器桥接依赖保留。

`cn.tuanjie.codely.bridge` 用于编辑器桥接工作流，并非游戏运行时的 NPC 服务。游戏代码还包含 xLua；当前第三方说明记录了 Windows x64 环境的接入验证，其他目标平台尚未确认。

## 项目结构

- `rpg/Assets/Scenes/`：标题、引导、小镇、森林和 LuaLab 场景。
- `rpg/Assets/Scripts/`：游戏逻辑，按引导、玩家、对话、任务、背包、装备、关系、存档、UI、敌人和调试模块组织。
- `rpg/Assets/Resources/`：任务、物品、NPC 配置和 UI 资源。
- `rpg/Assets/Data/`：NPC 配置数据。
- `rpg/Assets/Art/`：项目美术资源。
- `rpg/Assets/ThirdParty/`：第三方资源；来源与部分授权信息见 [`ATTRIBUTIONS.md`](rpg/Assets/ThirdParty/ATTRIBUTIONS.md)。
- `rpg/Assets/Editor/`：编辑器工具和编辑器测试。
- `rpg/Packages/`、`rpg/ProjectSettings/`：依赖清单与引擎项目设置。
- `docs/`：本地策划、流程和剧本文档，不纳入 Git。

## 发布说明

- 当前 `ProjectSettings` 中 `companyName` 仍为 `DefaultCompany`、`productName` 为 `rpg`、`bundleVersion` 为 `0.1`；正式构建前应按发行信息设置。
- 根目录尚无本项目代码的 `LICENSE` 文件。第三方资源的使用与再分发条件需按各自来源确认；`ATTRIBUTIONS.md` 是来源记录，不替代资源授权。
- 项目目前使用本机路径依赖，未完成可移植的干净克隆还原说明。
