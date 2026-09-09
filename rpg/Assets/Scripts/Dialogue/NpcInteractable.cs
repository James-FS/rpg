using UnityEngine;
using AIBot.Core.Context;
using AIBot.Unity;
using FogHarbor.Player;
using FogHarbor.UI;

namespace FogHarbor.Dialogue
{
    /// <summary>
    /// NPC 交互入口：实现 IInteractable，玩家按 E 时打开对话面板并绑定 NpcAgent。
    /// 同时把任务工具（QuestToolHost）注册进 agent，并挂接游戏状态快照（GameContextProvider）。
    /// 挂载到 NPC 根对象上，与 NpcAgent 同物体。
    /// </summary>
    public class NpcInteractable : MonoBehaviour, IInteractable
    {
        [Header("配置")]
        [SerializeField] private string promptText = "按 E 对话";
        [SerializeField] private string displayName = "NPC";

        private NpcAgent agent;
        private QuestToolHost toolHost;
        private bool toolsRegistered;

        private void Awake()
        {
            agent = GetComponent<NpcAgent>();
            if (agent == null)
                agent = GetComponentInChildren<NpcAgent>();

            toolHost = GetComponent<QuestToolHost>();
            if (toolHost == null)
                toolHost = gameObject.AddComponent<QuestToolHost>();
        }

        public string GetPrompt() => promptText;

        public void Interact()
        {
            if (agent == null)
            {
                Debug.LogWarning($"[NpcInteractable] {gameObject.name} 上未找到 NpcAgent，无法对话");
                return;
            }

            // 首次对话前：注册任务工具 + 绑定游戏快照（Chat 之前必须完成）
            if (!toolsRegistered)
            {
                EnsureGameContextProvider();
                toolHost.RegisterTools(agent);
                toolsRegistered = true;
            }

            // 通过 UIManager 打开对话面板并绑定 agent
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowDialogue(displayName, agent);
            }
            else
            {
                Debug.LogWarning("[NpcInteractable] UIManager 不存在，无法打开对话");
            }
        }

        private void EnsureGameContextProvider()
        {
            if (agent.gameContextProvider is IGameContext) return;
            var provider = GetComponent<GameContextProvider>();
            if (provider == null)
                provider = gameObject.AddComponent<GameContextProvider>();
            agent.gameContextProvider = provider;
            Debug.Log("[NpcInteractable] 已绑定 GameContextProvider（游戏状态快照）");
        }
    }
}