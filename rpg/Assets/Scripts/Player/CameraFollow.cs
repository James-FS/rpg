using UnityEngine;

namespace FogHarbor.Player
{
    /// <summary>
    /// 第三人称环绕相机（常见 RPG 手感）：平滑跟随目标，鼠标左右转视角、上下改俯仰，滚轮拉近拉远。
    /// 挂载到 Main Camera 上，Inspector 中指定 target 为玩家。
    /// 视角输入由 GameCursor 决定是否生效：面板打开或按 Alt 显示鼠标时暂停转视角。
    /// offset 现在只用来定义「开场的俯仰角与距离」——场景里的 (0, 7.5, -7.5) 即 45° 俯角、10.6m 距离，
    /// 运行中视角由 yaw / pitch 控制，不再直接使用 offset 的世界方向。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("开场视角：只决定起始俯仰角与跟随距离（当前 (0,7.5,-7.5) = 45° 俯角 / 10.6m），运行中由鼠标控制")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -8f);
        [SerializeField] private float smoothSpeed = 5f;

        [Header("鼠标视角")]
        [Tooltip("鼠标灵敏度（度 / 输入单位）")]
        [SerializeField] private float lookSensitivity = 3f;
        [Tooltip("俯仰角下限（度）。30° 时地面刚好铺满画面，再低会露出地平线与地面边缘")]
        [SerializeField] private float pitchMin = 30f;
        [Tooltip("俯仰角上限（度），越大越接近俯视")]
        [SerializeField] private float pitchMax = 75f;
        [Tooltip("上下反向（默认关闭：鼠标上推 = 抬头、镜头压低）")]
        [SerializeField] private bool invertY = false;

        [Header("滚轮缩放")]
        [Tooltip("滚轮每格的缩放步长")]
        [SerializeField] private float zoomStep = 0.1f;
        [Tooltip("最近 / 最远倍率（1 = 原始偏移；默认 0.7~1.5 属于小幅拉近拉远）")]
        [SerializeField] private float minZoom = 0.7f;
        [SerializeField] private float maxZoom = 1.5f;
        [Tooltip("缩放平滑速度（越大越跟手）")]
        [SerializeField] private float zoomSmooth = 10f;

        /// <summary>当前场景的游戏相机（唯一）。玩家控制器靠它换算移动方向。</summary>
        public static CameraFollow Instance { get; private set; }

        // 跨场景保留的视角状态：场景切换会换一个新相机，但视角不该跳回默认（与其它 RPG 一致）
        private static float savedYaw;
        private static float savedPitch;
        private static float savedZoom = 1f;
        private static bool hasSavedLook;

        /// <summary>每次进入 Play 时清掉上一局的视角残留（关掉域重载时静态量不会自动清）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSavedLook()
        {
            savedYaw = 0f;
            savedPitch = 0f;
            savedZoom = 1f;
            hasSavedLook = false;
        }

        // 视角状态：yaw = 水平环绕角（0 = 与开场一致，相机在目标的 -Z 方向），pitch = 俯仰角（度，越大越俯视）
        private float yaw;
        private float pitch;

        // 跟随状态：distance 为基准距离（= 开场 offset 长度），pivot 为平滑跟随的注视点
        private float distance = 10f;
        private Vector3 pivot;

        private float targetZoom = 1f;
        private float currentZoom = 1f;

        /// <summary>当前缩放倍率（1 = 原始偏移；&lt;1 拉近，&gt;1 拉远）。</summary>
        public float Zoom => currentZoom;

        /// <summary>当前水平环绕角（度）。</summary>
        public float Yaw => yaw;

        /// <summary>当前俯仰角（度）。</summary>
        public float Pitch => pitch;

        /// <summary>屏幕「上方」在世界中的方向（已抹平高度）：WASD 里的 W 就是朝它走。</summary>
        public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        /// <summary>屏幕「右方」在世界中的方向（已抹平高度）。</summary>
        public Vector3 PlanarRight => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        private void Awake()
        {
            Instance = this;

            // 用 offset 反推开场视角：方向决定俯仰/环绕角，长度决定距离
            Vector3 dir = offset.sqrMagnitude > 0.0001f ? offset.normalized : new Vector3(0f, 0.707f, -0.707f);
            distance = Mathf.Max(0.1f, offset.magnitude);
            pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg, pitchMin, pitchMax);
            yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;

            // 上一个场景调过的视角优先（首个场景才用 offset 里的默认角度）
            if (hasSavedLook)
            {
                yaw = savedYaw;
                pitch = Mathf.Clamp(savedPitch, pitchMin, pitchMax);
                targetZoom = currentZoom = savedZoom;
            }

            // 支点接住相机当前位置，开场不会瞬移（沿用原来「从场景摆放位置滑过去」的手感）
            pivot = transform.position - OrbitDirection * distance;
        }

        private void Update()
        {
            // 面板打开 / 切到鼠标模式时：视角与缩放都交给 UI，不转镜头
            if (!GameCursor.LookAllowed) return;

            AddLook(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                AddZoom(scroll);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            currentZoom = Mathf.Lerp(currentZoom, targetZoom, zoomSmooth * Time.deltaTime);
            pivot = Vector3.Lerp(pivot, target.position, smoothSpeed * Time.deltaTime);

            // 位置由「支点 + 环绕方向」精确算出：转视角是 1:1 跟手的，只有平移带平滑
            Vector3 dir = OrbitDirection;
            transform.position = pivot + dir * (distance * currentZoom);
            transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
        }

        /// <summary>从注视点指向相机的单位方向（由俯仰角与环绕角决定）。</summary>
        private Vector3 OrbitDirection => Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;

        /// <summary>
        /// 鼠标视角输入入口（单位：输入单位，与 Input.GetAxis("Mouse X"/"Mouse Y") 同量纲）。
        /// 鼠标输入与外部脚本 / AI 共用这一处，便于自动化验证。
        /// </summary>
        public void AddLook(float mouseX, float mouseY)
        {
            yaw = Mathf.Repeat(yaw + mouseX * lookSensitivity, 360f);

            float delta = (invertY ? mouseY : -mouseY) * lookSensitivity;
            pitch = Mathf.Clamp(pitch + delta, pitchMin, pitchMax);
        }

        /// <summary>直接设定视角（外部脚本 / AI 用；俯仰角自动夹在上下限内）。</summary>
        public void SetLook(float newYaw, float newPitch)
        {
            yaw = Mathf.Repeat(newYaw, 360f);
            pitch = Mathf.Clamp(newPitch, pitchMin, pitchMax);
        }

        /// <summary>按滚轮格数调整缩放（自动夹在最近 / 最远之间）；外部脚本与 AI 共用同一入口。</summary>
        public void AddZoom(float notches)
        {
            targetZoom = Mathf.Clamp(targetZoom + notches * zoomStep, minZoom, maxZoom);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
                pivot = target.position;   // 换目标时支点跟着走，避免镜头长距离滑行
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;

            // 相机随场景销毁前把视角记下来，给下一个场景的相机接着用
            savedYaw = yaw;
            savedPitch = pitch;
            savedZoom = targetZoom;
            hasSavedLook = true;
        }
    }
}
