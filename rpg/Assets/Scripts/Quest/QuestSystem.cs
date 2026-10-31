using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FogHarbor.Inventory;
using FogHarbor.Items;
using FogHarbor.Relationship;

namespace FogHarbor.Quest
{
    /// <summary>
    /// 任务系统：管理任务状态机与任务目标推进。
    /// 挂载在 AppRoot 上，通过 DontDestroyOnLoad 跨场景保留。
    /// 核心原则：所有状态变更由游戏代码确定性控制，模型不能直接改字段。
    /// 金币与物品交易已拆分到 RewardService（§6.1 差距 #5），本类只保留状态机。
    /// </summary>
    public class QuestSystem : MonoBehaviour
    {
        // questId → 运行时状态
        private readonly Dictionary<string, QuestState> questStates = new();
        // questId → QuestData 定义
        private readonly Dictionary<string, QuestData> questDefs = new();

        // 外部引用
        private InventorySystem inventory;

        /// <summary>任务状态变化时触发，UI 监听刷新。</summary>
        public event Action<string, QuestState> OnQuestStateChanged;

        // ─── 属性查询 ───

        public int QuestCount => questDefs.Count;

        // ─── 初始化 ───

        private void Awake()
        {
            inventory = GetComponent<InventorySystem>()
                ?? throw new MissingComponentException("QuestSystem requires InventorySystem on AppRoot.");

            LoadAllQuestDefs();
        }

        private void LoadAllQuestDefs()
        {
            questDefs.Clear();
            var assets = Resources.LoadAll<QuestData>("Quests");
            if (assets.Length == 0) throw new InvalidOperationException("Resources/Quests contains no QuestData assets.");
            foreach (var asset in assets)
            {
                ValidateDefinition(asset);
                if (questDefs.ContainsKey(asset.QuestId))
                    throw new InvalidOperationException($"Duplicate questId '{asset.QuestId}' in Resources/Quests.");
                questDefs.Add(asset.QuestId, asset);
                questStates.Add(asset.QuestId, QuestState.Available);
            }
            if (questDefs.Values.Count(def => def.IsMainQuest) != 1)
                throw new InvalidOperationException("Exactly one QuestData asset must be marked as the main quest.");
            Debug.Log($"[QuestSystem] 已加载 {questDefs.Count} 个任务定义");
        }

        private static void ValidateDefinition(QuestData asset)
        {
            if (asset == null) throw new InvalidOperationException("Resources/Quests contains a null QuestData asset.");
            if (string.IsNullOrWhiteSpace(asset.QuestId) || string.IsNullOrWhiteSpace(asset.Title))
                throw new InvalidOperationException($"QuestData '{asset.name}' requires questId and title.");
            if (asset.RewardGold < 0)
                throw new InvalidOperationException($"Quest '{asset.QuestId}' has a negative gold reward.");

            if (!string.IsNullOrEmpty(asset.TargetItemId))
            {
                ItemDatabase.GetById(asset.TargetItemId);
                if (asset.TargetCount <= 0)
                    throw new InvalidOperationException($"Quest '{asset.QuestId}' requires a positive target count.");
            }
            if (asset.AcceptanceItem != null)
            {
                if (asset.AcceptanceItemCount <= 0)
                    throw new InvalidOperationException($"Quest '{asset.QuestId}' has a non-positive acceptance item count.");
                if (ItemDatabase.GetById(asset.AcceptanceItem.ItemId) != asset.AcceptanceItem)
                    throw new InvalidOperationException($"Quest '{asset.QuestId}' acceptance item does not match the item database.");
            }
            if (!string.IsNullOrEmpty(asset.RewardItemId))
            {
                ItemDatabase.GetById(asset.RewardItemId);
                if (asset.RewardItemCount <= 0)
                    throw new InvalidOperationException($"Quest '{asset.QuestId}' has a reward item with a non-positive count.");
            }
            else if (asset.RewardItemCount != 0)
            {
                throw new InvalidOperationException($"Quest '{asset.QuestId}' has a reward count without a reward item.");
            }
        }

        // ─── 状态查询 ───

        public QuestState GetState(string questId)
        {
            if (questStates.TryGetValue(questId, out var state)) return state;
            throw new KeyNotFoundException($"Quest state is not registered for questId '{questId}'.");
        }

        public QuestData GetQuest(string questId)
        {
            return questDefs.TryGetValue(questId, out var data) ? data : null;
        }

        /// <summary>所有任务 ID（UI 遍历用，替代反射读取内部字段，§6.1 差距 #2）。</summary>
        public IReadOnlyCollection<string> GetAllQuestIds()
        {
            return questStates.Keys;
        }

        public void ValidateNpcReferences(RelationshipSystem relationships)
        {
            if (relationships == null) throw new ArgumentNullException(nameof(relationships));
            foreach (var def in questDefs.Values)
                if (!string.IsNullOrEmpty(def.RewardNpcId) && relationships.GetProfile(def.RewardNpcId) == null)
                    throw new InvalidOperationException($"Quest '{def.QuestId}' references unknown reward NPC '{def.RewardNpcId}'.");
        }

