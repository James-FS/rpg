using UnityEngine;

namespace FogHarbor.Quest
{
    /// <summary>
    /// 任务定义：用 ScriptableObject 在 Resources/Quests/ 下创建资产。
    /// 定义任务的固定属性，运行时状态由 QuestSystem 管理。
    /// </summary>
    [CreateAssetMenu(fileName = "NewQuest", menuName = "FogHarbor/Quest")]
    public class QuestData : ScriptableObject
    {
        [Header("基础")]
        [SerializeField] private string questId;
        [SerializeField] private string title;
        [TextArea][SerializeField] private string description;
        [SerializeField] private bool isMainQuest;

        [Header("目标")]
        [SerializeField] private string targetItemId;
        [SerializeField] private int targetCount = 1;

        [Header("奖励")]
        [SerializeField] private int rewardGold;
        [SerializeField] private string rewardItemId;
        [SerializeField] private int rewardItemCount;

        public string QuestId => questId;
        public string Title => title;
        public string Description => description;
        public bool IsMainQuest => isMainQuest;
        public string TargetItemId => targetItemId;
        public int TargetCount => targetCount;
        public int RewardGold => rewardGold;
        public string RewardItemId => rewardItemId;
        public int RewardItemCount => rewardItemCount;
    }
}
