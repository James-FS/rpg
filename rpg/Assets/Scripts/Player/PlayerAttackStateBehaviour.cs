using UnityEngine;

namespace FogHarbor.Player
{
    /// <summary>Synchronizes sword contact and recovery with the Attack animation state.</summary>
    public sealed class PlayerAttackStateBehaviour : StateMachineBehaviour
    {
        [Range(0f, 1f)]
        [SerializeField] private float hitProgress = 0.48f;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.GetComponentInParent<PlayerController>()?.OnAttackAnimationEnter(
                stateInfo.IsName("JumpAttack"));
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.GetComponentInParent<PlayerController>()?.OnAttackAnimationProgress(
                stateInfo.normalizedTime, hitProgress);
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.GetComponentInParent<PlayerController>()?.OnAttackAnimationExit();
        }
    }
}
