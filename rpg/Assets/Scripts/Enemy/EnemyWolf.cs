using System.Collections;
using UnityEngine;
using FogHarbor.Items;
using FogHarbor.Player;
using FogHarbor.Session;
using FogHarbor.UI;

namespace FogHarbor.Enemy
{
    /// <summary>
    /// 灰狼敌人：空闲巡逻 → 发现玩家追击 → 近身攻击。
    /// 支持对象池复用：死亡时通过 OnDeath 回调交给刷怪器回收（不销毁），
    /// 复用前调用 ResetWolf() 重置状态。
    /// 死亡触发可配置掉落（内容数据驱动），经 GameSession.PickupItem 走架构。
    /// </summary>
    public class EnemyWolf : MonoBehaviour
    {
        public enum WolfState { Patrol, Chase, Attack, Dead }

        /// <summary>死亡回调（对象池模式下由 WolfSpawner 回收；无回调则自行销毁）。</summary>
        public System.Action<EnemyWolf> OnDeath;

        [Header("属性")]
        [SerializeField] private int maxHp = 30;
        [SerializeField] private int attackDamage = 8;

        [Header("掉落（数据驱动：Prefab/场景配置，加内容只改这里）")]
        [SerializeField] private string dropItemId = "iron_ore";
        [SerializeField] private int dropCount = 1;
        [SerializeField, Range(0f, 1f)] private float dropChance = 0.6f;

        [Header("移动")]
        [SerializeField] private float patrolSpeed = 1.8f;
        [SerializeField] private float chaseSpeed = 4.2f;
        [SerializeField] private float patrolRadius = 4f;
        [SerializeField] private float rotationSpeed = 6f;

        [Header("索敌")]
        [SerializeField] private float detectRange = 7f;
        [SerializeField] private float loseRange = 12f;
        [SerializeField] private float attackRange = 1.8f;

        [Header("攻击")]
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("碰撞体（需匹配模型缩放：模型×1.7 时 0.98→1.66 高）")]
        [SerializeField] private float ccRadius = 0.5f;
        [SerializeField] private float ccHeight = 1.6f;
        [SerializeField] private float ccCenterY = 0.8f;

        private CharacterController controller;
        private Transform player;
        private WolfState state = WolfState.Patrol;
        private int currentHp;
        private float lastAttackTime;
        private float verticalVelocity;
        private Vector3 homePosition;
        private Vector3 patrolTarget;
        private float patrolWaitTimer;
        private Renderer bodyRenderer;
        private Color originalColor;
        private Coroutine flashRoutine;
        private Coroutine deathRoutine;

        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;

        /// <summary>当前状态（供 WolfAnimatorDriver 驱动动画，不改动内部状态机）。</summary>
        public WolfState CurrentState => state;

        /// <summary>每次实际咬中玩家时触发（供动画层重播 Attack clip，伤害与动画同步）。</summary>
        public event System.Action OnAttackLanded;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (controller == null) controller = gameObject.AddComponent<CharacterController>();
            controller.radius = ccRadius;
            controller.height = ccHeight;
            controller.center = new Vector3(0f, ccCenterY, 0f);

