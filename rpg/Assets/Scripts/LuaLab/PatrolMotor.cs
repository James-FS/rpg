using UnityEngine;

namespace FogHarbor.LuaLab
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PatrolMotor : MonoBehaviour
    {
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform chaseTarget;
        [SerializeField, Min(0.1f)] private float turnSpeed = 360f;
        private CharacterController controller;
        private float speed = 1.5f;
        private float verticalSpeed;
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        public bool HasTarget => chaseTarget != null && chaseTarget.gameObject.activeInHierarchy;
        public float TargetDistance => HasTarget ? DistanceTo(chaseTarget.position) : float.PositiveInfinity;
        public Vector3 GetTargetPosition() { return HasTarget ? chaseTarget.position : transform.position; }
        public void SetTarget(Transform target) { chaseTarget = target; }
        public int PointCount => patrolPoints == null ? 0 : patrolPoints.Length;
        public int TargetIndex { get; private set; }
        public int ArrivalCount { get; private set; }
        public string State { get; private set; } = "unloaded";

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
        }

        public void SetSpeed(float value) { speed = Mathf.Max(0f, value); }
        public void SetState(string value) { State = value; }
        public void NotifyArrival() { ArrivalCount++; }
        public Vector3 GetPatrolPoint(int index)
        {
            if (index < 0 || index >= PointCount || patrolPoints[index] == null)
                throw new System.ArgumentOutOfRangeException(nameof(index), "Assign valid LuaLab patrol points.");
            TargetIndex = index;
            return patrolPoints[index].position;
        }

        public float DistanceTo(Vector3 target)
        {
            target.y = transform.position.y;
            return Vector3.Distance(transform.position, target);
        }

        public void MoveTo(Vector3 target, float deltaTime)
        {
            Vector3 direction = target - transform.position;
            direction.y = 0f;
            Vector3 horizontal = Vector3.ClampMagnitude(direction, speed * deltaTime);
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), turnSpeed * deltaTime);
            Move(horizontal, deltaTime);
        }

        public void Idle(float deltaTime) { Move(Vector3.zero, deltaTime); }
        public void Stop() { if (animator != null) animator.SetFloat(SpeedParameter, 0f); }

        private void Move(Vector3 horizontal, float deltaTime)
        {
            if (controller == null || !controller.enabled) return;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * deltaTime;
            Vector3 before = transform.position;
            controller.Move(horizontal + Vector3.up * verticalSpeed * deltaTime);
            Vector3 actual = transform.position - before;
            actual.y = 0f;
            if (animator != null)
                animator.SetFloat(SpeedParameter, deltaTime > 0f
                    ? Mathf.Clamp01(actual.magnitude / deltaTime / 1.5f) : 0f);
        }
    }
}