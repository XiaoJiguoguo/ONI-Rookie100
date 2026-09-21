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
        // Building lifecycle notifications invalidate this snapshot. Reconcile changes
        // outside those hooks (e.g. activation changes) at a bounded fallback interval.
        private const float CacheLifetime = 5f;
        private static Dictionary<string, int> cachedCounts;
        private static readonly List<BuildingComplete> diffusers = new List<BuildingComplete>();
        private static readonly List<BuildingComplete> sanitation = new List<BuildingComplete>();
        private static float scannedAt;
        private static int invalidatedFrame = -1;

        public static void ResetCache()
        {
            cachedCounts = null;
            diffusers.Clear();
            sanitation.Clear();
            invalidatedFrame = -1;
        }

        public static void InvalidateBuildings()
        {
            cachedCounts = null;
            diffusers.Clear();
            sanitation.Clear();
            // Unity may defer destruction to the end of this frame. A scan during
            // cleanup must not become the long-lived snapshot for subsequent frames.
            invalidatedFrame = Time.frameCount;
        }

        /// <summary>场景内已建成建筑计数（PrefabID → 数量）。</summary>
        public static Dictionary<string, int> CountAllBuildings()
        {
            return CountAllBuildings(false);
        }

        public static Dictionary<string, int> CountAllBuildings(bool forceRefresh)
        {
            float now = Time.realtimeSinceStartup;
            if (!forceRefresh && cachedCounts != null && invalidatedFrame < 0 && now >= scannedAt && now - scannedAt < CacheLifetime)
                return ReadLiveOperation(cachedCounts);

            var counts = new Dictionary<string, int>();
            diffusers.Clear();
            sanitation.Clear();
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
                if (prefabName == "MineralDeoxidizer") diffusers.Add(building);
                if (Diagnostics.SanitationReadiness.Supports(prefabName)) sanitation.Add(building);
            }

            cachedCounts = counts;
            scannedAt = now;
            if (Time.frameCount > invalidatedFrame)
                invalidatedFrame = -1;
            // Callers retain the existing mutable API without owning the cached dictionary.
            return ReadLiveOperation(cachedCounts);
        }

        private static QuestBuildingSnapshot ReadLiveOperation(Dictionary<string, int> built)
        {
            var snapshot = new QuestBuildingSnapshot(built);
            int running = 0;
            foreach (var building in diffusers)
            {
                if (building == null || building.gameObject == null || !building.gameObject.activeInHierarchy) continue;
                var operational = building.GetComponent<Operational>();
                var energy = building.GetComponent<EnergyConsumer>();
                // IsActive alone may lag a power change. Require current operating
                // and power flags as well; missing evidence never grants completion.
                if (operational != null && energy != null && Diagnostics.DiffuserRules.IsRunning(
                    operational.IsActive, operational.IsOperational, energy.HasWire, energy.IsPowered))
                    running++;
            }
            snapshot.Operating["MineralDeoxidizer"] = running;
            foreach (var building in sanitation)
            {
                if (building == null || building.gameObject == null || !building.gameObject.activeInHierarchy) continue;
                var id = building.GetComponent<KPrefabID>();
                if (id == null) continue;
                string tag = id.PrefabTag.Name;
                var condition = Diagnostics.SanitationReadiness.Read(building, tag);
                if (!snapshot.SanitationIssues.TryGetValue(tag, out var issues))
                    snapshot.SanitationIssues[tag] = issues = new HashSet<Diagnostics.SanitationCondition>();
                issues.Add(condition);
                if (condition == Diagnostics.SanitationCondition.Ready)
                {
                    snapshot.Ready.TryGetValue(tag, out int ready);
                    snapshot.Ready[tag] = ready + 1;
                }
            }
            return snapshot;
        }
    }
}