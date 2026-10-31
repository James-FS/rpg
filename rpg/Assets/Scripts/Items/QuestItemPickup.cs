using System;
using UnityEngine;
using FogHarbor.Items;
using FogHarbor.Player;
using FogHarbor.Session;
using FogHarbor.UI;

namespace FogHarbor.Items
{
    /// <summary>
    /// 场景拾取物（§6.1：意图入口）：玩家按 E 交互后把"拾取意图"转发给 GameSession，
    /// 由 Session 编排入库 + 推进任务 + 存档。本类不直接调系统，也不编排流程。
    /// 挂到场景中的道具物体上（月光药草、铁矿等），配置 itemId 与数量。
    /// 需要物体带 Collider（PlayerInteractor 用 OverlapSphere 检测）。
    /// </summary>
    public class QuestItemPickup : MonoBehaviour, IInteractable
    {
        [Header("拾取物配置")]
        [SerializeField] private string itemId;
        [SerializeField] private int count = 1;
        [SerializeField] private string promptText = "按 E 拾取";
        [SerializeField] private bool destroyOnPickup = true;

        [Tooltip("需要镐子命中的次数；0 表示普通拾取")]
        [Min(0)] [SerializeField] private int requiredMiningHits;
        private int miningHits;
        private bool collected;
        public bool IsMineable => requiredMiningHits > 0 && !collected;
        public int MiningHits => miningHits;

        private GameSession gameSession;
        private PlayerController player;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new System.InvalidOperationException($"Pickup '{name}' requires itemId.");
            ItemDatabase.GetById(itemId);
            if (count <= 0) throw new System.InvalidOperationException($"Pickup '{name}' count must be positive.");
        }

        private void Start()
        {
            gameSession = GameSession.Instance
                ?? throw new MissingComponentException("QuestItemPickup requires a composed GameSession.");
            player = gameSession.Player
                ?? throw new MissingComponentException("QuestItemPickup requires a bound PlayerController.");
        }

        public string GetPrompt()
        {
            var data = ItemDatabase.GetById(itemId);
            string name = data.DisplayName;
            if (requiredMiningHits > 0)
                return $"左键 / E 挖掘 {name}（{miningHits}/{requiredMiningHits}，需装备镐子）";
            return string.IsNullOrEmpty(name) ? promptText : $"{promptText} {name}";
        }

        public void Interact()
        {
            if (collected) return;
            if (requiredMiningHits > 0)
            {
                var weapon = player.GetComponent<PlayerWeaponVisual>()
                    ?? throw new MissingComponentException("Mining requires PlayerWeaponVisual on the player.");
                if (!weapon.IsMiningTool)
                {
                    UIManager.Instance.ShowToast("请先装备镐子");
                    return;
                }
                if (player.IsBusy || player.IsJumping) return;
                Vector3 direction = transform.position - player.transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.001f)
                    player.transform.rotation = Quaternion.LookRotation(direction);
                player.PlayAttack();
                return;
            }
            CompletePickup(true);
        }

        /// <summary>仅由镐子动画命中回调调用；一次挥镐至多计一次。</summary>
        public bool TryMine(PlayerController miner)
        {
            if (!CanMine(miner, out _)) return false;

            miningHits++;
            if (miningHits < requiredMiningHits)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowToast($"挖掘 {miningHits}/{requiredMiningHits}");
                return true;
            }
            if (!CompletePickup(false))
                miningHits = requiredMiningHits - 1;
            return true;
        }

        /// <summary>按矿石碰撞体表面的距离判断，避免大矿石的中心距离阻止挖掘。</summary>
        public bool CanMine(PlayerController miner, out float surfaceSqrDistance)
        {
            surfaceSqrDistance = float.PositiveInfinity;
            if (miner == null) throw new System.ArgumentNullException(nameof(miner));
            if (!IsMineable || miner.IsDead) return false;
            var weapon = miner.GetComponent<PlayerWeaponVisual>()
                ?? throw new MissingComponentException("Mining requires PlayerWeaponVisual on the player.");
            if (!weapon.IsMiningTool) return false;
            Vector3 direction = transform.position - miner.transform.position;
            direction.y = 0f;
            if (Vector3.Dot(miner.transform.forward, direction) <= 0f) return false;

            Vector3 origin = miner.transform.position + Vector3.up * 0.5f;
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                Vector3 delta = collider.ClosestPoint(origin) - origin;
                surfaceSqrDistance = Mathf.Min(surfaceSqrDistance, delta.sqrMagnitude);
            }
            return surfaceSqrDistance <= miner.MiningReach * miner.MiningReach;
        }

        private bool CompletePickup(bool playGather)
        {
            if (gameSession == null) throw new InvalidOperationException("QuestItemPickup.Start did not bind GameSession.");

            var result = gameSession.PickupItem(itemId, count);
            if (!result.Success)
            {
                UIManager.Instance.ShowToast(result.Message);
                Debug.LogError($"[QuestItemPickup] 拾取失败: {result.Message}");
                return false;
            }

            var data = ItemDatabase.GetById(itemId);
            UIManager.Instance.ShowToast($"获得 {data.DisplayName} x{count}");

            // 表现层：转身面向拾取物并播放采集动作（短暂定身）
            if (playGather)
                player.PlayGather(transform.position);

            collected = true;
            if (destroyOnPickup)
                Destroy(gameObject);
            return true;
        }
    }
}

