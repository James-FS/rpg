using UnityEngine;

namespace FogHarbor.LuaLab
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class LuaLabTestPlayer : MonoBehaviour
    {
        [SerializeField] private PatrolMotor guard;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 0f, -5f);
        private CharacterController controller;
        private float verticalSpeed;
        private LuaEnvManager runtime;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            runtime = FindObjectOfType<LuaEnvManager>();
            if (guard != null) guard.SetTarget(transform);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) ResetPosition();
            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            Vector3 movement = Vector3.ClampMagnitude(input, 1f) * moveSpeed;
            if (movement.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(movement);
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            // Keep the test character on the sandbox ground.
            Vector3 desired = transform.position + movement * Time.deltaTime;
            desired.x = Mathf.Clamp(desired.x, -9f, 9f);
            desired.z = Mathf.Clamp(desired.z, -6f, 6f);
            Vector3 horizontal = desired - transform.position;
            horizontal.y = 0f;
            controller.Move(horizontal + Vector3.up * verticalSpeed * Time.deltaTime);
        }

        public void ResetPosition() { Teleport(spawnPosition); }

        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            verticalSpeed = 0f;
            controller.enabled = true;
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(12, 12, 640, 145), "LuaLab - Patrol / Chase");
            GUI.Label(new Rect(24, 38, 390, 24), "WASD / Arrow keys: move     R: reset player");
            if (guard == null) return;
            GUI.Label(new Rect(24, 62, 390, 24), "Guard: " + guard.State + "   Distance: " + guard.TargetDistance.ToString("F1") + " m");
            GUI.Label(new Rect(24, 86, 580, 24), "Save patrol_ai.lua, then F5 / Reload Lua to apply changes.");
            if (GUI.Button(new Rect(24, 114, 120, 28), "Reload Lua (F5)") && runtime != null)
                runtime.ReloadAll();
            if (runtime != null)
                GUI.Label(new Rect(156, 114, 480, 28), runtime.LastReloadMessage);
        }
    }
}