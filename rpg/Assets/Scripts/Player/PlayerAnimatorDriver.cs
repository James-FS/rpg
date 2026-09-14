using UnityEngine;

namespace FogHarbor.Player
{
    /// <summary>
    /// 把玩家的实际位移速度映射到 Animator 的 speed 参数，
    /// 驱动 Idle/Walk/Sprint 混合树（替换灰盒角色后补的动画驱动，不改 PlayerController）。
    /// 用位置差计算速度而非 controller.velocity：PlayerController 每帧用重力 Move 覆盖 velocity，
    /// 读 velocity 永远≈0。位移差只反映真实移动；低于 0.3m/s 归零避免重力抖动混入 walk。
    /// 输出归一化到 0..1（Tuanjie 引擎对 Simple1D BlendTree 的阈值强制归一化，threshold=0/0.5/1.0
    /// 对应 0/2.5/5 m/s：走路 2.5 → 纯 Walk，Shift 奔跑 5 → 纯 Run，故 speed = rawSpeed/5，clamp 0..1）。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerAnimatorDriver : MonoBehaviour
    {
        private const float MaxSpeedForAnim = 5f;

        private Animator animator;
        private Vector3 lastPos;
        private bool hasLast;

        private void Start()
        {
            animator = GetComponentInChildren<Animator>();
            lastPos = transform.position;
            hasLast = true;
        }

        private void Update()
        {
            if (animator == null || !hasLast)
                return;

            Vector3 delta = transform.position - lastPos;
            lastPos = transform.position;

            float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            if (speed < 0.3f)
                speed = 0f;

            animator.SetFloat("speed", Mathf.Clamp01(speed / MaxSpeedForAnim));
        }

        private void OnDisable()
        {
            hasLast = false;
        }
    }
}