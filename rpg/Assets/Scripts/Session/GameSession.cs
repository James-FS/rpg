using UnityEngine;
using UnityEngine.SceneManagement;
using FogHarbor.Items;
using FogHarbor.Quest;
using FogHarbor.Inventory;
using FogHarbor.Equipment;
using FogHarbor.Player;
using FogHarbor.Relationship;
using FogHarbor.Save;

namespace FogHarbor.Session
{
    /// <summary>
    /// 领奖/流程操作结果：携带成功与否与面向模型/玩家的中文消息。
    /// </summary>
    public readonly struct QuestResult
    {
        public readonly bool Success;
        public readonly string Message;

        public QuestResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static QuestResult Ok(string message) => new QuestResult(true, message);
        public static QuestResult Fail(string message) => new QuestResult(false, message);
    }

    /// <summary>
    /// 流程编排层（设计文档 §6.1 铁律第 3 条）：全项目唯一跨系统流程之家。
    /// 方法体只允许 = 系统调用序列 + Save 收尾；不存游戏数据、不带 UI、不写规则判断
    /// （"能不能"永远在系统层校验）。幂等靠系统层状态校验（如 Rewarded 后再领奖自然失败）。
    /// 挂载在 AppRoot 上，通过 DontDestroyOnLoad 跨场景保留。
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        private QuestSystem questSystem;
        private InventorySystem inventory;
        private EquipmentSystem equipmentSystem;
        private RewardService rewardService;
        private RelationshipSystem relationshipSystem;
        private SaveSystem saveSystem;

        private PlayerController player;
        private bool saveLoadedOnce;

        private void Awake()
        {
            questSystem = GetComponent<QuestSystem>();
            inventory = GetComponent<InventorySystem>();
            equipmentSystem = GetComponent<EquipmentSystem>();
            rewardService = GetComponent<RewardService>();
            relationshipSystem = GetComponent<RelationshipSystem>();
            saveSystem = GetComponent<SaveSystem>();

            if (questSystem == null) Debug.LogError("[GameSession] 未找到 QuestSystem");
            if (equipmentSystem == null) Debug.LogError("[GameSession] 未找到 EquipmentSystem");
            if (rewardService == null) Debug.LogError("[GameSession] 未找到 RewardService");
            if (relationshipSystem == null) Debug.LogError("[GameSession] 未找到 RelationshipSystem");
            if (saveSystem == null) Debug.LogError("[GameSession] 未找到 SaveSystem");
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void Start()
        {
            // 直接 Play（无 Boot 过渡）时 sceneLoaded 早于 AppRoot 创建，Start 兜底触发首次读档。
            TryBindAndLoad();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBindAndLoad();
        }

        /// <summary>场景加载后绑定玩家（PlayerController 在场景物体上，AppRoot 在 DontDestroyOnLoad），
        /// 并按差距 #6 恢复存档——只首次进入游戏场景时读档，之后场景切换不再重复读档。</summary>
        private void TryBindAndLoad()
        {
            if (player == null)
                player = FindObjectOfType<PlayerController>();

            if (player != null && !saveLoadedOnce)
            {
                saveLoadedOnce = true;
                if (saveSystem != null)
                    saveSystem.LoadAndApply(player);
            }
        }

        /// <summary>供意图入口/UI 注入玩家引用（UIManager 场景加载时会同步）。</summary>
        public void SetPlayer(PlayerController pc)
        {
            player = pc;
        }

        // ─── 用例：完成任务并领奖（主线终点，唯一入口）───
        // 流程：只读校验 → RewardService 发奖 → QuestSystem 置 Rewarded → Save。
        // 规则判断（能否领）由 QuestSystem.CanClaimReward 与 RewardService.GrantQuestReward 负责。

        /// <summary>完成任务-领奖完整流程。</summary>
        public QuestResult CompleteQuest(string questId)
        {
            if (!questSystem.CanClaimReward(questId))
            {
                var state = questSystem.GetState(questId);
                string stateText = state == QuestState.Rewarded ? "已领奖" : state.ToString();
                return QuestResult.Fail($"任务 {questId} 当前状态为 {stateText}，无法领取奖励");
            }

            var def = questSystem.GetQuest(questId);
            if (def == null)
                return QuestResult.Fail($"任务不存在: {questId}");

            if (!rewardService.GrantQuestReward(def))
            {
                string need = string.IsNullOrEmpty(def.TargetItemId) ? "未知物品"
                    : $"{def.TargetItemId} x{def.TargetCount}";
                return QuestResult.Fail($"还缺少任务物品 {need}，无法结算");
            }

            if (!questSystem.ClaimReward(questId))
            {
                // 防御性分支：CanClaimReward 通过后同帧必然成功；若状态被抢断则报错不产生额外副作用。
                return QuestResult.Fail("领奖状态推进失败（请勿重复尝试）");
            }

            saveSystem.Save(player);
            return QuestResult.Ok(BuildCompletionMessage(def));
        }

        // ─── 用例：接受任务（唯一入口）───

        /// <summary>接受任务流程：QuestSystem 校验并推进 → Save。</summary>
        public QuestResult AcceptQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return QuestResult.Fail("缺少 questId 参数");

            var def = questSystem.GetQuest(questId);
            if (def == null)
                return QuestResult.Fail($"任务不存在: {questId}");

            if (!questSystem.Accept(questId))
            {
                return QuestResult.Fail(
                    $"任务 {questId} 当前状态为 {questSystem.GetState(questId)}，无法接受");
            }

            saveSystem.Save(player);
            return QuestResult.Ok(
                $"玩家已接受任务「{def.Title}」。目标：{def.TargetItemId} x{def.TargetCount}。");
        }

