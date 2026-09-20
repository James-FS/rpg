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
        [Tooltip("咬中判定延迟（秒）：从攻击动作开始到伤害结算，对齐 Attack clip 的咬合帧（实测 1.2s clip 的最大前伸在 0.36s 处）")]
        [SerializeField] private float attackHitDelay = 0.36f;

        [Header("受击击退")]
        [Tooltip("每被击中一次沿受击方向后退的距离（米）")]
        [SerializeField] private float knockbackDistance = 1.4f;
        [Tooltip("击退持续时间（秒）：期间由击退接管移动，AI 不移动")]
        [SerializeField] private float knockbackDuration = 0.25f;

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
        private Coroutine attackRoutine;

        // 击退状态：>0 时由击退接管移动（AI 行为暂停），速度按剩余时间线性衰减
        private float knockbackTimeLeft;
        private Vector3 knockbackDir;
        private float knockbackSpeed;

        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;

        /// <summary>当前状态（供 WolfAnimatorDriver 驱动动画，不改动内部状态机）。</summary>
        public WolfState CurrentState => state;

        /// <summary>每次开始攻击动作时触发（供动画层从头播放 Attack clip；伤害在咬合帧才结算）。</summary>
        public event System.Action OnAttackStarted;

        /// <summary>咬中判定生效（动画咬合帧）时触发，此刻才真实结算伤害。</summary>
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

            // 被击退期间：由击退接管移动，AI 行为（巡逻/追击/攻击）暂停
            if (knockbackTimeLeft > 0f)
            {
                UpdateKnockback();
                return;
            }

            switch (state)
            {
                case WolfState.Patrol: UpdatePatrol(); break;
                case WolfState.Chase: UpdateChase(); break;
                case WolfState.Attack: UpdateAttack(); break;
            }
        }

        // ─── 击退 ───

        /// <summary>沿"从攻击者指向自己"的方向被推开；总位移约等于 knockbackDistance。</summary>
        private void ApplyKnockback(Vector3 hitFrom)
        {
            Vector3 dir = transform.position - hitFrom;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = -transform.forward;      // 与攻击者完全重合时往自己身后推
            dir.Normalize();

            knockbackDir = dir;
            knockbackTimeLeft = knockbackDuration;
            // 线性衰减 v(t) = v0·(1 - t/T) 的总位移 = v0·T/2 → v0 = 2·距离/T
            knockbackSpeed = 2f * knockbackDistance / Mathf.Max(0.01f, knockbackDuration);

            // 打断咬击前摇：被打中时不再结算这次伤害（玩家抢刀能把它咬空）
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
            lastAttackTime = Time.time;         // 击退后重新起冷却，避免贴脸连咬
        }

        private void UpdateKnockback()
        {
            float t = knockbackTimeLeft / Mathf.Max(0.01f, knockbackDuration);
            controller.Move(knockbackDir * (knockbackSpeed * t * Time.deltaTime));
            knockbackTimeLeft -= Time.deltaTime;
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

                // 先起手播动画，伤害等咬合帧再结算（避免"先掉血、后见咬"）
                OnAttackStarted?.Invoke();
                if (attackRoutine != null) StopCoroutine(attackRoutine);
                attackRoutine = StartCoroutine(AttackRoutine());
            }
        }

        /// <summary>攻击动作的伤害结算：等咬合时刻才判定（含距离复核），
        /// 玩家在收招前走出范围则算被躲开（不结算伤害）。</summary>
        private IEnumerator AttackRoutine()
        {
            yield return new WaitForSeconds(attackHitDelay);
            attackRoutine = null;

            if (state == WolfState.Dead) yield break;
            if (player == null) yield break;
            if (Vector3.Distance(transform.position, player.position) > attackRange * 1.4f) yield break;

            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage(attackDamage);
                OnAttackLanded?.Invoke();
                Debug.Log($"[EnemyWolf] {name} 咬中玩家，造成 {attackDamage} 伤害");
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
            else
            {
                ApplyKnockback(hitFrom);      // 未死才击退（死亡由回收流程接管，避免尸体滑行）
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

            if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
            knockbackTimeLeft = 0f;   // 清掉击退状态，避免复用后继续滑行
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
            if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
            if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }
            if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }

            currentHp = maxHp;
            state = WolfState.Patrol;
            homePosition = spawnPosition;
            patrolTarget = spawnPosition;
            patrolWaitTimer = Random.Range(0.5f, 1.5f);
            lastAttackTime = -attackCooldown;
            verticalVelocity = 0f;
            knockbackTimeLeft = 0f;

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