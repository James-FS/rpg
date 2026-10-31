using System.Collections.Generic;
using UnityEngine;

namespace FogHarbor.Items
{
    /// <summary>
    /// 物品数据库：启动时自动加载 Resources/Items/ 下所有 ItemData 资产。
    /// 提供 GetById(itemId) 查询接口，供 EquipmentSystem / UI 使用。
    /// </summary>
    public static class ItemDatabase
    {
        private static readonly Dictionary<string, ItemData> lookup = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            LoadAll();
        }

        public static void LoadAll()
        {
            lookup.Clear();
            var assets = Resources.LoadAll<ItemData>("Items");
            foreach (var asset in assets)
            {
                if (asset == null) throw new System.InvalidOperationException("Resources/Items contains a null ItemData asset.");
                if (string.IsNullOrWhiteSpace(asset.ItemId))
                    throw new System.InvalidOperationException($"ItemData '{asset.name}' has no itemId.");
                if (lookup.ContainsKey(asset.ItemId))
                    throw new System.InvalidOperationException($"Duplicate itemId '{asset.ItemId}' in Resources/Items.");
                lookup.Add(asset.ItemId, asset);
            }
            Debug.Log($"[ItemDatabase] 已加载 {lookup.Count} 个物品定义");
        }

        public static ItemData GetById(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) throw new System.ArgumentException("itemId is required.", nameof(itemId));
            if (lookup.TryGetValue(itemId, out var data)) return data;
            throw new KeyNotFoundException($"No ItemData is registered for itemId '{itemId}'.");
        }

        public static bool TryGetById(string itemId, out ItemData data)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                data = null;
                return false;
            }
            return lookup.TryGetValue(itemId, out data);
        }

        public static bool Exists(string itemId) => !string.IsNullOrWhiteSpace(itemId) && lookup.ContainsKey(itemId);
        public static IEnumerable<ItemData> GetAll() => lookup.Values;
    }
}
