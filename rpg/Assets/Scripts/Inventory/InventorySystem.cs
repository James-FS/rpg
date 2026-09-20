using System;
using System.Collections.Generic;
using UnityEngine;

namespace FogHarbor.Inventory
{
    /// <summary>
    /// 背包系统：管理玩家持有的物品及数量。
    /// 挂载在 AppRoot 上，场景切换时通过 DontDestroyOnLoad 保留。
    /// 只存 itemId → count，不存 ItemData 引用（定义由 ItemDatabase 查询）。
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        private readonly Dictionary<string, int> items = new();

        /// <summary>背包发生变化时触发，UI 监听此事件刷新显示。</summary>
        public event Action OnInventoryChanged;

        // ─── 查询 ───

        public bool Has(string itemId, int count = 1)
        {
            return items.TryGetValue(itemId, out int owned) && owned >= count;
        }

        public int GetCount(string itemId)
        {
            return items.TryGetValue(itemId, out int count) ? count : 0;
        }

        // ─── 增删 ───

        public void Add(string itemId, int count)
        {
            if (count <= 0) return;

            if (items.ContainsKey(itemId))
                items[itemId] += count;
            else
                items[itemId] = count;

            Debug.Log($"[Inventory] +{count} {itemId} (共 {items[itemId]})");
            OnInventoryChanged?.Invoke();
        }

        public bool Remove(string itemId, int count)
        {
            if (count <= 0) return true;
            if (!Has(itemId, count)) return false;

            items[itemId] -= count;
            if (items[itemId] <= 0)
                items.Remove(itemId);

            Debug.Log($"[Inventory] -{count} {itemId}");
            OnInventoryChanged?.Invoke();
            return true;
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

        /// <summary>从存档恢复背包数据（data 为 null 表示空背包，同样要通知 UI 清空显示）。</summary>
        public void LoadFromData(List<InventoryEntry> data)
        {
            items.Clear();
            if (data != null)
            {
                foreach (var entry in data)
                {
                    if (!string.IsNullOrEmpty(entry.itemId) && entry.count > 0)
                        items[entry.itemId] = entry.count;
                }
            }

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
