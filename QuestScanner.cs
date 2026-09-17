using System.Collections.Generic;
using Rookie100.Content;
using UnityEngine;

namespace Rookie100
{
    /// <summary>
    /// 场景建筑扫描器：统计各 PrefabID 已建成数量（供任务目标检测）。
    /// 模式取自 StorageNetwork 的 FindObjectsByType 扫描。
    /// </summary>
    public static class QuestScanner
    {
        /// <summary>场景内已建成建筑计数（PrefabID → 数量）。</summary>
        public static Dictionary<string, int> CountAllBuildings()
        {
            var counts = new Dictionary<string, int>();
            BuildingComplete[] buildings = Object.FindObjectsByType<BuildingComplete>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var building in buildings)
            {
                if (building == null || building.gameObject == null)
                {
                    continue;
                }

                KPrefabID kPrefabId = building.GetComponent<KPrefabID>();
                if (kPrefabId == null)
                {
                    continue;
                }

                string prefabName = kPrefabId.PrefabTag.Name;
                counts.TryGetValue(prefabName, out int count);
                counts[prefabName] = count + 1;
            }

            return counts;
        }
    }
}