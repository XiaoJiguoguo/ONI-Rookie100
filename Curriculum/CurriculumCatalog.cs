using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Rookie100.Curriculum
{
    // A planning/diagnostic sidecar. It never changes QuestStore, rewards or runtime prerequisites.
    public static class CurriculumCatalog
    {
        private static JObject catalog;
        public static JObject Task(string id) => catalog?["tasks"]?.FirstOrDefault(t => (string)t["id"] == id)?.DeepClone() as JObject;
        public static JArray Chapters => catalog?["chapters"]?.DeepClone() as JArray ?? new JArray();
        public static JArray Tasks => catalog?["tasks"]?.DeepClone() as JArray ?? new JArray();
        public static JObject Summary => catalog == null ? null : new JObject {
            ["schemaVersion"] = 1, ["catalogVersion"] = catalog["catalogVersion"],
            ["chapterIds"] = new JArray(catalog["chapters"].Select(x => (string)x["id"])),
            ["taskIds"] = new JArray(catalog["tasks"].Select(x => (string)x["id"])) };
        public static void Initialize(string contentPath)
        {
            catalog = null;
            try { var data = JObject.Parse(File.ReadAllText(Path.Combine(contentPath, "Curriculum", "catalog.v1.json"))); Validate(data); catalog = data; }
            catch (Exception e) { ModLogger.Warn("六章学习框架暂不可用（不影响原任务）: " + e.Message); }
        }
        public static void Validate(JObject data)
        {
            if (data == null || data.Value<int>("schemaVersion") != 1 || data.Value<string>("modId") != "ONI-Rookie100" || data.Value<string>("kind") != "curriculum.catalog") throw new InvalidDataException("框架版本或模组标识无效");
            var chapters = data["chapters"] as JArray; var tasks = data["tasks"] as JArray; var fields = data["fields"] as JArray;
            if (chapters == null || chapters.Count != 6 || tasks == null || fields == null) throw new InvalidDataException("需要六章、任务和字段定义");
            var chapterIds = new HashSet<string>(); var taskIds = new HashSet<string>(); var fieldIds = new HashSet<string>();
            foreach (var c in chapters) if (string.IsNullOrWhiteSpace(c.Value<string>("id")) || !chapterIds.Add(c.Value<string>("id"))) throw new InvalidDataException("缺失或重复章节");
            foreach (var f in fields) if (string.IsNullOrWhiteSpace(f.Value<string>("id")) || !fieldIds.Add(f.Value<string>("id"))) throw new InvalidDataException("缺失或重复字段");
            foreach (var t in tasks) if (string.IsNullOrWhiteSpace(t.Value<string>("id")) || !taskIds.Add(t.Value<string>("id"))) throw new InvalidDataException("缺失或重复任务");
            foreach (var t in tasks)
            {
                if (!chapterIds.Contains(t.Value<string>("chapterId")) || !new[]{"main","optional"}.Contains(t.Value<string>("track"))) throw new InvalidDataException("章节或任务分类无效");
                var criteria = t["criteria"] as JArray;
                if (criteria == null || criteria.Count == 0) throw new InvalidDataException("每项任务需要明确条件");
                foreach (var c in criteria.Concat((IEnumerable<JToken>)(t["applicability"] as JArray ?? new JArray())))
                    if (!fieldIds.Contains(c.Value<string>("field")) || !new[]{"eq","gte"}.Contains(c.Value<string>("operator"))) throw new InvalidDataException("条件字段或比较方式无效");
                foreach (string id in (t["prerequisites"] as JArray ?? new JArray()).Values<string>()) if (!taskIds.Contains(id)) throw new InvalidDataException("前置任务不存在");
            }
            var lookup = tasks.ToDictionary(t => t.Value<string>("id"));
            var memberships = new HashSet<string>();
            foreach (var chapter in chapters)
                foreach (string track in new[]{"main", "optional"})
                {
                    var list = chapter[track + "TaskIds"] as JArray;
                    if (list == null) throw new InvalidDataException("章节任务清单缺失");
                    foreach (string id in list.Values<string>())
                        if (id == null || !lookup.TryGetValue(id, out var task) || !memberships.Add(id) || task.Value<string>("chapterId") != chapter.Value<string>("id") || task.Value<string>("track") != track)
                            throw new InvalidDataException("章节任务归属不一致");
                }
            if (memberships.Count != tasks.Count) throw new InvalidDataException("章节遗漏任务");
            var done = new HashSet<string>(); var visiting = new HashSet<string>();
            foreach (string id in taskIds) Visit(id, lookup, done, visiting);
        }
        private static void Visit(string id, Dictionary<string,JToken> tasks, HashSet<string> done, HashSet<string> visiting)
        {
            if (done.Contains(id)) return;
            if (!visiting.Add(id)) throw new InvalidDataException("前置条件成环");
            foreach (string next in (tasks[id]["prerequisites"] as JArray ?? new JArray()).Values<string>()) Visit(next, tasks, done, visiting);
            visiting.Remove(id); done.Add(id);
        }
        public static JObject ObserveFacts(Dictionary<string,int> counts, int countsWorld, int activeWorld, System.DateTime? countsCapturedAt, System.DateTime now)
        {
            var facts = new JObject();
            if (catalog == null) return facts;
            if (activeWorld >= 0) facts["context.activeWorldId"] = Fact(activeWorld, activeWorld, now);
            if (counts == null || countsWorld != activeWorld || activeWorld < 0 || !countsCapturedAt.HasValue) return facts;
            foreach (var field in catalog["fields"])
            {
                string key = field.Value<string>("bridgeKey"), id = field.Value<string>("id");
                if (key != null && counts.TryGetValue(key, out int value)) facts[id] = Fact(value, activeWorld, countsCapturedAt.Value);
            }
            // All unobserved fields are absent; readers must treat them as unknown, never zero/false.
            return facts;
        }
        private static JObject Fact(int value, int world, System.DateTime time) => new JObject {
            ["value"] = value, ["world"] = world, ["capturedAt"] = time.ToUniversalTime().ToString("o"), ["source"] = "game" };
    }
}
