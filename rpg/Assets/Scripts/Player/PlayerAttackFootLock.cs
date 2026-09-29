using UnityEngine;

namespace FogHarbor.Player
{
    /// <summary>Keeps both feet planted while the sword attack twists through the hips and torso.</summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class PlayerAttackFootLock : MonoBehaviour
    {
        private static readonly int AttackState = Animator.StringToHash("Base Layer.Attack");
        private static readonly int CrouchAttackState = Animator.StringToHash("Base Layer.CrouchAttack");

        private Animator animator;
        private Transform leftFoot;
        private Transform rightFoot;
        private bool locked;
        private Vector3 leftPosition;
        private Vector3 rightPosition;
        private Quaternion leftRotation;
        private Quaternion rightRotation;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || leftFoot == null || rightFoot == null)
                return;

            int stateHash = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            bool attacking = stateHash == AttackState || stateHash == CrouchAttackState;
            if (!attacking)
            {
                locked = false;
                return;
            }

            if (!locked)
            {
                leftPosition = leftFoot.position;
                rightPosition = rightFoot.position;
                // IK goal rotations are not the raw foot-bone rotations.
                leftRotation = animator.GetIKRotation(AvatarIKGoal.LeftFoot);
                rightRotation = animator.GetIKRotation(AvatarIKGoal.RightFoot);
                locked = true;
            }

            LockFoot(AvatarIKGoal.LeftFoot, leftPosition, leftRotation);
            LockFoot(AvatarIKGoal.RightFoot, rightPosition, rightRotation);
        }

        private void LockFoot(AvatarIKGoal goal, Vector3 position, Quaternion rotation)
        {
            animator.SetIKPositionWeight(goal, 1f);
            animator.SetIKRotationWeight(goal, 1f);
            animator.SetIKPosition(goal, position);
            animator.SetIKRotation(goal, rotation);
        }
    }
}
