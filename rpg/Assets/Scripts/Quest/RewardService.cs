using System;
using UnityEngine;
using FogHarbor.Inventory;

namespace FogHarbor.Quest
{
    /// <summary>
    /// 奖励/钱包服务（§6.1 差距 #5）：持有玩家金币与交易原语（扣物、发奖）。
    /// 挂载在 AppRoot 上，通过 DontDestroyOnLoad 跨场景保留。
    /// 规则校验（能否扣、能否买）都在这里；跨系统流程编排由 GameSession 负责，本类不编排流程。
    /// </summary>
    public class RewardService : MonoBehaviour
    {
        private InventorySystem inventory;

        // 玩家金币（从 QuestSystem 拆分迁入）
        private int gold = 20;

        /// <summary>金币变化时触发，UI 监听刷新（删轮询）。</summary>
        public event Action<int> OnGoldChanged;

        public int Gold => gold;

        private void Awake()
        {
            inventory = GetComponent<InventorySystem>();
            if (inventory == null)
                Debug.LogError("[RewardService] 未找到 InventorySystem，请确保两者挂在同一物体上");
        }

        // ─── 金币 ───

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            gold += amount;
            OnGoldChanged?.Invoke(gold);
            Debug.Log($"[RewardService] +{amount} 金币（共 {gold}）");
        }

        public bool SpendGold(int amount)
        {
            if (amount <= 0) return true;
            if (gold < amount) return false;
            gold -= amount;
            OnGoldChanged?.Invoke(gold);
            Debug.Log($"[RewardService] -{amount} 金币（共 {gold}）");
            return true;
        }

        // ─── 交易原语（供 GameSession 编排）───

        /// <summary>
        /// 发放任务奖励：先校验目标物品（不扣）→ 扣目标物品 → 加金币 → 加奖励物品。
        /// 任一前提不满足则整体失败且不产生任何副作用（先校验后执行），由系统层保证幂等。
        /// </summary>
        public bool GrantQuestReward(QuestData def)
        {
            if (def == null) return false;

            // 1. 只读预检：目标物品是否足够
            if (!string.IsNullOrEmpty(def.TargetItemId) && def.TargetCount > 0
                && (inventory == null || !inventory.Has(def.TargetItemId, def.TargetCount)))
            {
                Debug.LogWarning($"[RewardService] 发放 {def.QuestId} 奖励失败：目标物品 {def.TargetItemId} 不足");
                return false;
            }

            // 2. 扣目标物品
            if (!string.IsNullOrEmpty(def.TargetItemId) && def.TargetCount > 0)
            {
                if (!inventory.Remove(def.TargetItemId, def.TargetCount))
                {
                    Debug.LogError($"[RewardService] 消耗 {def.TargetItemId} x{def.TargetCount} 失败");
                    return false;
                }
            }

            // 3. 发金币
            if (def.RewardGold > 0)
                AddGold(def.RewardGold);

            // 4. 发奖励物品
            if (!string.IsNullOrEmpty(def.RewardItemId) && def.RewardItemCount > 0)
                inventory.Add(def.RewardItemId, def.RewardItemCount);

            Debug.Log($"[RewardService] 已发放任务奖励: {def.Title}");
            return true;
        }

        // ─── 存档支持 ───

        public int GetSaveGold()
        {
            return gold;
        }

        public void LoadGold(int savedGold)
        {
            gold = savedGold;
            OnGoldChanged?.Invoke(gold);
            Debug.Log($"[RewardService] 从存档恢复金币: {gold}");
        }
    }
}