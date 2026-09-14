using UnityEngine;
using FogHarbor.Items;
using FogHarbor.Player;
using FogHarbor.Session;
using FogHarbor.UI;

namespace FogHarbor.Items
{
    /// <summary>
    /// 场景拾取物（§6.1：意图入口）：玩家按 E 交互后把"拾取意图"转发给 GameSession，
    /// 由 Session 编排入库 + 推进任务 + 存档。本类不直接调系统，也不编排流程。
    /// 挂到场景中的道具物体上（月光药草、铁矿等），配置 itemId 与数量。
    /// 需要物体带 Collider（PlayerInteractor 用 OverlapSphere 检测）。
    /// </summary>
    public class QuestItemPickup : MonoBehaviour, IInteractable
    {
        [Header("拾取物配置")]
        [SerializeField] private string itemId;
        [SerializeField] private int count = 1;
        [SerializeField] private string promptText = "按 E 拾取";
        [SerializeField] private bool destroyOnPickup = true;

        private GameSession gameSession;
        private PlayerController player;

        private void Awake()
        {
            gameSession = FindObjectOfType<GameSession>();
            player = FindObjectOfType<PlayerController>();
        }

        public string GetPrompt()
        {
            var data = ItemDatabase.GetById(itemId);
            string name = data != null ? data.DisplayName : itemId;
            return string.IsNullOrEmpty(name) ? promptText : $"{promptText} {name}";
        }

        public void Interact()
        {
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning($"[QuestItemPickup] {gameObject.name} 未配置 itemId");
                return;
            }

            // 场景刚加载时 Awake 早于 AppRoot 创建，延迟解析。
            if (gameSession == null)
                gameSession = FindObjectOfType<GameSession>();

            if (gameSession == null)
            {
                Debug.LogError("[QuestItemPickup] 未找到 GameSession，无法拾取");
                return;
            }

            var result = gameSession.PickupItem(itemId, count);
            if (!result.Success)
            {
                Debug.LogWarning($"[QuestItemPickup] 拾取失败: {result.Message}");
                return;
            }

            var data = ItemDatabase.GetById(itemId);
            string name = data != null ? data.DisplayName : itemId;
            if (UIManager.Instance != null)
                UIManager.Instance.ShowToast($"获得 {name} x{count}");
            else
                Debug.Log($"[QuestItemPickup] 获得 {name} x{count}");

            // 表现层：转身面向拾取物并播放采集动作（短暂定身）
            if (player == null)
                player = FindObjectOfType<PlayerController>();
            if (player != null)
                player.PlayGather(transform.position);

            if (destroyOnPickup)
                Destroy(gameObject);
        }
    }
}