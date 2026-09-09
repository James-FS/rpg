namespace FogHarbor.Quest
{
    /// <summary>
    /// 任务状态机：Available → Accepted → Completed → Rewarded
    /// </summary>
    public enum QuestState
    {
        Available,
        Accepted,
        Completed,
        Rewarded
    }
}
