using System.Collections.Generic;
using Rookie100.Content;

namespace Rookie100
{
    // Built counts retain the existing dictionary contract. Operating counts belong
    // to the same observation, but must never be added to the number of buildings.
    internal sealed class QuestBuildingSnapshot : Dictionary<string, int>
    {
        internal readonly Dictionary<string, int> Operating = new Dictionary<string, int>();
        internal readonly Dictionary<string, int> Ready = new Dictionary<string, int>();
        internal readonly Dictionary<string, HashSet<Diagnostics.SanitationCondition>> SanitationIssues =
            new Dictionary<string, HashSet<Diagnostics.SanitationCondition>>();

        internal QuestBuildingSnapshot(Dictionary<string, int> built) : base(built) { }

        internal static int ObjectiveCount(QuestObjectiveDef objective, Dictionary<string, int> counts)
        {
            if (objective == null || string.IsNullOrEmpty(objective.Tag) || counts == null) return 0;
            Dictionary<string, int> source = counts;
            if (objective.Type == "buildingOperating")
                source = (counts as QuestBuildingSnapshot)?.Operating;
            else if (objective.Type == "buildingReady")
                source = (counts as QuestBuildingSnapshot)?.Ready;
            else if (objective.Type != "buildingBuilt")
                return 0;
            int count;
            return source != null && source.TryGetValue(objective.Tag, out count) ? count : 0;
        }

        internal static bool IsMet(QuestObjectiveDef objective, Dictionary<string, int> counts)
        {
            return objective != null && objective.Count > 0 &&
                (objective.Type == "buildingBuilt" || objective.Type == "buildingOperating" || objective.Type == "buildingReady") &&
                ObjectiveCount(objective, counts) >= objective.Count;
        }
    }
}
