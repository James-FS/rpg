# LuaLab 巡逻载体

## 使用

1. 打开 `Assets/Scenes/LuaLab.scene`，点击 Play。
2. 小怪在 `PatrolPoints/PointA` 和 `PointB` 之间往返；到点停留 0.8 秒。
   场景中的蓝色胶囊是测试角色：WASD / 方向键移动，R 返回出生点。先点击 Game 窗口使其获得输入焦点。
   靠近小怪至 4 米内会触发追踪；1.2 米内停留；拉开至 1.5 米以上继续追踪；超过 6 米会返回当前巡逻目标点并恢复巡逻。
   左上角显示当前行为与目标距离。追踪速度 2.5 米/秒，测试角色速度 4 米/秒。
3. 在编辑模式移动两个巡逻点，可以改变路线。
4. 在 Play 中修改并保存 `Assets/StreamingAssets/LuaLab/patrol_ai.lua` 中的 `settings`，可以调整巡逻/追踪速度、索敌范围、到点距离和等待时间。
5. 点击 Game 窗口后按 **F5**，或点击左上角 **Reload Lua (F5)** 按钮。下一帧使用新行为，无需退出 Play 或重编译 C#。按钮右侧显示重载次数和结果。

## 热重载机制

`LuaEnvManager.ReloadAll()` 对已加载对象的模块清除 `package.loaded` 缓存，再通过 require 从文件读取。所有对象的新实例及 update/shutdown 函数准备成功后，统一替换缓存引用并释放旧引用。语法错误、缺少 new/update 等准备阶段错误会恢复原模块缓存，保留旧行为，界面显示失败原因，Console 输出警告；修复保存后可再次重载。

`patrol_ai.lua` 用 `save_state()` 保存当前巡逻目标、等待时间和追踪状态，新实例 `new(motor, savedState)` 接收它们；位置、角色目标和场景对象不重建。参数与函数逻辑使用新文件，已经开始的等待会保留剩余时间。模块只需返回表，并提供 new() 和实例 update(dt)。构造函数应只建立闭包/状态，不能修改场景、注册外部事件或启动协程；模块加载阶段也应避免外部副作用。

当前重载范围为挂载的顶层行为模块；其 require 的依赖不会递归重载。不属于 xLua Hotfix 对 C# 方法打补丁的功能。

## 文件职责

- `LuaEnvManager.cs`：管理此场景的 xLua 环境与 F5 重载，使用自定义 loader 从 StreamingAssets/LuaLab 读取模块；退出场景时先释放对象的 Lua 引用，再释放 LuaEnv。
- `LuaBehaviour.cs`：加载模块、创建每个物体的独立 Lua 实例、缓存并调用 update/shutdown 函数；重载时准备新实例，重绑函数引用并释放旧引用。
- `PatrolMotor.cs`：提供巡逻点、追踪目标位置与距离、移动、转向、重力和动画接口。行为切换由 Lua 决定。
- `patrol_ai.lua`：巡逻、等待、追踪、近距离停留、返回路线的状态与阈值。
- `LuaLabTestPlayer.cs`：独立测试角色，处理键盘移动、重力、场地边界、R 重置和状态显示。
- `Assets/Prefabs/LuaLab/LuaPatrolGuard.prefab`：外层为 CharacterController、PatrolMotor、LuaBehaviour；Visual 使用下载的 PatrolGuard 预制体。
- `Assets/Prefabs/LuaLab/PatrolGuard.controller`：复用现有人形 Idle/Walk 动画，Speed 参数控制切换，关闭 Root Motion。

模型显示高度调整为约 2 米；胶囊高度 2 米，半径 0.32 米。原始下载模型和预制体未改动。

## 重新放置载体

将 LuaPatrolGuard.prefab 放入场景，在 PatrolMotor 的 Patrol Points 中指定至少两个场景 Transform，在 LuaBehaviour 的 Runtime 中指定 LuaRuntime 上的 LuaEnvManager。运行时若未指定 Runtime，会在场景中查找它。
在 PatrolMotor 的 Chase Target 中指定追踪对象；未指定或目标被禁用/销毁时继续巡逻。当前场景已经绑定 LuaLabTestPlayer。

## 当前范围

本场景是独立练习区，不加入主游戏 Build Settings。当前实现两点巡逻、测试角色追踪与手动模块热重载；攻击尚未接入。移动为直接走向目标，不能绕过障碍物。文件 loader 面向桌面端；Android/WebGL 需要另外接入异步资源读取，IL2CPP 发布还需处理 xLua 代码生成与裁剪配置。
