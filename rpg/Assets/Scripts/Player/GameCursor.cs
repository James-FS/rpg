using UnityEngine;

namespace FogHarbor.Player
{
    /// <summary>
    /// 游戏内鼠标模式（常见第三人称 RPG 手感）：
    /// 默认锁定并隐藏鼠标，移动鼠标即转视角；按 Alt 切换显示鼠标（临时去点 UI / 切出窗口）；
    /// 对话框、背包、任务面板打开时自动显示鼠标，面板关闭后自动锁回。
    /// 挂载到 Main Camera 上（与 CameraFollow 同级），Alt 键与外部脚本共用同一入口 ToggleCursor()。
    /// </summary>
    public class GameCursor : MonoBehaviour
    {
        [Tooltip("开场是否直接进入游戏内（锁定并隐藏鼠标）；关闭可用于调试")]
        [SerializeField] private bool lockOnStart = true;

        [Tooltip("切换鼠标显示 / 隐藏的按键（LeftAlt 与 RightAlt 都会生效）")]
        [SerializeField] private KeyCode toggleKey = KeyCode.LeftAlt;

        /// <summary>当前场景的鼠标控制器（相机上唯一实例）。</summary>
        public static GameCursor Instance { get; private set; }

        /// <summary>Alt 切换出来的「想看鼠标」状态（面板打开不算在内）。</summary>
        private bool cursorShown;

        /// <summary>是否处于游戏内状态：鼠标锁定 + 隐藏，相机可以转视角。</summary>
        public bool LookActive => !cursorShown && !GameInput.Blocked;

        /// <summary>是否已切到「显示鼠标」状态（由 Alt 切换）。</summary>
        public bool CursorShown => cursorShown;

        /// <summary>
        /// 静态入口：没有 GameCursor 的场景（例如 Boot）按「游戏内」处理，
        /// 避免相机因为找不到组件就完全不能转视角。
        /// </summary>
        public static bool LookAllowed => Instance == null || Instance.LookActive;

        private void Awake()
        {
            Instance = this;
            cursorShown = !lockOnStart;
            Apply();
        }

        private void Update()
        {
            // 面板打开时不响应 Alt：否则「在对话框里按 Alt → 关掉面板后鼠标还亮着」会让人莫名其妙
            if (!GameInput.Blocked && TogglePressed())
                ToggleCursor();

            Apply();
        }

        /// <summary>
        /// 切换鼠标显隐（Alt 键与外部脚本 / AI 共用入口）。
        /// 面板打开时鼠标本来就是显示的，切换状态留到面板关闭后才体现。
        /// </summary>
        public void ToggleCursor()
        {
            SetCursorShown(!cursorShown);
        }

        /// <summary>直接设置鼠标显隐（true = 显示鼠标、退出游戏内视角控制）。</summary>
        public void SetCursorShown(bool shown)
        {
            cursorShown = shown;
            Apply();
        }

        private bool TogglePressed()
        {
            if (Input.GetKeyDown(toggleKey)) return true;
            // 左右 Alt 都算，避免「按了右边的 Alt 没反应」
            return toggleKey == KeyCode.LeftAlt && Input.GetKeyDown(KeyCode.RightAlt);
        }

        /// <summary>
        /// 每帧把「想要的鼠标状态」写进引擎。刻意与真实状态比较后再写：
        /// 编辑器里按 Esc、或失去焦点时引擎会自行解锁光标，这样下一帧能自动锁回去。
        /// </summary>
        private void Apply()
        {
            bool wantFree = !LookActive;
            CursorLockMode wantLock = wantFree ? CursorLockMode.None : CursorLockMode.Locked;
            if (Cursor.lockState != wantLock) Cursor.lockState = wantLock;
            if (Cursor.visible != wantFree) Cursor.visible = wantFree;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;

            // 退出 Play / 切换到别的场景时把光标还给引擎，避免编辑器里留下「看不见的鼠标」
            if (!Application.isPlaying) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
