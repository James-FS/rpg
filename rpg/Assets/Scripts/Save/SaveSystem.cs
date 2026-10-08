using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using FogHarbor.Session;
using FogHarbor.Inventory;
using FogHarbor.Equipment;
using FogHarbor.Player;
using FogHarbor.Quest;
using FogHarbor.Relationship;

namespace FogHarbor.Save
{
    /// <summary>
    /// 存档系统：将所有游戏状态序列化为 JSON，安全写入磁盘。
    /// 挂载在 AppRoot 上，通过 DontDestroyOnLoad 跨场景保留。
    /// 
    /// 写入流程（防损坏）：
    ///   save.tmp → 校验 JSON → 替换 save.json → 旧文件备份为 save.bak
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        private const int SAVE_VERSION = 2;

        private static string SaveDir => Application.persistentDataPath;
        private string slotName = "fog_harbor_save";
        private string SavePath => Path.Combine(SaveDir, slotName + ".json");
        private string TmpPath => Path.Combine(SaveDir, slotName + ".tmp");
        private string BakPath => Path.Combine(SaveDir, slotName + ".bak");
        public bool LastSaveSucceeded { get; private set; } = true;
        public string CurrentSlot => slotName;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool IsTestSlot => slotName == "fog_harbor_gm_test";
        public void SelectTestSlot(bool testing) => slotName = testing ? "fog_harbor_gm_test" : "fog_harbor_save";
#endif

        // 外部引用
        private InventorySystem inventory;
        private EquipmentSystem equipment;
        private QuestSystem questSystem;
        private RewardService rewardService;
        private RelationshipSystem relationshipSystem;

        /// <summary>存档/读档完成时触发。</summary>
        public event Action OnSaveComplete;
        public event Action OnLoadComplete;
        public event Action<bool, string> OnSaveResult;
        public string LastSaveMessage { get; private set; }
        public float LastSaveTime { get; private set; } = -100f;

        // ─── 初始化 ───

        private void Awake()
        {
            inventory = GetComponent<InventorySystem>();
            equipment = GetComponent<EquipmentSystem>();
            questSystem = GetComponent<QuestSystem>();
            rewardService = GetComponent<RewardService>();
            relationshipSystem = GetComponent<RelationshipSystem>();

            if (inventory == null) Debug.LogError("[SaveSystem] 未找到 InventorySystem");
            if (equipment == null) Debug.LogError("[SaveSystem] 未找到 EquipmentSystem");
            if (questSystem == null) Debug.LogError("[SaveSystem] 未找到 QuestSystem");
            if (rewardService == null) Debug.LogError("[SaveSystem] 未找到 RewardService");
            if (relationshipSystem == null) Debug.LogError("[SaveSystem] 未找到 RelationshipSystem");
        }

        // ─── 存档数据结构 ───

        [Serializable]
        public class SaveData
        {
            public int version = SAVE_VERSION;
            public int gold;
            public bool hasLocation;
            public string sceneName;
            public Vector3 playerPosition;
            public float playerYaw;
            public int hp;
            public int maxHp;
            public List<InventoryEntry> inventory;
            public List<string> starterItemGrants;
            public EquipmentSystem.EquipmentSaveData equipment;
            public List<QuestEntry> quests;
            public List<RelationshipEntry> relationships;
        }

        [Serializable]
        public class QuestEntry
        {
            public string questId;
            public string state;
        }

        // ─── 保存 ───

        /// <summary>保存游戏状态到磁盘。</summary>
        public bool Save(PlayerController player = null)
        {
            try { return WriteSnapshot(Capture(player)); }
            catch (Exception ex) { return ReportFailure(ex); }
        }

        public SaveData Capture(PlayerController player = null)
        {
            if (player == null) player = FindObjectOfType<PlayerController>();
            var data = new SaveData
            {
                gold = rewardService.GetSaveGold(),
                hasLocation = player != null && IsGameplayScene(player.gameObject.scene.name),
                sceneName = player != null ? player.gameObject.scene.name : null,
                playerPosition = player != null ? player.transform.position : Vector3.zero,
                playerYaw = player != null ? player.transform.eulerAngles.y : 0,
                hp = player != null ? player.CurrentHp : 100,
                maxHp = player != null ? player.MaxHp : 100,
                inventory = inventory.GetSaveData(),
                starterItemGrants = inventory.GetStarterItemGrants(),
                equipment = equipment.GetSaveData(),
                quests = new List<QuestEntry>(),
                relationships = relationshipSystem.GetSaveData()
            };

            // 任务状态 Dictionary → List（JsonUtility 不支持 Dictionary）
            foreach (var pair in questSystem.GetSaveData())
            {
                data.quests.Add(new QuestEntry
                {
                    questId = pair.Key,
                    state = pair.Value.ToString()
                });
            }

            return data;
        }

