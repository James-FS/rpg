using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
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
        [SerializeField] private float attackCooldown = 1.7f;
        [Tooltip("镐子连续攻击间隔（秒），与采矿动作时长匹配")]
        [SerializeField] private float miningAttackCooldown = 1.2f;
        [SerializeField] private int baseAttackDamage = 5;
        [Tooltip("移动中跳劈的前进距离（米）；原地跳劈不位移")]
        [SerializeField] private float jumpAttackTravelDistance = 1.2f;

        [Header("拔刀 / 收刀")]
        [Tooltip("拔刀动作的定身时长（秒），应略长于 WithdrawingSword 动画播完+过渡的总时长（≈1.4s）")]
        [SerializeField] private float drawSwordLockTime = 1.45f;
        [Tooltip("收刀动作的定身时长（秒），应略长于 SheathingSword 动画播完+过渡的总时长（≈1.2s）")]
        [SerializeField] private float sheatheSwordLockTime = 1.7f;

        [Header("防御")]
        [SerializeField] private int baseDefense = 0;

        [Header("生命")]
        [SerializeField] private int maxHp = 100;

        private CharacterController controller;
        private PlayerWeaponVisual weaponVisual;
        private float standingHeight;
        private Vector3 standingCenter;
        private bool isCrouching;
        private float verticalVelocity;
        private float lastAttackTime = float.NegativeInfinity;
        private bool attackInProgress;
        private bool attackHitApplied;
        private float attackSafetyUntil;
        private Vector3 jumpAttackDirection;
        private float jumpAttackTravelProgress;
        private Vector3 jumpAttackPendingMotion;
        private bool jumpAttackActive;
        private int currentHp;
        private Coroutine deathRoutine;

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

        /// <summary>单次剑击开始时触发，供动画与持剑外观订阅。</summary>
        public event Action OnAttackStarted;

        public event Action OnReviveReady;
        public bool CanRevive { get; private set; }
        public bool IsDead => currentHp <= 0;
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
        public bool IsBusy => IsDead || Time.time < busyUntil || (attackInProgress && Time.time < attackSafetyUntil);

        /// <summary>是否处于跳跃后的腾空状态。</summary>
        public bool IsJumping => isJumping;

        /// <summary>按住 Ctrl 时保持蹲姿；头顶空间不足时松开 Ctrl 仍保持蹲姿。</summary>
        public bool IsCrouching => isCrouching;

        /// <summary>
        /// 请求一次跳跃。键盘输入与外部脚本（测试/AI）共用同一入口，
        /// 实际是否起跳由 HandleMovement 中的落地判定决定。
        /// </summary>
        public void RequestJump()
        {
            if (IsDead || isCrouching) return;
            jumpRequestTime = Time.time;
        }

        /// <summary>
        /// 播放一次采集动作（拾取交互入口）：转身面向目标并短暂定身
        /// （期间移动/跳跃/攻击输入无效，重力照常避免悬空）。
        /// </summary>
        public void PlayGather(Vector3 targetPosition)
        {
            if (IsDead) return;
            Vector3 dir = targetPosition - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);

            OnGatherStarted?.Invoke();
            busyUntil = Time.time + gatherLockTime;
        }

        /// <summary>播放一次剑击；鼠标输入和外部调用共用此入口。</summary>
        public void PlayAttack()
        {
            // Swords require an explicit draw; mining tools remain ready when equipped.
            if (weaponVisual == null || !weaponVisual.CanAttack)
                return;

            float cooldown = weaponVisual != null && weaponVisual.IsMiningTool ? miningAttackCooldown : attackCooldown;
            if (IsBusy || Time.time < lastAttackTime + cooldown)
                return;

            // The mining clip is a planted, standing strike.
            if (weaponVisual != null && weaponVisual.IsMiningTool)
            {
                if (isJumping) return;
                if (isCrouching)
                {
                    if (!CanStandUp()) return;
                    isCrouching = false;
                    controller.height = standingHeight;
                    controller.center = standingCenter;
                }
            }
            lastAttackTime = Time.time;
            attackInProgress = true;
            attackHitApplied = false;
            jumpAttackDirection = isJumping && !GameInput.Blocked
                ? InputDirectionToWorld(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))
                : Vector3.zero;
            if (jumpAttackDirection.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(jumpAttackDirection);
            // Only a safety net for a missing Animator callback; normal recovery comes from Attack state exit.
            attackSafetyUntil = Time.time + 4f;
            OnAttackStarted?.Invoke();
        }

        /// <summary>由 Attack 状态回调；命中和动作锁定跟随动画进度。</summary>
        public void OnAttackAnimationEnter(bool isJumpAttack)
        {
            if (IsDead) return;
            attackInProgress = true;
            attackHitApplied = false;
            attackSafetyUntil = Time.time + 4f;
            jumpAttackActive = isJumpAttack;
            jumpAttackTravelProgress = 0f;
        }

        public void OnAttackAnimationProgress(float normalizedTime, float hitProgress)
        {
            if (IsDead || !attackInProgress)
                return;

            if (jumpAttackActive && jumpAttackDirection.sqrMagnitude > 0.01f)
            {
                // Move the actual collision body during the lunge; completed distance remains after the state exits.
                float progress = Mathf.Clamp01(normalizedTime / 0.7f);
                progress = progress * progress * (3f - 2f * progress);
                float delta = Mathf.Max(0f, progress - jumpAttackTravelProgress);
                // Animator updates after Update. Apply this on the next movement tick, before gravity,
                // so the final CharacterController.Move of the frame retains the ground contact.
                jumpAttackPendingMotion += jumpAttackDirection * (jumpAttackTravelDistance * delta);
                jumpAttackTravelProgress = progress;
            }

            if (attackHitApplied || normalizedTime < hitProgress)
                return;

            attackHitApplied = true;
            ResolveAttackHit();
        }

        public void OnAttackAnimationExit()
        {
            attackInProgress = false;
            jumpAttackActive = false;
            jumpAttackDirection = Vector3.zero;
        }

        /// <summary>播放一次拔刀动作：短暂定身（期间移动/跳跃输入无效）。
        /// 模型从腰间切到手上由 PlayerWeaponVisual 跟随动画进度处理。</summary>
        public void PlayDrawSword()
        {
            if (IsDead) return;
            OnDrawSwordStarted?.Invoke();
            busyUntil = Time.time + drawSwordLockTime;
        }

        /// <summary>播放一次收刀动作：短暂定身（模型切回腰间由 PlayerWeaponVisual 处理）。</summary>
        public void PlaySheatheSword()
        {
            if (IsDead) return;
            OnSheatheSwordStarted?.Invoke();
            busyUntil = Time.time + sheatheSwordLockTime;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            weaponVisual = GetComponent<PlayerWeaponVisual>();
            standingHeight = controller.height;
            standingCenter = controller.center;
            currentHp = maxHp;
        }

        private void Update()
        {
            HandleCrouch();
            HandleMovement();
            HandleAttack();
        }

        private void HandleCrouch()
        {
            bool held = !GameInput.Blocked
                && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
            if (IsBusy || isJumping || held == isCrouching)
                return;

            if (!held && !CanStandUp())
                return;

            isCrouching = held;
            float height = held ? Mathf.Max(controller.radius * 2.1f, standingHeight * 0.6f) : standingHeight;
            controller.height = height;
            controller.center = standingCenter - Vector3.up * ((standingHeight - height) * 0.5f);
        }

        private bool CanStandUp()
        {
            float radius = controller.radius * 0.95f;
            Vector3 center = transform.TransformPoint(standingCenter);
            Vector3 offset = transform.up * (standingHeight * 0.5f - radius - 0.03f);
            foreach (Collider hit in Physics.OverlapCapsule(center - offset, center + offset,
                         radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform))
                    return false;
            }
            return true;
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
            bool blocked = IsDead || GameInput.Blocked;
            bool grounded = controller.isGrounded;

            float h = blocked ? 0f : Input.GetAxisRaw("Horizontal");
            float v = blocked ? 0f : Input.GetAxisRaw("Vertical");

            Vector3 direction = InputDirectionToWorld(h, v);

            // 按住 Shift 奔跑：速度决定动画档位（动画系统待重新规划后再接入）
            bool sprinting = !blocked && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            float speed = sprinting ? runSpeed : walkSpeed;

            if (!IsBusy && !isCrouching && direction.sqrMagnitude > 0.01f)
            {
                controller.Move(direction * speed * Time.deltaTime);

                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            if (jumpAttackPendingMotion.sqrMagnitude > 0.000001f)
            {
                controller.Move(jumpAttackPendingMotion);
                jumpAttackPendingMotion = Vector3.zero;
            }

            // 跳跃输入：空格。与 RequestJump() 同一入口，便于外部脚本/AI 触发。
            if (!blocked && Input.GetKeyDown(KeyCode.Space))
                RequestJump();

            if (grounded)
                lastGroundedTime = Time.time;

            // 起跳判定：在输入缓冲期内、且仍在离地宽限期内（且未处于腾空状态，防二段跳）
            if (!isJumping && !IsBusy && !isCrouching
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
            // 面板打开时鼠标左键留给 UI；外部脚本仍可调 PlayAttack()。
            if (!GameInput.Blocked && Input.GetMouseButtonDown(0)
                && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                PlayAttack();
        }

        /// <summary>挥砍命中结算：由 Attack 状态到达剑刃接触进度时调用。
        /// 击退由 EnemyWolf.TakeDamage 内部处理，所以"打到哪一刻推哪一刻"。</summary>
        public float MiningReach => attackRange;

        private void ResolveAttackHit()
        {
            // Reject late animation callbacks after sheathing or changing equipment.
            if (weaponVisual == null || !weaponVisual.CanAttack) return;
            Vector3 center = transform.position + transform.forward * attackRange * 0.5f;
            Collider[] hits = Physics.OverlapSphere(center, attackRange * 0.5f);
            if (weaponVisual != null && weaponVisual.IsMiningTool)
            {
                FogHarbor.Items.QuestItemPickup nearestOre = null;
                float nearestOreDistance = float.PositiveInfinity;
                var miningHits = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f,
                    attackRange, ~0, QueryTriggerInteraction.Collide);
                foreach (var hit in miningHits)
                {
                    var ore = hit.GetComponentInParent<FogHarbor.Items.QuestItemPickup>();
                    if (ore == null || !ore.CanMine(this, out float surfaceDistance)) continue;
                    if (surfaceDistance < nearestOreDistance)
                    {
                        nearestOre = ore;
                        nearestOreDistance = surfaceDistance;
                    }
                }
                if (nearestOre != null)
                {
                    nearestOre.TryMine(this);
                    return;
                }
            }
            EnemyWolf target = null;
            float nearestSqrDistance = float.PositiveInfinity;

            foreach (var hit in hits)
            {
                var wolf = hit.GetComponentInParent<EnemyWolf>();
                if (wolf == null || wolf.CurrentState == EnemyWolf.WolfState.Dead)
                    continue;

                Vector3 toWolf = wolf.transform.position - transform.position;
                toWolf.y = 0f;
                if (Vector3.Dot(transform.forward, toWolf) <= 0f)
                    continue;

                float sqrDistance = toWolf.sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    target = wolf;
                    nearestSqrDistance = sqrDistance;
                }
            }

            if (target == null) return;
            target.TakeDamage(AttackDamage, transform.position);
            Debug.Log($"[PlayerController] 击中灰狼，造成 {AttackDamage} 点伤害");
        }

        /// <summary>外部调用：让玩家受到伤害（防御减伤：总防御抵扣伤害，最少 1 点）。</summary>
        public void TakeDamage(int damage)
        {
            if (IsDead) return;
            int effectiveDamage = Mathf.Max(1, damage - Defense);
            currentHp = Mathf.Max(0, currentHp - effectiveDamage);
            Debug.Log($"[PlayerController] 受到 {damage} 伤害（防御 {Defense} 减伤 {damage - effectiveDamage}），实际 {effectiveDamage}，剩余 HP: {currentHp}");
            OnPlayerHpChanged?.Invoke(currentHp, maxHp);

            if (UIManager.Instance != null)
                UIManager.Instance.ShowToast($"受到 {effectiveDamage} 点伤害");

            if (currentHp <= 0 && deathRoutine == null)
            {
                OnAttackAnimationExit();
                jumpAttackPendingMotion = Vector3.zero;
                jumpRequestTime = -999f;
                busyUntil = -999f;
                verticalVelocity = Mathf.Min(0f, verticalVelocity);
                Debug.Log("[PlayerController] 玩家死亡，播放死亡动画后等待复活");
                deathRoutine = StartCoroutine(DeathRoutine());
            }
        }

        private IEnumerator DeathRoutine()
        {
            // Match the Animator's scaled time so pausing does not cut the death animation short.
            var animationDriver = GetComponent<PlayerAnimatorDriver>();
            float delay = animationDriver != null ? animationDriver.DeathDuration : 1.5f;
            yield return new WaitForSeconds(delay);
            deathRoutine = null;
            if (IsDead)
            {
                CanRevive = true;
                OnReviveReady?.Invoke();
            }
        }

        /// <summary>Death UI intent: revive in Town only after the death animation completes.</summary>
        public bool RequestRevive()
        {
            if (!IsDead || !CanRevive) return false;
            CanRevive = false;
            var session = FindObjectOfType<FogHarbor.Session.GameSession>();
            if (session != null && session.ChangeScene("Town", true)) return true;
            CanRevive = true;
            return false;
        }

        private void CancelPendingDeath()
        {
            CanRevive = false;
            if (deathRoutine == null) return;
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }

        /// <summary>外部调用：让玩家恢复生命值。</summary>
        public void Heal(int amount)
        {
            currentHp = Mathf.Min(maxHp, currentHp + amount);
            if (currentHp > 0) CancelPendingDeath();
            Debug.Log($"[PlayerController] 恢复 {amount} 点生命，当前 HP: {currentHp}");
            OnPlayerHpChanged?.Invoke(currentHp, maxHp);
        }

        /// <summary>从存档恢复 HP。</summary>
        public void SetHp(int hp)
        {
            currentHp = Mathf.Clamp(hp, 0, maxHp);
            if (currentHp > 0) CancelPendingDeath();
            OnPlayerHpChanged?.Invoke(currentHp, maxHp);
        }

        public void RestorePose(Vector3 position, float yaw)
        {
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            verticalVelocity = 0;
            isJumping = false;
            jumpRequestTime = lastGroundedTime = -999f;
            jumpAttackPendingMotion = Vector3.zero;
            Physics.SyncTransforms();
            if (controller != null) controller.enabled = wasEnabled;
        }

        public void RestoreHealth(int hp, int savedMaxHp)
        {
            maxHp = Mathf.Max(1, savedMaxHp);
            SetHp(hp);
            if (IsDead) CanRevive = true;
        }
    }
}



