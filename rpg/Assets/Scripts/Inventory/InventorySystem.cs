using System;
using System.Collections.Generic;
using UnityEngine;
using FogHarbor.Items;

namespace FogHarbor.Inventory
{
    /// <summary>
    /// 背包系统：管理玩家持有的物品及数量。
    /// 挂载在 AppRoot 上，场景切换时通过 DontDestroyOnLoad 保留。
    /// 只存 itemId → count，不存 ItemData 引用（定义由 ItemDatabase 查询）。
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        public enum ConsumeResult
        {
            Success,
            InvalidItemId,
            UnknownItem,
            NotConsumable,
            NotOwned
        }

        private readonly Dictionary<string, int> items = new();
        private readonly HashSet<string> starterItemGrants = new();

        /// <summary>背包发生变化时触发，UI 监听此事件刷新显示。</summary>
        public event Action OnInventoryChanged;

        // ─── 查询 ───

        public bool Has(string itemId, int count = 1)
        {
            ItemDatabase.GetById(itemId);
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Inventory queries require a positive count.");
            return items.TryGetValue(itemId, out int owned) && owned >= count;
        }

        public int GetCount(string itemId)
        {
            ItemDatabase.GetById(itemId);
            return items.TryGetValue(itemId, out int count) ? count : 0;
        }

        // ─── 增删 ───

        public void Add(string itemId, int count)
        {
            ItemDatabase.GetById(itemId);
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Inventory additions require a positive count.");

            if (items.ContainsKey(itemId))
                items[itemId] = checked(items[itemId] + count);
            else
                items[itemId] = count;

            Debug.Log($"[Inventory] +{count} {itemId} (共 {items[itemId]})");
            OnInventoryChanged?.Invoke();
        }

        public bool Remove(string itemId, int count)
        {
            ItemDatabase.GetById(itemId);
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Inventory removals require a positive count.");
            if (!Has(itemId, count)) return false;

            items[itemId] -= count;
            if (items[itemId] <= 0)
                items.Remove(itemId);

            Debug.Log($"[Inventory] -{count} {itemId}");
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>验证并消耗一件可使用的消耗品；物品类型和持有规则归背包系统管理。</summary>
        public ConsumeResult TryConsumeConsumable(string itemId, out ItemData itemData)
        {
            itemData = null;
            if (string.IsNullOrEmpty(itemId)) return ConsumeResult.InvalidItemId;

            if (!ItemDatabase.TryGetById(itemId, out itemData)) return ConsumeResult.UnknownItem;
            if (itemData.Type != ItemType.Consumable) return ConsumeResult.NotConsumable;
            if (!Remove(itemId, 1)) return ConsumeResult.NotOwned;

            return ConsumeResult.Success;
        }

        /// <summary>Grant newly introduced starter items once, including migrated saves.</summary>
        public bool EnsureStarterItems(string equippedWeapon, string equippedArmor)
        {
            bool changed = false;
            foreach (var item in ItemDatabase.GetAll())
            {
                if (!item.StarterItem || !starterItemGrants.Add(item.ItemId)) continue;
                changed = true;
                if (!Has(item.ItemId) && equippedWeapon != item.ItemId && equippedArmor != item.ItemId)
                    Add(item.ItemId, 1);
            }
            return changed;
        }

        public List<string> GetStarterItemGrants() => new List<string>(starterItemGrants);

        public void LoadStarterItemGrants(List<string> grants)
        {
            if (grants == null) throw new ArgumentNullException(nameof(grants));
            var restored = new HashSet<string>();
            foreach (string id in grants)
            {
                ItemDatabase.GetById(id);
                if (!restored.Add(id))
                    throw new InvalidOperationException($"Duplicate starter item grant '{id}' in save data.");
            }
            starterItemGrants.Clear();
            foreach (string id in restored) starterItemGrants.Add(id);
        }

        // ─── 存档支持 ───

        /// <summary>返回可序列化的快照，供 SaveSystem 使用。</summary>
        public List<InventoryEntry> GetSaveData()
        {
            var list = new List<InventoryEntry>();
            foreach (var pair in items)
                list.Add(new InventoryEntry { itemId = pair.Key, count = pair.Value });
            return list;
        }

        /// <summary>从已校验的存档快照恢复背包数据，并在提交后通知 UI。</summary>
        public void LoadFromData(List<InventoryEntry> data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var restored = new Dictionary<string, int>();
            foreach (var entry in data)
            {
                if (entry == null) throw new InvalidOperationException("Save data contains a null inventory entry.");
                ItemDatabase.GetById(entry.itemId);
                if (entry.count <= 0) throw new InvalidOperationException($"Invalid inventory count for '{entry.itemId}': {entry.count}.");
                if (restored.ContainsKey(entry.itemId))
                    throw new InvalidOperationException($"Duplicate inventory entry '{entry.itemId}' in save data.");
                restored.Add(entry.itemId, entry.count);
            }

            items.Clear();
            foreach (var pair in restored) items.Add(pair.Key, pair.Value);

            Debug.Log($"[Inventory] 从存档恢复 {items.Count} 种物品");
            OnInventoryChanged?.Invoke();
        }

        /// <summary>获取所有物品（只读遍历用）。</summary>
        public IReadOnlyDictionary<string, int> GetAll() => items;
    }

    /// <summary>存档用结构。</summary>
    [Serializable]
    public class InventoryEntry
    {
        public string itemId;
        public int count;
    }
}
