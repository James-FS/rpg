using UnityEngine;
using System;
using System.Collections;
using System.IO;
using UnityEngine.SceneManagement;
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
    /// 方法负责系统调用顺序与存档提交，不拥有领域数据；业务拒绝由各系统返回，状态提交成功但写盘失败时恢复操作前快照。
    /// 幂等靠系统层状态校验（如 Rewarded 后再领奖自然失败）。
    /// 挂载在 AppRoot 上，通过 DontDestroyOnLoad 跨场景保留。
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        private QuestSystem questSystem;
        private InventorySystem inventory;
        private EquipmentSystem equipmentSystem;
        private RewardService rewardService;
        private RelationshipSystem relationshipSystem;
        private SaveSystem saveSystem;

        private PlayerController player;
        private bool saveLoadedOnce;
        private bool transitioning;
        private bool arrivalPending;
        private string arrivalTargetScene;
        private string arrivalOriginScene;
        public bool IsTransitioning => transitioning;
        public bool IsReadyToSave => saveLoadedOnce && !transitioning && player != null;
        public PlayerController Player => player;
        public QuestSystem Quests => questSystem;
        public InventorySystem Inventory => inventory;
        public EquipmentSystem Equipment => equipmentSystem;
        public RewardService Wallet => rewardService;
        public RelationshipSystem Relationships => relationshipSystem;
        public SaveSystem Saves => saveSystem;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                throw new InvalidOperationException("Only one GameSession may exist at a time.");

            questSystem = GetComponent<QuestSystem>()
                ?? throw new MissingComponentException("GameSession requires QuestSystem on AppRoot.");
            inventory = GetComponent<InventorySystem>()
                ?? throw new MissingComponentException("GameSession requires InventorySystem on AppRoot.");
            equipmentSystem = GetComponent<EquipmentSystem>()
                ?? throw new MissingComponentException("GameSession requires EquipmentSystem on AppRoot.");
            rewardService = GetComponent<RewardService>()
                ?? throw new MissingComponentException("GameSession requires RewardService on AppRoot.");
            relationshipSystem = GetComponent<RelationshipSystem>()
                ?? throw new MissingComponentException("GameSession requires RelationshipSystem on AppRoot.");
            saveSystem = GetComponent<SaveSystem>()
                ?? throw new MissingComponentException("GameSession requires SaveSystem on AppRoot.");

            BindPlayerFromScene();
            Instance = this;
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void Start()
        {
            questSystem.ValidateNpcReferences(relationshipSystem);
            // 直接 Play（无 Boot 过渡）时 sceneLoaded 早于 AppRoot 创建，Start 兜底触发首次读档。
            TryBindAndLoad();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindPlayerFromScene();
            TryBindAndLoad();
            // 传送落点必须在 sceneLoaded（激活即触发）处理：LoadSceneAsync 完成回调可能滞后数秒，
            // 若在协程续跑里落点，玩家会先暴露在默认出生点。
            if (arrivalPending && scene.name == arrivalTargetScene)
            {
                arrivalPending = false;
                if (player != null) PlaceAtArrivalGate(player, arrivalOriginScene);
            }
        }

        /// <summary>场景加载后绑定玩家（PlayerController 在场景物体上，AppRoot 在 DontDestroyOnLoad），
        /// 并只在首次进入游戏场景时恢复存档。</summary>
        private void TryBindAndLoad()
        {
            if (player == null)
                BindPlayerFromScene();

            if (player != null && !saveLoadedOnce)
            {
                try
                {
                    saveSystem.LoadAndApply(player);
                }
                catch (InvalidDataException ex)
                {
                    FogHarbor.Bootstrap.GameBootstrap.ReturnToTitleWithMessage(ex.Message);
                    return;
                }
                var beforeStarterItems = saveSystem.Capture(player);
                if (inventory.EnsureStarterItems(equipmentSystem.WeaponId, equipmentSystem.ArmorId)
                    && !saveSystem.Save(player))
                {
                    saveSystem.Apply(beforeStarterItems, player);
                    FogHarbor.Bootstrap.GameBootstrap.ReturnToTitleWithMessage(saveSystem.LastSaveMessage);
                    return;
                }
                saveLoadedOnce = true;
            }
        }

        /// <summary>Only the scene boundary discovers the active player; systems consume this binding.</summary>
        public PlayerController BindPlayerFromScene()
        {
            var players = FindObjectsOfType<PlayerController>();
            bool gameplayScene = SaveSystem.IsGameplayScene(SceneManager.GetActiveScene().name);
            if (players.Length > 1)
                throw new InvalidOperationException($"Scene '{SceneManager.GetActiveScene().name}' contains {players.Length} PlayerControllers; exactly one is supported.");
            if (players.Length == 1) SetPlayer(players[0]);
            else
            {
                player = null;
                equipmentSystem.ClearPlayer();
                if (gameplayScene)
                    throw new MissingComponentException($"Gameplay scene '{SceneManager.GetActiveScene().name}' requires a PlayerController.");
            }
            return player;
        }

        /// <summary>供意图入口/UI 注入玩家引用（UIManager 场景加载时会同步）。</summary>
        public void SetPlayer(PlayerController pc)
        {
            player = pc ?? throw new ArgumentNullException(nameof(pc));
            equipmentSystem.SetPlayer(player);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Save departure, load the target, carry health, then save the arrival.</summary>
        public bool ChangeScene(string target, bool revive = false)
        {
            if (!SaveSystem.IsGameplayScene(target) || !Application.CanStreamedLevelBeLoaded(target))
                throw new ArgumentException($"'{target}' is not a configured gameplay scene.", nameof(target));
            if (!IsReadyToSave) throw new InvalidOperationException("Scene transitions require a bound, loaded player session.");
            if (transitioning) return false;
            if (!saveSystem.Save(player)) return false;
            transitioning = true;
            int hp = revive ? player.MaxHp : player.CurrentHp;
            int maxHp = player.MaxHp;
            arrivalPending = !revive;
            arrivalTargetScene = target;
            arrivalOriginScene = SceneManager.GetActiveScene().name;
            StartCoroutine(Travel(target, hp, maxHp));
            return true;
        }

        private IEnumerator Travel(string target, int hp, int maxHp)
        {
            FogHarbor.UI.UIManager.Instance.HideForTravel();
            yield return SceneManager.LoadSceneAsync(target);
            player = BindPlayerFromScene();
            player.RestoreHealth(hp, maxHp);
            if (!saveSystem.Save(player))
            {
                FogHarbor.UI.UIManager.Instance.ShowToast(saveSystem.LastSaveMessage);
                transitioning = false;
                yield break;
            }
            transitioning = false;
        }

        /// <summary>落到目标场景中指回出发场景的那扇门口；门 Prefab 约定 forward 朝向场景内部，
        /// 因此落在门前一格并面朝场景，避免站在门的碰撞体上。复活仍走默认出生点。</summary>
        private void PlaceAtArrivalGate(PlayerController player, string originScene)
        {
            FogHarbor.World.SceneGate arrivalGate = null;
            foreach (var gate in UnityEngine.Object.FindObjectsOfType<FogHarbor.World.SceneGate>())
            {
                if (gate.TargetSceneName != originScene) continue;
                if (arrivalGate != null)
                    throw new InvalidOperationException($"Scene '{SceneManager.GetActiveScene().name}' has multiple gates back to '{originScene}'.");
                arrivalGate = gate;
            }
            if (arrivalGate == null)
                throw new MissingReferenceException($"Scene '{SceneManager.GetActiveScene().name}' has no gate back to '{originScene}'.");

            Transform t = arrivalGate.transform;
            Vector3 forward = t.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.001f)
                throw new InvalidOperationException($"Arrival gate '{arrivalGate.name}' must face horizontally.");
            forward.Normalize();
            // CharacterController is disabled by RestorePose during the teleport.
            player.RestorePose(t.position + forward * 1.2f + Vector3.up * 0.1f,
                Quaternion.LookRotation(forward).eulerAngles.y);
        }

        // ─── 用例：完成任务并领奖（主线终点，唯一入口）───
        // 流程：只读校验 → RewardService 发奖 → QuestSystem 置 Rewarded → Save。
        // 规则判断（能否领）由 QuestSystem.CanClaimReward 与 RewardService.GrantQuestReward 负责。

        /// <summary>完成任务-领奖完整流程。</summary>
        public QuestResult CompleteQuest(string questId) => ExecuteTransaction(() => CompleteQuestCore(questId));

        private QuestResult CompleteQuestCore(string questId)
        {
            var def = questSystem.GetQuest(questId);
            if (def == null)
                return QuestResult.Fail($"任务不存在: {questId}");

            if (!questSystem.CanClaimReward(questId))
            {
                var state = questSystem.GetState(questId);
                string stateText = state == QuestState.Rewarded ? "已领奖" : state.ToString();
                return QuestResult.Fail($"任务 {questId} 当前状态为 {stateText}，无法领取奖励");
            }

            if (!rewardService.GrantQuestReward(def))
            {
                string need = string.IsNullOrEmpty(def.TargetItemId) ? "未知物品"
                    : $"{def.TargetItemId} x{def.TargetCount}";
                return QuestResult.Fail($"还缺少任务物品 {need}，无法结算");
            }

            if (!questSystem.ClaimReward(questId))
                throw new InvalidOperationException($"Quest '{questId}' changed state during its reward transaction.");

            int favorGained = 0;
            if (!string.IsNullOrEmpty(def.RewardNpcId) && def.RewardFavor != 0)
            {
                relationshipSystem.AddFavor(def.RewardNpcId, def.RewardFavor);
                favorGained = def.RewardFavor;
            }

            return QuestResult.Ok(BuildCompletionMessage(def, favorGained));
        }

        // ─── 用例：接受任务（唯一入口）───

        /// <summary>接受任务流程：QuestSystem 校验并推进 → Save。</summary>
        public QuestResult DeliverQuest(string questId) => ExecuteTransaction(() => DeliverQuestCore(questId));

        private QuestResult DeliverQuestCore(string questId)
        {
            if (string.IsNullOrEmpty(questId) || questSystem.GetQuest(questId) == null)
                return QuestResult.Fail("委托不存在。");
            if (questSystem.GetState(questId) == QuestState.Accepted)
            {
                if (!questSystem.CanComplete(questId))
                    return QuestResult.Fail("还没有收集齐委托需要的物品。");
                if (!questSystem.Complete(questId))
                    throw new InvalidOperationException($"Quest '{questId}' failed its completion transition after passing CanComplete.");
                var result = CompleteQuestCore(questId);
                if (!result.Success)
                    throw new InvalidOperationException($"Quest '{questId}' became eligible for delivery but its reward transaction failed: {result.Message}");
                return result;
            }
            return CompleteQuestCore(questId);
        }

        public QuestResult AcceptQuest(string questId) => ExecuteTransaction(() => AcceptQuestCore(questId));

        private QuestResult AcceptQuestCore(string questId)
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

            rewardService.GrantQuestSupplies(def);
            string supplies = def.AcceptanceItem != null
                ? $" 获得{def.AcceptanceItem.DisplayName} ×{def.AcceptanceItemCount}（已放入背包）。" : "";
            return QuestResult.Ok(
                $"玩家已接受任务「{def.Title}」。目标：{def.TargetItemId} x{def.TargetCount}。{supplies}");
        }

        // ─── 用例：拾取物品并推进任务（唯一入口）───

        /// <summary>拾取流程：物品入库 → 目标已满足的 Accepted 任务置 Completed → Save。</summary>
        public QuestResult PickupItem(string itemId, int count) => ExecuteTransaction(() => PickupItemCore(itemId, count));

        private QuestResult PickupItemCore(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("Pickup itemId must be configured.", nameof(itemId));
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Pickup count must be positive.");

            int before = inventory.GetCount(itemId);
            inventory.Add(itemId, count);

            // 推进任务：物品入库后由 QuestSystem 校验哪些 Accepted 任务目标已满足（系统层规则）。
            questSystem.TryCompleteOnPickup(itemId);

            return QuestResult.Ok($"获得 {itemId} x{count}（现有 {before + count}）");
        }

        // ─── 用例：装备 / 使用 / 丢弃（背包面板发意图，§6.1 差距 #1）───

        /// <summary>装备流程：EquipmentSystem 校验并穿戴（内部处理背包扣取与旧装备退回）→ Save。</summary>
        public QuestResult EquipItem(string itemId) => ExecuteTransaction(() => EquipItemCore(itemId));

        private QuestResult EquipItemCore(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return QuestResult.Fail("缺少 itemId");

            if (!equipmentSystem.Equip(itemId))
                return QuestResult.Fail($"无法装备 {itemId}（类型不匹配或背包中没有）");

            return QuestResult.Ok($"已装备 {itemId}");
        }

        /// <summary>卸下装备流程：EquipmentSystem 校验并脱下（物品退回背包）→ Save。</summary>
        public QuestResult UnequipItem(string itemId) => ExecuteTransaction(() => UnequipItemCore(itemId));

        private QuestResult UnequipItemCore(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return QuestResult.Fail("缺少 itemId");

            if (!equipmentSystem.UnequipItem(itemId))
                return QuestResult.Fail($"未装备 {itemId}，无法卸下");

            return QuestResult.Ok($"已卸下 {itemId}");
        }

        /// <summary>使用消耗品流程：由背包校验并消耗 → 应用物品效果 → Save。</summary>
        public QuestResult UseItem(string itemId) => ExecuteTransaction(() => UseItemCore(itemId));

        private QuestResult UseItemCore(string itemId)
        {
            var result = inventory.TryConsumeConsumable(itemId, out var data);
            switch (result)
            {
                case InventorySystem.ConsumeResult.InvalidItemId:
                    return QuestResult.Fail("缺少 itemId");
                case InventorySystem.ConsumeResult.UnknownItem:
                    return QuestResult.Fail($"未知物品: {itemId}");
                case InventorySystem.ConsumeResult.NotConsumable:
                    return QuestResult.Fail($"{data.DisplayName} 不是可消耗物品");
                case InventorySystem.ConsumeResult.NotOwned:
                    return QuestResult.Fail($"背包中没有 {itemId}");
                case InventorySystem.ConsumeResult.Success:
                    break;
                default:
                    return QuestResult.Fail($"无法使用物品: {itemId}");
            }

            if (data.HealAmount > 0)
                player.Heal(data.HealAmount);

            return QuestResult.Ok($"已使用 {data.DisplayName}");
        }

        /// <summary>丢弃物品流程：背包扣减 → Save。</summary>
        public QuestResult DropItem(string itemId) => ExecuteTransaction(() => DropItemCore(itemId));

        private QuestResult DropItemCore(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return QuestResult.Fail("缺少 itemId");

            if (!inventory.Remove(itemId, 1))
                return QuestResult.Fail($"背包中没有 {itemId}");

            return QuestResult.Ok($"已丢弃 {itemId}");
        }

        private QuestResult ExecuteTransaction(Func<QuestResult> operation)
        {
            var before = saveSystem.Capture(player);
            try
            {
                var result = operation();
                if (!result.Success) return result;
                if (saveSystem.Save(player)) return result;

                saveSystem.Apply(before, player);
                return QuestResult.Fail(saveSystem.LastSaveMessage);
            }
            catch (Exception operationError)
            {
                try
                {
                    saveSystem.Apply(before, player);
                }
                catch (Exception rollbackError)
                {
                    throw new AggregateException("The game operation failed and its state rollback also failed.", operationError, rollbackError);
                }
                throw;
            }
        }

        private string BuildCompletionMessage(QuestData def, int favorGained)
        {
            string rewardDesc = $"{def.RewardGold} 金币";
            if (!string.IsNullOrEmpty(def.RewardItemId))
                rewardDesc += $"、{def.RewardItemId} x{def.RewardItemCount}";
            string favorDesc = string.Empty;
            if (favorGained != 0)
            {
                string npcName = relationshipSystem.GetProfile(def.RewardNpcId)?.DisplayName
                    ?? throw new InvalidOperationException($"Quest '{def.QuestId}' references an unknown reward NPC.");
                string verb = favorGained > 0 ? "提升" : "下降";
                favorDesc = $"{npcName} 对你的好感{verb}了 {Mathf.Abs(favorGained)} 点。";
            }
            return $"任务「{def.Title}」完成！已扣除任务物品，发放奖励：{rewardDesc}。{favorDesc}";
        }

        // ─── 用例：提交金币/背包只读查询（供 AI 只读工具使用）───

        /// <summary>当前玩家金币（只读查询）。</summary>
        public int GetGold()
        {
            return rewardService.Gold;
        }

        // ─── 用例：记录玩家选择（好感度，§6.1 白名单工具 record_player_choice）───

        /// <summary>记录玩家对 NPC 的选择：白名单校验在 RelationshipSystem 层 → 好感变化 → Save。</summary>
        public QuestResult RecordPlayerChoice(string npcId, string choiceId) =>
            ExecuteTransaction(() => RecordPlayerChoiceCore(npcId, choiceId));

        private QuestResult RecordPlayerChoiceCore(string npcId, string choiceId)
        {
            if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(choiceId))
                return QuestResult.Fail("缺少 npcId 或 choice 参数");

            var profile = relationshipSystem.GetProfile(npcId);
            if (profile == null)
                return QuestResult.Fail($"未找到 NPC 配置: {npcId}");

            if (!relationshipSystem.TryRecordPlayerChoice(npcId, choiceId, out int delta))
                return QuestResult.Fail($"选择 {choiceId} 不在 {profile.DisplayName} 的白名单内，无法记录");

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
            return relationshipSystem.GetFavor(npcId);
        }
    }
}



