using UnityEngine;

namespace FogHarbor.Player
{
    /// <summary>
    /// 攻击动画（Humanoid 肌肉重定向）会把角色整体压低，看起来像陷进地里。
    /// 每帧用双脚最低点对齐站立高度，保证脚贴地、人不下沉。
    /// </summary>
    public class AttackHeightLock : MonoBehaviour
    {
        [Tooltip("脚底目标高度（世界 Y）；<=0 时用首帧双脚高度")]
        [SerializeField] private float standFootY = -1f;
        [Tooltip("允许的脚底误差（米），小于则不修正")]
        [SerializeField] private float tolerance = 0.02f;

        private Transform leftFoot;
        private Transform rightFoot;
        private CharacterController controller;
        private bool captured;

        private void Start()
        {
            controller = GetComponentInParent<CharacterController>();
            var anim = GetComponentInChildren<Animator>();
            if (anim == null) return;
            foreach (var t in anim.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "mixamorig:LeftFoot") leftFoot = t;
                else if (t.name == "mixamorig:RightFoot") rightFoot = t;
            }
        }

        private void LateUpdate()
        {
            if (leftFoot == null || rightFoot == null) return;

            float minFoot = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
            if (!captured)
            {
                // 出生点在半空时（场景里玩家 y 高于地面）不能立刻采集：
                // 悬空高度会被当成站立高度，之后每帧把角色抬回半空，表现为永久浮空。
                // 等落地（isGrounded）再采集，之后行为不变。
                if (controller != null && !controller.isGrounded) return;
                if (standFootY <= 0f) standFootY = minFoot;
                captured = true;
            }

            float delta = standFootY - minFoot;
            if (delta > tolerance)
                transform.position += Vector3.up * delta;
        }
    }
}