            currentHp = maxHp;
            lastAttackTime = -attackCooldown;

            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                bodyRenderer = renderer;
                originalColor = renderer.material.color;
            }
        }

        private void OnEnable()
        {
            // 每次从池激活时重新绑定玩家（场景切换后旧引用失效）
            player = null;
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        private void Update()
        {
            if (state == WolfState.Dead) return;
            if (controller == null) return;
            ApplyGravity();

            switch (state)
            {
                case WolfState.Patrol: UpdatePatrol(); break;
                case WolfState.Chase: UpdateChase(); break;
                case WolfState.Attack: UpdateAttack(); break;
            }
        }

        // ─── 状态：巡逻 ───

        private void UpdatePatrol()
        {
            if (player != null && Vector3.Distance(transform.position, player.position) <= detectRange)
            {
                state = WolfState.Chase;
                return;
            }

            if (patrolWaitTimer > 0f)
            {
                patrolWaitTimer -= Time.deltaTime;
                return;
            }

            if (Vector3.Distance(transform.position, patrolTarget) < 0.3f)
            {
                patrolWaitTimer = Random.Range(1f, 3f);
                patrolTarget = homePosition + new Vector3(
                    Random.Range(-patrolRadius, patrolRadius), 0f,
                    Random.Range(-patrolRadius, patrolRadius));
                return;
            }

            Vector3 dir = patrolTarget - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                dir.Normalize();
                controller.Move(dir * patrolSpeed * Time.deltaTime);
                FaceDirection(dir);
            }
        }

        // ─── 状态：追击 ───

        private void UpdateChase()
        {
            if (player == null) { state = WolfState.Patrol; return; }

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist > loseRange) { state = WolfState.Patrol; return; }
            if (dist <= attackRange) { state = WolfState.Attack; return; }

            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                dir.Normalize();
                controller.Move(dir * chaseSpeed * Time.deltaTime);
                FaceDirection(dir);
            }
        }

        // ─── 状态：攻击 ───

        private void UpdateAttack()
        {
            if (player == null) { state = WolfState.Patrol; return; }

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist > attackRange * 1.4f) { state = WolfState.Chase; return; }

            Vector3 faceDir = player.position - transform.position;
            faceDir.y = 0f;
            FaceDirection(faceDir);

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                lastAttackTime = Time.time;
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.TakeDamage(attackDamage);
                    OnAttackLanded?.Invoke();
                    Debug.Log($"[EnemyWolf] {name} 攻击玩家，造成 {attackDamage} 伤害");
                }
            }
        }

        // ─── 受击 / 死亡 ───

        public void TakeDamage(int damage, Vector3 hitFrom)
        {
            if (state == WolfState.Dead) return;

            currentHp = Mathf.Max(0, currentHp - damage);
            FlashHit();

            if (state == WolfState.Patrol)
                state = WolfState.Chase;

            if (currentHp <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            if (state == WolfState.Dead) return;
            state = WolfState.Dead;

            if (bodyRenderer != null)
                bodyRenderer.material.color = new Color(0.28f, 0.28f, 0.30f, 1f);

            if (UIManager.Instance != null)
                UIManager.Instance.ShowToast("击败了灰狼！");
            Debug.Log($"[EnemyWolf] {name} 死亡");

            // 掉落：数据驱动配置，经 GameSession.PickupItem 走架构（入库+推进任务+存档）。
            if (!string.IsNullOrEmpty(dropItemId) && dropCount > 0
                && Random.value <= dropChance)
            {
                SpawnDrop();
            }

            if (deathRoutine != null) StopCoroutine(deathRoutine);
            deathRoutine = StartCoroutine(DeathRoutine());
        }

        /// <summary>发掉落：让 GameSession 拾取物品（复用拾取推进逻辑），并显示 toast。</summary>
        private void SpawnDrop()
        {
            var session = FindObjectOfType<GameSession>();
            if (session == null)
            {
                Debug.LogWarning($"[EnemyWolf] 未找到 GameSession，{dropItemId} 掉落未入库");
                return;
            }

            var result = session.PickupItem(dropItemId, dropCount);
            var data = ItemDatabase.GetById(dropItemId);
            string name = data != null ? data.DisplayName : dropItemId;
            if (result.Success && UIManager.Instance != null)
                UIManager.Instance.ShowToast($"获得 {name} x{dropCount}");
            else if (!result.Success)
                Debug.LogWarning($"[EnemyWolf] 掉落入库失败: {result.Message}");
        }

        private IEnumerator DeathRoutine()
        {
            // 等 Death clip（1.07s）播完再回收，避免狼死到一半凭空消失
            yield return new WaitForSeconds(1.2f);

            if (OnDeath != null)
            {
                // 对象池模式：交给刷怪器回收（不销毁）
                OnDeath.Invoke(this);
            }
            else
            {
                Destroy(gameObject);
            }
            deathRoutine = null;
        }

        private void FlashHit()
        {
            if (bodyRenderer == null) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            bodyRenderer.material.color = Color.red;
            yield return new WaitForSeconds(0.12f);
            if (bodyRenderer != null)
                bodyRenderer.material.color = originalColor;
            flashRoutine = null;
        }

        // ─── 对象池复用重置 ───

        /// <summary>从池里重新激活时调用：重置 HP/状态/巡逻原点/颜色。</summary>
        public void ResetWolf(Vector3 spawnPosition)
        {
            if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }
            if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }

            currentHp = maxHp;
            state = WolfState.Patrol;
            homePosition = spawnPosition;
            patrolTarget = spawnPosition;
            patrolWaitTimer = Random.Range(0.5f, 1.5f);
            lastAttackTime = -attackCooldown;
            verticalVelocity = 0f;

            transform.position = new Vector3(spawnPosition.x, 0.1f, spawnPosition.z);
            transform.rotation = Quaternion.identity;

            if (bodyRenderer != null)
                bodyRenderer.material.color = originalColor;

            if (controller != null)
                controller.enabled = true;
        }

        // ─── 通用 ───

        private void FaceDirection(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            else
                verticalVelocity += Physics.gravity.y * Time.deltaTime;
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }
    }
}