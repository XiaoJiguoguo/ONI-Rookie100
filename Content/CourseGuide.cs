using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Rookie100.Content
{
    // Read-only teaching content. Never participates in progress or reward state.
    public sealed class CourseGuide
    {
        public string Id;
        public string Goal;
        public string Cause;
        public string[] Prerequisites;
        public string[] Nodes;
        public object[][] Edges;
        public string[][] Layout;
        public string LayoutNote;
        public string[] Steps;
        public string[] Feedback;
        public string[] Buildings;

        private static readonly Dictionary<string, CourseGuide> guides = Load();
        public static CourseGuide Find(string id) => id != null && guides.TryGetValue(id, out var guide) ? guide : null;

        private static Dictionary<string, CourseGuide> Load()
        {
            try
            {
                using (var stream = typeof(CourseGuide).Assembly.GetManifestResourceStream("Rookie100.EarlyCourse.json"))
                using (var reader = new StreamReader(stream))
                {
                    var items = JsonConvert.DeserializeObject<List<CourseGuide>>(reader.ReadToEnd());
                    ModLogger.Log("[Phase4B] Loaded course guides: " + items.Count);
                    return items.ToDictionary(g => g.Id, StringComparer.Ordinal);
                }
            }
            catch (Exception e)
            {
                ModLogger.Warn("教学内容加载失败: " + e.Message);
                return new Dictionary<string, CourseGuide>();
            }
        }
    }
}
