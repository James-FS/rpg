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
        [SerializeField] private float jumpHeight = 1.6f;
        [Tooltip("离开地面后仍可起跳的宽限时间（秒），用于台阶/斜坡边缘")]
        [SerializeField] private float coyoteTime = 0.12f;
        [Tooltip("落地前提前按跳的输入缓冲时间（秒）")]
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("采集")]
        [Tooltip("采集动作的定身时长（秒），应略长于 Gather 动画播完+过渡的总时长")]
        [SerializeField] private float gatherLockTime = 2.6f;

        [Header("攻击")]
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackCooldown = 1f;
        [SerializeField] private int baseAttackDamage = 5;

        [Header("防御")]
        [SerializeField] private int baseDefense = 0;

        [Header("生命")]
        [SerializeField] private int maxHp = 100;

        private CharacterController controller;
        private float verticalVelocity;
        private float lastAttackTime;
        private int currentHp;

        // 跳跃状态
        private float lastGroundedTime = -999f;
        private float jumpRequestTime = -999f;
        private bool isJumping;

        // 装备加成（由 EquipmentSystem 写回；T2 遗留补全：让装备真正影响战斗）
        private int bonusAttack;
        private int bonusDefense;

        // 采集动作（Gather 动画）期间的定身：到时前移动/跳跃/攻击输入无效，重力照常
        private Animator animator;
        private float busyUntil = -999f;

        /// <summary>HP 变化时触发（§6.1 差距 #3：HUD 订阅刷新，删每帧轮询）。</summary>
        public event Action<int, int> OnPlayerHpChanged;

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
        /// 播放一次采集动作（拾取交互入口）：转身面向目标、触发 Gather 动画并短暂定身
        /// （期间移动/跳跃/攻击输入无效，重力照常避免悬空）。动画由控制器自动切回移动状态。
        /// </summary>
        public void PlayGather(Vector3 targetPosition)
        {
            Vector3 dir = targetPosition - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);

            if (animator != null)
                animator.SetTrigger("gather");

            busyUntil = Time.time + gatherLockTime;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            currentHp = maxHp;
        }

        private void Update()
        {
            HandleMovement();
            HandleAttack();
        }

        private void HandleMovement()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 direction = new Vector3(h, 0f, v).normalized;

            // 按住 Shift 奔跑：速度决定动画档位（PlayerAnimatorDriver 把实际速度映射进 Walk/Run 混合树）
            bool sprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float speed = sprinting ? runSpeed : walkSpeed;

            if (!IsBusy && direction.sqrMagnitude > 0.01f)
            {
                controller.Move(direction * speed * Time.deltaTime);

                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // 跳跃输入：空格。与 RequestJump() 同一入口，便于外部脚本/AI 触发。
            if (Input.GetKeyDown(KeyCode.Space))
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
            if (!IsBusy && Input.GetMouseButtonDown(0) && Time.time >= lastAttackTime + attackCooldown)
            {
                lastAttackTime = Time.time;

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
