using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using FogHarbor.Inventory;
using FogHarbor.Equipment;
using FogHarbor.Quest;
using FogHarbor.Player;
using FogHarbor.Save;
using FogHarbor.Session;

namespace FogHarbor.UI
{
    /// <summary>
    /// UI 总管理器（§6.1：意图入口）：管理 Canvas 与面板，处理快捷键。
    /// 面板的"装备/使用/丢弃"按钮只发意图，由本类转发到 GameSession 编排。
    /// 挂载在 AppRoot 上（与其它系统同级）。
    /// UI 来源三级：① 场景里已有的 UI 实例（Boot 场景内嵌 GameUI 实例，编辑期即可看见/编辑）
    /// → ② Assets/Resources/UI/GameUI.prefab（由菜单「Tools/雾港小镇/烘焙 UI 预制体」生成）
    /// → ③ 面板脚本里的代码构建（兜底，删掉 Prefab 也能跑）。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        /// <summary>UI 预制体的 Resources 路径（不含扩展名）。</summary>
        public const string GameUIPrefabPath = "UI/GameUI";

        public static UIManager Instance { get; private set; }

        private InventorySystem inventory;
        private EquipmentSystem equipment;
        private QuestSystem questSystem;
        private RewardService rewardService;
        private GameSession gameSession;
        private SaveSystem saveSystem;
        private PlayerController player;

        private HUDPanel hudPanel;
        private QuestTrackerPanel questTrackerPanel;
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
            saveSystem = GetComponent<SaveSystem>();

            CreateEventSystem();
        }

        private void Start()
        {
            // 查找玩家（场景中的 PlayerController）
            FindPlayer();

            // 面板挂载点：场景实例 → Prefab → 代码创建
            var canvas = ResolveCanvas();

            hudPanel = GetOrCreatePanel<HUDPanel>("HUD", canvas);
            questTrackerPanel = GetOrCreatePanel<QuestTrackerPanel>("QuestTrackerPanel", canvas);
            inventoryPanel = GetOrCreatePanel<InventoryPanel>("InventoryPanel", canvas);
            questPanel = GetOrCreatePanel<QuestPanel>("QuestPanel", canvas);
            dialoguePanel = GetOrCreatePanel<DialoguePanel>("DialoguePanel", canvas);

            // 初始化（HUD 走事件订阅：HP/金币/任务状态变化时自动刷新）
            hudPanel.Initialize(player, questSystem);
            questTrackerPanel.Initialize(questSystem, inventory, saveSystem);
            inventoryPanel.Initialize(inventory, equipment);
            questPanel.Initialize(questSystem, inventory);

            inventoryPanel.Hide();
            questPanel.Hide();
            dialoguePanel.Hide();
            hudPanel.gameObject.SetActive(true);

            SyncInputBlock();   // 静态闸门跨场景保留，开场按实际面板状态复位
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.B)) inventoryPanel.Toggle();
            if (Input.GetKeyDown(KeyCode.Q)) questPanel.Toggle();
            if (Input.GetKeyDown(KeyCode.Escape)) CloseAllPanels();

            SyncInputBlock();
        }

        // ─── 世界输入闸门 ───

        /// <summary>是否有模态面板打开（对话框 / 背包 / 任务）。打开时屏蔽世界输入，见 GameInput。</summary>
        public bool IsModalPanelOpen =>
            (dialoguePanel != null && dialoguePanel.gameObject.activeSelf)
            || (inventoryPanel != null && inventoryPanel.gameObject.activeSelf)
            || (questPanel != null && questPanel.gameObject.activeSelf);

        /// <summary>把"面板是否打开"同步给世界输入闸门（每帧一行赋值，避免遗漏任何开关面板的路径）。</summary>
        private void SyncInputBlock()
        {
            GameInput.Blocked = IsModalPanelOpen;
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
            SyncInputBlock();
        }

        /// <summary>仅显示对话面板骨架（无 agent 绑定，调试用）。</summary>
        public void ShowDialogueSkeleton(string npcName)
        {
            CloseAllPanels();
            dialoguePanel.ShowSkeleton(npcName);
            SyncInputBlock();
        }

        // ─── UI 意图转发（§6.1 差距 #1：面板不直调系统，由意图入口转发到 GameSession）───

        public void RequestEquip(string itemId)
        {
            gameSession?.EquipItem(itemId);
        }

        public void RequestUnequip(string itemId)
        {
            gameSession?.UnequipItem(itemId);
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
            SyncInputBlock();
        }

        /// <summary>
        /// 解析面板挂载用的 Canvas：① 场景里已有 → 直接用（Boot 场景内嵌的 UI 实例）；
        /// ② Resources/UI/GameUI.prefab → 实例化；③ 都没有 → 代码创建。
        /// </summary>
        private Transform ResolveCanvas()
        {
            var existing = GetComponentInChildren<Canvas>(true);
            if (existing != null)
                return existing.transform;

            var prefab = Resources.Load<GameObject>(GameUIPrefabPath);
            if (prefab != null)
            {
                var instance = Instantiate(prefab, transform);
                instance.name = "Canvas";
                return instance.transform;
            }

            return CreateCanvasObject(transform).transform;
        }

        /// <summary>
        /// 创建 Canvas 对象（运行时与编辑器烘焙工具共用同一套配置，改这里两边一起生效）。
        /// </summary>
        public static Canvas CreateCanvasObject(Transform parent)
        {
            var canvasGO = new GameObject("Canvas");
            if (parent != null)
                canvasGO.transform.SetParent(parent, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>EventSystem（挂在 AppRoot 下，随 DontDestroyOnLoad 跨场景保留）。</summary>
        private void CreateEventSystem()
        {
            if (GetComponentInChildren<EventSystem>() != null)
                return;
            var esGO = new GameObject("EventSystem");
            esGO.transform.SetParent(transform, false);
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        /// <summary>取 Canvas 下已有的面板组件；没有才新建（新建时面板脚本会自己构建 UI）。</summary>
        private T GetOrCreatePanel<T>(string name, Transform parent) where T : Component
        {
            var existing = parent.GetComponentInChildren<T>(true);
            if (existing != null)
                return existing;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            UIHelper.Stretch(rt);
            return go.AddComponent<T>();
        }
    }
}
