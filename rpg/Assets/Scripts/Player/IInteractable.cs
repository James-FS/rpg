namespace FogHarbor.Player
{
    /// <summary>
    /// 可交互对象接口：NPC、物品拾取、场景入口等实现此接口。
    /// PlayerInteractor 通过此接口统一处理所有交互逻辑。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>返回交互提示文本，如 "按 E 对话"。</summary>
        string GetPrompt();

        /// <summary>执行交互逻辑。</summary>
        void Interact();
    }
}
