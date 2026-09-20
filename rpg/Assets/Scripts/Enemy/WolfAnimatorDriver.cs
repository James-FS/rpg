using UnityEngine;

namespace FogHarbor.Enemy
{
    /// <summary>
    /// 灰狼动画驱动：把 EnemyWolf 状态机（Patrol/Chase/Attack/Dead）映射到
    /// Quaternius 德牧 Animator 的 clip（AnimalArmature|Idle/Walk/Run/Attack/Death）。
    /// 挂在 Enemy_Wolf 根上，与 EnemyWolf 同物体。Patrol 用位移速度在 Idle/Walk 间切换
    /// （位置差而非 controller.velocity —— EnemyWolf 每帧重力 Move 会覆盖 velocity）。
    /// </summary>
    [RequireComponent(typeof(EnemyWolf))]
    public class WolfAnimatorDriver : MonoBehaviour
    {
        private const string ClipIdle = "AnimalArmature|Idle";
        private const string ClipWalk = "AnimalArmature|Walk";
        private const string ClipRun = "AnimalArmature|Run";
        private const string ClipAttack = "AnimalArmature|Attack";
        private const string ClipDeath = "AnimalArmature|Death";

        private EnemyWolf wolf;
        private Animator animator;
        private Vector3 lastPos;
        private string currentClip = "";

        private void Awake()
        {
            wolf = GetComponent<EnemyWolf>();
        }

        private void Start()
        {
            animator = GetComponentInChildren<Animator>();
            lastPos = transform.position;
            currentClip = "";
            wolf.OnAttackStarted += RestartAttackClip;
        }

        private void OnDestroy()
        {
            if (wolf != null) wolf.OnAttackStarted -= RestartAttackClip;
        }

        /// <summary>每次开始攻击动作时从头播放 Attack：Attack 状态停留时间常长于 clip 本身，
        /// 不重播会定格在收尾咬合帧直到玩家离开攻击范围。伤害由 EnemyWolf 在咬合帧单独结算。</summary>
        private void RestartAttackClip()
        {
            if (animator == null || wolf.CurrentState == EnemyWolf.WolfState.Dead) return;
            animator.Play(ClipAttack, 0, 0f);
            currentClip = ClipAttack;
        }

        private void Update()
        {
            if (wolf == null || animator == null)
                return;

            string clip = ClipIdle;
            switch (wolf.CurrentState)
            {
                case EnemyWolf.WolfState.Patrol:
                    float speed = Time.deltaTime > 0f
                        ? (transform.position - lastPos).magnitude / Time.deltaTime
                        : 0f;
                    clip = speed > 0.4f ? ClipWalk : ClipIdle;
                    break;
                case EnemyWolf.WolfState.Chase:
                    clip = ClipRun;
                    break;
                case EnemyWolf.WolfState.Attack:
                    clip = ClipAttack;
                    break;
                case EnemyWolf.WolfState.Dead:
                    clip = ClipDeath;
                    break;
            }
            lastPos = transform.position;

            if (clip != currentClip)
            {
                currentClip = clip;
                animator.CrossFadeInFixedTime(clip, 0.12f);
            }
        }
    }
}