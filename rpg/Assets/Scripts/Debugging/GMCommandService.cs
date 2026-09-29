#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIBot.Unity;
using FogHarbor.Dialogue;
using FogHarbor.Inventory;
using FogHarbor.Items;
using FogHarbor.Player;
using FogHarbor.Quest;
using FogHarbor.Save;
using FogHarbor.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FogHarbor.Debugging
{
    public sealed class GMCommandService : MonoBehaviour
    {
        public SaveSystem Saves { get; private set; }
        public QuestSystem Quests { get; private set; }
        public InventorySystem Inventory { get; private set; }
        public RewardService Wallet { get; private set; }
        public GameSession Session { get; private set; }
        public bool Transitioning { get; private set; }
        public bool Testing => Saves != null && Saves.IsTestSlot;
        public bool CanWrite => Testing && !Transitioning && !RequestAgents.Any(a => a.IsBusy);
        public NpcAgent[] Agents => FindObjectsOfType<NpcAgent>();
        private NpcAgent[] RequestAgents => FindObjectsOfType<NpcAgent>(true)
            .Concat(originalProfiles.Keys).Concat(unsupportedAgents.Keys)
            .Where(a => a != null).Distinct().ToArray();
        public PlayerController Player => FindObjectOfType<PlayerController>();
        public string NpcName(NpcAgent agent)
        {
            string id = agent.connectionProfile != null ? agent.connectionProfile.npcId : agent.npcId;
            return GetComponent<FogHarbor.Relationship.RelationshipSystem>().GetProfile(id)?.DisplayName ?? agent.name;
        }
        public string TestIdentity { get; private set; }
        private SaveSystem.SaveData beforeTest;
        private readonly Dictionary<NpcAgent, AIBotConnectionProfile> originalProfiles = new();
        private readonly Dictionary<NpcAgent, AIBotConnectionProfile> testProfiles = new();
        private readonly Dictionary<NpcAgent, bool> unsupportedAgents = new();
        private float nextRefresh;

        private void Awake()
        {
            Saves = GetComponent<SaveSystem>();
            Quests = GetComponent<QuestSystem>();
            Inventory = GetComponent<InventorySystem>();
            Wallet = GetComponent<RewardService>();
            Session = GetComponent<GameSession>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshAgents();
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;
            RefreshAgents();
        }

        public void RefreshAgents()
        {
            if (!Testing || Transitioning) return;
            foreach (var agent in originalProfiles.Keys.Where(a => a == null).ToArray())
            {
                if (testProfiles.TryGetValue(agent, out var clone) && clone != null) Destroy(clone);
                testProfiles.Remove(agent);
                originalProfiles.Remove(agent);
            }
            foreach (var agent in Agents)
                if (!originalProfiles.ContainsKey(agent) && !unsupportedAgents.ContainsKey(agent)) BindTestProfile(agent);
        }

        private void BindTestProfile(NpcAgent agent)
        {
            if (agent.IsBusy) { agent.CancelRunning(); return; }
            if (agent.connectionProfile == null)
            {
                // Local mode has no public memory reset API; fail closed instead of leaking test memory.
                unsupportedAgents.Add(agent, agent.enabled);
                agent.CancelRunning();
                agent.enabled = false;
                return;
            }
            var original = agent.connectionProfile;
            foreach (var pair in testProfiles)
                if (pair.Value == original && originalProfiles.TryGetValue(pair.Key, out var source)) { original = source; break; }
            originalProfiles.Add(agent, original);
            var clone = Instantiate(original);
            clone.hideFlags = HideFlags.DontSave;
            clone.name = original.name + " (GM Runtime)";
            clone.playerId = TestIdentity;
            clone.sessionId = "s-" + TestIdentity;
            testProfiles.Add(agent, clone);
            agent.connectionProfile = clone;
            if (!agent.ReloadConfig()) throw new InvalidOperationException("NPC 配置重载失败: " + agent.name);
        }

        public bool EnsureNpcTestIdentity(NpcAgent agent)
        {
            if (Transitioning) return false;
            if (!Testing) return true;
            RefreshAgents();
            return agent != null && originalProfiles.ContainsKey(agent) && agent.isActiveAndEnabled;
        }

        private void RotateIdentity()
        {
            TestIdentity = "gm-" + Guid.NewGuid().ToString("N");
            foreach (var pair in testProfiles)
            {
                if (pair.Key == null) continue;
                pair.Value.playerId = TestIdentity;
                pair.Value.sessionId = "s-" + TestIdentity;
                if (!pair.Key.ReloadConfig()) throw new InvalidOperationException("NPC 配置重载失败");
            }
        }

        private void RestoreProfiles()
        {
            if (originalProfiles.Keys.Any(a => a != null && a.IsBusy))
                throw new InvalidOperationException("NPC 请求尚未结束，未恢复正式会话");
            try
            {
                foreach (var pair in originalProfiles)
                {
                    if (pair.Key == null) continue;
                    pair.Key.connectionProfile = pair.Value;
                    if (!pair.Key.ReloadConfig())
                        throw new InvalidOperationException("NPC 正式配置重载失败: " + pair.Key.name);
                }
            }
            catch
            {
                // Keep all backends in test mode if restoring even one NPC fails.
                foreach (var pair in testProfiles)
                {
                    if (pair.Key == null) continue;
                    pair.Key.connectionProfile = pair.Value;
                    if (!pair.Key.ReloadConfig())
                    {
                        if (!unsupportedAgents.ContainsKey(pair.Key)) unsupportedAgents.Add(pair.Key, pair.Key.enabled);
                        pair.Key.enabled = false;
                    }
                }
                throw;
            }
            foreach (var clone in testProfiles.Values) if (clone != null) Destroy(clone);
            testProfiles.Clear();
            originalProfiles.Clear();
            foreach (var pair in unsupportedAgents) if (pair.Key != null) pair.Key.enabled = pair.Value;
            unsupportedAgents.Clear();
            TestIdentity = null;
        }

        private async Task CancelRequests()
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while (true)
            {
                // Re-discover while awaiting cancellation, including hidden and newly loaded NPCs.
                var agents = RequestAgents;
                foreach (var agent in agents) agent.CancelRunning();
                if (!agents.Any(a => a != null && a.IsBusy)) return;
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("取消 NPC 请求超时，状态未恢复");
                await Task.Delay(25);
                if (this == null) throw new OperationCanceledException();
            }
        }

        public async Task<QuestResult> SwitchTestAsync(bool start)
        {
            if (Transitioning) return QuestResult.Fail("存档操作正在执行");
            if (start == Testing) return QuestResult.Fail(start ? "已在测试槽" : "已在正式槽");
            if (start && Agents.Any(a => a.connectionProfile == null))
                return QuestResult.Fail("测试隔离仅支持 Server Connection Profile，请先配置所有 NPC");
            Transitioning = true;
            SaveSystem.SaveData rollback = null;
            try
            {
                await CancelRequests();
                if (start)
                {
                    beforeTest = Saves.Capture(Player);
                    Saves.WriteDebugSnapshot("before_test", beforeTest);
                    Saves.WriteDebugSnapshot("checkpoint", beforeTest);
                    Saves.SelectTestSlot(true);
                    if (!Saves.Save(Player)) throw new InvalidOperationException("测试槽保存失败");
                    RotateIdentity();
                    foreach (var agent in Agents) BindTestProfile(agent);
                }
                else
                {
                    rollback = Saves.Capture(Player);
                    if (!Saves.Apply(beforeTest, Player)) throw new InvalidOperationException("进入测试前的快照无效");
                    RestoreProfiles();
                    Saves.SelectTestSlot(false);
                    beforeTest = null;
                }
                return QuestResult.Ok(start ? "测试槽已启用，正式存档保持不变" : "已恢复进入测试前的状态及正式会话");
            }
            catch (Exception ex)
            {
                if (start)
                {
                    try
                    {
                        RestoreProfiles();
                        Saves.SelectTestSlot(false);
                        beforeTest = null;
                    }
                    catch (Exception cleanupError)
                    {
                        return QuestResult.Fail(ex.Message + "；会话清理失败，未切回正式槽：" + cleanupError.Message);
                    }
                }
                else if (rollback != null) Saves.Apply(rollback, Player);
                return QuestResult.Fail(ex.Message);
            }
            finally { Transitioning = false; }
        }

        public QuestResult SaveCheckpoint()
        {
            if (!CanWrite) return Denied();
            try
            {
                Saves.WriteDebugSnapshot("checkpoint", Saves.Capture(Player));
                return QuestResult.Ok("完整测试检查点已保存");
            }
            catch (Exception ex) { return QuestResult.Fail(ex.Message); }
        }

        public async Task<QuestResult> RestoreCheckpointAsync()
        {
            if (!Testing || Transitioning) return Denied();
            Transitioning = true;
            SaveSystem.SaveData rollback = null;
            try
            {
                var snapshot = Saves.ReadDebugSnapshot("checkpoint");
                await CancelRequests();
                rollback = Saves.Capture(Player);
                if (!Saves.Apply(snapshot, Player)) throw new InvalidOperationException("检查点无效");
                RotateIdentity();
                return Persist("已恢复检查点，并创建新的测试 AI 身份");
            }
            catch (Exception ex)
            {
                if (rollback != null) Saves.Apply(rollback, Player);
                return QuestResult.Fail(ex.Message);
            }
            finally { Transitioning = false; }
        }

        private QuestResult Denied() => QuestResult.Fail(!Testing ? "请先开始测试，正式槽禁止 GM 修改" : "NPC 请求或存档操作尚未结束");
        public QuestResult SaveTest() => CanWrite ? Persist("测试槽已保存") : Denied();
        private QuestResult Persist(string message) => Saves.Save(Player)
            ? QuestResult.Ok(message) : QuestResult.Fail(message + "；运行状态已改变，但保存失败");

        public QuestResult QuestCommand(string id, string command)
        {
            if (!CanWrite) return Denied();
            var def = Quests.GetQuest(id);
            if (def == null) return QuestResult.Fail("未找到任务");
            var before = Quests.GetState(id);
            int countBefore = string.IsNullOrEmpty(def.TargetItemId) ? 0 : Inventory.GetCount(def.TargetItemId);
            QuestResult result;
            switch (command)
            {
                case "accept": result = Session.AcceptQuest(id); break;
                case "deliver": result = Session.CompleteQuest(id); break;
                case "reset":
                    Quests.DebugReset(id);
                    return Persist("状态已重置为 Available；原有物品和奖励未回退");
                case "fill":
                case "prepare":
                    if (before == QuestState.Rewarded) return QuestResult.Fail("任务已领奖，请恢复检查点或重置状态");
                    if (!string.IsNullOrEmpty(def.TargetItemId) && !ItemDatabase.Exists(def.TargetItemId))
                        return QuestResult.Fail("目标物品定义不存在");
                    if (command == "prepare" && before == QuestState.Available)
                    {
                        result = Session.AcceptQuest(id);
                        if (!result.Success) return result;
                    }
                    int missing = Mathf.Max(0, def.TargetCount - countBefore);
                    if (missing > 0 && !string.IsNullOrEmpty(def.TargetItemId)) Session.PickupItem(def.TargetItemId, missing);
                    if (Quests.CanComplete(id)) Quests.Complete(id);
                    return Persist($"{def.TargetItemId}: {countBefore} → {Inventory.GetCount(def.TargetItemId ?? "")}; {before} → {Quests.GetState(id)}；未发奖励");
                default: return QuestResult.Fail("未知命令");
            }
            if (result.Success && !Saves.LastSaveSucceeded) return QuestResult.Fail(result.Message + "；运行状态已改变，但保存失败");
            return result;
        }

        public QuestResult ChangeItem(string id, int amount, bool remove)
        {
            if (!CanWrite) return Denied();
            if (!ItemDatabase.Exists(id) || amount < 1 || amount > 9999) return QuestResult.Fail("物品或数量无效（1–9999）");
            int before = Inventory.GetCount(id);
            if (!remove && before > int.MaxValue - amount) return QuestResult.Fail("物品数量溢出");
            if (remove)
            {
                if (!Inventory.Remove(id, amount)) return QuestResult.Fail("持有数量不足");
            }
            else Session.PickupItem(id, amount);
            return Persist($"{id}: {before} → {Inventory.GetCount(id)}");
        }

        public QuestResult Heal()
        {
            if (!CanWrite) return Denied();
            if (Player == null) return QuestResult.Fail("当前场景没有玩家");
            Player.Heal(Player.MaxHp);
            return Persist("生命已恢复");
        }

        public QuestResult AddGold(int amount)
        {
            if (!CanWrite) return Denied();
            if (amount < 1 || amount > 9999 || Wallet.Gold > int.MaxValue - amount) return QuestResult.Fail("金币数量无效");
            Wallet.AddGold(amount);
            return Persist($"增加 {amount} 金币，现有 {Wallet.Gold}");
        }

        private void OnDestroy()
        {
            foreach (var agent in RequestAgents) agent.CancelRunning();
            foreach (var pair in originalProfiles)
                if (pair.Key != null) pair.Key.connectionProfile = pair.Value;
            foreach (var clone in testProfiles.Values) if (clone != null) Destroy(clone);
            foreach (var pair in unsupportedAgents) if (pair.Key != null) pair.Key.enabled = pair.Value;
        }
    }
}
#endif
