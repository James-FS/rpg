using System;
using System.Collections.Generic;
using UnityEngine;

namespace FogHarbor.Relationship
{
    /// <summary>
    /// 好感度系统（§6.1 新系统接入模板六步示例）：
    /// 只管 favor 数据 + 规则 + 广播事件；跨系统流程（如记录选择+存档）由 GameSession 编排。
    /// 挂载在 AppRoot 上，通过 DontDestroyOnLoad 跨场景保留。
    /// </summary>
    public class RelationshipSystem : MonoBehaviour
    {
        // npcId → 当前好感；未记录过的 NPC 使用 NpcProfile.initialFavor
        private readonly Dictionary<string, int> favors = new Dictionary<string, int>();
        // npcId → 数据资产（Resources/NPCs 加载，内容扩展只加资产不加代码）
        private readonly Dictionary<string, NpcProfile> profiles = new Dictionary<string, NpcProfile>();

        /// <summary>好感变化时触发（npcId, favor）。</summary>
        public event Action<string, int> OnFavorChanged;

        // ─── 初始化 ───

        private void Awake()
        {
            LoadNpcProfiles();
        }

        private void LoadNpcProfiles()
        {
            profiles.Clear();
            var assets = Resources.LoadAll<NpcProfile>("NPCs");
            if (assets.Length == 0) throw new InvalidOperationException("Resources/NPCs contains no NpcProfile assets.");
            foreach (var p in assets)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.NpcId) || string.IsNullOrWhiteSpace(p.DisplayName))
                    throw new InvalidOperationException("Every NPC profile requires npcId and displayName.");
                if (p.InitialFavor < 0) throw new InvalidOperationException($"NPC '{p.NpcId}' has a negative initial favor.");
                if (profiles.ContainsKey(p.NpcId)) throw new InvalidOperationException($"Duplicate NPC profile '{p.NpcId}'.");
                var choiceIds = new HashSet<string>();
                foreach (var choice in p.Choices)
                {
                    if (choice == null || string.IsNullOrWhiteSpace(choice.choiceId))
                        throw new InvalidOperationException($"NPC '{p.NpcId}' has an invalid choice entry.");
                    if (!choiceIds.Add(choice.choiceId))
                        throw new InvalidOperationException($"NPC '{p.NpcId}' has duplicate choiceId '{choice.choiceId}'.");
                }
                profiles.Add(p.NpcId, p);
            }
            Debug.Log($"[RelationshipSystem] 已加载 {profiles.Count} 个 NPC 好感配置");
        }

        // ─── 查询 ───

        /// <summary>当前好感；无记录时返回初始好感。</summary>
        public int GetFavor(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId)) throw new ArgumentException("npcId is required.", nameof(npcId));
            if (favors.TryGetValue(npcId, out int v)) return v;
            if (profiles.TryGetValue(npcId, out var p)) return p.InitialFavor;
            throw new KeyNotFoundException($"NPC profile '{npcId}' is not registered.");
        }

        public NpcProfile GetProfile(string npcId)
        {
            return string.IsNullOrEmpty(npcId) ? null : profiles.TryGetValue(npcId, out var p) ? p : null;
        }

        public IReadOnlyCollection<string> GetNpcIds() => profiles.Keys;

        // ─── 规则 ───

        /// <summary>记录玩家选择（系统层规则）：只在白名单内生效，返回是否记录成功与变化量。</summary>
        public bool TryRecordPlayerChoice(string npcId, string choiceId, out int favorDelta)
        {
            favorDelta = 0;
            var profile = GetProfile(npcId);
            if (profile == null) return false;
            if (!profile.TryGetChoice(choiceId, out favorDelta)) return false;

            AddFavor(npcId, favorDelta);
            return true;
        }

        /// <summary>直接增减好感（供未来交易/任务等系统调用；AI 必须走白名单工具）。</summary>
        public void AddFavor(string npcId, int delta)
        {
            if (delta == 0) return;
            if (!profiles.ContainsKey(npcId)) throw new KeyNotFoundException($"NPC profile '{npcId}' is not registered.");
            int current = GetFavor(npcId);
            favors[npcId] = Mathf.Max(0, checked(current + delta)); // 好感不落负数
            Debug.Log($"[Relationship] {npcId} 好感 {(delta > 0 ? "+" : "")}{delta} → {favors[npcId]}");
            OnFavorChanged?.Invoke(npcId, favors[npcId]);
        }

        // ─── 存档支持 ───

        public List<RelationshipEntry> GetSaveData()
        {
            var list = new List<RelationshipEntry>();
            foreach (var p in profiles.Values)
                list.Add(new RelationshipEntry { npcId = p.NpcId, favor = GetFavor(p.NpcId) });
            return list;
        }

        public void LoadFromData(List<RelationshipEntry> data)
        {
            favors.Clear();
            if (data != null)
            {
                foreach (var entry in data)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.npcId))
                        throw new InvalidOperationException("Save data contains an invalid NPC relationship entry.");
                    if (!profiles.ContainsKey(entry.npcId))
                        throw new KeyNotFoundException($"Save data references unknown NPC '{entry.npcId}'.");
                    favors[entry.npcId] = entry.favor;
                }
            }
            Debug.Log($"[Relationship] 从存档恢复 {favors.Count} 个 NPC 好感");
            foreach (var profile in profiles.Values) OnFavorChanged?.Invoke(profile.NpcId, GetFavor(profile.NpcId));
        }
    }

    /// <summary>好感度存档条目（跨系统共享类型）。</summary>
    [Serializable]
    public class RelationshipEntry
    {
        public string npcId;
        public int favor;
    }
}
