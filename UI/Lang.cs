using System;
using System.Collections.Generic;
using System.Reflection;

namespace Rookie100.UI
{
    /// <summary>
    /// 面板 UI 文案：语言跟随游戏本体语言（Localization locale），无开关、无独立持久化。
    /// 中文为源文案，英文经 zh-key 字典查表（Lang.T(中文) 取当前语言）。
    /// 游戏语言在主菜单选定、进游戏后不变，故 Load() 时读取一次即可。
    /// 建筑/科技/元素名一律使用游戏本体本地化串，不再维护平行语料。
    /// </summary>
    public static class Lang
    {
        /// <summary>当前是否英文界面（locale 非 zh* 一律按英文处理；探测失败默认中文）。</summary>
        public static bool English { get; private set; }

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            { "状态尚未确认，请查看建筑原生状态并稍后复查。", "Status is not confirmed. Consult the building's native status and check again shortly." },
            { "尚未建成户外厕所。厕所需要泥土；洗手盆才需要用水。", "No completed outhouse found. An outhouse needs dirt; the wash basin needs water." },
            { "尚未建成洗手盆。建议放在离开厕所的路线上，并检查允许洗手的方向。", "No completed wash basin found. Place one on the toilet exit route and check its permitted washing direction." },
            { "至少一座设施具备基本使用条件。复制人通路、门权限和如厕后的洗手路线尚未自动核验。", "At least one facility meets basic use conditions. Duplicant access, door permissions and the route from toilet to handwashing have not been automatically verified." },
            { "厕所正在等待泥土补给。检查泥土库存、复制人通路、运送安排和建筑优先级；给洗手盆送水不能代替厕所的泥土。", "The outhouse is waiting for dirt. Check dirt stocks, duplicant access, delivery assignments and priority. Water for the wash basin cannot replace dirt for the outhouse." },
            { "洗手盆内的水不足以使用。准备可取用的水源或已有瓶装水；若用手压泵，确认能取到所需的水，再检查复制人的取水、运送通路和优先级。仅建好手压泵不代表已经送水。", "The wash basin lacks enough water for use. Provide an accessible source or existing bottled water. If using a pitcher pump, check that it can collect the required water, then check duplicant collection, delivery access and priorities. Building a pump does not establish that water was delivered." },
            { "设施需要清理或正在等待清理完成。查看原生状态，安排复制人处理并检查通路；完成后会重新检查可用状态。", "The facility needs cleaning or is awaiting its completion. Consult the native status, assign duplicants and check access; readiness is checked again after completion." },
            { "游戏当前不允许这座设施使用。查看原生状态是否有禁用、损坏或其他限制；不能仅凭这个状态推断为缺水或缺泥土。", "The game currently prevents this facility from being used. Check its native status for disabling, damage or other restrictions; this alone does not establish a water or dirt shortage." },
            { "检查选中的氧气扩散器", "Check selected Oxygen Diffuser" },
            { "诊断结果（可上下滚动）", "Diagnosis (scroll to read)" },
            { "请先在游戏中选中一台已建成的氧气扩散器，再打开此引导并点击检查。", "Select a completed Oxygen Diffuser in the world, then open this guide and click Check." },
            { "这是所选建筑的本次检查结果。多项问题可以同时存在；处理后让游戏运行片刻，再点击检查。结果较长时可上下滚动查看。", "Snapshot of the selected building. Multiple issues can coexist. After changes, let the game run briefly and check again. Scroll to read longer results." },
            { "暂时无法读取建筑状态，请重新选择建筑后再试。", "Building status is unavailable. Select the building again and retry." },
            { "【正在运行】\n当前正在制氧。单台建筑运行不代表整个殖民地氧气充足；请结合氧气分布和复制人的消耗判断。", "[Running]\nCurrently producing oxygen. One running building does not establish that the colony has enough oxygen; consider oxygen distribution and duplicant consumption." },
            { "【未接电线】\n原因：建筑电力接口未接入电线，无法获得工作所需的电力。\n检查：打开电力视图，沿建筑电力接口检查电线是否连续接到供电电路。", "[No power wire]\nWhy: the building power port is not wired, so it cannot receive operating power.\nCheck: open the power overlay and trace a continuous wire from the building port to a supplied circuit." },
            { "【已接线，但无电】\n原因：当前没有获得供电，不等同于手动或自动化关闭。\n检查：打开电力视图，查看同一电路的发电机是否工作、电池是否有电，以及电线是否断开或损坏。", "[Wired, but unpowered]\nWhy: the building currently receives no power. This is separate from manual or automation shutdown.\nCheck: inspect generators, battery charge, and disconnected or damaged wires on the same circuit in the power overlay." },
            { "【建筑内藻类不足】\n原因：氧气扩散器消耗藻类制氧，建筑内的藻类不足以开始转换。有电也不能代替藻类。\n检查：先看建筑内容物和殖民地藻类库存；若有库存却未送达，再检查复制人能否到达、门的通行权限、运送工作安排和建筑优先级。这里没有判断殖民地是否已耗尽藻类。", "[Not enough algae inside this building]\nWhy: the Oxygen Diffuser consumes algae to produce oxygen. Its stored algae is below the amount needed to start conversion; electricity cannot replace algae.\nCheck: inspect building contents and colony algae stocks. If algae exists but is not delivered, check duplicant access, door permissions, delivery assignments and building priority. Colony-wide depletion has not been checked." },
            { "【周围气压过高】\n原因：游戏已将这台建筑置于过压停机状态，暂时不能继续向周围排出氧气；这不代表缺电或缺藻类。\n检查：打开气体视图，查看建筑排气位置附近格子的气体质量，以及房间是否封闭、气体通道是否被砖块或门挡住。先确认有需要供氧的相邻空间，再改善气体流通；不要仅因过压就继续加建制氧设备。", "[High surrounding gas pressure]\nWhy: the game has placed this building in its overpressure state, temporarily stopping oxygen emission. This does not establish a power or algae shortage.\nCheck: inspect gas mass around the emission area in the gas overlay. Look for a sealed room or tiles and doors blocking gas movement. Confirm that adjacent space needs oxygen before improving gas flow; do not add more oxygen producers just because this one is overpressured." },
            { "【已被手动禁用】\n原因：建筑的手动开关当前为关闭，即使有电、有藻类也不会工作。\n检查：选中建筑，在原生操作面板中启用它；若操作需要复制人执行，等待操作完成后复查。", "[Manually disabled]\nWhy: the building manual switch is off. Power and algae alone will not make it run.\nCheck: enable the selected building in its native action panel. If a duplicant must perform the action, wait for completion and check again." },
            { "【被自动化关闭】\n原因：自动化控制当前不允许建筑运行，通常对应控制端口的红色信号；这与电路没有电是两回事。\n检查：打开自动化视图，沿控制端口检查线路、传感器阈值和逻辑门输出。如果这是你设计的停机条件，可以保持关闭。", "[Disabled by automation]\nWhy: automation currently prevents operation, usually a red signal at the control port. This is separate from a lack of electrical power.\nCheck: trace the control port wiring, sensor thresholds and logic gate outputs in the automation overlay. Leave it off if this shutdown is intentional." },
            { "【建筑已损坏】\n原因：游戏判定建筑已损坏，无法正常工作。\n检查：查看建筑原生状态中的损坏原因、维修材料和复制人通路；先处理持续损坏的原因，再安排维修。", "[Building broken]\nWhy: the game reports that the building is broken and cannot work normally.\nCheck: inspect its native status for damage causes, repair materials and duplicant access. Resolve ongoing damage before arranging repairs." },
            { "【还有其他运行条件未满足】\n已检查项目中未找到明确原因，但游戏仍不允许建筑运行。请查看建筑原生状态提示，不要据此直接判定为缺电、缺藻类或过压。", "[Other operating conditions unmet]\nThe checked items do not identify a specific cause, but the game still prevents operation. Consult the native status; do not assume missing power, algae or excessive pressure." },
            { "【当前未在制氧】\n已检查项目中未发现明确阻碍。建筑可能正在切换状态；让游戏运行片刻后重新检查，并对照原生状态提示。", "[Not currently producing oxygen]\nThe checked items show no specific blocker. The building may be changing state; let the game run briefly, check again, and consult its native status." },
            { "【部分状态未能读取】\n本次无法完成全部检查。已列出的原因仍可参考，未列出的原因不能据此排除；请结合建筑原生状态提示。", "[Some status information unavailable]\nNot every check could be completed. Listed findings still apply, but an unlisted cause has not been ruled out. Consult the native building status." },
            // 标题栏
            { "缺氧新手百天 · 任务指引", "Rookie 100 Days · Quest Guide" },
            { "已领取", "claimed" },
            { "重置全部任务进度（开始新一轮任务时使用）", "Reset all quest progress (for a fresh run)" },
            { "✕ 退出", "✕ Close" },
            { "关闭面板（Esc）", "Close panel (Esc)" },
            // 详情区
            { "暂无任务数据", "No quest data" },
            { "任务说明", "Brief" },
            { "任务目标", "Objectives" },
            { "知识任务：接取后即可领取奖励", "Knowledge quest: claim reward after accepting" },
            { "打印舱奖励", "Printing Pod rewards" },
            { "需要先完成并领取：", "Complete and claim first: " },
            { "▶ 接取任务", "▶ Accept" },
            { "已接取，按目标开始建造吧", "Accepted - start building per the objectives" },
            { "任务进行中，继续完成剩余目标", "In progress - keep working on objectives" },
            { "领取奖励！", "Claim Reward!" },
            { "任务已完成，继续下一个任务！", "Done - continue to the next quest!" },
            { "视频讲解章节", "Video chapters" },
            { "内容来源：B站UP主「大叔追云彩」《缺氧新手活过100天》合辑 · 仅提纲式引用，非转载；如侵权我将删除该功能", "Source: Bilibili creator \"Uncle Chasing Clouds\" — outline reference only; will be removed upon infringement claim." },
            { "▶ 展开章节（", "▶ Show chapters (" },
            { " 段，点击跳转对应时刻）", " segments; click to jump to that moment)" },
            { "▼ 收起章节", "▼ Hide chapters" },
            { "查看建筑大图标与解锁所需科技", "View building icon and required research" },
            // 引导卡
            { "建筑引导", "Building Guide" },
            { "科技详情", "Tech Details" },
            { "← 返回「", "← Back to 「" },
            { "知道了", "Got it" },
            { "解锁该建筑所需的研究（点击查看详情）：", "Research required to unlock this building (click for details):" },
            { "已研究", "Researched" },
            { "可研究", "Researchable" },
            { "先决未达成", "Prerequisites missing" },
            { "无前置科技：开局即可建造。", "No prerequisite tech: available from the start." },
            { "前置满足，可开始研究", "Prerequisites met - researchable" },
            { "前置未完成，请先研究链上科技", "Prerequisites incomplete - research the chain first" },
            { "该科技解锁的建筑：", "Buildings unlocked by this tech:" },
            { "无建造类解锁项。", "No building unlocks." },
            { "研究点需求：", "Research cost: " },
            { "需要", "needs" },
            { "已建造", "built" },
            { "未建造", "not built" },
            { "、", ", " },
            { "研究「", "To research 「" },
            { "」需要先建造研究站：", "」, build this station first: " },
            { "。请立即建造以开启研究！", ". Build it now to start research!" },
            { "打开研究面板", "Open Research Panel" },
            { "基础研究", "Basic Research" },
            { "高级研究", "Advanced Research" },
            { "星际研究", "Space Research" },
            { "辐射研究", "Nuclear Research" },
            { "轨道研究", "Orbital Research" },
            { "应用研究", "Applied Research" },
            // 材料侧栏
            { "建造材料　▼", "Materials　▼" },
            { "收起建造材料清单", "Collapse materials" },
            { "展开建造材料清单", "Expand materials" },
            { "推荐布局", "Suggested layout" },
            { "造价数据不可用。", "Build cost unavailable." },
            { "无需建造材料。", "No construction materials needed." },
            { "（材料目录未匹配到可用元素）", "(no matching elements found)" },
            { " 等", " and " },
            { "种", " more" },
            { "　可用：", "　available: " },
            { "获取方式", "How to obtain" },
            { "（获取方式见游戏内元素数据库）", "(see the in-game element database)" },
            // 通知
            { "任务完成：「", "Quest complete: 「" },
            { "」——到打印舱面板领取奖励！", "」 - claim the reward at the Printing Pod!" },
            { "已领取「", "Claimed 「" },
            { "」奖励！下一任务已解锁。", "」! Next quest unlocked." },
            // 管理菜单
            { "百天助手", "100 Days" },
            { " 吨", " t" },
            { "新手百天助手：从入门到太空的分阶段课程（大叔追云彩《活过100天》系列）", "Rookie 100 Days: staged lessons from beginner to space (based on the Survive-100-Days series by Uncle Chasing Clouds)" },
        };

