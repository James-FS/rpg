using System.Collections;
using UnityEngine;
using FogHarbor.Items;
using FogHarbor.Equipment;

namespace FogHarbor.Player
{
    /// <summary>
    /// 武器视觉：把当前装备的武器模型挂到角色身上，并处理拔刀 / 收刀的外观切换。
    /// 挂在 Player 上，只订阅 EquipmentSystem.OnEquipmentChanged 跟随外观，不参与任何数值计算
    /// （攻击力仍由 EquipmentSystem 写回 PlayerController）。
    ///
    /// 状态：
    ///   收刀（默认）→ 武器挂在左腰挂点 WaistSocket；
    /// 按 R 拔刀 → 播放 WithdrawingSword 动画，握把到达腰间位置后把模型平滑引导（位置+旋转连续插值）到手上（WeaponSocket）；
    /// 再按 R → 播放 SheathingSword，动画最后一段把模型平滑引导回腰间（到插入时刻刚好贴合，无瞬间跳变）。
    /// 引导时机通过轮询动画状态的 normalizedTime 决定（drawSwapProgress / sheatheSwapProgress / swapBlendProgress），
    /// 比固定计时更耐过渡延迟；定身时长由 PlayerController 的动作锁负责。
    ///
    /// 挂点约定：挂点均为骨骼下的空物体，其局部缩放已把骨骼的绑定缩放归一化到 1
    /// （本角色的 mixamorig 骨骼绑定缩放约 158.8，不归一化的话模型会放大两百多倍），
    /// 所以武器 Prefab 的缩放按"世界单位"设置即可。
    /// </summary>
    public class PlayerWeaponVisual : MonoBehaviour
    {
        [Header("挂点")]
        [Tooltip("左腰挂点（收刀时的放置位置，胯部骨骼的子物体）；留空则按 stowSocketName 在层级里查找")]
        [SerializeField] private Transform stowSocket;
        [Tooltip("左腰挂点的名字")]
        [SerializeField] private string stowSocketName = "WaistSocket";
        [Tooltip("手部挂点（拔刀后持握位置）；留空则按 socketName 在层级里查找")]
        [SerializeField] private Transform weaponSocket;
        [Tooltip("手部挂点的名字")]
        [SerializeField] private string socketName = "WeaponSocket";

        [Header("拔刀 / 收刀")]
        [Tooltip("拔刀 / 收刀的按键：装备武器后按一次拔出、再按一次收回")]
        [SerializeField] private KeyCode drawKey = KeyCode.R;
        [Tooltip("拔刀：从该进度开始把模型从腰间平滑引导到手上（0~1；0.34 = 实测握把离挂点最近的时刻）")]
        [SerializeField] private float drawSwapProgress = 0.34f;
        [Tooltip("收刀：把模型从手上平滑引导回腰间，到该进度刚好贴合（0~1；0.81 = 实测握把离挂点最近的时刻）")]
        [SerializeField] private float sheatheSwapProgress = 0.81f;
        [Tooltip("引导时长（归一化进度的比例）：在这段进度内连续插值位置+旋转，消除瞬间跳变（收刀时原为 16cm/120° 跳变）")]
        [SerializeField] private float swapBlendProgress = 0.2f;

        private EquipmentSystem equipment;
        private PlayerController player;
        private Animator animator;
        private GameObject weaponInstance;
        private string currentItemId = "";
        private float bindDeadline;

        // 拔刀状态跨场景保留（换场景会换一个新的玩家对象，但"刀拔没拔出来"这件事不该被重置）
        private static bool drawnShared;
        private bool drawn;

        /// <summary>每次进入 Play 清掉上一局的拔刀状态。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedDraw()
        {
            drawnShared = false;
        }

        /// <summary>当前显示的武器 itemId，空字符串表示空手。</summary>
        public string CurrentItemId => currentItemId;

        /// <summary>是否处于拔刀（手持）状态；false = 收在腰间。</summary>
        public bool IsDrawn => drawn;

        private void OnEnable()
        {
            // AppRoot 由 GameBootstrapper 在 AfterSceneLoad 创建，晚于场景物体的 OnEnable，
            // 因此这里拿不到就开一个短重试窗口，避免"装备了但身上没有刀"这种静默失败。
            bindDeadline = Time.time + 2f;
            drawn = drawnShared;
            animator = GetComponentInChildren<Animator>();
            player = GetComponent<PlayerController>();
            TryBind();
        }

        private void OnDisable()
        {
            UnbindEquipment();
            ClearWeapon();
        }

        private void Update()
        {
            if (equipment == null && Time.time < bindDeadline)
                TryBind();

            // 拔刀 / 收刀：面板打开时不响应（否则在对话框里打字会把 r 当成拔刀键）
            if (!GameInput.Blocked && Input.GetKeyDown(drawKey))
                ToggleDraw();
        }

        // ─── 拔刀 / 收刀 ───

        /// <summary>切换拔刀 / 收刀（按键与外部脚本、AI 共用同一入口）。
        /// 未装备武器、动作中（采集/攻击/拔收刀）或腾空时不响应。</summary>
        public void ToggleDraw()
        {
            if (equipment == null || !equipment.HasWeapon)
                return;
            if (player != null && (player.IsBusy || player.IsJumping))
                return;

            if (!drawn) StartCoroutine(DrawRoutine());
            else StartCoroutine(SheatheRoutine());
        }

        /// <summary>拔刀：播拔刀动画，握把到达腰间时开始把模型从腰间平滑引导到手上。</summary>
        private IEnumerator DrawRoutine()
        {
            drawn = true;
            drawnShared = true;
            if (player != null) player.PlayDrawSword();
            yield return GuideWeapon("DrawSword", drawSwapProgress, drawSwapProgress + swapBlendProgress,
                ResolveStowSocket(), ResolveSocket());
            Debug.Log($"[WeaponVisual] 拔刀：{currentItemId}");
        }

