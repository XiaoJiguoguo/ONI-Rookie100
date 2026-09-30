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

            int world = ClusterManager.Instance?.activeWorld?.id ?? -1;
            var facilities = new List<SanitationRules.Facility>();
            var roomIds = new Dictionary<Room, int>();
            var minions = Object.FindObjectsByType<MinionIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var building in buildings)
            {
                if (building == null || building.GetMyWorldId() != world) continue;
                string kind = building.GetComponent<KPrefabID>()?.PrefabTag.Name;
                if (kind != "Outhouse" && kind != "WashBasin" && kind != "LiquidPumpingStation") continue;
                var facility = new SanitationRules.Facility { Kind = kind, World = world };
                var room = Game.Instance?.roomProber?.GetRoomOfGameObject(building.gameObject);
                if (room != null && room.roomType == Db.Get().RoomTypes.Latrine)
                {
                    if (!roomIds.TryGetValue(room, out int roomId)) { roomId = roomIds.Count; roomIds.Add(room, roomId); }
                    facility.Room = roomId;
                }
                var operational = building.GetComponent<Operational>();
                bool enabled = operational != null && operational.IsOperational;
                if (kind == "Outhouse")
                    facility.Ready = enabled && building.GetComponent<Toilet>()?.IsUsable() == true;
                else if (kind == "WashBasin")
                {
                    var sanitizer = building.GetComponent<HandSanitizer>();
                    var storage = building.GetComponent<Storage>();
                    facility.Ready = enabled && sanitizer != null && storage != null &&
                        storage.GetMassAvailable(SimHashes.Water) >= sanitizer.massConsumedPerUse &&
                        building.GetSMI<HandSanitizer.SMInstance>()?.IsReady() == true;
                }
                else
                    // Native Sim200ms creates/removes these virtual water sources using its
                    // actual intake cells and unobstructed depth, with a >1 kg cutoff.
                    facility.Ready = enabled && building.GetComponent<LiquidPumpingStation>() != null &&
                        (building.GetComponent<Storage>()?.GetMassAvailable(SimHashes.Water) ?? 0f) > 1f;
                if (facility.Ready)
                {
                    Workable work = kind == "Outhouse" ? (Workable)building.GetComponent<ToiletWorkableUse>() :
                        kind == "WashBasin" ? building.GetComponent<HandSanitizer.Work>() :
                        building.GetComponent<LiquidPumpingStation>();
                    if (work != null)
                        foreach (var minion in minions)
                        {
                            if (minion == null || minion.GetMyWorldId() != world) continue;
                            var nav = minion.GetComponent<Navigator>();
                            var offsets = kind == "LiquidPumpingStation" ? new[] { new CellOffset(0, 1) } : work.GetOffsets();
                            if (nav != null && nav.CanReach(work.GetCell(), offsets))
                                facility.Workers.Add(minion.GetInstanceID());
                        }
                }
                facilities.Add(facility);
            }
            foreach (var pair in SanitationRules.Evaluate(facilities, world, QuestStore.HasSanitationTrial(world)))
                counts[pair.Key] = pair.Value;
            return counts;
        }
    }
}
