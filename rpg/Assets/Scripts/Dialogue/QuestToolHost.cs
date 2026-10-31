using System;
using System.Threading.Tasks;
using UnityEngine;
using AIBot.Core.Tools;
using AIBot.Unity;
using FogHarbor.Inventory;
using FogHarbor.Quest;
using FogHarbor.Relationship;
using FogHarbor.Session;
using Newtonsoft.Json.Linq;

namespace FogHarbor.Dialogue
{
    /// <summary>
    /// 任务工具宿主（§6.1：AI 侧适配器/意图入口）：把 AIBot 的工具请求翻译为
    /// GameSession 用例调用，再组回复文本。不在此处编排流程——跨系统流程只进 GameSession。
    /// 核心原则：AIBot 负责"怎么说"，游戏代码负责"能不能做"。
    /// 挂载在 NPC 根对象上，与 NpcAgent 同物体。
    /// </summary>
    public class QuestToolHost : MonoBehaviour
    {
        private QuestSystem questSystem;
        private InventorySystem inventory;
        private GameSession gameSession;

        /// <summary>把全部任务工具注册到 NpcAgent（在 Chat 之前调用）。
        /// AppRoot 在启动时完成系统组合；此处只读取其明确绑定。</summary>
        public void RegisterTools(NpcAgent agent)
        {
            if (agent == null) throw new ArgumentNullException(nameof(agent));
            if (agent.connectionProfile == null)
                throw new MissingComponentException($"NpcAgent '{agent.name}' requires a Server Connection Profile.");
            if (!agent.connectionProfile.enableGameTools || agent.connectionProfile.enableSimulatedTools)
                throw new InvalidOperationException($"NpcAgent '{agent.name}' must enable game tools and disable simulated tools.");
            string[] requiredTools = { "get_quest_status", "accept_quest", "complete_quest", "record_player_choice" };
            foreach (string toolId in requiredTools)
                if (agent.connectionProfile.enabledToolIds == null || !agent.connectionProfile.enabledToolIds.Contains(toolId))
                    throw new InvalidOperationException($"NpcAgent '{agent.name}' profile does not enable required game tool '{toolId}'.");

            gameSession = GameSession.Instance
                ?? throw new MissingComponentException("Quest tools require a composed GameSession.");
            questSystem = gameSession.Quests;
            inventory = gameSession.Inventory;

            agent.Tools.Register(new GetQuestStatusTool(questSystem, inventory, gameSession));
            agent.Tools.Register(new AcceptQuestTool(questSystem, gameSession));
            agent.Tools.Register(new CompleteQuestTool(questSystem, gameSession));
            agent.Tools.Register(new RecordPlayerChoiceTool(gameSession, agent));
            Debug.Log("[QuestToolHost] 已注册任务工具: get_quest_status / accept_quest / complete_quest / record_player_choice");
        }

        // ─── get_quest_status：只读查询 ───

        private class GetQuestStatusTool : IAgentTool
        {
            private readonly QuestSystem questSystem;
            private readonly InventorySystem inventory;
            private readonly GameSession gameSession;
            public string Id => "get_quest_status";
            public string Description => "查询玩家的任务状态和关键物品持有情况（只读）。";
            public string ParametersSchema => "{\"type\":\"object\",\"properties\":{\"questId\":{\"type\":\"string\",\"description\":\"任务ID，省略时默认查询主任务\"}}}";

            public GetQuestStatusTool(QuestSystem qs, InventorySystem inv, GameSession session)
            {
                questSystem = qs;
                inventory = inv;
                gameSession = session;
            }

            public Task<ToolResult> ExecuteAsync(string argsJson, object hostContext)
            {
                var args = string.IsNullOrEmpty(argsJson) ? new JObject() : JObject.Parse(argsJson);
                string questId = args["questId"]?.ToString();
                if (string.IsNullOrEmpty(questId))
                    questId = questSystem.GetMainQuestId();

                if (string.IsNullOrEmpty(questId) || questSystem.GetQuest(questId) == null)
                {
                    return Task.FromResult(ToolResult.Fail($"任务不存在: {questId}"));
                }

                var quest = questSystem.GetQuest(questId);
                bool hasTarget = !string.IsNullOrEmpty(quest.TargetItemId)
                    && inventory.Has(quest.TargetItemId, quest.TargetCount);

                string result =
                    $"任务[{questId}] {quest.Title}\n" +
                    $"状态: {questSystem.GetState(questId)}\n" +
                    $"目标: {quest.TargetItemId} x{quest.TargetCount}\n" +
                    $"持有任务目标物品: {(hasTarget ? "是" : "否")}\n" +
                    $"玩家金币: {gameSession.GetGold()}";

                return Task.FromResult(ToolResult.Ok(result));
            }
        }

        // ─── accept_quest：接任务流程收口到 GameSession ───

        private class AcceptQuestTool : IAgentTool
        {
            private readonly QuestSystem questSystem;
            private readonly GameSession gameSession;
            public string Id => "accept_quest";
            public string Description => "让玩家接受指定任务。只有当任务处于 Available 状态时才能接受。";
            public string ParametersSchema => "{\"type\":\"object\",\"properties\":{\"questId\":{\"type\":\"string\",\"description\":\"任务ID\"}},\"required\":[\"questId\"]}";

