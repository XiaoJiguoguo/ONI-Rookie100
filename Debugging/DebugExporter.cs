using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rookie100.Content;
using Rookie100.Monitoring;

namespace Rookie100.Debugging
{
    // Opt-in, read-only diagnostic export. No reward, save or gameplay mutation.
    // Transport contract is independent of Unity. Native lifecycle is wired separately.
    public static class DebugExporter
    {
        private static string Output;
        public static void Initialize(string contentPath)
        {
            Output = Environment.GetEnvironmentVariable("ROOKIE100_DEBUG_FILE");
            if (string.IsNullOrWhiteSpace(Output) && File.Exists(Path.Combine(contentPath, "debug_export.enabled")))
                Output = Path.Combine(contentPath, "debug_snapshot.json");
        }
        private static string session;
        private static long sequence;
        private static Dictionary<string, int> counts;
        private static System.DateTime? objectivesCapturedAt;
        private static int countsWorld = -1;
        private static bool warned;
        public static void BeginSession()
        {
            session = Guid.NewGuid().ToString("N"); sequence = 0; counts = null; objectivesCapturedAt = null; countsWorld = -1; warned = false;
            Write("loaded");
        }
        public static void ObserveObjectives(Dictionary<string, int> observed)
        {
            if (string.IsNullOrWhiteSpace(Output)) return;
            counts = observed == null ? null : new Dictionary<string, int>(observed);
            objectivesCapturedAt = System.DateTime.UtcNow;
            countsWorld = ClusterManager.Instance?.activeWorld?.id ?? -1;
        }
        public static void Write(string state = "loaded")
        {
            if (string.IsNullOrWhiteSpace(Output) || session == null) return;
            try
            {
                var monitor = DuplicantMonitor.Instance;
                IEnumerable<DuplicantReading> readings = monitor?.State.Readings;
                int activeWorld = ClusterManager.Instance?.activeWorld?.id ?? -1;
                bool currentObjectives = counts != null && countsWorld >= 0 && countsWorld == activeWorld;
                var facts = state == "disconnected" ? new Newtonsoft.Json.Linq.JObject() :
                    Curriculum.CurriculumCatalog.ObserveFacts(counts, countsWorld, activeWorld, objectivesCapturedAt, System.DateTime.UtcNow);
                if (state != "disconnected") ObserveContext(facts, activeWorld);
                var tasks = QuestStore.OrderedQuests.Select(q => new {
                    id = q.Id, title = q.Title ?? q.Id, learned = QuestStore.IsLearned(q.Id),
                    objectives = q.Objectives.Select(o => new {
                        label = o.Label ?? o.Condition ?? o.Tag ?? "任务条件",
                        met = !currentObjectives ? (bool?)null : QuestStore.IsObjectiveMet(o, counts),
                        detail = !currentObjectives ? "尚未采集当前星体的任务条件" : QuestStore.GetObjectiveStatusText(o, counts),
                        type = o.Type, condition = o.Condition, tag = o.Tag, required = o.Count
                    }).ToArray()
                }).ToArray();
                var data = new {
                    schemaVersion = 1, modId = "ONI-Rookie100", kind = "debug.snapshot", source = "game",
                    sessionId = session, sequence = ++sequence, capturedAt = System.DateTime.UtcNow.ToString("o"), state,
                    colonyKey = QuestStore.ActiveColonyKey ?? "", world = activeWorld,
                    objectivesCapturedAt = objectivesCapturedAt?.ToString("o"),
                    countsWorld,
                    curriculum = Curriculum.CurriculumCatalog.Summary,
                    facts,
                    paused = state == "disconnected" ? (bool?)null : SpeedControlScreen.Instance?.IsPaused,
                    cycle = state == "disconnected" ? (int?)null : GameClock.Instance?.GetCycle(),
                    tasks = state == "disconnected" ? tasks.Take(0).ToArray() : tasks,
                    duplicants = (state == "disconnected" ? Enumerable.Empty<DuplicantReading>() : readings ?? Enumerable.Empty<DuplicantReading>()).Where(d => d.World == activeWorld).Select(d => new {
                        id = d.Id, name = d.Name ?? "未知", world = d.World,
                        health = d.Health, stress = d.Stress, breath = d.Breath, calories = d.Calories
                    }).ToArray(), counts = state == "disconnected" || !currentObjectives ? null : counts
                };
                // Same-directory replace prevents the bridge reading partial JSON.
                string path = Path.GetFullPath(Output), temporary = path + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temporary, JsonConvert.SerializeObject(data));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch (Exception e)
            {
                if (!warned) { warned = true; ModLogger.Warn("调试快照导出失败（不影响游戏）: " + e.Message); }
            }
        }
        public static void EndSession()
        {
            Write("disconnected"); session = null; counts = null; objectivesCapturedAt = null;
        }
        private static void ObserveContext(Newtonsoft.Json.Linq.JObject facts, int world)
        {
            // Native observations fail independently; missing values stay unknown.
            void Set(string key, Newtonsoft.Json.Linq.JToken value) => facts[key] = new Newtonsoft.Json.Linq.JObject {
                ["value"] = value, ["world"] = world,
                ["capturedAt"] = System.DateTime.UtcNow.ToString("o"), ["source"] = "game" };
            try { Set("context.dlcOwned", new Newtonsoft.Json.Linq.JArray(DlcManager.GetOwnedDLCIds())); } catch { }
            try
            {
                if (Game.Instance != null)
                    Set("context.dlcEnabled", new Newtonsoft.Json.Linq.JArray(DlcManager.GetActiveDLCIds()
                        .Where(id => Game.IsDlcActiveForCurrentSave(id))));
            }
            catch { }
            try
            {
                var start = ClusterManager.Instance?.GetStartWorld();
                if (start != null) Set("context.startWorldId", start.id);
            }
            catch { }
        }
    }
}
