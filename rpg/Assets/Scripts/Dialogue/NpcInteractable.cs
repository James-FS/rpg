using System;
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

            if (agent == null)
                throw new MissingComponentException($"NPC '{name}' requires a NpcAgent.");

            toolHost = GetComponent<QuestToolHost>();
            if (toolHost == null)
                toolHost = gameObject.AddComponent<QuestToolHost>();
        }

        public string GetPrompt() => promptText;

        public void Interact()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var gm = FindObjectOfType<FogHarbor.Debugging.GMCommandService>();
            if (gm != null && !gm.EnsureNpcTestIdentity(agent)) return;
#endif
            if (!agent.isActiveAndEnabled)
                throw new InvalidOperationException($"NPC '{name}' has a disabled NpcAgent.");

            // 首次对话前：注册任务工具 + 绑定游戏快照（Chat 之前必须完成）
            if (!toolsRegistered)
            {
                EnsureGameContextProvider();
                toolHost.RegisterTools(agent);
                toolsRegistered = true;
            }

            // 通过 UIManager 打开对话面板并绑定 agent
            var ui = UIManager.Instance
                ?? throw new MissingComponentException("NPC interaction requires UIManager.");
            ui.ShowDialogue(displayName, agent);
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
