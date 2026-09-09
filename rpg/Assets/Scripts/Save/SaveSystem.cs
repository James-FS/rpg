using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
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
        private const string SAVE_FILE = "fog_harbor_save.json";
        private const string TMP_FILE  = "fog_harbor_save.tmp";
        private const string BAK_FILE  = "fog_harbor_save.bak";
        private const int SAVE_VERSION = 1;

        private static string SaveDir => Application.persistentDataPath;
        private static string SavePath => Path.Combine(SaveDir, SAVE_FILE);
        private static string TmpPath  => Path.Combine(SaveDir, TMP_FILE);
        private static string BakPath  => Path.Combine(SaveDir, BAK_FILE);

        // 外部引用
        private InventorySystem inventory;
        private EquipmentSystem equipment;
        private QuestSystem questSystem;
        private RewardService rewardService;
        private RelationshipSystem relationshipSystem;

        /// <summary>存档/读档完成时触发。</summary>
        public event Action OnSaveComplete;
        public event Action OnLoadComplete;

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
            public int hp;
            public int maxHp;
            public List<InventoryEntry> inventory;
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
            var data = new SaveData
            {
                gold = rewardService.GetSaveGold(),
                hp = player != null ? player.CurrentHp : 100,
                maxHp = player != null ? player.MaxHp : 100,
                inventory = inventory.GetSaveData(),
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

            try
            {
                string json = JsonUtility.ToJson(data, true);

                // 1. 写入临时文件
                File.WriteAllText(TmpPath, json);

                // 2. 校验 JSON 合法性（反序列化测试）
                JsonUtility.FromJson<SaveData>(json);

                // 3. 如果旧存档存在，备份为 .bak
                if (File.Exists(SavePath))
                {
                    if (File.Exists(BakPath))
                        File.Delete(BakPath);
                    File.Move(SavePath, BakPath);
                }

                // 4. 临时文件替换为正式存档
                File.Move(TmpPath, SavePath);

                Debug.Log($"[SaveSystem] 存档已保存: {SavePath}");
                OnSaveComplete?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 存档失败: {e.Message}");

                // 清理临时文件
                if (File.Exists(TmpPath))
                    File.Delete(TmpPath);

                return false;
            }
        }

        // ─── 读取 ───

        /// <summary>从磁盘加载存档，返回 null 表示无存档。</summary>
        public SaveData Load()
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("[SaveSystem] 无存档文件，将以新游戏启动");
                return null;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<SaveData>(json);

                if (data == null || data.version != SAVE_VERSION)
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

            // 恢复金币
            rewardService.LoadGold(data.gold);

            // 恢复背包
            inventory.LoadFromData(data.inventory);

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
                player.SetHp(data.hp);
                Debug.Log($"[SaveSystem] 玩家 HP 已恢复: {data.hp}/{data.maxHp}");
            }

            Debug.Log("[SaveSystem] 所有系统状态已恢复");
            OnLoadComplete?.Invoke();
            return true;
        }

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
