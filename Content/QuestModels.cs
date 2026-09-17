using System.Collections.Generic;
using System.Linq;
using Rookie100.UI;

namespace Rookie100.Content
{
    /// <summary>任务内容包（quests.json）。</summary>
    public class QuestContent
    {
        public int Version { get; set; }
        public List<QuestPhaseDef> Phases { get; set; } = new List<QuestPhaseDef>();
        public List<QuestDef> Quests { get; set; } = new List<QuestDef>();
    }

    /// <summary>任务阶段（阶段一 活下来 / 阶段二 工业化 / 阶段三 开发 / 阶段四 太空）。</summary>
    public class QuestPhaseDef
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Desc { get; set; }
        public string TitleEn { get; set; }
        public string DescEn { get; set; }

        /// <summary>当前语言显示标题。</summary>
        public string TitleDisp => Lang.English && !string.IsNullOrEmpty(TitleEn) ? TitleEn : Title;

        public string DescDisp => Lang.English && !string.IsNullOrEmpty(DescEn) ? DescEn : Desc;
    }

    /// <summary>
    /// 一个任务 = 一集视频的教学内容。
    /// 前置依赖 + 建造目标 + 打印舱奖励，全部完成后领取奖励解锁下一环。
    /// </summary>
    public class QuestDef
    {
        public string Id { get; set; }
        public string Phase { get; set; }
        public int Order { get; set; }
        public string Title { get; set; }
        public string TitleEn { get; set; }
        /// <summary>任务目标描述（基于视频内容提炼）。</summary>
        public string Description { get; set; }
        public string DescEn { get; set; }

        /// <summary>当前语言显示标题。</summary>
        public string TitleDisp => Lang.English && !string.IsNullOrEmpty(TitleEn) ? TitleEn : Title;

        /// <summary>当前语言显示说明。</summary>
        public string DescDisp => Lang.English && !string.IsNullOrEmpty(DescEn) ? DescEn : Description;

        /// <summary>前置任务 Id 列表（全部领取后本任务解锁）。</summary>
        public List<string> Requires { get; set; } = new List<string>();
        /// <summary>完成条件（全部达成任务完成）。</summary>
        public List<QuestObjectiveDef> Objectives { get; set; } = new List<QuestObjectiveDef>();
        /// <summary>打印舱奖励。</summary>
        public List<QuestRewardDef> Rewards { get; set; } = new List<QuestRewardDef>();
        /// <summary>参考视频。</summary>
        public string VideoUrl { get; set; }
        /// <summary>视频分段索引（"M:SS 标题" 格式）。</summary>
        public List<string> Segments { get; set; } = new List<string>();
        /// <summary>视频分段索引英文版（与 Segments 等长、索引对齐；可选）。</summary>
        public List<string> SegmentsEn { get; set; }
        /// <summary>推荐布局文字示意图（每行一段，等宽展示；可选）。</summary>
        public List<string> Layout { get; set; }
    }

    /// <summary>任务目标：buildingBuilt = 统计已建成建筑（Tag 为 PrefabID）。</summary>
    public class QuestObjectiveDef
    {
        public string Type { get; set; }
        public string Tag { get; set; }
        public int Count { get; set; } = 1;
        /// <summary>UI 显示名（如"户外厕所"）。</summary>
        public string Label { get; set; }
        public string LabelEn { get; set; }

        /// <summary>当前语言显示名。</summary>
        public string LabelDisp => Lang.English && !string.IsNullOrEmpty(LabelEn) ? LabelEn : Label;
    }

    /// <summary>打印舱奖励：一种元素物资。</summary>
    public class QuestRewardDef
    {
        public string Element { get; set; }
        public float Amount { get; set; }
        /// <summary>UI 显示名（如"沙子 1000kg"）。</summary>
        public string Label { get; set; }
        public string LabelEn { get; set; }

        /// <summary>当前语言显示名。</summary>
        public string LabelDisp => Lang.English && !string.IsNullOrEmpty(LabelEn) ? LabelEn : Label;
    }

    /// <summary>任务状态机。</summary>
    public enum QuestStatus
    {
        /// <summary>前置未领取，锁定。</summary>
        Locked,
        /// <summary>已解锁，未接取（可点"接取任务"）。</summary>
        Available,
        /// <summary>已接取，目标零进度。</summary>
        Accepted,
        /// <summary>已接取，目标部分达成。</summary>
        InProgress,
        /// <summary>目标全部达成，待领取奖励。</summary>
        Completed,
        /// <summary>已领取奖励。</summary>
        Claimed
    }
}