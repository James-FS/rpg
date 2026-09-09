# Third-Party Asset Attributions

本目录收录本项目使用的第三方资源。统一记录名称、来源、授权，遵守各授权要求（CC0 无需署名，但记录在案便于追溯；CC-BY 必须保留署名）。

## Kenney · Nature Kit

- **用途**：Town/Forest 场景环境（树木 / 岩石 / 草丛 / 地面 / 悬崖 / 栅栏等低模 3D 物件）
- **来源**：https://kenney.nl/assets/nature-kit
- **授权**：CC0（公有领域，个人/商用均可，无需署名）
- **本地路径**：`Assets/ThirdParty/Kenney/NatureKit/`（仅导入 FBX 格式，329 个模型 + License.txt）
- **导入日期**：2026-09-05
- **备注**：原始包含 FBX/OBJ/GLTF/DAE/STL 五种格式与 2D 预览图；本项目仅需要 FBX，故未整体复制。下载链接：
  `https://kenney.nl/media/pages/assets/nature-kit/37ac38a37b-1677698939/kenney_nature-kit.zip`

## Kenney · Blocky Characters

- **用途**：玩家（character-a）与 NPC 药师林洛（character-i）角色模型，替换灰盒胶囊体
- **来源**：https://kenney.nl/assets/blocky-characters
- **授权**：CC0（公有领域，个人/商用均可，无需署名）
- **本地路径**：`Assets/ThirdParty/Kenney/BlockyCharacters/`（18 个 FBX + 贴图 + License.txt）
- **导入日期**：2026-09-06
- **备注**：每角色 27 个通用动画（idle 循环 / walk / sprint / attack-melee / interact 等）；已为 idle/walk/sprint 开启 loop。Animator 控制器在 `Assets/Animations/BlockyPlayer.controller`（speed 混合树）与 `BlockyNpcIdle.controller`。下载链接：
  `https://kenney.nl/media/pages/assets/blocky-characters/8369c0cf30-1749547469/kenney_blocky-characters_20.zip`

## Quaternius · Zombie Apocalypse Kit（德国牧羊犬/狼）

- **用途**：Enemy_Wolf.prefab 敌人模型（低模德牧，Idle/Walk/Run/Attack/Death 动画）
- **来源**：https://quaternius.com/packs/zombieapocalypsekit.html（本项目早于 2026-04-13 已下载）
- **授权**：CC0（公有领域，个人/商用均可，无需署名）
- **本地路径**：`Assets/Quaternius/Quaternius Zombie Apocalypse Kit/`（FBX `Models/FBX format/Characters_GermanShepherd.fbx` + `Zombie_Atlas.png` 贴图 + `quaternius_zombie_apocalypse_kit_characters_germanshepherd.controller`）
- **导入日期**：2026-04-13（2026-09-06 复用为狼模型）
- **备注**：同目录 prefab 文件在 AssetDatabase 中无效（main asset 加载为 null），故 Enemy_Wolf 从 FBX 实例化自行挂 Animator。动画状态名：`AnimalArmature|Idle/Walk/Run/Attack/Death`。动画驱动：`Assets/Scripts/Enemy/WolfAnimatorDriver.cs`（EnemyWolf 状态机 → clip CrossFade）。
