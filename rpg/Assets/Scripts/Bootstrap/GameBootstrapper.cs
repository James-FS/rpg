using UnityEngine;
using FogHarbor.Inventory;
using FogHarbor.Equipment;
using FogHarbor.Quest;
using FogHarbor.Relationship;
using FogHarbor.Save;
using FogHarbor.Session;
using FogHarbor.UI;

namespace FogHarbor.Bootstrap
{
    /// <summary>
    /// 全局自动引导器：任何场景启动后自动创建 AppRoot 及其所有系统。
    /// 这样从 Town/Forest 场景直接进入 Play 也能工作，不必依赖 Boot 场景。
    /// Boot.scene 中已有的 AppRoot 会被检测到并跳过。
    /// </summary>
    public static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            // 当前场景已有 AppRoot（如从 Boot.scene 启动），直接使用
            if (GameObject.Find("AppRoot") != null)
                return;

            CreateAppRoot();
        }

        private static void CreateAppRoot()
        {
            var appRoot = new GameObject("AppRoot");
            Object.DontDestroyOnLoad(appRoot);

            // 按依赖顺序挂载组件（AddComponent 立即触发 Awake）
            appRoot.AddComponent<GameBootstrap>();
            appRoot.AddComponent<InventorySystem>();
            appRoot.AddComponent<EquipmentSystem>();
            appRoot.AddComponent<QuestSystem>();
            appRoot.AddComponent<RewardService>();
            appRoot.AddComponent<RelationshipSystem>();
            appRoot.AddComponent<SaveSystem>();
            appRoot.AddComponent<GameSession>();
            appRoot.AddComponent<UIManager>();

            Debug.Log("[GameBootstrapper] 自动创建 AppRoot 及所有系统（从任意场景启动）");
        }
    }
}