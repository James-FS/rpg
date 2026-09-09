using UnityEngine;

namespace FogHarbor.Player
{
    /// <summary>
    /// 简单相机跟随：平滑跟随目标，保持固定偏移并看向目标。
    /// 挂载到 Main Camera 上，Inspector 中指定 target 为玩家。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -8f);
        [SerializeField] private float smoothSpeed = 5f;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(
                transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            transform.LookAt(target);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
