using UnityEngine;

namespace FogHarbor.Items
{
    public enum ItemType
    {
        Weapon,
        Armor,
        Consumable,
        QuestItem,
        Material
    }

    /// <summary>
    /// 物品定义：用 ScriptableObject 在 Data/Items/ 下创建资产。
    /// InventorySystem 只存 itemId 和数量，需要查属性时通过 ItemDatabase 查询。
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "FogHarbor/Item")]
    public class ItemData : ScriptableObject
    {
        [Header("基础")]
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [TextArea][SerializeField] private string description;

        [Header("类型")]
        [SerializeField] private ItemType itemType;

        [Header("属性（按类型填写）")]
        [SerializeField] private int attack;
        [SerializeField] private int defense;
        [SerializeField] private int healAmount;

        [Header("外观")]
        [Tooltip("装备后握在手里的模型（Prefab）；留空表示该物品没有手持外观")]
        [SerializeField] private GameObject holdPrefab;

        [Header("标记")]
        [SerializeField] private bool isQuestItem;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public ItemType Type => itemType;
        public int Attack => attack;
        public int Defense => defense;
        public int HealAmount => healAmount;
        public GameObject HoldPrefab => holdPrefab;
        public bool IsQuestItem => isQuestItem;
    }
}
