using System;
using System.Collections.Generic;
using HarmonyLib;
using Rookie100.Content;
using UnityEngine;

namespace Rookie100
{
    // A completed in-game use followed by a completed wash, never animation playback.
    public static class SanitationWitness
    {
        private sealed class Visit { public int World; public float Time; public string Colony; public Workable Toilet; }
        private static readonly Dictionary<WorkerBase, Visit> visits = new Dictionary<WorkerBase, Visit>();
        public static void Clear() => visits.Clear();
        public static void Used(Workable toilet, WorkerBase worker)
        {
            if (worker == null || toilet.GetComponent<KPrefabID>()?.PrefabTag.Name != "Outhouse" ||
                string.IsNullOrEmpty(QuestStore.ActiveColonyKey) || QuestStore.IsClaimed("q01")) return;
            visits[worker] = new Visit { World = toilet.GetMyWorldId(), Time = Time.time, Colony = QuestStore.ActiveColonyKey, Toilet = toilet };
        }
        public static void Washed(Workable basin, WorkerBase worker)
        {
            if (worker == null || !visits.TryGetValue(worker, out Visit visit)) return;
            visits.Remove(worker);
            if (basin.GetComponent<KPrefabID>()?.PrefabTag.Name != "WashBasin" ||
                visit.Colony != QuestStore.ActiveColonyKey || visit.World != basin.GetMyWorldId() ||
                Time.time < visit.Time || Time.time - visit.Time > 120f) return;
            if (visit.Toilet == null) return;
            var room = Game.Instance?.roomProber?.GetRoomOfGameObject(basin.gameObject);
            if (room == null || room.roomType != Db.Get().RoomTypes.Latrine ||
                Game.Instance.roomProber.GetRoomOfGameObject(visit.Toilet.gameObject) != room) return;
            QuestStore.MarkSanitationTrial(visit.World);
        }
    }

    [HarmonyPatch(typeof(ToiletWorkableUse), "OnCompleteWork")]
    public static class SanitationToiletPatch
    {
        public static void Postfix(ToiletWorkableUse __instance, WorkerBase __0)
        {
            try { SanitationWitness.Used(__instance, __0); }
            catch (Exception e) { ModLogger.Warn("Sanitation use observation: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(HandSanitizer.Work), "OnCompleteWork")]
    public static class SanitationWashPatch
    {
        public static void Postfix(HandSanitizer.Work __instance, WorkerBase __0)
        {
            try { SanitationWitness.Washed(__instance, __0); }
            catch (Exception e) { ModLogger.Warn("Sanitation wash observation: " + e.Message); }
        }
    }
}
