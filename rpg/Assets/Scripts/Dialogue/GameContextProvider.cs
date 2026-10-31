using System;
using System.Collections.Generic;
using UnityEngine;
using AIBot.Core.Context;
using AIBot.Unity;
using FogHarbor.Inventory;
using FogHarbor.Quest;
using FogHarbor.Relationship;
using FogHarbor.Session;
using Newtonsoft.Json;

namespace FogHarbor.Dialogue
{
    /// <summary>
    /// 提供给 AIBot 的只读游戏状态快照（方案 §8.4 / §6.1 差距 #8）。
    /// 只传必要状态：剧情阶段、任务状态、目标物品持有、金币、背包清单、当前 NPC 好感。
    /// 魔法字符串已下沉：主任务 ID 与目标物品来自 QuestData，好感来自 RelationshipSystem。
    /// 不传 API Key、完整存档、文件路径或设备信息。
    /// 字段命名注意：stage/favorability 与 Core 的 SimGameState 同名，会被 Server 合并进会话状态；
    /// 背包用 inventory 而非 items——SimGameState.items 是模拟沙盒的字典字段，重名会让请求体解析失败。
    /// </summary>
    public class GameContextProvider : MonoBehaviour, IGameContext
    {
        private QuestSystem questSystem;
        private InventorySystem inventory;
        private RewardService rewardService;
        private RelationshipSystem relationshipSystem;

        private void Start()
        {
            var session = GameSession.Instance
                ?? throw new MissingComponentException("GameContextProvider requires a composed GameSession.");
            questSystem = session.Quests;
            inventory = session.Inventory;
            rewardService = session.Wallet;
            relationshipSystem = session.Relationships;
        }

        /// <summary>剧情阶段由主任务状态推导，供 loreBlocks 的 unlockStage 门控与 Prompt「当前阶段」使用。</summary>
        public int CurrentStage
        {
            get
            {
                return ResolveStage();
            }
        }

        private int ResolveStage()
        {
            string questId = questSystem.GetMainQuestId();
            if (string.IsNullOrEmpty(questId)) throw new InvalidOperationException("No main quest is registered.");
            switch (questSystem.GetState(questId))
            {
                case QuestState.Available: return 0;
                case QuestState.Accepted: return 1;
                case QuestState.Completed: return 2;
                case QuestState.Rewarded: return 3;
                default: throw new ArgumentOutOfRangeException(nameof(questId), questId, "Quest has an unsupported state.");
            }
        }

        private string GetCurrentNpcId()
        {
            // 与 NpcAgent 同物体：优先取 Connection Profile 的 npcId，其次 agent.npcId。
            var agent = GetComponentInParent<NpcAgent>()
                ?? throw new MissingComponentException("GameContextProvider requires a parent NpcAgent.");
            string npcId = agent.connectionProfile != null && !string.IsNullOrEmpty(agent.connectionProfile.npcId)
                ? agent.connectionProfile.npcId : agent.npcId;
            if (string.IsNullOrEmpty(npcId)) throw new InvalidOperationException($"NpcAgent '{agent.name}' has no npcId.");
            return npcId;
        }

        public string SnapshotJson
        {
            get
            {
                string mainQuestId = questSystem.GetMainQuestId();
                var quest = questSystem.GetQuest(mainQuestId)
                    ?? throw new InvalidOperationException($"Main quest '{mainQuestId}' has no definition.");
                string targetItemId = quest.TargetItemId;
                int targetCount = quest.TargetCount;

                var snapshot = new Dictionary<string, object>
                {
                    ["questId"] = mainQuestId,
                    ["questState"] = questSystem.GetState(mainQuestId).ToString(),
                    ["hasQuestTarget"] = !string.IsNullOrEmpty(targetItemId)
                        && inventory.Has(targetItemId, targetCount),
                    ["gold"] = rewardService.Gold,
                    ["inventory"] = GetItemList(),
                    // 与 SimGameState 同名：Server 会把它合并进会话状态，工具与状态面板据此显示
                    ["stage"] = CurrentStage
                };

                // 当前 NPC 好感（favorability 与 SimGameState 同名，同样会被 Server 合并）
                string npcId = GetCurrentNpcId();
                if (!string.IsNullOrEmpty(npcId))
                {
                    snapshot["npcId"] = npcId;
                    snapshot["favorability"] = relationshipSystem.GetFavor(npcId);
                }

                return JsonConvert.SerializeObject(snapshot, Formatting.None);
            }
        }

        private List<string> GetItemList()
        {
            var list = new List<string>();
            foreach (var pair in inventory.GetAll())
                list.Add($"{pair.Key} x{pair.Value}");
            return list;
        }
    }
}
