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
        private static readonly string Output = Environment.GetEnvironmentVariable("ROOKIE100_DEBUG_FILE");
        private static string session;
        private static long sequence;
        private static Dictionary<string, int> counts;
        private static DateTime? objectivesCapturedAt;
        private static bool warned;
        public static void BeginSession()
        {
            session = Guid.NewGuid().ToString("N"); sequence = 0; counts = null; objectivesCapturedAt = null; warned = false;
            Write("loaded");
        }
        public static void ObserveObjectives(Dictionary<string, int> observed)
        {
            if (string.IsNullOrWhiteSpace(Output)) return;
            counts = observed == null ? null : new Dictionary<string, int>(observed);
            objectivesCapturedAt = DateTime.UtcNow;
        }
        public static void Write(string state = "loaded")
        {
            if (string.IsNullOrWhiteSpace(Output) || session == null) return;
            try
            {
                var monitor = DuplicantMonitor.Instance;
                IEnumerable<DuplicantReading> readings = monitor?.State.Readings;
                var tasks = QuestStore.OrderedQuests.Select(q => new {
                    id = q.Id, title = q.Title ?? q.Id, learned = QuestStore.IsLearned(q.Id),
                    objectives = q.Objectives.Select(o => new {
                        label = o.Label ?? o.Condition ?? o.Tag ?? "任务条件",
                        met = counts == null ? (bool?)null : QuestStore.IsObjectiveMet(o, counts),
                        detail = counts == null ? "尚未采集任务条件" : QuestStore.GetObjectiveStatusText(o, counts),
                        type = o.Type, condition = o.Condition, tag = o.Tag, required = o.Count
                    }).ToArray()
                }).ToArray();
                var data = new {
                    schemaVersion = 1, modId = "ONI-Rookie100", kind = "debug.snapshot", source = "game",
                    sessionId = session, sequence = ++sequence, capturedAt = DateTime.UtcNow.ToString("o"), state,
                    colonyKey = QuestStore.ActiveColonyKey ?? "", world = monitor?.State.World ?? -1,
                    objectivesCapturedAt = objectivesCapturedAt?.ToString("o"),
                    tasks = state == "disconnected" ? tasks.Take(0).ToArray() : tasks,
                    duplicants = (state == "disconnected" ? Enumerable.Empty<DuplicantReading>() : readings ?? Enumerable.Empty<DuplicantReading>()).Select(d => new {
                        id = d.Id, name = d.Name ?? "未知", world = d.World,
                        health = d.Health, stress = d.Stress, breath = d.Breath, calories = d.Calories
                    }).ToArray(), counts = state == "disconnected" ? null : counts
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
    }
}