        /// <summary>主任务 ID（由唯一一个 IsMainQuest 标记的数据资产决定）。</summary>
        public string GetMainQuestId()
        {
            foreach (var def in questDefs.Values)
                if (def.IsMainQuest) return def.QuestId;
            throw new InvalidOperationException("No QuestData asset is marked as the main quest.");
        }

        /// <summary>检查任务是否可完成：状态为 Accepted 且背包中有足够目标物品。</summary>
        public bool CanComplete(string questId)
        {
            if (!questDefs.TryGetValue(questId, out var def))
                throw new KeyNotFoundException($"Quest '{questId}' is not registered.");
            if (GetState(questId) != QuestState.Accepted) return false;
            if (string.IsNullOrEmpty(def.TargetItemId)) return true;
            return inventory.Has(def.TargetItemId, def.TargetCount);
        }

        /// <summary>物品入库后推进任务（系统层规则）：把目标物为 itemId 且已满足条件的
        /// Accepted 任务置为 Completed。由 GameSession 拾取用例调用。</summary>
        public void TryCompleteOnPickup(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("itemId is required.", nameof(itemId));
            foreach (var def in questDefs.Values)
            {
                if (def.TargetItemId != itemId) continue;
                if (GetState(def.QuestId) != QuestState.Accepted) continue;
                Complete(def.QuestId); // 复用 CanComplete 校验
            }
        }

        // ─── 状态推进 ───

        /// <summary>接受任务：Available → Accepted。</summary>
        public bool Accept(string questId)
        {
            if (!questDefs.ContainsKey(questId))
                throw new KeyNotFoundException($"Quest '{questId}' is not registered.");

            if (GetState(questId) != QuestState.Available)
            {
                Debug.LogWarning($"[QuestSystem] 任务 {questId} 当前状态为 {GetState(questId)}，无法接受");
                return false;
            }

            SetState(questId, QuestState.Accepted);
            Debug.Log($"[QuestSystem] 接受任务: {questDefs[questId].Title}");
            return true;
        }

        /// <summary>完成任务：Accepted → Completed（仅标记，不消耗物品、不发奖励）。</summary>
        public bool Complete(string questId)
        {
            if (!CanComplete(questId))
            {
                Debug.LogWarning($"[QuestSystem] 任务 {questId} 无法完成（状态或物品不满足）");
                return false;
            }

            SetState(questId, QuestState.Completed);
            Debug.Log($"[QuestSystem] 任务完成: {questDefs[questId].Title}");
            return true;
        }

        /// <summary>只读预检：任务是否已完成且可领奖（Completed 且未 Rewarded）。流程编排层取前校验。</summary>
        public bool CanClaimReward(string questId)
        {
            if (!questDefs.ContainsKey(questId)) throw new KeyNotFoundException($"Quest '{questId}' is not registered.");
            return GetState(questId) == QuestState.Completed;
        }

        /// <summary>领取奖励：Completed → Rewarded。纯状态机推进，不处理消耗/发奖
        /// （消耗与发奖由 RewardService 在 Session 流程中完成，§6.1 差距 #5）。</summary>
        public bool ClaimReward(string questId)
        {
            if (!questDefs.TryGetValue(questId, out var def))
                throw new KeyNotFoundException($"Quest '{questId}' is not registered.");

            if (GetState(questId) != QuestState.Completed)
            {
                Debug.LogWarning($"[QuestSystem] 任务 {questId} 当前状态为 {GetState(questId)}，无法领奖");
                return false;
            }

            SetState(questId, QuestState.Rewarded);
            Debug.Log($"[QuestSystem] 任务已领奖: {def.Title}");
            return true;
        }

        // ─── 内部 ───

        private void SetState(string questId, QuestState newState)
        {
            if (!questDefs.ContainsKey(questId)) throw new KeyNotFoundException($"Quest '{questId}' is not registered.");
            if (!Enum.IsDefined(typeof(QuestState), newState)) throw new ArgumentOutOfRangeException(nameof(newState));
            questStates[questId] = newState;
            OnQuestStateChanged?.Invoke(questId, newState);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugReset(string questId)
        {
            if (!questDefs.ContainsKey(questId)) return false;
            SetState(questId, QuestState.Available);
            return true;
        }
#endif

        // ─── 存档支持 ───

        public Dictionary<string, QuestState> GetSaveData()
        {
            return new Dictionary<string, QuestState>(questStates);
        }

        public void LoadFromData(Dictionary<string, QuestState> data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var restored = new Dictionary<string, QuestState>();

            foreach (var pair in data)
            {
                if (!questDefs.ContainsKey(pair.Key))
                    throw new KeyNotFoundException($"Save data references unknown questId '{pair.Key}'.");
                restored.Add(pair.Key, pair.Value);
            }

            // 新任务（存档中没有的）默认 Available
            foreach (var questId in questDefs.Keys)
            {
                if (!restored.ContainsKey(questId))
                    restored.Add(questId, QuestState.Available);
            }

            questStates.Clear();
            foreach (var pair in restored) questStates.Add(pair.Key, pair.Value);

            Debug.Log($"[QuestSystem] 从存档恢复 {questStates.Count} 个任务状态");
            foreach (var pair in questStates) OnQuestStateChanged?.Invoke(pair.Key, pair.Value);
        }
    }
}
