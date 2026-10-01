using System;
using System.Collections.Generic;
using System.Linq;
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
            BuildingComplete[] buildings = UnityEngine.Object.FindObjectsByType<BuildingComplete>(
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
            var living = new List<LivingRoomRules.Facility>();
            // Accessible inventory is a native observation. A failed read remains unknown.
            try
            {
                var inventory = ClusterManager.Instance?.activeWorld?.worldInventory?.GetAccessibleAmounts();
                if (inventory != null)
                {
                    counts["sanitation:DirtKnown"] = 1;
                    counts["sanitation:DirtAccessible"] = inventory.TryGetValue(new Tag("Dirt"), out float dirt) && dirt > 0f ? 1 : 0;
                }
            }
            catch (Exception e) { ModLogger.Warn("读取引导库存失败: " + e.Message); }
            var facilities = new List<SanitationRules.Facility>();
            var roomIds = new Dictionary<Room, int>();
            var minions = UnityEngine.Object.FindObjectsByType<MinionIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var building in buildings)
            {
                if (building == null || building.GetMyWorldId() != world) continue;
                string kind = building.GetComponent<KPrefabID>()?.PrefabTag.Name;
                if (kind == BedConfig.ID || kind == DiningTableConfig.ID)
                {
                    bool isBed = kind == BedConfig.ID;
                    var nativeRoom = Game.Instance?.roomProber?.GetRoomOfGameObject(building.gameObject);
                    Workable work = isBed ? (Workable)building.GetComponent<Sleepable>() : building.GetComponent<MessStation>();
                    bool reachable = false;
                    if (work != null)
                        foreach (var minion in minions)
                            if (minion != null && minion.GetMyWorldId() == world &&
                                minion.GetComponent<Navigator>()?.CanReach(work.GetCell(), work.GetOffsets()) == true)
                                reachable = true;
                    var roomTypes = Db.Get().RoomTypes;
                    bool recognized = nativeRoom != null && (isBed
                        ? nativeRoom.roomType == roomTypes.Barracks || nativeRoom.roomType == roomTypes.Bedroom || nativeRoom.roomType == roomTypes.PrivateBedroom
                        : nativeRoom.roomType == roomTypes.MessHall || nativeRoom.roomType == roomTypes.GreatHall || nativeRoom.roomType == roomTypes.BanquetHall);
                    // Passive beds/tables need not have Operational. Sleepable's
                    // native implementation also explicitly allows that component to be absent.
                    var operation = building.GetComponent<Operational>();
                    living.Add(new LivingRoomRules.Facility { Kind = kind, World = world,
                        Usable = work != null && (operation == null || operation.IsOperational),
                        Reachable = reachable,
                        Recognized = recognized });
                }
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
                // Reachability is useful even before supplies arrive.
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
            foreach (var pair in LivingRoomRules.Evaluate(living, world)) counts[pair.Key] = pair.Value;
            counts["sanitation:OuthouseBuilt"] = facilities.Any(f => f.Kind == "Outhouse") ? 1 : 0;
            counts["sanitation:WashBasinBuilt"] = facilities.Any(f => f.Kind == "WashBasin") ? 1 : 0;
            counts["sanitation:PumpBuilt"] = facilities.Any(f => f.Kind == "LiquidPumpingStation") ? 1 : 0;
            counts["sanitation:PumpReachable"] = facilities.Any(f => f.Kind == "LiquidPumpingStation" && f.Workers.Count > 0) ? 1 : 0;
            return counts;
        }
    }
}
