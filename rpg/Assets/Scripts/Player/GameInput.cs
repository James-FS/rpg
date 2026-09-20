namespace FogHarbor.Player
{
    /// <summary>
    /// 游戏世界输入总闸：有面板打开时由 UIManager 置为 true，
    /// 玩家控制器 / 交互器统一读它，避免「点 UI 时顺手挥了一刀」「在对话框里打字角色乱跑」这类穿透。
    /// 只拦输入读取，不拦公开 API（PlayAttack / RequestJump / Interact 等外部脚本入口照旧可用）。
    /// </summary>
    public static class GameInput
    {
        /// <summary>是否屏蔽世界输入（对话框 / 背包 / 任务面板打开时为 true）。</summary>
        public static bool Blocked { get; set; }
    }
}
