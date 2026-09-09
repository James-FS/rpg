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
                if (!string.IsNullOrEmpty(asset.ItemId))
                {
                    lookup[asset.ItemId] = asset;
                }
            }
            Debug.Log($"[ItemDatabase] 已加载 {lookup.Count} 个物品定义");
        }

        public static ItemData GetById(string itemId)
        {
            if (lookup.TryGetValue(itemId, out var data))
                return data;

            Debug.LogWarning($"[ItemDatabase] 未找到物品: {itemId}");
            return null;
        }

        public static bool Exists(string itemId) => lookup.ContainsKey(itemId);
    }
}
