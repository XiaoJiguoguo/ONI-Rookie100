using System.Collections.Generic;
using System.Linq;

namespace Rookie100
{
    // Pure evaluation: one world and one duplicant must connect the whole working chain.
    public static class SanitationRules
    {
        public sealed class Facility
        {
            public string Kind;
            public int World;
            public bool Ready;
            public int Room = -1;
            public HashSet<int> Workers = new HashSet<int>();
        }

        public static Dictionary<string, int> Evaluate(IEnumerable<Facility> facilities, int world, bool witnessed)
        {
            var local = facilities.Where(f => f.World == world).ToList();
            var ready = local.Where(f => f.Ready).ToList();
            var result = new Dictionary<string, int>();
            foreach (string kind in new[] { "Outhouse", "WashBasin", "LiquidPumpingStation" })
                result["sanitation:" + kind] = ready.Any(f => f.Kind == kind) ? 1 : 0;
            result["sanitation:Room"] = local.Any(f => f.Kind == "Outhouse" && f.Room >= 0) ? 1 : 0;
            var pumpWorkers = new HashSet<int>(ready.Where(f => f.Kind == "LiquidPumpingStation").SelectMany(f => f.Workers));
            bool connected = ready.Where(f => f.Kind == "Outhouse" && f.Room >= 0).Any(toilet =>
                ready.Where(f => f.Kind == "WashBasin" && f.Room == toilet.Room).Any(basin =>
                    toilet.Workers.Any(id => basin.Workers.Contains(id) && pumpWorkers.Contains(id))));
            result["sanitation:Connected"] = connected ? 1 : 0;
            result["sanitation:Trial"] = witnessed ? 1 : 0;
            return result;
        }
    }
}
