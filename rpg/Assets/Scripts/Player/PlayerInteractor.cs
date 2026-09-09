using System.Collections.Generic;
using UnityEngine;
using FogHarbor.UI;

namespace FogHarbor.Player
{
    /// <summary>
    /// 玩家交互检测器：检测附近的可交互对象，按 E 键执行交互。
    /// 使用 OverlapSphere 进行检测，自动锁定最近的可交互对象。
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("交互参数")]
        [SerializeField] private float detectRadius = 2f;
        [SerializeField] private LayerMask interactableMask = ~0;

        private IInteractable nearestInteractable;
        private readonly List<IInteractable> targets = new List<IInteractable>();

        /// <summary>当前最近可交互对象的提示文本。</summary>
        public string CurrentPrompt { get; private set; }

        private void Update()
        {
            DetectInteractables();

            if (nearestInteractable != null && Input.GetKeyDown(KeyCode.E))
            {
                nearestInteractable.Interact();
            }

            // 交互提示通过 UIManager 显示
            if (UIManager.Instance != null)
                UIManager.Instance.SetPrompt(CurrentPrompt);
        }

        private void DetectInteractables()
        {
            targets.Clear();
            Collider[] hits = Physics.OverlapSphere(transform.position, detectRadius, interactableMask);

            foreach (var hit in hits)
            {
                var interactable = hit.GetComponent<IInteractable>();
                if (interactable != null)
                    targets.Add(interactable);
            }

            // 在所有命中的可交互对象中找距离最近的
            nearestInteractable = null;
            float minDist = float.MaxValue;

            foreach (var target in targets)
            {
                if (target is MonoBehaviour mb)
                {
                    float dist = Vector3.Distance(transform.position, mb.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearestInteractable = target;
                    }
                }
            }

            CurrentPrompt = nearestInteractable?.GetPrompt();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectRadius);
        }
    }
}
