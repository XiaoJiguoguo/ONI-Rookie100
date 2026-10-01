using System.Collections.Generic;

namespace Rookie100
{
    // Pure hint selection. Missing observations are treated as unknown, never as evidence of success.
    public static class BeginnerGuidance
    {
        public sealed class Hint
        {
            public string Key, Chinese, English;
            public float DemoSeconds;
            public bool NeedsRepair;
        }
        public static Hint Get(string questId, Dictionary<string, int> counts, bool learned)
        {
            counts = counts ?? new Dictionary<string, int>();
            bool Has(string key) => counts.TryGetValue(key, out int value) && value > 0;
            Hint H(string key, string zh, string en, float seconds = 0f, bool ready = false) => new Hint
            { Key = key, Chinese = zh, English = en, DemoSeconds = seconds, NeedsRepair = learned && !ready };
            if (questId == "q01")
            {
                if (!Has("sanitation:OuthouseBuilt") && !Has("sanitation:WashBasinBuilt"))
                    return H("layout", "先规划茅房、洗手盆和入口，预留宿舍与餐厅；让离开厕所的动线经过洗手盆。", "Plan the outhouse, wash basin and entrance; reserve bedroom and dining space. The exit route should pass the basin.");
                if (!Has("sanitation:Outhouse"))
                {
                    if (!Has("sanitation:DirtKnown")) return H("inventory-unknown", "泥土库存暂不可用，先检查茅房补给与通路；不要把未知库存当成没有泥土。", "Dirt inventory is unknown. Check supply and access; unknown does not mean empty.", 3f);
                    if (!Has("sanitation:DirtAccessible")) return H("dirt", "当前可访问库存没有泥土：检查附近泥土与挖掘路线，为茅房准备补给。", "No accessible dirt is recorded. Check nearby dirt and excavation access for outhouse supplies.", 3f);
                    if (!Has("sanitation:OuthouseBuilt")) return H("build-toilet", "已有可访问泥土；建造茅房，并检查施工位置与材料数量。", "Accessible dirt exists. Build the outhouse and check construction access and material quantities.", 3f);
                    return H("delivery", "有泥土但茅房尚不可用：检查是否送到、搬运通路、优先级与清理状态，不必重复挖掘。", "Dirt exists but the outhouse is not usable. Check delivery, access, priorities and cleaning before digging more.", 3f);
                }
                if (!Has("sanitation:LiquidPumpingStation"))
                {
                    if (!Has("sanitation:PumpBuilt")) return H("pump", "查看附近清水，在复制人可达的取水位置建手压泵；按地形挖通或搭梯子，保留挡水地块。", "Find water and build a reachable pitcher pump. Dig access or use ladders as needed; retain water barriers.");
                    if (!Has("sanitation:PumpReachable")) return H("route", "手压泵已经建好，但当前复制人没有可达取水位置：检查通道与梯子，避免挖穿水池。", "The pitcher pump exists but no duplicant can reach its work position. Check paths and ladders without breaching the reservoir.");
                    return H("pump-water", "取水路线已通，但手压泵尚不能取清水：检查水源、泵的位置和设施状态。", "The pump is reachable but cannot collect water. Check water availability, placement and facility status.");
                }
                if (!Has("sanitation:WashBasin"))
                {
                    if (!Has("sanitation:WashBasinBuilt")) return H("build-basin", "补建洗手盆，将它放在离开厕所的路线上。", "Build a wash basin on the route out of the toilet.", 1.5f);
                    return H("basin-water", "洗手盆已经建成但尚不可用：先检查清水补给，再检查污水清理、通路与使用方向。", "The basin exists but is not usable. Check water delivery, wastewater clearing, access and direction.", 1.5f);
                }
                if (!Has("sanitation:Room")) return H("room", "设施已可用，房间仍未成立：打开原版房间叠层，检查围合、面积和指定设施条件。", "Facilities work but the room is not recognized. Use the native room overlay to check enclosure, size and required facilities.", 4.5f);
                if (!Has("sanitation:Connected")) return H("connected", "检查同一个复制人能否到达整套设施；茅房和洗手盆需在同一公共厕所。检查出口动线与洗手方向。", "Check that one duplicant reaches the full chain and that the outhouse and basin share a latrine. Review exit routing and basin direction.", 6f);
                if (!Has("sanitation:Trial")) return H("trial", "让同一复制人在同一公共厕所实际如厕后洗手；播放教学动画不算通过。", "Observe the same duplicant actually using the toilet then washing in the same latrine. Demo playback does not count.", 7.5f);
                return H("ready", "卫生间当前可用，六项检查通过。接下来安顿宿舍，再完善餐厅。", "All six sanitation checks pass. Set up the bedroom next, then the dining room.", 8.7f, true);
            }
            string prefix = questId == "q01_bedroom" ? "Beds" : "Tables";
            string room = questId == "q01_bedroom" ? "Barracks" : "MessHall";
            if (questId != "q01_bedroom" && questId != "q01_dining") return null;
            if (!Has("livingRoom:" + prefix + "Usable")) return H("facility", questId == "q01_bedroom" ? "先预留宿舍位置，让床铺建成、可用且可达；已做好的设施直接认可。" : "先预留餐厅位置，让用餐设施建成、可用且可达；需要研究时再按需解锁。", "Reserve the room and make its facilities built, usable and reachable. Existing working facilities count.");
            if (!Has("livingRoom:" + room)) return H("room", "设施已可用；打开原版房间叠层，按游戏提示补齐房间条件。", "Facilities work. Open the native room overlay and satisfy its remaining conditions.");
            return H("ready", "设施可用且房间识别通过；完成历史会保留，之后失效只提示修复。", "Facilities work and the room is recognized. Learning history remains if repairs are later needed.", 0f, true);
        }
    }
}