            public AcceptQuestTool(QuestSystem qs, GameSession session)
            {
                questSystem = qs;
                gameSession = session;
            }

            public Task<ToolResult> ExecuteAsync(string argsJson, object hostContext)
            {
                var args = string.IsNullOrEmpty(argsJson) ? new JObject() : JObject.Parse(argsJson);
                string questId = args["questId"]?.ToString();

                if (string.IsNullOrEmpty(questId))
                    return Task.FromResult(ToolResult.Fail("缺少 questId 参数"));

                if (questSystem.GetQuest(questId) == null)
                    return Task.FromResult(ToolResult.Fail($"任务不存在: {questId}"));

                // 接任务流程（校验→推进→存档）只由 GameSession 编排。
                var result = gameSession.AcceptQuest(questId);

                return Task.FromResult(result.Success
                    ? ToolResult.Ok(result.Message)
                    : ToolResult.Fail(result.Message));
            }
        }

        // ─── complete_quest：领奖事务收口到 GameSession ───

        private class CompleteQuestTool : IAgentTool
        {
            private readonly QuestSystem questSystem;
            private readonly GameSession gameSession;
            public string Id => "complete_quest";
            public string Description => "校验玩家是否完成任务条件，若满足则扣除任务物品、发放奖励并将任务标记为已领奖。同一个任务只会成功发放一次奖励。";
            public string ParametersSchema => "{\"type\":\"object\",\"properties\":{\"questId\":{\"type\":\"string\",\"description\":\"任务ID\"}},\"required\":[\"questId\"]}";

            public CompleteQuestTool(QuestSystem qs, GameSession session)
            {
                questSystem = qs;
                gameSession = session;
            }

            public Task<ToolResult> ExecuteAsync(string argsJson, object hostContext)
            {
                var args = string.IsNullOrEmpty(argsJson) ? new JObject() : JObject.Parse(argsJson);
                string questId = args["questId"]?.ToString();

                if (string.IsNullOrEmpty(questId))
                    return Task.FromResult(ToolResult.Fail("缺少 questId 参数"));

                if (questSystem.GetQuest(questId) == null)
                    return Task.FromResult(ToolResult.Fail($"任务不存在: {questId}"));

                // 领奖完整事务（校验→扣物→发奖→置 Rewarded→存档）只由 GameSession 编排。
                var result = gameSession.CompleteQuest(questId);

                return Task.FromResult(result.Success
                    ? ToolResult.Ok(result.Message)
                    : ToolResult.Fail(result.Message));
            }
        }

        // ─── record_player_choice：好感度白名单工具（§8.3 / §6.1 差距 #8）───

        private class RecordPlayerChoiceTool : IAgentTool
        {
            private readonly GameSession gameSession;
            private readonly string currentNpcId;
            public string Id => "record_player_choice";
            public string Description => "记录玩家对当前对话 NPC 的选择，用于好感度。只允许写入该 NPC 白名单内声明的选择 ID；省略 npcId 时默认为当前对话 NPC。";
            public string ParametersSchema => "{\"type\":\"object\",\"properties\":{\"npcId\":{\"type\":\"string\",\"description\":\"NPC ID（省略时默认当前对话 NPC）\"},\"choice\":{\"type\":\"string\",\"description\":\"白名单内的选择 ID（礼貌/尊重/关心等）\"}},\"required\":[\"choice\"]}";

            public RecordPlayerChoiceTool(GameSession session, NpcAgent agent)
            {
                gameSession = session ?? throw new ArgumentNullException(nameof(session));
                if (agent == null) throw new ArgumentNullException(nameof(agent));
                currentNpcId = agent.connectionProfile != null && !string.IsNullOrEmpty(agent.connectionProfile.npcId)
                    ? agent.connectionProfile.npcId : agent.npcId;
                if (string.IsNullOrEmpty(currentNpcId))
                    throw new InvalidOperationException($"NpcAgent '{agent.name}' has no npcId.");
            }

            public Task<ToolResult> ExecuteAsync(string argsJson, object hostContext)
            {
                var args = string.IsNullOrEmpty(argsJson) ? new JObject() : JObject.Parse(argsJson);
                string npcId = args["npcId"]?.ToString();
                string choice = args["choice"]?.ToString();

                // 锁定语义：只能向"当前对话 NPC"表达选择；显式传入其他 NPC 则拒绝。
                if (string.IsNullOrEmpty(npcId))
                    npcId = currentNpcId;
                else if (npcId != currentNpcId)
                    return Task.FromResult(ToolResult.Fail($"只能向当前对话的 NPC（{currentNpcId}）表达选择"));

                if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(choice))
                    return Task.FromResult(ToolResult.Fail("缺少 npcId 或 choice 参数"));

                var result = gameSession.RecordPlayerChoice(npcId, choice);

                return Task.FromResult(result.Success
                    ? ToolResult.Ok(result.Message)
                    : ToolResult.Fail(result.Message));
            }
        }
    }
}