        /// <summary>读取游戏本体语言（locale 以 zh 开头视为中文）。在模组 OnLoad 时调用一次。</summary>
        public static void Load()
        {
            string code = DetectGameLocale();
            English = !string.IsNullOrEmpty(code) &&
                      !code.Trim().StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            ModLogger.Log($"界面语言跟随游戏设置: locale='{code ?? "<null>"}' -> {(English ? "English" : "中文")}");
        }

        /// <summary>取当前语言文案：英文模式且有映射时返回英文，否则返回中文原文。</summary>
        public static string T(string zh)
        {
            return English && En.TryGetValue(zh, out var en) ? en : zh;
        }

        /// <summary>
        /// 反射探测游戏 locale。U59 二进制实锤 Localization 系存在
        /// GetLocale / GetCurrentLanguageCode / GetLanguageCode 及 sLocale 字段，
        /// 具体可用成员随版本而异，按候选链安全尝试，全程失败返回 null（默认中文）。
        /// </summary>
        private static string DetectGameLocale()
        {
            try
            {
                Type locType = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = asm.GetType("Localization");
                    if (t != null)
                    {
                        locType = t;
                        break;
                    }
                }

                if (locType == null)
                {
                    ModLogger.Warn("locale 探测: 未找到 Localization 类型，默认中文");
                    return null;
                }

                const BindingFlags StaticAny = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                foreach (MethodInfo m in locType.GetMethods(StaticAny))
                {
                    if (m.IsSpecialName || m.GetParameters().Length != 0 || m.ReturnType != typeof(string))
                    {
                        continue;
                    }

                    if (m.Name == "GetLocale" || m.Name == "GetCurrentLanguageCode" ||
                        m.Name == "GetLanguageCode" || m.Name == "GetLocaleCode")
                    {
                        string value = m.Invoke(null, null) as string;
                        ModLogger.Log($"locale 探测: Localization.{m.Name}() = '{value ?? "<null>"}'");
                        if (!string.IsNullOrEmpty(value))
                        {
                            return value;
                        }
                    }
                }

                foreach (PropertyInfo p in locType.GetProperties(StaticAny))
                {
                    if (!p.IsSpecialName && p.PropertyType == typeof(string) &&
                        (p.Name == "Locale" || p.Name == "CurrentLanguageCode"))
                    {
                        string value = p.GetValue(null, null) as string;
                        ModLogger.Log($"locale 探测: Localization.{p.Name} = '{value ?? "<null>"}'");
                        if (!string.IsNullOrEmpty(value))
                        {
                            return value;
                        }
                    }
                }

                FieldInfo f = locType.GetField("sLocale", StaticAny);
                if (f != null && f.FieldType == typeof(string))
                {
                    string value = f.GetValue(null) as string;
                    ModLogger.Log($"locale 探测: Localization.sLocale = '{value ?? "<null>"}'");
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }

                ModLogger.Warn("locale 探测: Localization 上所有候选成员均不可用，默认中文");
                return null;
            }
            catch (Exception e)
            {
                ModLogger.Warn("locale 探测异常: " + e.Message);
                return null;
            }
        }
    }
}
