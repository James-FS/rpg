using System;
using UnityEngine;
using FogHarbor.Items;
using FogHarbor.Inventory;
using FogHarbor.Player;

namespace FogHarbor.Equipment
{
    /// <summary>
    /// 装备系统：管理武器和护甲两个槽位，穿戴/脱下装备并更新玩家属性。
    /// 挂载在 AppRoot 上，通过 DontDestroyOnLoad 跨场景保留。
    /// </summary>
    public class EquipmentSystem : MonoBehaviour
    {
        public enum EquipSlot
        {
            Weapon,
            Armor
        }

        [Serializable]
        public class EquipmentSaveData
        {
            public string weapon;
            public string armor;
        }

        // 当前装备的 itemId，空字符串表示未装备
        private string weaponId = "";
        private string armorId = "";

        // 属性累加
        private int bonusAttack;
        private int bonusDefense;

        // 外部引用
        private InventorySystem inventory;
        private PlayerController player;

        /// <summary>装备变化时触发，UI 监听刷新。</summary>
        public event Action OnEquipmentChanged;

        // ─── 属性查询 ───

        public string WeaponId => weaponId;
        public string ArmorId => armorId;
        public int BonusAttack => bonusAttack;
        public int BonusDefense => bonusDefense;
        public bool HasWeapon => !string.IsNullOrEmpty(weaponId);
        public bool HasArmor => !string.IsNullOrEmpty(armorId);

        // ─── 初始化 ───

        private void Awake()
        {
            // InventorySystem 挂在同一个 AppRoot 上，直接获取
            inventory = GetComponent<InventorySystem>();
            if (inventory == null)
                Debug.LogError("[EquipmentSystem] 未找到 InventorySystem，请确保两者挂在同一物体上");
        }

        private void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // PlayerController 在场景物体上，AppRoot 跨场景保留——场景切换后重新绑定并应用加成。
            player = UnityEngine.Object.FindObjectOfType<PlayerController>();
            ApplyStatsToPlayer();
        }

        private void Start()
        {
            // 兜底绑定：直接 Play（场景已加载，sceneLoaded 不触发）时也能拿到 PlayerController。
            if (player == null)
                player = UnityEngine.Object.FindObjectOfType<PlayerController>();
            RecalculateStats();
            ApplyStatsToPlayer();
        }

        /// <summary>手动绑定 PlayerController（供外部注入；通常由 OnSceneLoaded 自动完成）。</summary>
        public void SetPlayer(PlayerController pc)
        {
            player = pc;
            ApplyStatsToPlayer();
        }

        // ─── 穿戴 / 脱下 ───

        /// <summary>穿戴装备：从背包移除物品并放入对应槽位，已有装备退回背包。</summary>
        public bool Equip(string itemId)
        {
            var itemData = ItemDatabase.GetById(itemId);
            if (itemData == null) return false;

            if (itemData.Type != ItemType.Weapon && itemData.Type != ItemType.Armor)
            {
                Debug.LogWarning($"[Equipment] {itemId} 不是可装备物品");
                return false;
            }

            if (inventory == null || !inventory.Has(itemId, 1))
            {
                Debug.LogWarning($"[Equipment] 背包中没有 {itemId}，无法装备");
                return false;
            }

            // 从背包取出
            inventory.Remove(itemId, 1);

            // 确定槽位
            EquipSlot slot = itemData.Type == ItemType.Weapon ? EquipSlot.Weapon : EquipSlot.Armor;

            // 如果该槽位已有装备，退回背包
            string oldId = slot == EquipSlot.Weapon ? weaponId : armorId;
            if (!string.IsNullOrEmpty(oldId))
            {
                inventory.Add(oldId, 1);
            }

            // 装备新物品
            if (slot == EquipSlot.Weapon)
                weaponId = itemId;
            else
                armorId = itemId;

            RecalculateStats();
            ApplyStatsToPlayer();
            OnEquipmentChanged?.Invoke();

            Debug.Log($"[Equipment] 装备 {itemData.DisplayName} (ATK+{itemData.Attack} DEF+{itemData.Defense})");
            return true;
        }

        /// <summary>脱下指定槽位的装备，放回背包。</summary>
        public bool Unequip(EquipSlot slot)
        {
            string currentId = slot == EquipSlot.Weapon ? weaponId : armorId;

            if (string.IsNullOrEmpty(currentId))
            {
                Debug.LogWarning($"[Equipment] {slot} 槽位为空，无需脱下");
                return false;
            }

            if (inventory != null)
                inventory.Add(currentId, 1);

            if (slot == EquipSlot.Weapon)
                weaponId = "";
            else
                armorId = "";

            RecalculateStats();
            ApplyStatsToPlayer();
            OnEquipmentChanged?.Invoke();

            Debug.Log($"[Equipment] 脱下 {currentId}");
            return true;
        }

        // ─── 内部逻辑 ───

        private void RecalculateStats()
        {
            bonusAttack = 0;
            bonusDefense = 0;

            if (!string.IsNullOrEmpty(weaponId))
            {
                var data = ItemDatabase.GetById(weaponId);
                if (data != null) bonusAttack += data.Attack;
            }

            if (!string.IsNullOrEmpty(armorId))
            {
                var data = ItemDatabase.GetById(armorId);
                if (data != null) bonusDefense += data.Defense;
            }
        }

        private void ApplyStatsToPlayer()
        {
            if (player == null)
            {
                Debug.Log($"[Equipment] 玩家未绑定，加成暂未应用（ATK+{bonusAttack} DEF+{bonusDefense}）");
                return;
            }

            // 真正写回玩家属性（T2 遗留补全：装备加成参与战斗）
            player.SetAttackBonus(bonusAttack);
            player.SetDefenseBonus(bonusDefense);
            Debug.Log($"[Equipment] 加成已应用: ATK+{bonusAttack} DEF+{bonusDefense}");
        }

        // ─── 存档支持 ───

        public EquipmentSaveData GetSaveData()
        {
            return new EquipmentSaveData
            {
                weapon = weaponId,
                armor = armorId
            };
        }

        public void LoadFromData(EquipmentSaveData data)
        {
            weaponId = data?.weapon ?? "";
            armorId = data?.armor ?? "";
            RecalculateStats();
            ApplyStatsToPlayer();
            OnEquipmentChanged?.Invoke();
            Debug.Log($"[Equipment] 从存档恢复: 武器={weaponId} 护甲={armorId}");
        }
    }
}
