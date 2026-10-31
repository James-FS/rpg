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
using FogHarbor.Items;

namespace FogHarbor.Save
{
    public enum SaveLoadStatus
    {
        NoSave,
        Loaded,
        RecoveredFromBackup,
        Corrupt
    }

    public readonly struct SaveLoadResult
    {
        public readonly SaveLoadStatus Status;
        public readonly SaveSystem.SaveData Data;
        public readonly string Error;

        public SaveLoadResult(SaveLoadStatus status, SaveSystem.SaveData data = null, string error = null)
        {
            Status = status;
            Data = data;
            Error = error;
        }
    }

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
            inventory = GetComponent<InventorySystem>()
                ?? throw new MissingComponentException("SaveSystem requires InventorySystem on AppRoot.");
            equipment = GetComponent<EquipmentSystem>()
                ?? throw new MissingComponentException("SaveSystem requires EquipmentSystem on AppRoot.");
            questSystem = GetComponent<QuestSystem>()
                ?? throw new MissingComponentException("SaveSystem requires QuestSystem on AppRoot.");
            rewardService = GetComponent<RewardService>()
                ?? throw new MissingComponentException("SaveSystem requires RewardService on AppRoot.");
            relationshipSystem = GetComponent<RelationshipSystem>()
                ?? throw new MissingComponentException("SaveSystem requires RelationshipSystem on AppRoot.");
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
            return WriteSnapshot(Capture(player));
        }

        public SaveData Capture(PlayerController player = null)
        {
            if (player == null)
                player = (GameSession.Instance
                    ?? throw new MissingComponentException("SaveSystem requires a composed GameSession.")).Player;
            if (player == null)
                throw new InvalidOperationException("Cannot capture a gameplay save without a bound PlayerController.");

            var data = new SaveData
            {
                gold = rewardService.GetSaveGold(),
                hasLocation = IsGameplayScene(player.gameObject.scene.name),
                sceneName = player.gameObject.scene.name,
                playerPosition = player.transform.position,
                playerYaw = player.transform.eulerAngles.y,
                hp = player.CurrentHp,
                maxHp = player.MaxHp,
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
            if (!Validate(data)) throw new InvalidDataException("The captured save snapshot violates the save schema.");
            ValidateContentReferences(data);
            string json = JsonUtility.ToJson(data, true);
            if (!Validate(JsonUtility.FromJson<SaveData>(json)))
                throw new InvalidDataException("The serialized save snapshot failed validation.");

            try
            {
                Directory.CreateDirectory(SaveDir);
                File.WriteAllText(TmpPath, json);
                if (File.Exists(SavePath)) File.Replace(TmpPath, SavePath, BakPath);
                else File.Move(TmpPath, SavePath);
            }
            catch (IOException ex)
            {
                DeleteTemporaryAfterFailure();
                return ReportFailure(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                DeleteTemporaryAfterFailure();
                return ReportFailure(ex);
            }
            catch (PlatformNotSupportedException ex)
            {
                DeleteTemporaryAfterFailure();
                return ReportFailure(ex);
            }
            Debug.Log("[SaveSystem] 存档已保存: " + SavePath);
            PublishResult(true);
            try { OnSaveComplete?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
            return true;
        }

        private void DeleteTemporaryAfterFailure()
        {
            if (!File.Exists(TmpPath)) return;
            try { File.Delete(TmpPath); }
            catch (IOException cleanupError) { Debug.LogException(cleanupError); }
            catch (UnauthorizedAccessException cleanupError) { Debug.LogException(cleanupError); }
        }

        private bool ReportFailure(Exception ex)
        {
            Debug.LogException(ex);
            LastSaveSucceeded = false;
            LastSaveMessage = "存档失败：" + ex.Message;
            LastSaveTime = Time.unscaledTime;
            try { OnSaveResult?.Invoke(false, LastSaveMessage); } catch (Exception eventError) { Debug.LogException(eventError); }
            return false;
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
            var result = Load();
            if (result.Status == SaveLoadStatus.Corrupt)
                throw new InvalidDataException("存档与备份均无法读取：" + result.Error);
            if (result.Status == SaveLoadStatus.NoSave || !result.Data.hasLocation)
                return fallback;
            if (!Application.CanStreamedLevelBeLoaded(result.Data.sceneName))
                throw new InvalidDataException($"存档场景 '{result.Data.sceneName}' 未加入 Build Settings。");
            return result.Data.sceneName;
        }
        private bool quitSaved;
        private void OnEnable() { Application.wantsToQuit += BeforeQuit; }
        private void OnDisable() { Application.wantsToQuit -= BeforeQuit; }
        private bool BeforeQuit() { quitSaved = SaveOnLifecycleEvent(); return quitSaved; }
        private void OnApplicationQuit() { if (!quitSaved) SaveOnLifecycleEvent(); }
        private void OnApplicationPause(bool paused) { if (paused) SaveOnLifecycleEvent(); }
        private bool SaveOnLifecycleEvent()
        {
            var session = GameSession.Instance;
            var player = session != null ? session.Player : null;
            if (session != null && session.IsReadyToSave && player != null && IsGameplayScene(player.gameObject.scene.name))
                return Save(player);
            return true;
        }

        // ─── 读取 ───

        /// <summary>Reads the primary save, then the backup. Corruption is distinct from a new game.</summary>
        public SaveLoadResult Load()
        {
            bool hasMain = File.Exists(SavePath);
            bool hasBackup = File.Exists(BakPath);
            if (!hasMain && !hasBackup)
            {
                Debug.Log("[SaveSystem] 无存档文件，将以新游戏启动");
                return new SaveLoadResult(SaveLoadStatus.NoSave);
            }

            string mainError = null;
            if (hasMain)
            {
                if (TryReadValidatedFile(SavePath, out var data, out mainError))
                {
                    Debug.Log("[SaveSystem] 存档已加载");
                    return new SaveLoadResult(SaveLoadStatus.Loaded, data);
                }
                Debug.LogWarning($"[SaveSystem] 主存档不可用，尝试备份恢复：{mainError}");
            }

            if (hasBackup)
            {
                if (TryReadValidatedFile(BakPath, out var backup, out var backupError))
                {
                    Debug.LogWarning("[SaveSystem] 从备份恢复成功");
                    return new SaveLoadResult(SaveLoadStatus.RecoveredFromBackup, backup, mainError);
                }
                string error = $"主存档：{mainError ?? "不存在"}；备份：{backupError}";
                Debug.LogError("[SaveSystem] 主存档与备份均不可用：" + error);
                return new SaveLoadResult(SaveLoadStatus.Corrupt, error: error);
            }

            string mainOnlyError = mainError ?? "主存档不可读";
            Debug.LogError("[SaveSystem] 主存档不可用且没有备份：" + mainOnlyError);
            return new SaveLoadResult(SaveLoadStatus.Corrupt, error: mainOnlyError);
        }

        private bool TryReadValidatedFile(string path, out SaveData data, out string error)
        {
            data = null;
            error = null;
            try
            {
                data = Migrate(JsonUtility.FromJson<SaveData>(File.ReadAllText(path)));
                if (!Validate(data))
                {
                    error = "内容或版本不符合存档格式";
                    data = null;
                    return false;
                }
                ValidateContentReferences(data);
                return true;
            }
            catch (IOException ex) { error = ex.Message; }
            catch (UnauthorizedAccessException ex) { error = ex.Message; }
            catch (ArgumentException ex) { error = ex.Message; }
            catch (InvalidDataException ex) { error = ex.Message; }
            data = null;
            return false;
        }

        /// <summary>加载存档并恢复所有系统状态。由 GameSession 在启动首次进入游戏场景时调用。</summary>
        public SaveLoadResult LoadAndApply(PlayerController player = null)
        {
            var result = Load();
            if (result.Status == SaveLoadStatus.NoSave)
            {
                Debug.Log("[SaveSystem] 新游戏，无需恢复");
                OnLoadComplete?.Invoke();
                return result;
            }

            if (result.Status == SaveLoadStatus.Corrupt)
                throw new InvalidDataException("存档与备份均无法读取：" + result.Error);

            Apply(result.Data, player);
            return result;
        }

        public void Apply(SaveData data, PlayerController player = null)
        {
            data = Migrate(data);
            if (!Validate(data)) throw new InvalidDataException("Cannot apply an invalid save snapshot.");
            ValidateContentReferences(data);
            if (player == null)
                player = (GameSession.Instance
                    ?? throw new MissingComponentException("SaveSystem requires a composed GameSession.")).Player;
            if (player == null) throw new InvalidOperationException("Cannot apply a gameplay save without a bound PlayerController.");
            // 恢复金币
            rewardService.LoadGold(data.gold);

            // 恢复背包
            inventory.LoadFromData(data.inventory);
            inventory.LoadStarterItemGrants(data.starterItemGrants);

            // 恢复装备
            equipment.LoadFromData(data.equipment);

            // 恢复任务状态
            var questDict = new Dictionary<string, QuestState>();
            foreach (var entry in data.quests)
            {
                questDict[entry.questId] = (QuestState)Enum.Parse(typeof(QuestState), entry.state);
            }
            questSystem.LoadFromData(questDict);

            // 恢复 NPC 好感
            relationshipSystem.LoadFromData(data.relationships);

            // 恢复玩家 HP
            player.RestoreHealth(data.hp, data.maxHp);
            if (data.hasLocation && data.sceneName == player.gameObject.scene.name)
                player.RestorePose(data.playerPosition, data.playerYaw);
            Debug.Log($"[SaveSystem] 玩家 HP 已恢复: {data.hp}/{data.maxHp}");

            Debug.Log("[SaveSystem] 所有系统状态已恢复");
            OnLoadComplete?.Invoke();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Abs(value) <= 100000f;

        private static SaveData Migrate(SaveData data)
        {
            if (data == null) return null;
            if (data.version == 1)
            {
                data.equipment ??= new EquipmentSystem.EquipmentSaveData();
                data.starterItemGrants ??= new List<string>();
                data.version = SAVE_VERSION;
            }
            return data;
        }

        private void ValidateContentReferences(SaveData data)
        {
            foreach (var entry in data.inventory)
                if (!ItemDatabase.TryGetById(entry.itemId, out _))
                    throw new InvalidDataException($"Save inventory references unknown item '{entry.itemId}'.");

            if (data.starterItemGrants != null)
                foreach (string itemId in data.starterItemGrants)
                    if (!ItemDatabase.TryGetById(itemId, out _))
                        throw new InvalidDataException($"Save starter grants reference unknown item '{itemId}'.");

            if (!string.IsNullOrEmpty(data.equipment.weapon))
            {
                if (!ItemDatabase.TryGetById(data.equipment.weapon, out var weapon))
                    throw new InvalidDataException($"Save weapon slot references unknown item '{data.equipment.weapon}'.");
                if (weapon.Type != ItemType.Weapon)
                    throw new InvalidDataException($"Save weapon slot references non-weapon item '{data.equipment.weapon}'.");
            }
            if (!string.IsNullOrEmpty(data.equipment.armor))
            {
                if (!ItemDatabase.TryGetById(data.equipment.armor, out var armor))
                    throw new InvalidDataException($"Save armor slot references unknown item '{data.equipment.armor}'.");
                if (armor.Type != ItemType.Armor)
                    throw new InvalidDataException($"Save armor slot references non-armor item '{data.equipment.armor}'.");
            }

            foreach (var entry in data.quests)
                if (questSystem.GetQuest(entry.questId) == null)
                    throw new InvalidDataException($"Save data references unknown quest '{entry.questId}'.");

            if (data.relationships != null)
                foreach (var entry in data.relationships)
                    if (relationshipSystem.GetProfile(entry.npcId) == null)
                        throw new InvalidDataException($"Save data references unknown NPC '{entry.npcId}'.");
        }

        public static bool Validate(SaveData data)
        {
            if (data == null || data.version != SAVE_VERSION || data.gold < 0
                || data.maxHp <= 0 || data.hp < 0 || data.hp > data.maxHp
                || data.inventory == null || data.quests == null || data.equipment == null) return false;
            if (data.hasLocation && (!IsGameplayScene(data.sceneName)
                || !Finite(data.playerPosition.x) || !Finite(data.playerPosition.y) || !Finite(data.playerPosition.z)
                || !Finite(data.playerYaw))) return false;
            var questIds = new HashSet<string>();
            foreach (var entry in data.quests)
                if (entry == null || string.IsNullOrEmpty(entry.questId)
                    || !Enum.TryParse<QuestState>(entry.state, out var state)
                    || !Enum.IsDefined(typeof(QuestState), state) || !questIds.Add(entry.questId)) return false;
            var inventoryIds = new HashSet<string>();
            foreach (var entry in data.inventory)
                if (entry == null || string.IsNullOrEmpty(entry.itemId) || entry.count <= 0
                    || !inventoryIds.Add(entry.itemId)) return false;
            if (data.starterItemGrants != null)
            {
                var grantIds = new HashSet<string>();
                foreach (string itemId in data.starterItemGrants)
                    if (string.IsNullOrEmpty(itemId) || !grantIds.Add(itemId)) return false;
            }
            // Older version-1 saves predate relationships; null restores initial NPC favor.
            if (data.relationships != null)
            {
                var npcIds = new HashSet<string>();
                foreach (var entry in data.relationships)
                    if (entry == null || string.IsNullOrEmpty(entry.npcId) || entry.favor < 0 || !npcIds.Add(entry.npcId)) return false;
            }
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

