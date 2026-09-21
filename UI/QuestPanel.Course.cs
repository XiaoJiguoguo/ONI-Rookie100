using System;
using System.Collections.Generic;
using System.Linq;
using Rookie100.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rookie100.UI
{
    public sealed partial class QuestPanel
    {
        private readonly HashSet<string> courseFolds = new HashSet<string>();
        private bool colonistsExpanded;
        private RectTransform colonistContent;
        private string colonistRoster = "";
        private readonly List<ColonistCard> colonistCards = new List<ColonistCard>();

        private static string CT(string cn, string en) => Lang.English ? en : cn;

        private GameObject CourseButton(string id, Transform parent, string title, System.Action action)
        {
            var button = MakeThinButton(id, parent, title, action, BlueBtn, BlueBtnHover);
            button.AddComponent<LayoutElement>().preferredHeight = 28f;
            button.GetComponentInChildren<TextMeshProUGUI>().color = WhiteText;
            return button;
        }

        private bool CourseFold(QuestDef quest, string section, string title)
        {
            string key = quest.Id + ":" + section;
            bool open = courseFolds.Contains(key);
            CourseButton("Course_" + section, detailContent, (open ? "▼ " : "▶ ") + title, () =>
            {
                if (!courseFolds.Remove(key)) courseFolds.Add(key);
                RefreshDetail();
            });
            return open;
        }

        private void BuildCourseGuide(QuestDef quest)
        {
            var guide = CourseGuide.Find(quest.Id);
            if (guide == null) return;
            CreateSectionTitle(StatusIcons.Knowledge, CT("因果与操作指南", "Why and how"), HeadingText);
            if (Lang.English)
                CreateBodyText("The expanded course is currently available in Chinese. Existing objectives and rewards remain bilingual.", MutedText, 10);
            CreateBodyText(guide.Goal, BlueText);
            if (CourseFold(quest, "prepare", CT("查看准备 · 资源与因果", "Preparation and resources")))
            {
                CreateBodyText(guide.Cause, BodyTextC);
                foreach (string text in guide.Prerequisites) CreateBodyText("• " + text, BodyTextC);
                CreateBodyText(CT("建筑与所需科技（点击查看）", "Buildings and required research (click to inspect)"), BlueText);
                foreach (string id in guide.Buildings)
                {
                    if (Assets.GetBuildingDef(id) == null) continue;
                    string buildingId = id;
                    var button = CourseButton("CourseBuilding_" + id, detailContent, LocalizeBuildingName(id) + "  ▶",
                        () => OpenGuideModal(ObjectiveForBuilding(buildingId)));
                    Sprite sprite = TryResolveSprite(id, out Color tint);
                    if (sprite != null) AddLeadingIcon(button, sprite, tint, 20f);
                }
                var chain = new List<TechGuideEntry>();
                foreach (string id in guide.Buildings) TryCollectTechChain(id, chain);
                var unique = chain.GroupBy(t => t.Id).Select(g => g.First()).ToList();
                foreach (var tech in unique)
                {
                    string techId = tech.Id;
                    CourseButton("CourseTech_" + techId, detailContent,
                        CT("定位研究：", "Locate research: ") + tech.DisplayName + (tech.Complete ? CT("（已研究）", " (complete)") : ""),
                        () => TryOpenResearchScreen(techId));
                }
                if (unique.Count == 0)
                    CourseButton("ResearchOverview", detailContent, CT("打开研究总览", "Open research overview"), () => TryOpenResearchScreen());
            }
            if (CourseFold(quest, "example", CT("查看示例 · 流程与参考布局", "Flow and example layout")))
            {
                CreateBodyText(CT("原理示意，不是经过实机验证的施工蓝图。", "Concept only; not a tested construction blueprint."), WarningText);
                foreach (var edge in guide.Edges)
                {
                    int from = Convert.ToInt32(edge[0]);
                    int to = Convert.ToInt32(edge[1]);
                    CreateBodyText(guide.Nodes[from] + "  ── " + edge[2] + " →  " + guide.Nodes[to], BlueText);
                }
                foreach (var cells in guide.Layout)
                {
                    var row = new GameObject("CourseLayoutRow", typeof(RectTransform));
                    row.transform.SetParent(detailContent, false);
                    var layout = row.AddComponent<HorizontalLayoutGroup>();
                    layout.spacing = 4f;
                    layout.childControlWidth = layout.childControlHeight = true;
                    layout.childForceExpandWidth = true;
                    layout.childForceExpandHeight = false;
                    foreach (string cell in cells)
                    {
                        var box = CreateBox("Zone", row.transform, RowBg);
                        var group = box.AddComponent<VerticalLayoutGroup>();
                        group.padding = new RectOffset(6, 6, 6, 6);
                        group.childControlWidth = group.childControlHeight = true;
                        group.childForceExpandHeight = false;
                        var label = CreateText("ZoneName", box.transform, cell, 11, TextAlignmentOptions.Center);
                        label.textWrappingMode = TextWrappingModes.Normal;
                        var size = box.AddComponent<LayoutElement>();
                        size.minWidth = 0f;
                        size.preferredWidth = 0f;
                        size.flexibleWidth = 1f;
                    }
                }
                CreateBodyText(guide.LayoutNote, MutedText);
            }
            if (CourseFold(quest, "check", CT("检查目标 · 学习自检", "Objectives and learning checklist")))
            {
                CreateBodyText(CT("当前领奖按上方任务目标判断。下面的学习自检需要你实际观察，不代表已经自动通过。",
                    "Rewards use the objectives above. The learning checklist below requires your own observation."), WarningText);
                CourseButton("CheckNow", detailContent, CT("刷新当前目标状态", "Refresh objective status"),
                    () => RefreshAll(QuestScanner.CountAllBuildings(forceRefresh: true)));
                for (int i = 0; i < guide.Steps.Length; i++) CreateBodyText((i + 1) + ". " + guide.Steps[i], BodyTextC);
                foreach (string text in guide.Feedback) CreateBodyText("• " + text, MutedText);
                foreach (var objective in quest.Objectives.GroupBy(o => o.Tag).Select(g => g.First()))
                {
                    var target = objective;
                    CourseButton("CheckGuide_" + objective.Tag, detailContent,
                        CT("查看设施：", "Inspect facility: ") + objective.LabelDisp, () => OpenGuideModal(target));
                }
            }
        }

        private sealed class ColonistCard
        {
            public MinionIdentity Identity;
            public TextMeshProUGUI Text;
            public TextMeshProUGUI Name;
        }

        private void BuildColonistCards()
        {
            CourseButton("ColonistsFold", detailContent, (colonistsExpanded ? "▼ " : "▶ ") + CT("复制人状态卡", "Duplicant status cards"), () =>
            {
                colonistsExpanded = !colonistsExpanded;
                RefreshDetail();
            });
            colonistContent = null;
            if (!colonistsExpanded) return;
            CreateBodyText(CT("当前星球的存活复制人；每 2 秒刷新。卡路里是个人储备，如厕百分比是膀胱充满程度；疾病不等同于身上携带病菌。",
                "Living duplicants on this world; refreshed every 2 seconds. Calories are personal reserves. Bladder % shows fullness; illness is different from carrying germs."), MutedText, 10);
            var content = new GameObject("ColonistCards", typeof(RectTransform));
            content.transform.SetParent(detailContent, false);
            colonistContent = content.GetComponent<RectTransform>();
            var group = content.AddComponent<VerticalLayoutGroup>();
            group.spacing = 5f;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            colonistRoster = "";
            RefreshColonistCards();
        }

        private void RefreshColonistCards()
        {
            if (!colonistsExpanded || colonistContent == null || !IsOpen() || ClusterManager.Instance == null) return;
            var roster = Components.LiveMinionIdentities.Items
                .Where(m => m != null && m.GetMyWorldId() == ClusterManager.Instance.activeWorldId)
                .OrderBy(m => m.GetInstanceID()).ToList();
            string key = Lang.English + ":" + string.Join(",", roster.Select(m => m.GetInstanceID()));
            if (key != colonistRoster)
            {
                colonistRoster = key;
                colonistCards.Clear();
                ClearChildren(colonistContent);
                foreach (var minion in roster)
                {
                    var identity = minion;
                    var card = CreateBox("DuplicantCard", colonistContent, RowBg);
                    var layout = card.AddComponent<VerticalLayoutGroup>();
                    layout.padding = new RectOffset(8, 8, 6, 6);
                    layout.spacing = 4f;
                    layout.childControlWidth = layout.childControlHeight = true;
                    layout.childForceExpandHeight = false;
                    var button = CourseButton("LocateDuplicant", card.transform,
                        identity.GetProperName() + CT(" · 定位", " · Locate"), () =>
                        {
                            if (identity == null || SelectTool.Instance == null) return;
                            if (identity.GetMyWorldId() != ClusterManager.Instance.activeWorldId)
                            {
                                RefreshColonistCards();
                                return;
                            }
                            var selectable = identity.GetComponent<KSelectable>();
                            if (selectable == null) return;
                            SelectTool.Instance.SelectAndFocus(identity.transform.position, selectable);
                            Close();
                        });
                    var personality = Db.Get().Personalities.TryGet(identity.personalityResourceId);
                    Sprite portrait = personality?.GetMiniIcon();
                    if (portrait != null) AddLeadingIcon(button, portrait, Color.white, 24f);
                    var label = CreateText("Vitals", card.transform, "", 12, TextAlignmentOptions.TopLeft);
                    label.textWrappingMode = TextWrappingModes.Normal;
                    colonistCards.Add(new ColonistCard { Identity = identity, Text = label, Name = button.GetComponentInChildren<TextMeshProUGUI>() });
                }
                if (roster.Count == 0)
                    CreateText("Empty", colonistContent, CT("此星球暂无存活复制人。", "No living duplicants on this world."), 12, TextAlignmentOptions.TopLeft);
                ModLogger.Log("[Phase4B] Duplicant cards refreshed: " + roster.Count);
            }
            foreach (var card in colonistCards)
            {
                if (card.Identity == null || card.Text == null) continue;
                card.Name.text = card.Identity.GetProperName() + CT(" · 定位", " · Locate");
                var calories = Db.Get().Amounts.Calories.Lookup(card.Identity);
                var bladder = Db.Get().Amounts.Bladder.Lookup(card.Identity);
                var sicknesses = card.Identity.modifiers?.sicknesses;
                string energy = calories == null ? CT("不适用／不可用", "N/A") : calories.GetValueString();
                string toilet = bladder == null || bladder.GetMax() <= 0 ? CT("不适用／不可用", "N/A") :
                    (Mathf.Clamp01(bladder.value / bladder.GetMax()) * 100f).ToString("0") + "%";
                string illness = sicknesses == null ? CT("状态不可用", "Unavailable") :
                    sicknesses.IsInfected() ? string.Join(CT("、", ", "), sicknesses.ModifierList
                        .Where(s => s != null && s.Sickness != null).Select(s => s.Sickness.Name)) : CT("未检测到当前疾病", "No current illness detected");
                card.Text.text = CT("卡路里：", "Calories: ") + energy + "\n" + CT("如厕需求：", "Bladder: ") + toilet + "\n" + CT("疾病：", "Illness: ") + illness;
            }
        }
    }
}
