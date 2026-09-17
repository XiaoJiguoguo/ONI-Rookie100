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
