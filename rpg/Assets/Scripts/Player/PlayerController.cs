using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using FogHarbor.Enemy;
using FogHarbor.UI;

namespace FogHarbor.Player
{
    /// <summary>
    /// 玩家控制器：移动、攻击、受击。
    /// 使用 CharacterController 实现移动，支持 WASD 和方向键。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移动")]
        [Tooltip("走路速度（米/秒）")]
        [SerializeField] private float walkSpeed = 2.5f;
        [Tooltip("按住 Shift 奔跑的速度（米/秒）")]
        [SerializeField] private float runSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float gravity = -9.81f;

        [Header("跳跃")]
        [Tooltip("跳跃最高点相对起跳点的高度（米）")]
        [SerializeField] private float jumpHeight = 1.1f;
        [Tooltip("离开地面后仍可起跳的宽限时间（秒），用于台阶/斜坡边缘")]
        [SerializeField] private float coyoteTime = 0.12f;
        [Tooltip("落地前提前按跳的输入缓冲时间（秒）")]
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("采集")]
        [Tooltip("采集动作的定身时长（秒），应略长于 Gather 动画播完+过渡的总时长（×5 倍速、exitTime 0.52 下约 1.0s）")]
        [SerializeField] private float gatherLockTime = 1.1f;

        [Header("攻击")]
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackCooldown = 1f;
        [SerializeField] private int baseAttackDamage = 5;
        [Tooltip("连段重置时间（秒）：距上次出招超过该时间后，下一击回到第一段（外砍）")]
        [SerializeField] private float attackComboResetTime = 2.5f;
        [Tooltip("第一段·外砍（OutwardSlash）的定身时长（秒）")]
        [SerializeField] private float attackOutLockTime = 1.3f;
        [Tooltip("第二段·内砍（InwardSlash）的定身时长（秒）")]
        [SerializeField] private float attackInLockTime = 1.4f;
        [Tooltip("第一段命中判定延迟（秒）：起手到剑最前伸的帧（实测 0.95s）")]
        [SerializeField] private float attackOutHitDelay = 0.95f;
        [Tooltip("第二段命中判定延迟（秒）：起手到剑最前伸的帧（实测 1.34s）")]
        [SerializeField] private float attackInHitDelay = 1.34f;

        [Header("拔刀 / 收刀")]
        [Tooltip("拔刀动作的定身时长（秒），应略长于 WithdrawingSword 动画播完+过渡的总时长（≈1.4s）")]
        [SerializeField] private float drawSwordLockTime = 1.45f;
        [Tooltip("收刀动作的定身时长（秒），应略长于 SheathingSword 动画播完+过渡的总时长（≈1.55s）")]
        [SerializeField] private float sheatheSwordLockTime = 1.6f;

        [Header("防御")]
        [SerializeField] private int baseDefense = 0;

        [Header("生命")]
        [SerializeField] private int maxHp = 100;

        private CharacterController controller;
        private float verticalVelocity;
        private float lastAttackTime;
        private Coroutine attackRoutine;
        private int currentHp;

        // 攻击连段状态（0=外砍 / 1=内砍，交替循环；间隔过久自动重置）
        private int attackComboIndex;
        private float lastComboTime = -999f;
        private float lastAttackHitDelay = 0.9f;

        // 跳跃状态
        private float lastGroundedTime = -999f;
        private float jumpRequestTime = -999f;
        private bool isJumping;

        // 装备加成（由 EquipmentSystem 写回；T2 遗留补全：让装备真正影响战斗）
        private int bonusAttack;
        private int bonusDefense;

        // 采集动作期间的定身：到时前移动/跳跃/攻击输入无效，重力照常
        private float busyUntil = -999f;

        /// <summary>HP 变化时触发（§6.1 差距 #3：HUD 订阅刷新，删每帧轮询）。</summary>
        public event Action<int, int> OnPlayerHpChanged;

        /// <summary>采集动作开始时触发（动画层订阅后播放 Gather 动画，不含任何游戏逻辑）。</summary>
        public event Action OnGatherStarted;

        /// <summary>拔刀动作开始时触发（动画层订阅后播放拔刀动画）。</summary>
        public event Action OnDrawSwordStarted;

        /// <summary>收刀动作开始时触发（动画层订阅后播放收刀动画）。</summary>
        public event Action OnSheatheSwordStarted;

        /// <summary>第一段攻击（外砍）开始时触发（动画层订阅后播放对应斩击动画）。</summary>
        public event Action OnAttackOutStarted;

        /// <summary>第二段攻击（内砍）开始时触发（动画层订阅后播放对应斩击动画）。</summary>
        public event Action OnAttackInStarted;

        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        /// <summary>总攻击 = 基础攻击 + 装备加成。</summary>
        public int AttackDamage => baseAttackDamage + bonusAttack;
        /// <summary>总防御 = 基础防御 + 装备加成（减伤用）。</summary>
        public int Defense => baseDefense + bonusDefense;

        /// <summary>装备系统写回攻击加成（系统层规则：EquipmentSystem 计算，本类只收值）。</summary>
        public void SetAttackBonus(int bonus)
        {
            bonusAttack = Mathf.Max(0, bonus);
        }

        public void SetDefenseBonus(int bonus)
        {
            bonusDefense = Mathf.Max(0, bonus);
        }

        /// <summary>是否站在地面上（外部脚本/AI 可查询）。</summary>
        public bool IsGrounded => controller != null && controller.isGrounded;

        /// <summary>是否处于采集等定身状态（外部脚本/AI 可查询）。</summary>
        public bool IsBusy => Time.time < busyUntil;

        /// <summary>是否处于跳跃后的腾空状态。</summary>
        public bool IsJumping => isJumping;

        /// <summary>
        /// 请求一次跳跃。键盘输入与外部脚本（测试/AI）共用同一入口，
        /// 实际是否起跳由 HandleMovement 中的落地判定决定。
        /// </summary>
        public void RequestJump()
        {
            jumpRequestTime = Time.time;
        }

        /// <summary>
        /// 播放一次采集动作（拾取交互入口）：转身面向目标并短暂定身
        /// （期间移动/跳跃/攻击输入无效，重力照常避免悬空）。
        /// </summary>
        public void PlayGather(Vector3 targetPosition)
        {
            Vector3 dir = targetPosition - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);

            OnGatherStarted?.Invoke();
            busyUntil = Time.time + gatherLockTime;
        }

        /// <summary>
        /// 播放一次攻击动作（连段：第 1 击外砍 OutwardSlash → 第 2 击内砍 InwardSlash，交替循环；
        /// 距上次出招超过 attackComboResetTime 后重置回第一段）。
        /// 键盘输入与外部脚本（测试/AI）共用同一入口；伤害判定仍在 HandleAttack 里做。
        /// </summary>
        public void PlayAttack()
        {
            if (Time.time - lastComboTime > attackComboResetTime)
                attackComboIndex = 0;

            bool inward = attackComboIndex % 2 == 1;
            if (inward)
            {
                OnAttackInStarted?.Invoke();
                busyUntil = Time.time + attackInLockTime;
                lastAttackHitDelay = attackInHitDelay;
            }
            else
            {
                OnAttackOutStarted?.Invoke();
                busyUntil = Time.time + attackOutLockTime;
                lastAttackHitDelay = attackOutHitDelay;
            }

            attackComboIndex++;
            lastComboTime = Time.time;
        }

        /// <summary>播放一次拔刀动作：短暂定身（期间移动/跳跃输入无效）。
        /// 模型从腰间切到手上由 PlayerWeaponVisual 跟随动画进度处理。</summary>
        public void PlayDrawSword()
        {
            OnDrawSwordStarted?.Invoke();
            busyUntil = Time.time + drawSwordLockTime;
        }

        /// <summary>播放一次收刀动作：短暂定身（模型切回腰间由 PlayerWeaponVisual 处理）。</summary>
        public void PlaySheatheSword()
        {
            OnSheatheSwordStarted?.Invoke();
            busyUntil = Time.time + sheatheSwordLockTime;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            currentHp = maxHp;
        }

        private void Update()
        {
            HandleMovement();
            HandleAttack();
        }

        /// <summary>
        /// 把 WASD 输入换算成世界方向：按相机的水平朝向旋转（W = 镜头前方，标准第三人称 RPG 手感）。
        /// 相机不存在时退回世界方向；默认视角（yaw = 0）下两者完全一致。外部脚本 / AI 也可用它预览移动方向。
        /// </summary>
        public Vector3 InputDirectionToWorld(float h, float v)
        {
            var cam = CameraFollow.Instance;
            if (cam == null)
                return new Vector3(h, 0f, v).normalized;

            return (cam.PlanarRight * h + cam.PlanarForward * v).normalized;
        }

        private void HandleMovement()
        {
            // 有面板打开时屏蔽世界输入：重力照常，但移动/奔跑/跳跃都不响应
            bool blocked = GameInput.Blocked;

            float h = blocked ? 0f : Input.GetAxisRaw("Horizontal");
            float v = blocked ? 0f : Input.GetAxisRaw("Vertical");

            Vector3 direction = InputDirectionToWorld(h, v);

            // 按住 Shift 奔跑：速度决定动画档位（动画系统待重新规划后再接入）
            bool sprinting = !blocked && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            float speed = sprinting ? runSpeed : walkSpeed;

            if (!IsBusy && direction.sqrMagnitude > 0.01f)
            {
                controller.Move(direction * speed * Time.deltaTime);

                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // 跳跃输入：空格。与 RequestJump() 同一入口，便于外部脚本/AI 触发。
            if (!blocked && Input.GetKeyDown(KeyCode.Space))
                RequestJump();

            bool grounded = controller.isGrounded;
            if (grounded)
                lastGroundedTime = Time.time;

            // 起跳判定：在输入缓冲期内、且仍在离地宽限期内（且未处于腾空状态，防二段跳）
            if (!isJumping && !IsBusy
                && Time.time - jumpRequestTime <= jumpBufferTime
                && Time.time - lastGroundedTime <= coyoteTime)
            {
                // 由重力与目标高度反推初速度：v = sqrt(2 * |g| * h)
                verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
                isJumping = true;
                jumpRequestTime = -999f;
                lastGroundedTime = -999f;   // 起跳后立刻作废宽限，防止空中再次起跳
            }

            // 重力： grounded 时给一个微小下压力，防止贴地抖动
            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
                isJumping = false;          // 落地，恢复可跳状态
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }

        private void HandleAttack()
        {
            // 面板打开时鼠标左键留给 UI（点对话框/背包不会再挥砍）；外部脚本仍可调 PlayAttack()
            if (GameInput.Blocked) return;

            if (!IsBusy && Input.GetMouseButtonDown(0) && Time.time >= lastAttackTime + attackCooldown)
            {
                lastAttackTime = Time.time;
                PlayAttack();

                // 命中结算延后到剑身接触帧：没挥到就不掉血、不击退（与狼的"咬合帧才结算"同一套做法）
                if (attackRoutine != null) StopCoroutine(attackRoutine);
                attackRoutine = StartCoroutine(AttackHitRoutine(lastAttackHitDelay));
            }
        }

        /// <summary>挥砍命中结算：等剑到最前伸的时刻再做范围判定（延迟由当前连段段的实测值决定）。
        /// 击退由 EnemyWolf.TakeDamage 内部处理，所以"打到哪一刻推哪一刻"。</summary>
        private IEnumerator AttackHitRoutine(float hitDelay)
        {
            yield return new WaitForSeconds(hitDelay);
            attackRoutine = null;

            Vector3 center = transform.position + transform.forward * attackRange * 0.5f;
            Collider[] hits = Physics.OverlapSphere(center, attackRange * 0.5f);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                var wolf = hit.GetComponent<EnemyWolf>();
                if (wolf != null)
                {
                    wolf.TakeDamage(AttackDamage, transform.position);
                    Debug.Log($"[PlayerController] 击中灰狼，造成 {AttackDamage} 点伤害");
                    break; // 一次攻击只打中一个目标
                }
            }
        }

        /// <summary>外部调用：让玩家受到伤害（防御减伤：总防御抵扣伤害，最少 1 点）。</summary>
        public void TakeDamage(int damage)
        {
            int effectiveDamage = Mathf.Max(1, damage - Defense);
            currentHp = Mathf.Max(0, currentHp - effectiveDamage);
            Debug.Log($"[PlayerController] 受到 {damage} 伤害（防御 {Defense} 减伤 {damage - effectiveDamage}），实际 {effectiveDamage}，剩余 HP: {currentHp}");
            OnPlayerHpChanged?.Invoke(currentHp, maxHp);

            if (UIManager.Instance != null)
                UIManager.Instance.ShowToast($"受到 {effectiveDamage} 点伤害");

            if (currentHp <= 0)
            {
                Debug.Log("[PlayerController] 玩家死亡，重生回小镇");
                StartCoroutine(RespawnRoutine());
            }
        }

        private IEnumerator RespawnRoutine()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowToast("你被击败了…回到了小镇");
            yield return new WaitForSecondsRealtime(1.5f);
            SceneManager.LoadScene("Town");
        }

        /// <summary>外部调用：让玩家恢复生命值。</summary>
        public void Heal(int amount)
        {
            currentHp = Mathf.Min(maxHp, currentHp + amount);
            Debug.Log($"[PlayerController] 恢复 {amount} 点生命，当前 HP: {currentHp}");
            OnPlayerHpChanged?.Invoke(currentHp, maxHp);
        }

        /// <summary>从存档恢复 HP。</summary>
        public void SetHp(int hp)
        {
            currentHp = Mathf.Clamp(hp, 0, maxHp);
            OnPlayerHpChanged?.Invoke(currentHp, maxHp);
        }
    }
}
