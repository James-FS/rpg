using AIBot.Core.Output;
using AIBot.Unity;
using UnityEngine;

namespace FogHarbor.Dialogue
{
    [RequireComponent(typeof(NpcAgent))]
    public sealed class NpcDialogueAnimator : MonoBehaviour
    {
        private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
        private static readonly int Wave = Animator.StringToHash("Base Layer.Wave");
        private static readonly int Nod = Animator.StringToHash("Base Layer.Nod");
        private static readonly int Bow = Animator.StringToHash("Base Layer.Bow");

        [SerializeField] private Animator animator;

        private NpcAgent agent;

        private void Awake()
        {
            agent = GetComponent<NpcAgent>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
        }

        private void OnEnable()
        {
            agent.onReply.AddListener(OnReply);
        }

        private void OnDisable()
        {
            agent.onReply.RemoveListener(OnReply);
        }

        private void OnReply(StructuredReply reply)
        {
            if (reply == null || animator == null)
                return;

            int state;
            switch (reply.action)
            {
                case "wave": state = Wave; break;
                case "nod": state = Nod; break;
                case "bow": state = Bow; break;
                case "idle": state = Idle; break;
                default: return;
            }

            animator.CrossFadeInFixedTime(state, 0.15f, 0, 0f);
        }
    }
}
