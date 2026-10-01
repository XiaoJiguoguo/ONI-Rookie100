using System;
using System.Collections.Generic;
using System.Linq;

namespace Rookie100.Monitoring
{
    // Session-only observations. No monitoring data is written into quest/reward history.
    public sealed class DuplicantReading
    {
        public int Id, World;
        public string Name;
        public float? Health, Stress, Breath, Calories;
        public DuplicantReading Copy() => (DuplicantReading)MemberwiseClone();
    }

    public sealed class MonitorState
    {
        private List<DuplicantReading> visible = new List<DuplicantReading>();
        private int? selected;
        private string colony;
        public int World { get; private set; } = -1;
        public int Count => visible.Count;
        public DuplicantReading Current => visible.FirstOrDefault(x => x.Id == selected)?.Copy();
        public IReadOnlyList<DuplicantReading> Readings => visible.Select(x => x.Copy()).ToList();
        public void Apply(string colonyKey, int world, IEnumerable<DuplicantReading> readings)
        {
            if (!string.Equals(colony, colonyKey, StringComparison.Ordinal)) selected = null;
            colony = colonyKey; World = world;
            visible = (readings ?? Enumerable.Empty<DuplicantReading>()).Where(x => x != null && x.World == world)
                .GroupBy(x => x.Id).Select(x => x.First().Copy()).OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.Id).ToList();
            if (!visible.Any(x => x.Id == selected)) selected = visible.Count > 0 ? (int?)visible[0].Id : null;
        }
        public bool Move(int direction)
        {
            if (visible.Count == 0) return false;
            int index = visible.FindIndex(x => x.Id == selected);
            selected = visible[((index + (direction < 0 ? -1 : 1)) % visible.Count + visible.Count) % visible.Count].Id;
            return true;
        }
        public static float? Percent(float value, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0f) return null;
            return Math.Max(0f, Math.Min(100f, value / maximum * 100f));
        }
    }

    public sealed class QuestNoticeLink
    {
        public string QuestId { get; }
        public string ColonyKey { get; }
        public QuestNoticeLink(string questId, string colonyKey) { QuestId = questId; ColonyKey = colonyKey; }
        public bool CanOpen(string currentColony, Func<string, bool> questExists) =>
            !string.IsNullOrEmpty(QuestId) && !string.IsNullOrEmpty(ColonyKey) &&
            string.Equals(ColonyKey, currentColony, StringComparison.Ordinal) && questExists != null && questExists(QuestId);
    }
}
