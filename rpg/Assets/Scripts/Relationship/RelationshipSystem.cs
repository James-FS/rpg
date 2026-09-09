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
            foreach (var p in assets)
            {
                if (p != null && !string.IsNullOrEmpty(p.NpcId))
                    profiles[p.NpcId] = p;
            }
            Debug.Log($"[RelationshipSystem] 已加载 {profiles.Count} 个 NPC 好感配置");
        }

        // ─── 查询 ───

        /// <summary>当前好感；无记录时返回初始好感。</summary>
        public int GetFavor(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return 0;
            if (favors.TryGetValue(npcId, out int v)) return v;
            return profiles.TryGetValue(npcId, out var p) ? p.InitialFavor : 0;
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
            if (string.IsNullOrEmpty(npcId) || delta == 0) return;
            int current = GetFavor(npcId);
            favors[npcId] = Mathf.Max(0, current + delta); // 好感不落负数
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
                    if (entry != null && !string.IsNullOrEmpty(entry.npcId))
                        favors[entry.npcId] = entry.favor;
                }
            }
            Debug.Log($"[Relationship] 从存档恢复 {favors.Count} 个 NPC 好感");
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