        /// <summary>收刀：播收刀动画，最后一段把模型从手上平滑引导回腰间（位置+旋转连续插值）。</summary>
        private IEnumerator SheatheRoutine()
        {
            drawn = false;
            drawnShared = false;
            if (player != null) player.PlaySheatheSword();
            yield return GuideWeapon("SheatheSword", sheatheSwapProgress - swapBlendProgress, sheatheSwapProgress,
                ResolveSocket(), ResolveStowSocket());
            Debug.Log($"[WeaponVisual] 收刀：{currentItemId}");
        }

        /// <summary>
        /// 把武器模型从 fromSocket 的姿势平滑引导到 toSocket 的姿势：
        /// 动画进度进入 [startNorm, endNorm] 后，每帧按平滑曲线插值世界位置+旋转（消除瞬间跳变），
        /// 到 endNorm 时恰好与目标姿势重合，再挂到目标挂点下（零局部量）。两端衔接均无跳变。
        /// </summary>
        private IEnumerator GuideWeapon(string stateName, float startNorm, float endNorm, Transform fromSocket, Transform toSocket)
        {
            float deadline = Time.time + 6f;

            // 等动画进入目标状态
            while (Time.time < deadline)
            {
                if (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
                    break;
                yield return null;
            }

            // 引导窗口：逐帧插值（动画提前结束也安全退出）
            while (Time.time < deadline)
            {
                var st = animator != null ? animator.GetCurrentAnimatorStateInfo(0) : default;
                if (animator == null || !st.IsName(stateName))
                    break;

                float n = st.normalizedTime;
                if (n >= endNorm)
                    break;

                if (n >= startNorm && weaponInstance != null && fromSocket != null && toSocket != null)
                {
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(startNorm, endNorm, n));
                    var wt = weaponInstance.transform;
                    wt.position = Vector3.Lerp(fromSocket.position, toSocket.position, k);
                    wt.rotation = Quaternion.Slerp(fromSocket.rotation, toSocket.rotation, k);
                }
                yield return null;
            }

            // 落位：挂到目标挂点（武器可能已被装备变化清掉，需判空）
            if (weaponInstance != null && toSocket != null)
            {
                var wt = weaponInstance.transform;
                wt.SetParent(toSocket, false);
                wt.localPosition = Vector3.zero;
                wt.localRotation = Quaternion.identity;
                wt.localScale = Vector3.one;
            }
        }

        // ─── 绑定 ───

        private void TryBind()
        {
            if (equipment != null)
                return;

            equipment = FindObjectOfType<EquipmentSystem>();
            if (equipment == null)
                return;

            equipment.OnEquipmentChanged += OnEquipmentChanged;
            RefreshVisual();
        }

        private void UnbindEquipment()
        {
            if (equipment != null)
                equipment.OnEquipmentChanged -= OnEquipmentChanged;
            equipment = null;
        }

        private void OnEquipmentChanged()
        {
            // 换了武器（或卸下）后重置为收刀状态，重新从腰间开始
            string newId = equipment != null ? equipment.WeaponId : "";
            if (newId != currentItemId)
            {
                drawn = false;
                drawnShared = false;
            }
            RefreshVisual();
        }

        // ─── 外观切换 ───

        /// <summary>按当前装备与拔刀状态刷新外观：收刀挂腰间、拔刀挂手上。</summary>
        private void RefreshVisual()
        {
            currentItemId = equipment != null ? equipment.WeaponId : "";
            if (string.IsNullOrEmpty(currentItemId))
            {
                ClearWeapon();
                return;
            }

            ShowWeapon(currentItemId, drawn ? ResolveSocket() : ResolveStowSocket());
        }

        /// <summary>按 itemId 把武器模型挂到指定挂点；空 itemId / 没有配置手持模型时显示空手。</summary>
        private void ShowWeapon(string itemId, Transform socket)
        {
            ClearWeapon();

            if (string.IsNullOrEmpty(itemId) || socket == null)
                return;

            var data = ItemDatabase.GetById(itemId);
            if (data == null)
                return;

            if (data.HoldPrefab == null)
            {
                Debug.Log($"[WeaponVisual] {data.DisplayName} 未配置手持模型，只生效属性不显示外观");
                return;
            }

            weaponInstance = Instantiate(data.HoldPrefab, socket, false);
            weaponInstance.name = "Weapon_" + itemId;
            weaponInstance.transform.localPosition = Vector3.zero;
            weaponInstance.transform.localRotation = Quaternion.identity;
            weaponInstance.transform.localScale = Vector3.one;
        }

        private void ClearWeapon()
        {
            if (weaponInstance != null)
                Destroy(weaponInstance);
            weaponInstance = null;
        }

        /// <summary>手部挂点（拔刀后持握）：优先用 Inspector 绑定，其次按名字查找。</summary>
        private Transform ResolveSocket()
        {
            if (weaponSocket != null)
                return weaponSocket;

            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == socketName)
                {
                    weaponSocket = t;
                    return t;
                }
            }

            Debug.LogWarning($"[WeaponVisual] 未找到武器挂点 '{socketName}'，武器外观不会显示");
            return null;
        }

        /// <summary>左腰挂点（收刀时的默认位置）：优先用 Inspector 绑定，其次按名字查找。</summary>
        private Transform ResolveStowSocket()
        {
            if (stowSocket != null)
                return stowSocket;

            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == stowSocketName)
                {
                    stowSocket = t;
                    return t;
                }
            }

            Debug.LogWarning($"[WeaponVisual] 未找到腰间挂点 '{stowSocketName}'，武器外观不会显示");
            return null;
        }
    }
}
