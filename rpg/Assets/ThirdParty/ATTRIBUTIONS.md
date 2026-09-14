# Third-Party Asset Attributions

本目录收录本项目使用的第三方资源。统一记录名称、来源、授权，遵守各授权要求（CC0 无需署名，但记录在案便于追溯；CC-BY 必须保留署名）。

> 2026-09-13 整理：所有第三方资源包已统一移动至 `Assets/ThirdParty/`（使用 GUID 保留的 AssetDatabase 移动，引用未破坏）。

## Kenney · Nature Kit

- **用途**：Town/Forest 地面石板路（`path_stone`）、Town 街边原木（`log` / `log_large`）
- **来源**：https://kenney.nl/assets/nature-kit
- **授权**：CC0（公有领域，个人/商用均可，无需署名）
- **本地路径**：`Assets/ThirdParty/Kenney/NatureKit/`
- **导入日期**：2026-09-05
- **备注**：2026-09-13 清理后仅保留在用文件（3 个 FBX + License.txt），其余模型已移出（外部备份 `D:\Code\rpg\_rpg_removed_assets_20260913`）。下载链接：
  `https://kenney.nl/media/pages/assets/nature-kit/37ac38a37b-1677698939/kenney_nature-kit.zip`

## Kenney · Blocky Characters

- **用途**：曾用于 NPC 药师林洛（`character-i` + `texture-i`）
- **来源**：https://kenney.nl/assets/blocky-characters
- **授权**：CC0（公有领域，个人/商用均可，无需署名）
- **本地路径**：`Assets/ThirdParty/Kenney/BlockyCharacters/`
- **导入日期**：2026-09-06
- **备注**：2026-09-13 林洛已替换为 Mixamo 模型（见下），`character-i` / `texture-i` / `BlockyNpcIdle.controller` 已不再被场景引用，并整体移入外部备份 `D:\Code\rpg\_rpg_removed_assets_20260913\npc_swap_20260913\`（保留 .meta，可拷回恢复）。玩家与 NPC 动画控制器现位于：`Assets/Art/Animations/PlayerHumanoid.controller`、`Assets/Art/Animations/NpcLinIdle.controller`。

## Mixamo（Adobe）· 角色模型

- **用途**：玩家角色 `Character_Rigged`（2026-09-12 替换）、NPC 药师林洛 `herbalist_mixamo`（2026-09-13 替换）
- **来源**：Adobe Mixamo（`mixamorig` 骨骼命名）
- **授权**：Mixamo 素材（按 Adobe Mixamo 条款，可免费用于项目）
- **本地路径**：
  - 玩家：`Assets/Art/Characters/Player/`（`Character_Rigged.fbx` + 贴图 + `Animations/`（Idle/Walk/Run/Gather））
  - 林洛：`Assets/Art/Characters/herbalist/`（`herbalist_mixamo.fbx` + `herbalist_basecolor.png` + `herbalist_body.mat`）
- **备注**：两个模型均按 Humanoid 导入（自动生成 Avatar）；林洛使用 `NpcLinIdle.controller`（复用玩家 Idle 剪辑，经 Humanoid 重定向）；玩家使用 `PlayerHumanoid.controller`（speed 混合树）。比例基准：玩家高 2.70m；林洛 2.45m（比玩家略矮、体型偏瘦）。

## Quaternius · Zombie Apocalypse Kit（德国牧羊犬/狼）

- **用途**：Enemy_Wolf.prefab 敌人模型（低模德牧，Idle/Walk/Run/Attack/Death 动画）
- **来源**：https://quaternius.com/packs/zombieapocalypsekit.html（本项目早于 2026-04-13 已下载）
- **授权**：CC0（公有领域，个人/商用均可，无需署名）
- **本地路径**：`Assets/ThirdParty/Quaternius/Quaternius Zombie Apocalypse Kit/`
- **导入日期**：2026-04-13（2026-09-06 复用为狼模型）
- **备注**：同目录 prefab 文件在 AssetDatabase 中无效（main asset 加载为 null），故 Enemy_Wolf 从 FBX 实例化自行挂 Animator。动画状态名：`AnimalArmature|Idle/Walk/Run/Attack/Death`。动画驱动：`Assets/Scripts/Enemy/WolfAnimatorDriver.cs`（EnemyWolf 状态机 → clip CrossFade）。

## Polytope Studio · Low Poly Environment（免费版）

- **用途**：Town/Forest 植被与地物（果树 / 松树 / 灌木 / 花 / 草 / 蘑菇 / 岩石及树桩等），URP 管线
- **本地路径**：`Assets/ThirdParty/Polytope Studio/Lowpoly_Environments/`（`Prefabs/` 常用件；`Sources/` 网格/材质/贴图与 PT_* shader；`URP/` 各 URP 版本导入包）
- **导入日期**：2026-09-09
- **备注**：使用自定义 `PT_*` shader（已导入与本项目 URP 14.2.0-t1 对应的版本）；免费版无风吹顶点动画。授权以原始分发渠道为准（包内未附 LICENSE 文件）。

## SimpleNaturePack

- **用途**：Town 街边树桩 `Stump_01`
- **本地路径**：`Assets/ThirdParty/SimpleNaturePack/`
- **导入日期**：2026-09-08
- **备注**：2026-09-13 清理后仅保留 Stump_01 相关 4 件（模型 / 预制体 / 贴图 / 材质）。授权以原始分发渠道为准（包内未附 LICENSE 文件）。

## Free Viking Pack

- **用途**：Town 维京高脚屋 `A_BaseB`（装饰建筑）
- **本地路径**：`Assets/ThirdParty/Free Viking Pack/`
- **导入日期**：2026-09-08
- **备注**：2026-09-13 清理后仅保留 A_BaseB（模型 / 预制体）与在用材质。授权以原始分发渠道为准（包内未附 LICENSE 文件）。

---

## 其他第三方（保持约定路径，勿移动）

- **TextMesh Pro**：`Assets/TextMesh Pro/` —— Unity 官方 TMP Essential Resources（约定路径，移动会破坏 TMP 设置发现）。
- **中文字体**：`Assets/Codely/Fonts/` —— Noto Sans SC（OFL 授权，见 `NotoSansSC-OFL.txt`）；由 Codely TMPChineseFont 扩展维护，共 2 个字体资产（Dynamic TMP 字体 + 源 .otf）。
