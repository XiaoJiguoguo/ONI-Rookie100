using System.Collections.Generic;
using System.Linq;

namespace Rookie100
{
    // Native room recognition is supplied by the game adapter; do not duplicate its thresholds.
    public static class LivingRoomRules
    {
        public sealed class Facility
        {
            public string Kind;
            public int World;
            public bool Usable;
            public bool Reachable;
            public bool Recognized;
        }
        public static Dictionary<string, int> Evaluate(IEnumerable<Facility> facilities, int world)
        {
            var local = facilities.Where(f => f != null && f.World == world).ToList();
            var beds = local.Where(f => f.Kind == "Bed").ToList();
            var tables = local.Where(f => f.Kind == "DiningTable").ToList();
            return new Dictionary<string, int>
            {
                ["livingRoom:BedsUsable"] = beds.Any(f => f.Usable && f.Reachable) ? 1 : 0,
                ["livingRoom:Barracks"] = beds.Any(f => f.Usable && f.Reachable && f.Recognized) ? 1 : 0,
                ["livingRoom:TablesUsable"] = tables.Any(f => f.Usable && f.Reachable) ? 1 : 0,
                ["livingRoom:MessHall"] = tables.Any(f => f.Usable && f.Reachable && f.Recognized) ? 1 : 0
            };
        }
    }
}