        // ─── 用例：拾取物品并推进任务（唯一入口）───

        /// <summary>拾取流程：物品入库 → 目标已满足的 Accepted 任务置 Completed → Save。</summary>
        public QuestResult PickupItem(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId))
                return QuestResult.Fail("拾取物未配置 itemId");

            if (inventory == null)
                return QuestResult.Fail("背包系统未就绪，无法拾取");

            int before = inventory.GetCount(itemId);
            inventory.Add(itemId, count);

            // 推进任务：物品入库后由 QuestSystem 校验哪些 Accepted 任务目标已满足（系统层规则）。
            questSystem.TryCompleteOnPickup(itemId);

            saveSystem.Save(player);
            return QuestResult.Ok($"获得 {itemId} x{count}（现有 {before + count}）");
        }

        // ─── 用例：装备 / 使用 / 丢弃（背包面板发意图，§6.1 差距 #1）───

        /// <summary>装备流程：EquipmentSystem 校验并穿戴（内部处理背包扣取与旧装备退回）→ Save。</summary>
        public QuestResult EquipItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return QuestResult.Fail("缺少 itemId");

            if (!equipmentSystem.Equip(itemId))
                return QuestResult.Fail($"无法装备 {itemId}（类型不匹配或背包中没有）");

            saveSystem.Save(player);
            return QuestResult.Ok($"已装备 {itemId}");
        }

        /// <summary>使用消耗品流程：校验物品定义（只读）→ 回血 → 扣减背包 → Save。</summary>
        public QuestResult UseItem(string itemId)
        {
            var data = ItemDatabase.GetById(itemId);
            if (data == null)
                return QuestResult.Fail($"未知物品: {itemId}");
            if (data.Type != ItemType.Consumable)
                return QuestResult.Fail($"{data.DisplayName} 不是可消耗物品");
            if (!inventory.Has(itemId, 1))
                return QuestResult.Fail($"背包中没有 {itemId}");

            if (player != null && data.HealAmount > 0)
                player.Heal(data.HealAmount);

            inventory.Remove(itemId, 1);
            saveSystem.Save(player);
            return QuestResult.Ok($"已使用 {data.DisplayName}");
        }

        /// <summary>丢弃物品流程：背包扣减 → Save。</summary>
        public QuestResult DropItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return QuestResult.Fail("缺少 itemId");

            if (!inventory.Remove(itemId, 1))
                return QuestResult.Fail($"背包中没有 {itemId}");

            saveSystem.Save(player);
            return QuestResult.Ok($"已丢弃 {itemId}");
        }

        private static string BuildCompletionMessage(QuestData def)
        {
            string rewardDesc = $"{def.RewardGold} 金币";
            if (!string.IsNullOrEmpty(def.RewardItemId))
                rewardDesc += $"、{def.RewardItemId} x{def.RewardItemCount}";
            return $"任务「{def.Title}」完成！已扣除任务物品，发放奖励：{rewardDesc}。";
        }

        // ─── 用例：提交金币/背包只读查询（供 AI 只读工具使用）───

        /// <summary>当前玩家金币（只读查询）。</summary>
        public int GetGold()
        {
            return rewardService != null ? rewardService.Gold : 0;
        }

        // ─── 用例：记录玩家选择（好感度，§6.1 白名单工具 record_player_choice）───

        /// <summary>记录玩家对 NPC 的选择：白名单校验在 RelationshipSystem 层 → 好感变化 → Save。</summary>
        public QuestResult RecordPlayerChoice(string npcId, string choiceId)
        {
            if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(choiceId))
                return QuestResult.Fail("缺少 npcId 或 choice 参数");

            var profile = relationshipSystem.GetProfile(npcId);
            if (profile == null)
                return QuestResult.Fail($"未找到 NPC 配置: {npcId}");

            if (!relationshipSystem.TryRecordPlayerChoice(npcId, choiceId, out int delta))
                return QuestResult.Fail($"选择 {choiceId} 不在 {profile.DisplayName} 的白名单内，无法记录");

            saveSystem.Save(player);

            string hint = "";
            foreach (var c in profile.Choices)
            {
                if (c != null && c.choiceId == choiceId)
                {
                    hint = string.IsNullOrEmpty(c.replyHint) ? "" : c.replyHint;
                    break;
                }
            }
            string deltaText = delta > 0 ? $"+{delta}" : delta.ToString();
            string reply = $"已记录你对{profile.DisplayName}的选择「{choiceId}」（好感{deltaText}，当前 {relationshipSystem.GetFavor(npcId)}）";
            if (!string.IsNullOrEmpty(hint)) reply += "。" + hint;
            return QuestResult.Ok(reply);
        }

        /// <summary>当前 NPC 好感（只读查询，供 AI 工具/快照使用）。</summary>
        public int GetFavor(string npcId)
        {
            return relationshipSystem != null ? relationshipSystem.GetFavor(npcId) : 0;
        }
    }
}