using System;
using System.Collections.Generic;
using System.Linq;
using Rookie100.UI;

namespace Rookie100.Diagnostics
{
    internal enum SanitationCondition { Ready, NeedsDirt, NeedsWater, NeedsCleaning, NotOperational, Unknown }

    internal static class SanitationReadiness
    {
        internal static bool Supports(string tag) => tag == "Outhouse" || tag == "WashBasin";

        // Native readiness is distinct from IsActive: nobody needs to be using the
        // facility when claiming. This does not establish routing or door access.
        internal static SanitationCondition Read(BuildingComplete building, string tag)
        {
            if (building == null || building.gameObject == null || !building.gameObject.activeInHierarchy)
                return SanitationCondition.Unknown;
            try
            {
                var operational = building.GetComponent<Operational>();
                if (operational == null) return SanitationCondition.Unknown;
                if (!operational.IsOperational) return SanitationCondition.NotOperational;
                if (tag == "Outhouse")
                {
                    var smi = building.GetSMI<Toilet.StatesInstance>();
                    if (smi == null) return SanitationCondition.Unknown;
                    if (smi.IsInsideState(smi.sm.needsdirt)) return SanitationCondition.NeedsDirt;
                    if (smi.IsInsideState(smi.sm.full) || smi.IsInsideState(smi.sm.fullWaitingForClean) ||
                        smi.IsInsideState(smi.sm.earlyclean) || smi.IsInsideState(smi.sm.earlyWaitingForClean))
                        return SanitationCondition.NeedsCleaning;
                    return smi.IsInsideState(smi.sm.ready) && smi.GetFlushesRemaining() > 0
                        ? SanitationCondition.Ready : SanitationCondition.Unknown;
                }
                if (tag == "WashBasin")
                {
                    var smi = building.GetSMI<HandSanitizer.SMInstance>();
                    if (smi == null) return SanitationCondition.Unknown;
                    if (smi.OutputFull() || smi.IsInsideState(smi.sm.full)) return SanitationCondition.NeedsCleaning;
                    if (!smi.IsReady()) return SanitationCondition.NeedsWater;
                    return smi.IsInsideState(smi.sm.ready) ? SanitationCondition.Ready : SanitationCondition.Unknown;
                }
                return SanitationCondition.Unknown;
            }
            catch
            {
                // Polling failures must fail closed without flooding Player.log.
                return SanitationCondition.Unknown;
            }
        }

        internal static string Explain(string tag, Dictionary<string, int> counts)
        {
            if (!Supports(tag)) return Lang.T("状态尚未确认，请查看建筑原生状态并稍后复查。");
            if (counts == null || !counts.TryGetValue(tag, out int built) || built == 0)
                return Lang.T(tag == "Outhouse" ? "尚未建成户外厕所。厕所需要泥土；洗手盆才需要用水。" : "尚未建成洗手盆。建议放在离开厕所的路线上，并检查允许洗手的方向。");
            var snapshot = counts as QuestBuildingSnapshot;
            if (snapshot == null) return Message(SanitationCondition.Unknown);
            if (snapshot.Ready.TryGetValue(tag, out int ready) && ready > 0)
                return Message(SanitationCondition.Ready);
            if (!snapshot.SanitationIssues.TryGetValue(tag, out var issues) || issues.Count == 0)
                return Message(SanitationCondition.Unknown);
            return string.Join("\n", issues.OrderBy(i => i).Select(Message));
        }

        private static string Message(SanitationCondition condition)
        {
            switch (condition)
            {
                case SanitationCondition.Ready: return Lang.T("至少一座设施具备基本使用条件。复制人通路、门权限和如厕后的洗手路线尚未自动核验。");
                case SanitationCondition.NeedsDirt: return Lang.T("厕所正在等待泥土补给。检查泥土库存、复制人通路、运送安排和建筑优先级；给洗手盆送水不能代替厕所的泥土。");
                case SanitationCondition.NeedsWater: return Lang.T("洗手盆内的水不足以使用。准备可取用的水源或已有瓶装水；若用手压泵，确认能取到所需的水，再检查复制人的取水、运送通路和优先级。仅建好手压泵不代表已经送水。");
                case SanitationCondition.NeedsCleaning: return Lang.T("设施需要清理或正在等待清理完成。查看原生状态，安排复制人处理并检查通路；完成后会重新检查可用状态。");
                case SanitationCondition.NotOperational: return Lang.T("游戏当前不允许这座设施使用。查看原生状态是否有禁用、损坏或其他限制；不能仅凭这个状态推断为缺水或缺泥土。");
                default: return Lang.T("状态尚未确认，请查看建筑原生状态并稍后复查。");
            }
        }
    }
}
