using UnityEngine;

namespace FogHarbor.Player
{
    /// <summary>
    /// 把玩家的实际运动状态映射到 Animator 参数：
    /// ① speed：实际水平位移速度 → Idle/Walk/Run 混合树（0=Idle / 0.5=Walk / 1=Run）。
    ///    用位置差计算而非 controller.velocity：PlayerController 每帧用重力 Move 覆盖 velocity，读 velocity 永远≈0。
    ///    只统计水平位移（忽略 Y），跳跃/重力垂直运动不会混入行走档位。
    ///    低于 0.3m/s 归零避免站立微抖；输出归一化 = rawSpeed / 5
    ///    （走路 2.5 → 纯 Walk，奔跑 5 → 纯 Run，斜向移动按实际合速度插值）。
    ///    逐帧瞬时测速受帧率抖动影响大（卡顿帧会让参数瞬间跳到 0~1，动画在待机/走/跑间闪切），
    ///    因此经 speedSmoothTime 时间常数（默认 0.1s）平滑后再写参数。
    /// ② airborne：PlayerController.IsJumping → JumpAir/JumpLand 状态切换
    ///    （起跳→腾空、落地→缓冲着地，空中不播跑步动画）。
    /// ③ gather：订阅 PlayerController.OnGatherStarted 一次性触发器 → 播放 Gather 采集动画
    ///    （定身时长由 PlayerController.gatherLockTime 控制，本类只管动画）。
    /// ④ draw / sheathe：订阅 OnDrawSwordStarted / OnSheatheSwordStarted → 播放拔刀 / 收刀动画
    ///    （模型在腰间与手上之间的切换由 PlayerWeaponVisual 处理）。
    /// ⑤ attackOut / attackIn：订阅 OnAttackOutStarted / OnAttackInStarted → 播放两段斩击
    ///    （连段顺序：外砍 → 内砍 交替，由 PlayerController 决定）。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerAnimatorDriver : MonoBehaviour
    {
        private const float MaxSpeedForAnim = 5f;
        private const float DeadZone = 0.3f;

        [Tooltip("速度参数平滑时间常数（秒）：滤掉逐帧测速的帧率抖动，避免动画在待机/走/跑之间闪切")]
        [SerializeField] private float speedSmoothTime = 0.1f;

        private Animator animator;
        private PlayerController player;
        private Vector3 lastPos;
        private bool hasLast;
        private float smoothedSpeed;

        private void Start()
        {
            animator = GetComponentInChildren<Animator>();
            player = GetComponent<PlayerController>();
            if (animator == null)
                Debug.LogWarning("[PlayerAnimatorDriver] 未找到 Animator，动画参数不会更新");
            if (player != null)
            {
                player.OnGatherStarted += HandleGatherStarted;
                player.OnDrawSwordStarted += HandleDrawSwordStarted;
                player.OnSheatheSwordStarted += HandleSheatheSwordStarted;
                player.OnAttackOutStarted += HandleAttackOutStarted;
                player.OnAttackInStarted += HandleAttackInStarted;
            }
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.OnGatherStarted -= HandleGatherStarted;
                player.OnDrawSwordStarted -= HandleDrawSwordStarted;
                player.OnSheatheSwordStarted -= HandleSheatheSwordStarted;
                player.OnAttackOutStarted -= HandleAttackOutStarted;
                player.OnAttackInStarted -= HandleAttackInStarted;
            }
        }

        private void OnEnable()
        {
            lastPos = transform.position;
            hasLast = true;
        }

        private void OnDisable()
        {
            hasLast = false;
        }

        private void HandleGatherStarted()
        {
            if (animator != null)
                animator.SetTrigger("gather");
        }

        private void HandleDrawSwordStarted()
        {
            if (animator != null)
                animator.SetTrigger("draw");
        }

        private void HandleSheatheSwordStarted()
        {
            if (animator != null)
                animator.SetTrigger("sheathe");
        }

        private void HandleAttackOutStarted()
        {
            if (animator != null)
                animator.SetTrigger("attackOut");
        }

        private void HandleAttackInStarted()
        {
            if (animator != null)
                animator.SetTrigger("attackIn");
        }

        private void Update()
        {
            if (animator == null || !hasLast)
                return;

            Vector3 delta = transform.position - lastPos;
            lastPos = transform.position;
            delta.y = 0f;

            float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            if (speed < DeadZone)
                speed = 0f;

            // 帧率无关的时间常数平滑（指数滤波）：跟随真实速度变化（起步/停下约 0.1s，肉眼无感），
            // 但不被卡顿帧的瞬时测速尖峰带跑偏，避免动画档位闪切造成的腿部抖动。
            float target = Mathf.Clamp01(speed / MaxSpeedForAnim);
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, target, 1f - Mathf.Exp(-Time.deltaTime / speedSmoothTime));
            animator.SetFloat("speed", smoothedSpeed);

            if (player != null)
                animator.SetBool("airborne", player.IsJumping);
        }
    }
}
