using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using FogHarbor.Enemy;
using FogHarbor.UI;

namespace FogHarbor.Player
{
    /// <summary>
    /// 玩家控制器：移动、攻击、受击。
    /// 使用 CharacterController 实现移动，支持 WASD 和方向键。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移动")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float gravity = -9.81f;

        [Header("攻击")]
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackCooldown = 1f;
        [SerializeField] private int baseAttackDamage = 5;

        [Header("防御")]
        [SerializeField] private int baseDefense = 0;

        [Header("生命")]
        [SerializeField] private int maxHp = 100;

        private CharacterController controller;
        private float verticalVelocity;
        private float lastAttackTime;
        private int currentHp;

        // 装备加成（由 EquipmentSystem 写回；T2 遗留补全：让装备真正影响战斗）
        private int bonusAttack;
        private int bonusDefense;

        /// <summary>HP 变化时触发（§6.1 差距 #3：HUD 订阅刷新，删每帧轮询）。</summary>
        public event Action<int, int> OnPlayerHpChanged;

        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        /// <summary>总攻击 = 基础攻击 + 装备加成。</summary>
        public int AttackDamage => baseAttackDamage + bonusAttack;
        /// <summary>总防御 = 基础防御 + 装备加成（减伤用）。</summary>
        public int Defense => baseDefense + bonusDefense;

        /// <summary>装备系统写回攻击加成（系统层规则：EquipmentSystem 计算，本类只收值）。</summary>
        public void SetAttackBonus(int bonus)
        {
            bonusAttack = Mathf.Max(0, bonus);
        }

        public void SetDefenseBonus(int bonus)
        {
            bonusDefense = Mathf.Max(0, bonus);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            currentHp = maxHp;
        }

        private void Update()
        {
            HandleMovement();
            HandleAttack();
        }

        private void HandleMovement()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 direction = new Vector3(h, 0f, v).normalized;

            if (direction.sqrMagnitude > 0.01f)
            {
                controller.Move(direction * moveSpeed * Time.deltaTime);

                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // 重力： grounded 时给一个微小下压力，防止贴地抖动
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            else
                verticalVelocity += gravity * Time.deltaTime;

            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }

        private void HandleAttack()
        {
            if (Input.GetMouseButtonDown(0) && Time.time >= lastAttackTime + attackCooldown)
            {
                lastAttackTime = Time.time;

                Vector3 center = transform.position + transform.forward * attackRange * 0.5f;
                Collider[] hits = Physics.OverlapSphere(center, attackRange * 0.5f);

                foreach (var hit in hits)
                {
                    if (hit.gameObject == gameObject) continue;

                    var wolf = hit.GetComponent<EnemyWolf>();
                    if (wolf != null)
                    {
                        wolf.TakeDamage(AttackDamage, transform.position);
                        Debug.Log($"[PlayerController] 击中灰狼，造成 {AttackDamage} 点伤害");
                        break; // 一次攻击只打中一个目标
                    }
                }
            }
        }

        /// <summary>外部调用：让玩家受到伤害（防御减伤：总防御抵扣伤害，最少 1 点）。</summary>
        public void TakeDamage(int damage)
        {
            int effectiveDamage = Mathf.Max(1, damage - Defense);
            currentHp = Mathf.Max(0, currentHp - effectiveDamage);
            Debug.Log($"[PlayerController] 受到 {damage} 伤害（防御 {Defense} 减伤 {damage - effectiveDamage}），实际 {effectiveDamage}，剩余 HP: {currentHp}");
            OnPlayerHpChanged?.Invoke(currentHp, maxHp);

            if (UIManager.Instance != null)
                UIManager.Instance.ShowToast($"受到 {effectiveDamage} 点伤害");

            if (currentHp <= 0)
            {
                Debug.Log("[PlayerController] 玩家死亡，重生回小镇");
                StartCoroutine(RespawnRoutine());
            }
        }

        private IEnumerator RespawnRoutine()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowToast("你被击败了…回到了小镇");
            yield return new WaitForSecondsRealtime(1.5f);
            SceneManager.LoadScene("Town");
        }

        /// <summary>外部调用：让玩家恢复生命值。</summary>
        public void Heal(int amount)
        {
            currentHp = Mathf.Min(maxHp, currentHp + amount);
            Debug.Log($"[PlayerController] 恢复 {amount} 点生命，当前 HP: {currentHp}");
            OnPlayerHpChanged?.Invoke(currentHp, maxHp);
        }

        /// <summary>从存档恢复 HP。</summary>
        public void SetHp(int hp)
        {
            currentHp = Mathf.Clamp(hp, 0, maxHp);
            OnPlayerHpChanged?.Invoke(currentHp, maxHp);
        }
    }
}
