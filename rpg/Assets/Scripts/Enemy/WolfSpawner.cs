using System.Collections.Generic;
using UnityEngine;

namespace FogHarbor.Enemy
{
    /// <summary>
    /// 灰狼刷怪器（对象池 + 区域随机刷怪 + 上限）。
    /// 启动时预热整个对象池（上限只），隐藏备用；按初始数量随机点激活。
    /// 狼死亡后由 OnDeath 回调回收进池；若活跃数低于上限，每隔 respawnInterval 秒补刷一只。
    /// 挂到 Forest 场景的空物体上，指定 wolfPrefab 与刷怪区域。
    /// </summary>
    public class WolfSpawner : MonoBehaviour
    {
        [Header("刷怪区域")]
        [SerializeField] private Vector3 spawnCenter = new Vector3(3f, 0f, 3f);
        [SerializeField] private float spawnRadius = 6f;

        [Header("数量与节奏")]
        [SerializeField] private int maxWolves = 4;          // 同时存在上限
        [SerializeField] private int initialWolves = 2;      // 开局刷出几只
        [SerializeField] private float respawnInterval = 15f; // 每死一只后的补刷间隔
        [SerializeField] private float minRespawnDistanceFromPlayer = 5f; // 不在玩家脸前刷

        [Header("Prefab")]
        [SerializeField] private EnemyWolf wolfPrefab;

        /// <summary>空闲池（inactive 的狼）。</summary>
        private readonly List<EnemyWolf> pool = new();
        /// <summary>场上活跃的狼。</summary>
        private readonly List<EnemyWolf> active = new();

        private float respawnTimer;
        private Transform player;

        public int ActiveCount => active.Count;
        public int PoolSize => pool.Count;

        private void Awake()
        {
            if (wolfPrefab == null)
            {
                Debug.LogError("[WolfSpawner] 未指定 wolfPrefab！");
                return;
            }

            // 预热对象池：实例化上限只，全部隐藏
            for (int i = 0; i < maxWolves; i++)
            {
                var wolf = Instantiate(wolfPrefab, transform);
                wolf.name = $"Wolf_{i:00}";
                wolf.gameObject.SetActive(false);
                wolf.OnDeath = OnWolfDied;
                pool.Add(wolf);
            }

            // 初始刷出
            for (int i = 0; i < initialWolves && i < maxWolves; i++)
                SpawnOne();

            Debug.Log($"[WolfSpawner] 预热 {pool.Count} 只（池），初始活跃 {active.Count} 只");
        }

        private void Start()
        {
            var pc = Object.FindObjectOfType<FogHarbor.Player.PlayerController>();
            if (pc != null) player = pc.transform;
        }

        private void Update()
        {
            if (wolfPrefab == null) return;

            // 活跃数低于上限 → 计时补刷
            if (active.Count < maxWolves)
            {
                respawnTimer += Time.deltaTime;
                if (respawnTimer >= respawnInterval)
                {
                    respawnTimer = 0f;
                    SpawnOne();
                }
            }
        }

        /// <summary>从池取一只放到区域内随机点并激活；池空则跳过。</summary>
        private void SpawnOne()
        {
            if (pool.Count == 0 || wolfPrefab == null) return;

            // 取池尾
            var wolf = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);

            Vector3 pos = PickSpawnPosition();
            if (pos == Vector3.zero) { pool.Add(wolf); return; } // 没找到合适点，放回

            wolf.ResetWolf(pos);
            wolf.gameObject.SetActive(true);
            active.Add(wolf);
        }

        /// <summary>在区域内随机选点；若离玩家太近则重试几次。</summary>
        private Vector3 PickSpawnPosition()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 rnd = Random.insideUnitCircle * spawnRadius;
                Vector3 pos = spawnCenter + new Vector3(rnd.x, 0.1f, rnd.y);

                // 落点不能是地面以下（村庄没有坑，简单 clamp 到地面 0.1）
                pos.y = 0.1f;

                if (player != null)
                {
                    float dist = Vector3.Distance(pos, player.position);
                    if (dist < minRespawnDistanceFromPlayer) continue;
                }
                return pos;
            }
            return Vector3.zero;
        }

        /// <summary>狼死亡回调：从活跃列表移除，回收进池（隐藏待复用）。</summary>
        private void OnWolfDied(EnemyWolf wolf)
        {
            if (wolf == null) return;
            active.Remove(wolf);
            wolf.gameObject.SetActive(false);
            pool.Add(wolf);
            respawnTimer = 0f; // 死亡即开始下一轮补刷计时
            Debug.Log($"[WolfSpawner] 回收 {wolf.name} → 池。活跃 {active.Count}/{maxWolves}");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
            Gizmos.DrawSphere(spawnCenter, spawnRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(spawnCenter, spawnRadius);
        }
    }
}