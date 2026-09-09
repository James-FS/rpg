using System.Collections.Generic;
using UnityEngine;
using AIBot.Core.Context;
using AIBot.Unity;
using FogHarbor.Inventory;
using FogHarbor.Quest;
using FogHarbor.Relationship;
using Newtonsoft.Json;

namespace FogHarbor.Dialogue
{
    /// <summary>
    /// 提供给 AIBot 的只读游戏状态快照（方案 §8.4 / §6.1 差距 #8）。
    /// 只传必要状态：任务状态、目标物品持有、金币、物品列表、当前 NPC 好感。
    /// 魔法字符串已下沉：主任务 ID 与目标物品来自 QuestData，好感来自 RelationshipSystem。
    /// 不传 API Key、完整存档、文件路径或设备信息。
    /// </summary>
    public class GameContextProvider : MonoBehaviour, IGameContext
    {
        private QuestSystem questSystem;
        private InventorySystem inventory;
        private RewardService rewardService;
        private RelationshipSystem relationshipSystem;

        private void Awake()
        {
            questSystem = FindObjectOfType<QuestSystem>();
            inventory = FindObjectOfType<InventorySystem>();
            rewardService = FindObjectOfType<RewardService>();
            relationshipSystem = FindObjectOfType<RelationshipSystem>();
            if (questSystem == null) Debug.LogWarning("[GameContextProvider] 未找到 QuestSystem");
            if (inventory == null) Debug.LogWarning("[GameContextProvider] 未找到 InventorySystem");
            if (rewardService == null) Debug.LogWarning("[GameContextProvider] 未找到 RewardService");
            if (relationshipSystem == null) Debug.LogWarning("[GameContextProvider] 未找到 RelationshipSystem");
        }

        public int CurrentStage => 0;

        private string GetCurrentNpcId()
        {
            // 与 NpcAgent 同物体：优先取 Connection Profile 的 npcId，其次 agent.npcId。
            var agent = GetComponentInParent<NpcAgent>();
            if (agent != null)
            {
                if (agent.connectionProfile != null && !string.IsNullOrEmpty(agent.connectionProfile.npcId))
                    return agent.connectionProfile.npcId;
                if (!string.IsNullOrEmpty(agent.npcId))
                    return agent.npcId;
            }
            return null;
        }

        public string SnapshotJson
        {
            get
            {
                string mainQuestId = questSystem?.GetMainQuestId();
                var quest = !string.IsNullOrEmpty(mainQuestId) ? questSystem.GetQuest(mainQuestId) : null;
                string targetItemId = quest != null ? quest.TargetItemId : null;
                int targetCount = quest != null ? quest.TargetCount : 1;

                var snapshot = new Dictionary<string, object>
                {
                    ["questId"] = mainQuestId,
                    ["questState"] = !string.IsNullOrEmpty(mainQuestId) && questSystem != null
                        ? questSystem.GetState(mainQuestId).ToString()
                        : "unknown",
                    ["hasQuestTarget"] = !string.IsNullOrEmpty(targetItemId)
                        && inventory != null && inventory.Has(targetItemId, targetCount),
                    ["gold"] = rewardService != null ? rewardService.Gold : 0,
                    ["items"] = GetItemList()
                };

                // 当前 NPC 好感（替换写死 linFavor=5）
                string npcId = GetCurrentNpcId();
                if (!string.IsNullOrEmpty(npcId))
                {
                    snapshot["npcId"] = npcId;
                    snapshot["linFavor"] = relationshipSystem != null ? relationshipSystem.GetFavor(npcId) : 0;
                }

                return JsonConvert.SerializeObject(snapshot, Formatting.None);
            }
        }

        private List<string> GetItemList()
        {
            var list = new List<string>();
            if (inventory != null)
            {
                foreach (var pair in inventory.GetAll())
                    list.Add($"{pair.Key} x{pair.Value}");
            }
            return list;
        }
    }
}