        private bool WriteSnapshot(SaveData data)
        {
            try
            {
                if (!Validate(data)) throw new InvalidDataException("Invalid save snapshot.");
                Directory.CreateDirectory(SaveDir);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(TmpPath, json);
                if (!Validate(JsonUtility.FromJson<SaveData>(json))) throw new InvalidDataException("Save verification failed.");
                if (File.Exists(SavePath)) File.Replace(TmpPath, SavePath, BakPath);
                else File.Move(TmpPath, SavePath);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(TmpPath)) File.Delete(TmpPath); } catch (IOException) { }
                return ReportFailure(ex);
            }
            Debug.Log("[SaveSystem] 存档已保存: " + SavePath);
            PublishResult(true);
            try { OnSaveComplete?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
            return true;
        }

        private bool ReportFailure(Exception ex)
        {
            Debug.LogError("[SaveSystem] 存档失败: " + ex.Message);
            return PublishResult(false);
        }
        private bool PublishResult(bool success)
        {
            LastSaveSucceeded = success;
            LastSaveMessage = success ? "已自动保存" : "保存失败，进度尚未写入磁盘，请重试。";
            LastSaveTime = Time.unscaledTime;
            try { OnSaveResult?.Invoke(success, LastSaveMessage); } catch (Exception ex) { Debug.LogException(ex); }
            return success;
        }
        public static bool IsGameplayScene(string name) => name == "Town" || name == "Forest";
        public string GetResumeScene(string fallback)
        {
            var data = Load();
            return data != null && data.hasLocation && IsGameplayScene(data.sceneName)
                && Application.CanStreamedLevelBeLoaded(data.sceneName) ? data.sceneName : fallback;
        }
        private bool quitSaved;
        private void OnEnable() { Application.wantsToQuit += BeforeQuit; }
        private void OnDisable() { Application.wantsToQuit -= BeforeQuit; }
        private bool BeforeQuit() { quitSaved = SaveOnLifecycleEvent(); return quitSaved; }
        private void OnApplicationQuit() { if (!quitSaved) SaveOnLifecycleEvent(); }
        private void OnApplicationPause(bool paused) { if (paused) SaveOnLifecycleEvent(); }
        private bool SaveOnLifecycleEvent()
        {
            var session = GetComponent<GameSession>();
            var player = FindObjectOfType<PlayerController>();
            if (session != null && session.IsReadyToSave && player != null && IsGameplayScene(player.gameObject.scene.name))
                return Save(player);
            return true;
        }

        // ─── 读取 ───

        /// <summary>从磁盘加载存档，返回 null 表示无存档。</summary>
        public SaveData Load()
        {
            if (!File.Exists(SavePath))
            {
                if (File.Exists(BakPath))
                {
                    Debug.LogWarning("[SaveSystem] 主存档不存在，尝试从备份恢复");
                    return LoadBackup();
                }

                Debug.Log("[SaveSystem] 无存档文件，将以新游戏启动");
                return null;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<SaveData>(json);

                if (!Validate(data))
                {
                    Debug.LogWarning("[SaveSystem] 存档版本不匹配或损坏，尝试加载备份");
                    return LoadBackup();
                }

                Debug.Log("[SaveSystem] 存档已加载");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 读取存档失败: {e.Message}，尝试加载备份");
                return LoadBackup();
            }
        }

        private SaveData LoadBackup()
        {
            if (!File.Exists(BakPath))
            {
                Debug.LogWarning("[SaveSystem] 无备份文件");
                return null;
            }

            try
            {
                string json = File.ReadAllText(BakPath);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (!Validate(data))
                {
                    Debug.LogError("[SaveSystem] 备份版本不匹配或内容损坏");
                    return null;
                }

                Debug.Log("[SaveSystem] 从备份加载成功");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 备份加载也失败: {e.Message}");
                return null;
            }
        }

        /// <summary>加载存档并恢复所有系统状态。由 GameSession 在启动首次进入游戏场景时调用。</summary>
        public bool LoadAndApply(PlayerController player = null)
        {
            var data = Load();
            if (data == null)
            {
                Debug.Log("[SaveSystem] 新游戏，无需恢复");
                OnLoadComplete?.Invoke();
                return false;
            }

            return Apply(data, player);
        }

        public bool Apply(SaveData data, PlayerController player = null)
        {
            if (!Validate(data)) return false;
            if (player == null) player = FindObjectOfType<PlayerController>();
            // 恢复金币
            rewardService.LoadGold(data.gold);

            // 恢复背包
            inventory.LoadFromData(data.inventory);
            inventory.LoadStarterItemGrants(data.starterItemGrants);

            // 恢复装备
            equipment.LoadFromData(data.equipment);

            // 恢复任务状态
            var questDict = new Dictionary<string, QuestState>();
            if (data.quests != null)
            {
                foreach (var entry in data.quests)
                {
                    if (Enum.TryParse<QuestState>(entry.state, out var state))
                        questDict[entry.questId] = state;
                }
            }
            questSystem.LoadFromData(questDict);

            // 恢复 NPC 好感
            relationshipSystem.LoadFromData(data.relationships);

            // 恢复玩家 HP
            if (player != null)
            {
                player.RestoreHealth(data.hp, data.maxHp);
                if (data.hasLocation && data.sceneName == player.gameObject.scene.name)
                    player.RestorePose(data.playerPosition, data.playerYaw);
                Debug.Log($"[SaveSystem] 玩家 HP 已恢复: {data.hp}/{data.maxHp}");
            }

            Debug.Log("[SaveSystem] 所有系统状态已恢复");
            OnLoadComplete?.Invoke();
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Abs(value) <= 100000f;

        public static bool Validate(SaveData data)
        {
            if (data == null || (data.version != 1 && data.version != SAVE_VERSION) || data.gold < 0
                || data.maxHp <= 0 || data.hp < 0 || data.hp > data.maxHp
                || data.inventory == null || data.quests == null) return false;
            if (data.hasLocation && (!IsGameplayScene(data.sceneName)
                || !Finite(data.playerPosition.x) || !Finite(data.playerPosition.y) || !Finite(data.playerPosition.z)
                || !Finite(data.playerYaw))) return false;
            foreach (var entry in data.quests)
                if (entry == null || string.IsNullOrEmpty(entry.questId)
                    || !Enum.TryParse<QuestState>(entry.state, out var state)
                    || !Enum.IsDefined(typeof(QuestState), state)) return false;
            foreach (var entry in data.inventory)
                if (entry == null || string.IsNullOrEmpty(entry.itemId) || entry.count < 0) return false;
            // Older version-1 saves predate relationships; null restores initial NPC favor.
            if (data.relationships != null)
                foreach (var entry in data.relationships)
                    if (entry == null || string.IsNullOrEmpty(entry.npcId) || entry.favor < 0) return false;
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void WriteDebugSnapshot(string name, SaveData data)
        {
            if (name != "checkpoint" && name != "before_test") throw new ArgumentException("Invalid snapshot name");
            if (!Validate(data)) throw new InvalidDataException("Invalid save snapshot");
            string path = Path.Combine(SaveDir, "fog_harbor_gm_" + name + ".json");
            Directory.CreateDirectory(SaveDir);
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
            else File.Move(path + ".tmp", path);
        }

        public SaveData ReadDebugSnapshot(string name)
        {
            if (name != "checkpoint" && name != "before_test") throw new ArgumentException("Invalid snapshot name");
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path.Combine(SaveDir, "fog_harbor_gm_" + name + ".json")));
            if (!Validate(data)) throw new InvalidDataException("Invalid save snapshot");
            return data;
        }
#endif

        // ─── 存档检查 ───

        public bool HasSave()
        {
            return File.Exists(SavePath);
        }

        public bool HasBackup()
        {
            return File.Exists(BakPath);
        }

        /// <summary>删除存档（调试用）。</summary>
        public void DeleteSave()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            if (File.Exists(BakPath)) File.Delete(BakPath);
            if (File.Exists(TmpPath)) File.Delete(TmpPath);
            Debug.Log("[SaveSystem] 存档已删除");
        }
    }
}

