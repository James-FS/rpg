using System;
using System.Collections.Generic;
using UnityEngine;

namespace FogHarbor.Relationship
{
    /// <summary>
    /// NPC 好感数据资产（§6.1 内容扩展协议 / 差距 #8）：
    /// 替换散在代码里的写死配置（linFavor=5、NPC 名称）。
    /// record_player_choice 工具只允许写入 Choices 白名单里的选择。
    /// 挂到 Resources/NPCs/ 下，RelationshipSystem 启动时加载。
    /// </summary>
    [CreateAssetMenu(fileName = "NewNpcProfile", menuName = "FogHarbor/Npc Profile")]
    public class NpcProfile : ScriptableObject
    {
        [Header("身份")]
        [SerializeField] private string npcId;
        [SerializeField] private string displayName;

        [Header("好感")]
        [SerializeField] private int initialFavor;

        [Header("玩家选择白名单")]
        [Tooltip("record_player_choice 只允许记录这里的 choiceId，并应用对应好感变化")]
        [SerializeField] private List<ChoiceEntry> choices = new List<ChoiceEntry>();

        public string NpcId => npcId;
        public string DisplayName => displayName;
        public int InitialFavor => initialFavor;

        /// <summary>白名单查询：choiceId 是否允许记录；允许则输出好感变化量。</summary>
        public bool TryGetChoice(string choiceId, out int favorDelta)
        {
            favorDelta = 0;
            if (string.IsNullOrEmpty(choiceId) || choices == null) return false;
            foreach (var c in choices)
            {
                if (c != null && string.Equals(c.choiceId, choiceId, StringComparison.Ordinal))
                {
                    favorDelta = c.favorDelta;
                    return true;
                }
            }
            return false;
        }

        public IReadOnlyList<ChoiceEntry> Choices => choices;
    }

    /// <summary>白名单选项：choiceId → 好感变化量，replyHint 供工具回复参考。</summary>
    [Serializable]
    public class ChoiceEntry
    {
        [Header("选项 ID（模型调用 record_player_choice 时填写）")]
        public string choiceId;
        public int favorDelta;
        [TextArea]
        public string replyHint;
    }
}