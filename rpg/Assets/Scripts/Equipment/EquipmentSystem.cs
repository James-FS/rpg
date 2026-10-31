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
            inventory = GetComponent<InventorySystem>()
                ?? throw new MissingComponentException("EquipmentSystem requires InventorySystem on AppRoot.");
        }

        /// <summary>手动绑定 PlayerController（供外部注入；通常由 OnSceneLoaded 自动完成）。</summary>
        public void SetPlayer(PlayerController pc)
        {
            player = pc ?? throw new ArgumentNullException(nameof(pc));
            if (player.GetComponent<PlayerWeaponVisual>() is { } weaponVisual)
                weaponVisual.BindEquipment(this);
            ApplyStatsToPlayer();
        }

        public void ClearPlayer() => player = null;

        // ─── 穿戴 / 脱下 ───

        /// <summary>穿戴装备：从背包移除物品并放入对应槽位，已有装备退回背包。</summary>
        public bool Equip(string itemId)
        {
            if (!ItemDatabase.TryGetById(itemId, out var itemData)) return false;

            if (itemData.Type != ItemType.Weapon && itemData.Type != ItemType.Armor)
            {
                Debug.LogWarning($"[Equipment] {itemId} 不是可装备物品");
                return false;
            }

            if (!inventory.Has(itemId, 1))
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

        /// <summary>按装备 ID 找到其所在槽位并脱下；槽位归属判断由装备系统负责。</summary>
        public bool UnequipItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            if (weaponId == itemId) return Unequip(EquipSlot.Weapon);
            if (armorId == itemId) return Unequip(EquipSlot.Armor);
            return false;
        }

        // ─── 内部逻辑 ───

        private void RecalculateStats()
        {
            bonusAttack = 0;
            bonusDefense = 0;

            if (!string.IsNullOrEmpty(weaponId))
            {
                bonusAttack += ItemDatabase.GetById(weaponId).Attack;
            }

            if (!string.IsNullOrEmpty(armorId))
            {
                bonusDefense += ItemDatabase.GetById(armorId).Defense;
            }
        }

        private void ApplyStatsToPlayer()
        {
            if (player == null) throw new InvalidOperationException("Equipment stats cannot be applied before GameSession binds the player.");

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
            if (data == null) throw new ArgumentNullException(nameof(data));
            weaponId = data.weapon ?? "";
            armorId = data.armor ?? "";
            RecalculateStats();
            ApplyStatsToPlayer();
            OnEquipmentChanged?.Invoke();
            Debug.Log($"[Equipment] 从存档恢复: 武器={weaponId} 护甲={armorId}");
        }
    }
}
