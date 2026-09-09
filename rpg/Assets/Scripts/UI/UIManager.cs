using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using FogHarbor.Inventory;
using FogHarbor.Equipment;
using FogHarbor.Quest;
using FogHarbor.Player;
using FogHarbor.Session;

namespace FogHarbor.UI
{
    /// <summary>
    /// UI 总管理器（§6.1：意图入口）：创建 Canvas，管理所有面板，处理快捷键。
    /// 面板的"装备/使用/丢弃"按钮只发意图，由本类转发到 GameSession 编排。
    /// 挂载在 AppRoot 上（与其它系统同级），运行时自动创建所有 UI。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        private InventorySystem inventory;
        private EquipmentSystem equipment;
        private QuestSystem questSystem;
        private RewardService rewardService;
        private GameSession gameSession;
        private PlayerController player;

        private HUDPanel hudPanel;
        private InventoryPanel inventoryPanel;
        private QuestPanel questPanel;
        private DialoguePanel dialoguePanel;

        private string currentPrompt = "";

        private void Awake()
        {
            Instance = this;

            inventory = GetComponent<InventorySystem>();
            equipment = GetComponent<EquipmentSystem>();
            questSystem = GetComponent<QuestSystem>();
            rewardService = GetComponent<RewardService>();
            gameSession = GetComponent<GameSession>();

            CreateCanvas();
        }

        private void Start()
        {
            // 查找玩家（场景中的 PlayerController）
            FindPlayer();

            // 创建面板
            var canvas = GetComponentInChildren<Canvas>().transform;

            hudPanel = CreatePanel<HUDPanel>("HUD", canvas);
            inventoryPanel = CreatePanel<InventoryPanel>("InventoryPanel", canvas);
            questPanel = CreatePanel<QuestPanel>("QuestPanel", canvas);
            dialoguePanel = CreatePanel<DialoguePanel>("DialoguePanel", canvas);

            // 初始化（HUD 走事件订阅：HP/金币/任务状态变化时自动刷新）
            hudPanel.Initialize(player, rewardService, questSystem);
            inventoryPanel.Initialize(inventory, equipment);
            questPanel.Initialize(questSystem);

            inventoryPanel.Hide();
            questPanel.Hide();
            dialoguePanel.Hide();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.B)) inventoryPanel.Toggle();
            if (Input.GetKeyDown(KeyCode.Q)) questPanel.Toggle();
            if (Input.GetKeyDown(KeyCode.Escape)) CloseAllPanels();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FindPlayer();
            hudPanel?.SetPlayer(player);
        }

        // ─── 公开方法 ───

        public void SetPrompt(string text)
        {
            if (text == currentPrompt) return; // 去重：无变化时不做 UI 更新
            currentPrompt = text ?? "";
            hudPanel?.SetPrompt(currentPrompt);
        }

        /// <summary>右上/toast 提示（拾取、系统消息）。</summary>
        public void ShowToast(string message)
        {
            if (hudPanel != null)
                hudPanel.ShowToast(message);
        }

        public void ShowDialogue(string npcName, AIBot.Unity.NpcAgent agent)
        {
            CloseAllPanels();
            dialoguePanel.Show(npcName, agent);
        }

        /// <summary>仅显示对话面板骨架（无 agent 绑定，调试用）。</summary>
        public void ShowDialogueSkeleton(string npcName)
        {
            CloseAllPanels();
            dialoguePanel.ShowSkeleton(npcName);
        }

        // ─── UI 意图转发（§6.1 差距 #1：面板不直调系统，由意图入口转发到 GameSession）───

        public void RequestEquip(string itemId)
        {
            gameSession?.EquipItem(itemId);
        }

        public void RequestUseItem(string itemId)
        {
            gameSession?.UseItem(itemId);
        }

        public void RequestDrop(string itemId)
        {
            gameSession?.DropItem(itemId);
        }

        // ─── 内部 ───

        private void FindPlayer()
        {
            player = FindObjectOfType<PlayerController>();
        }

        private void CloseAllPanels()
        {
            inventoryPanel.Hide();
            questPanel.Hide();
            dialoguePanel.Hide();
        }

        private void CreateCanvas()
        {
            // Canvas
            var canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // EventSystem（挂在 AppRoot 下，随 DontDestroyOnLoad 跨场景保留）
            if (GetComponentInChildren<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.transform.SetParent(transform, false);
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }
        }

        private T CreatePanel<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            UIHelper.Stretch(rt);
            return go.AddComponent<T>();
        }
    }
}
