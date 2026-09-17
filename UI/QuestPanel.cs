using System.Collections.Generic;
using System.Linq;
using Rookie100.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rookie100.UI
{
    /// <summary>
    /// 新手百天任务面板（视觉对标 StorageNetwork：浅色官方主题 + web_box/web_button 精灵
    /// + 蓝色/粉色按钮体系 + 拖拽 + 位置记忆）。
    /// 左栏：阶段分组 + 任务列表（状态徽章 / 迷你进度条）
    /// 右栏：任务详情（接取按钮 / 目标图标计数 / 奖励图标 / 领取按钮 / 视频章节）
    /// 状态机：锁定 → 可接取 →（接取）→ 已接取 → 进行中 → 可领取 → 已领取。
    /// </summary>
    public sealed class QuestPanel : KScreen
    {
        private static QuestPanel instance;

        private readonly HashSet<string> expandedPhases = new HashSet<string>();
        private string selectedQuestId;
        private bool videoExpanded;
        private Dictionary<string, int> currentCounts;
        private TextMeshProUGUI headerTitle;

        private RectTransform windowRect;
        private RectTransform treeContent;
        private RectTransform detailContent;

        // ---- 配色（对应 StorageNetworkPanelPalette） ----
        private static readonly Color WindowBg = new Color(0.78f, 0.79f, 0.80f, 0.98f);
        /// <summary>官方风格红色窗口描边（窗框底色，内容区浅色内缩后露出红色边线）。</summary>
        private static readonly Color FrameBg = new Color(0.50f, 0.23f, 0.39f, 1f);
        private static readonly Color ContentBg = new Color(0.88f, 0.89f, 0.91f, 0.98f);
        private static readonly Color HeaderBg = new Color(0.43f, 0.20f, 0.34f, 1f);
        private static readonly Color TreeBg = new Color(0.82f, 0.81f, 0.75f, 1f);
        private static readonly Color DetailBg = new Color(0.88f, 0.89f, 0.91f, 0.98f);
        private static readonly Color RowBg = new Color(0.76f, 0.76f, 0.70f, 1f);
        private static readonly Color RowHover = new Color(0.86f, 0.85f, 0.79f, 1f);
        private static readonly Color RowSelected = new Color(0.93f, 0.84f, 0.57f, 1f);
        private static readonly Color RowClaimedBg = new Color(0.72f, 0.80f, 0.68f, 1f);
        private static readonly Color RowCompleteBg = new Color(0.88f, 0.81f, 0.60f, 1f);
        private static readonly Color HeadingText = new Color(0.18f, 0.19f, 0.18f, 1f);
        private static readonly Color BodyTextC = new Color(0.20f, 0.21f, 0.20f, 1f);
        private static readonly Color MutedText = new Color(0.34f, 0.35f, 0.33f, 1f);
        private static readonly Color HeaderText = new Color(0.96f, 0.96f, 0.90f, 1f);
        private static readonly Color BlueBtn = new Color(0.17f, 0.19f, 0.25f, 1f);
        private static readonly Color BlueBtnHover = new Color(0.25f, 0.28f, 0.35f, 1f);
        private static readonly Color PinkBtn = new Color(0.53f, 0.27f, 0.40f, 1f);
        private static readonly Color PinkBtnHover = new Color(0.62f, 0.33f, 0.47f, 1f);
        private static readonly Color PositiveText = new Color(0.28f, 0.48f, 0.34f, 1f);
        private static readonly Color WarningText = new Color(0.50f, 0.42f, 0.34f, 1f);
        private static readonly Color BlueText = new Color(0.20f, 0.28f, 0.45f, 1f);
        private static readonly Color BarBg = new Color(0.55f, 0.55f, 0.50f, 1f);
        private static readonly Color WhiteText = Color.white;

        // ---- 生命周期 ----

        public static bool IsOpen()
        {
            return instance != null && instance.gameObject != null && instance.gameObject.activeInHierarchy;
        }

        public static void Show()
        {
            if (instance == null)
            {
                try
                {
                    instance = Create();
                }
                catch (System.Exception e)
                {
                    ModLogger.Error("面板创建失败", e);
                    return;
                }
            }

            if (!instance.gameObject.activeSelf)
            {
                instance.gameObject.SetActive(true);
            }

            if (!instance.IsActive())
            {
                instance.Activate();
            }

            instance.RefreshAll();
        }

        private static QuestPanel Create()
        {
            Transform parent = GameScreenManager.Instance != null
                ? GameScreenManager.Instance.ssOverlayCanvas.transform
                : null;
            var root = new GameObject("Rookie100QuestPanel");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image blocker = root.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.10f);
            blocker.raycastTarget = false;

            QuestPanel panel = root.AddComponent<QuestPanel>();
            panel.activateOnSpawn = false;
            panel.ConsumeMouseScroll = true;
            QuestTracker.QuestCompleted += OnQuestEvent;
            QuestTracker.QuestClaimed += OnQuestEvent;
            try
            {
                panel.BuildWindow(root.transform);
            }
            catch
            {
                root.SetActive(false);
                Destroy(root);
                throw;
            }

            root.SetActive(false);
            return panel;
        }

        private static void OnQuestEvent(QuestDef quest)
        {
            if (instance == null || !IsOpen())
            {
                return;
            }

            // 领取奖励后自动跳到下一任务，任务链一目了然
            if (quest != null && quest.Id == instance.selectedQuestId && QuestStore.IsClaimed(quest.Id))
            {
                var ordered = QuestStore.OrderedQuests;
                int index = ordered.FindIndex(q => q.Id == quest.Id);
                if (index >= 0 && index + 1 < ordered.Count)
                {
                    instance.selectedQuestId = ordered[index + 1].Id;
                    var next = ordered[index + 1];
                    if (!string.IsNullOrEmpty(next.Phase))
                    {
                        instance.expandedPhases.Add(next.Phase);
                    }
                }
            }

            instance.RefreshAll();
        }

        private void Close()
        {
            if (IsActive())
            {
                Deactivate();
            }

            gameObject.SetActive(false);
        }

        public override void OnKeyDown(KButtonEvent e)
        {
            if (e.Consumed)
            {
                return;
            }

            if (e.TryConsume(global::Action.Escape))
            {
                Close();
            }
        }

        // ---- 状态展示 ----

        private struct StatusStyle
        {
            public string Glyph;
            public Color Color;
        }

        private static StatusStyle StyleOf(QuestStatus status)
        {
            switch (status)
            {
                case QuestStatus.Locked:
                    return new StatusStyle { Glyph = "🔒", Color = MutedText };
                case QuestStatus.Available:
                    return new StatusStyle { Glyph = "⚪", Color = BodyTextC };
                case QuestStatus.Accepted:
                    return new StatusStyle { Glyph = "◈", Color = BlueText };
                case QuestStatus.InProgress:
                    return new StatusStyle { Glyph = "❂", Color = BlueText };
                case QuestStatus.Completed:
                    return new StatusStyle { Glyph = "★", Color = WarningText };
                default:
                    return new StatusStyle { Glyph = "✓", Color = PositiveText };
            }
        }

        private static Color RowColorOf(QuestStatus status, bool selected)
        {
            if (selected)
            {
                return RowSelected;
            }

            switch (status)
            {
                case QuestStatus.Completed:
                    return RowCompleteBg;
                case QuestStatus.Claimed:
                    return RowClaimedBg;
                default:
                    return RowBg;
            }
        }

        // ---- 窗口构建 ----

        private void BuildWindow(Transform parent)
        {
            // 窗口：酒红色官方描边（web_box 九宫格边缘露出即红色边线）
            GameObject window = CreateBox("Window", parent, FrameBg);
            ApplyThinBoxSprite(window.GetComponent<Image>());
            windowRect = window.GetComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.anchoredPosition = Vector2.zero;
            windowRect.sizeDelta = new Vector2(940f, 640f);
            if (!WindowDrag.TryApplyLayout(windowRect, new Vector2(940f, 640f)))
            {
                windowRect.sizeDelta = new Vector2(940f, 640f);
            }

            // 标题栏（可拖拽，酒红色与游戏 HUD 一致）
            GameObject header = CreateBox("Header", window.transform, HeaderBg);
            SetTopStretch(header.GetComponent<RectTransform>(), 6f, 6f, 6f, 30f);
            header.AddComponent<WindowDrag>().Configure(windowRect);

            headerTitle = CreateText("Title", header.transform, "缺氧新手百天 · 任务指引", 14,
                TextAlignmentOptions.MidlineLeft);
            headerTitle.fontStyle = FontStyles.Bold;
            headerTitle.color = HeaderText;
            headerTitle.raycastTarget = false;
            Stretch(headerTitle.rectTransform(), 12f, 0f);
            headerTitle.rectTransform().offsetMax = new Vector2(-200f, 0f);

            // 重置按钮
            GameObject resetButton = MakeThinButton("ResetButton", header.transform, "⟲",
                () =>
                {
                    QuestStore.ResetAll();
                    RefreshAll();
                }, BlueBtn, BlueBtnHover, 12);
            RectTransform resetRect = resetButton.GetComponent<RectTransform>();
            resetRect.anchorMin = new Vector2(1f, 0.5f);
            resetRect.anchorMax = new Vector2(1f, 0.5f);
            resetRect.pivot = new Vector2(1f, 0.5f);
            resetRect.anchoredPosition = new Vector2(-92f, 0f);
            resetRect.sizeDelta = new Vector2(28f, 22f);
            resetButton.GetComponentInChildren<TextMeshProUGUI>().color = HeaderText;
            resetButton.AddComponent<ToolTip>().SetSimpleTooltip("重置全部任务进度（开始新一轮任务时使用）");

            // 退出按钮
            GameObject exitButton = MakeThinButton("ExitButton", header.transform, "✕ 退出",
                Close, PinkBtn, PinkBtnHover, 12);
            RectTransform exitRect = exitButton.GetComponent<RectTransform>();
            exitRect.anchorMin = new Vector2(1f, 0.5f);
            exitRect.anchorMax = new Vector2(1f, 0.5f);
            exitRect.pivot = new Vector2(1f, 0.5f);
            exitRect.anchoredPosition = new Vector2(-56f, 0f);
            exitRect.sizeDelta = new Vector2(52f, 22f);
            exitButton.GetComponentInChildren<TextMeshProUGUI>().color = HeaderText;
            exitButton.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            exitButton.AddComponent<ToolTip>().SetSimpleTooltip("关闭面板（Esc）");

            // 主内容区（浅色，内缩露出红色描边）
            GameObject content = CreateBox("Content", window.transform, ContentBg);
            SetStretch(content.GetComponent<RectTransform>(), 2f, 2f, 2f, 38f);
            HorizontalLayoutGroup columns = content.AddComponent<HorizontalLayoutGroup>();
            columns.padding = new RectOffset(4, 4, 4, 4);
            columns.spacing = 4f;
            columns.childControlWidth = true;
            columns.childControlHeight = true;
            columns.childForceExpandWidth = false;
            columns.childForceExpandHeight = true;

            // 左栏：任务列表
            GameObject tree = CreateBox("Tree", content.transform, TreeBg);
            LayoutElement treeLayout = tree.AddComponent<LayoutElement>();
            treeLayout.minWidth = 250f;
            treeLayout.preferredWidth = 250f;
            treeLayout.flexibleWidth = 0f;

            GameObject treeViewport = new GameObject("TreeViewport");
            treeViewport.transform.SetParent(tree.transform, false);
            RectTransform treeViewportRect = treeViewport.AddComponent<RectTransform>();
            SetStretch(treeViewportRect, 4f, 4f, 4f, 4f);
            treeViewport.AddComponent<RectMask2D>();

            GameObject treeScrollObject = new GameObject("TreeContent");
            treeScrollObject.transform.SetParent(treeViewport.transform, false);
            treeContent = treeScrollObject.AddComponent<RectTransform>();
            treeContent.anchorMin = new Vector2(0f, 1f);
            treeContent.anchorMax = new Vector2(1f, 1f);
            treeContent.pivot = new Vector2(0.5f, 1f);
            treeContent.offsetMin = Vector2.zero;
            treeContent.offsetMax = Vector2.zero;
            VerticalLayoutGroup treeLayoutGroup = treeScrollObject.AddComponent<VerticalLayoutGroup>();
            treeLayoutGroup.spacing = 2f;
            treeLayoutGroup.padding = new RectOffset(3, 3, 4, 4);
            treeLayoutGroup.childControlWidth = true;
            treeLayoutGroup.childControlHeight = true;
            treeLayoutGroup.childForceExpandWidth = true;
            treeLayoutGroup.childForceExpandHeight = false;
            treeScrollObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect treeScroll = tree.AddComponent<ScrollRect>();
            treeScroll.viewport = treeViewportRect;
            treeScroll.content = treeContent;
            treeScroll.horizontal = false;
            treeScroll.scrollSensitivity = 25f;

            // 右栏：任务详情
            GameObject detail = CreateBox("Detail", content.transform, DetailBg);
            detail.AddComponent<LayoutElement>().flexibleWidth = 1f;

            GameObject detailViewportObject = new GameObject("DetailViewport");
            detailViewportObject.transform.SetParent(detail.transform, false);
            RectTransform detailViewport = detailViewportObject.AddComponent<RectTransform>();
            SetStretch(detailViewport, 12f, 12f, 10f, 10f);
            detailViewportObject.AddComponent<RectMask2D>();

            GameObject detailScrollObject = new GameObject("DetailContent");
            detailScrollObject.transform.SetParent(detailViewportObject.transform, false);
            detailContent = detailScrollObject.AddComponent<RectTransform>();
            detailContent.anchorMin = new Vector2(0f, 1f);
            detailContent.anchorMax = new Vector2(1f, 1f);
            detailContent.pivot = new Vector2(0.5f, 1f);
            detailContent.offsetMin = Vector2.zero;
            detailContent.offsetMax = Vector2.zero;
            VerticalLayoutGroup detailLayoutGroup = detailScrollObject.AddComponent<VerticalLayoutGroup>();
            detailLayoutGroup.spacing = 6f;
            detailLayoutGroup.padding = new RectOffset(4, 4, 4, 12);
            detailLayoutGroup.childControlWidth = true;
            detailLayoutGroup.childControlHeight = true;
            detailLayoutGroup.childForceExpandWidth = true;
            detailLayoutGroup.childForceExpandHeight = false;
            detailScrollObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect detailScroll = detail.AddComponent<ScrollRect>();
            detailScroll.viewport = detailViewport;
            detailScroll.content = detailContent;
            detailScroll.horizontal = false;
            detailScroll.scrollSensitivity = 30f;

            var phases = QuestStore.Phases;
            if (phases.Count > 0)
            {
                expandedPhases.Add(phases[0].Id);
            }

            SelectDefaultQuest();
        }

        private void SelectDefaultQuest()
        {
            var ordered = QuestStore.OrderedQuests;
            var preferred = ordered.FirstOrDefault(q =>
            {
                var status = QuestStore.GetStatus(q);
                return status == QuestStatus.Completed ||
                       status == QuestStatus.InProgress ||
                       status == QuestStatus.Accepted ||
                       status == QuestStatus.Available;
            });
            selectedQuestId = (preferred ?? ordered.FirstOrDefault())?.Id;
        }

        // ---- 刷新 ----

        private void RefreshAll()
        {
            currentCounts = QuestScanner.CountAllBuildings();
            if (headerTitle != null)
            {
                headerTitle.text = $"缺氧新手百天 · 任务指引（已领取 {QuestStore.ClaimedCount}/{QuestStore.Quests.Count}）";
            }

            RefreshTree();
            RefreshDetail();
        }

        private void RefreshTree()
        {
            ClearChildren(treeContent);

            foreach (var phase in QuestStore.Phases)
            {
                var quests = QuestStore.GetQuestsOfPhase(phase.Id);
                int claimed = quests.Count(q => QuestStore.IsClaimed(q.Id));
                bool expanded = expandedPhases.Contains(phase.Id);
                string arrow = expanded ? "▼" : "▶";

                GameObject phaseHeader = MakeFoldoutHeader(
                    $"Phase_{phase.Id}",
                    treeContent,
                    $"{arrow} {phase.Title}  ({claimed}/{quests.Count})",
                    () =>
                    {
                        if (!expandedPhases.Remove(phase.Id))
                        {
                            expandedPhases.Add(phase.Id);
                        }

                        RefreshTree();
                    },
                    RowBg,
                    RowHover,
                    12,
                    FontStyles.Bold,
                    true);
                phaseHeader.AddComponent<LayoutElement>().preferredHeight = 26f;

                if (!expanded)
                {
                    continue;
                }

                foreach (var quest in quests)
                {
                    CreateQuestRow(quest);
                }
            }
        }

        private void CreateQuestRow(QuestDef quest)
        {
            var status = QuestStore.GetStatus(quest, currentCounts);
            var style = StyleOf(status);
            bool selected = quest.Id == selectedQuestId;

            GameObject row = MakeFoldoutHeader(
                $"Quest_{quest.Id}",
                treeContent,
                $"  {style.Glyph} #{quest.Order:d2} {quest.Title}",
                () => SelectQuest(quest.Id),
                RowColorOf(status, selected),
                RowHover,
                11,
                FontStyles.Normal,
                false);
            row.AddComponent<LayoutElement>().preferredHeight = 25f;
            TextMeshProUGUI label = row.GetComponentInChildren<TextMeshProUGUI>();
            label.color = selected ? HeadingText : style.Color;
            label.overflowMode = TextOverflowModes.Ellipsis;

            // 迷你进度条（进行中任务的右侧展示）
            var total = quest.Objectives.Count;
            if (status == QuestStatus.InProgress && total > 0)
            {
                int met = quest.Objectives.Count(o => QuestStore.IsObjectiveMet(o, currentCounts));
                float fraction = total > 0 ? (float)met / total : 0f;
                Image bar = CreateMiniBar(label.rectTransform, fraction);
                bar.rectTransform.anchoredPosition = new Vector2(-24f, 0f);
                bar.rectTransform.sizeDelta = new Vector2(56f, 5f);
            }
        }

        private void RefreshDetail()
        {
            ClearChildren(detailContent);

            var quest = QuestStore.GetQuest(selectedQuestId);
            if (quest == null)
            {
                CreateSectionTitle("暂无任务数据", MutedText);
                return;
            }

            var status = QuestStore.GetStatus(quest, currentCounts);
            var style = StyleOf(status);
            var phase = QuestStore.Phases.FirstOrDefault(p => p.Id == quest.Phase);

            // 标题区
            CreateSectionTitle($"#{quest.Order:d2}  {quest.Title}", HeadingText, 16);
            CreateBodyText(phase != null ? phase.Title : quest.Phase, MutedText);

            // 任务说明
            CreateSectionTitle("📋 任务说明", HeadingText);
            CreateBodyText(quest.Description ?? quest.Title, BodyTextC);

            // 目标进度
            if (quest.Objectives.Count > 0)
            {
                CreateSectionTitle("🎯 任务目标", HeadingText);
                foreach (var objective in quest.Objectives)
                {
                    bool met = QuestStore.IsObjectiveMet(objective, currentCounts);
                    string text = $"{(met ? "✅" : "❂")} " + QuestStore.GetObjectiveStatusText(objective, currentCounts);
                    GameObject row = CreateIconRow(objective.Tag, text, met ? PositiveText : BlueText);
                    row.AddComponent<LayoutElement>().preferredHeight = 26f;
                }
            }
            else
            {
                CreateSectionTitle("🎯 任务目标", HeadingText);
                CreateBodyText("📖 知识任务：接取后即可领取奖励", MutedText);
            }

            // 奖励
            CreateSectionTitle("🎁 打印舱奖励", HeadingText);
            foreach (var reward in quest.Rewards)
            {
                GameObject row = CreateIconRow(reward.Element, $"✦ {reward.Label}", WarningText);
                row.AddComponent<LayoutElement>().preferredHeight = 26f;
            }

            CreateSpacer(4f);

            // 状态 / 操作按钮
            switch (status)
            {
                case QuestStatus.Locked:
                    {
                        // 具体指出缺失的前置任务
                        var missing = new List<string>();
                        foreach (var reqId in quest.Requires)
                        {
                            if (!QuestStore.IsClaimed(reqId))
                            {
                                var req = QuestStore.GetQuest(reqId);
                                missing.Add(req != null
                                    ? $"「#{req.Order:d2} {req.Title}」"
                                    : reqId);
                            }
                        }

                        CreateBodyText("🔒 需要先完成并领取：" + string.Join("、", missing), MutedText);
                        break;
                    }
                case QuestStatus.Available:
                    CreateAcceptButton(quest);
                    break;
                case QuestStatus.Accepted:
                    CreateBodyText("◈ 已接取，按目标开始建造吧", BlueText);
                    break;
                case QuestStatus.InProgress:
                    CreateBodyText("❂ 任务进行中，继续完成剩余目标", BlueText);
                    break;
                case QuestStatus.Completed:
                    CreateClaimButton(quest);
                    break;
                case QuestStatus.Claimed:
                    CreateBodyText("✓ 任务已完成，继续下一个任务！", PositiveText);
                    break;
            }

            // 视频参考章节
            CreateSpacer(4f);
            CreateSectionTitle("🎬 视频讲解章节", MutedText);
            GameObject videoButton = MakeThinButton("VideoToggle", detailContent,
                videoExpanded ? "▼ 收起章节" : "▶ 展开章节（" + quest.Segments.Count + " 段，点击跳转对应时刻）",
                () =>
                {
                    videoExpanded = !videoExpanded;
                    RefreshDetail();
                },
                BlueBtn, BlueBtnHover, 11, FontStyles.Normal);
            videoButton.GetComponentInChildren<TextMeshProUGUI>().color = WhiteText;
            videoButton.AddComponent<LayoutElement>().preferredHeight = 24f;

            if (videoExpanded)
            {
                foreach (var segment in quest.Segments.Take(12))
                {
                    string url = null;
                    var m = System.Text.RegularExpressions.Regex.Match(segment, @"^(\d{1,2}):(\d{2})\s+(.*)$");
                    if (m.Success && !string.IsNullOrEmpty(quest.VideoUrl))
                    {
                        int seconds = int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value);
                        url = quest.VideoUrl + "?t=" + seconds;
                    }

                    CreateKeyPointRow(segment, url);
                }
            }
        }

        private void CreateAcceptButton(QuestDef quest)
        {
            GameObject button = MakeThinButton("AcceptButton", detailContent,
                "▶ 接取任务",
                () =>
                {
                    if (QuestStore.AcceptQuest(quest.Id))
                    {
                        RefreshAll();
                    }
                },
                BlueBtn, BlueBtnHover, 14, FontStyles.Bold);
            LayoutElement layout = button.AddComponent<LayoutElement>();
            layout.preferredHeight = 38f;
            layout.minHeight = 38f;
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.color = WhiteText;
            label.alignment = TextAlignmentOptions.Center;
        }

        private void CreateClaimButton(QuestDef quest)
        {
            GameObject button = MakeThinButton("ClaimButton", detailContent,
                "🎁 领取奖励！",
                () =>
                {
                    if (QuestTracker.Instance != null)
                    {
                        QuestTracker.Instance.ClaimRewards(quest);
                    }
                },
                PinkBtn, PinkBtnHover, 14, FontStyles.Bold);
            LayoutElement layout = button.AddComponent<LayoutElement>();
            layout.preferredHeight = 40f;
            layout.minHeight = 40f;
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.color = WhiteText;
            label.alignment = TextAlignmentOptions.Center;
        }

        private void SelectQuest(string questId)
        {
            selectedQuestId = questId;
            // 自动展开选中任务所在的阶段
            var quest = QuestStore.GetQuest(questId);
            if (quest != null && !string.IsNullOrEmpty(quest.Phase))
            {
                expandedPhases.Add(quest.Phase);
            }

            RefreshAll();
        }

        // ---- UI 工具 ----

        private void CreateSectionTitle(string text, Color color, int size = 13)
        {
            GameObject go = new GameObject("SectionTitle");
            go.transform.SetParent(detailContent, false);
            go.AddComponent<RectTransform>();
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = color;
            label.raycastTarget = false;
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = size + 8f;
            layout.minHeight = size + 8f;
        }

        private TextMeshProUGUI CreateBodyText(string text, Color color, int size = 12)
        {
            GameObject go = new GameObject("Body");
            go.transform.SetParent(detailContent, false);
            go.AddComponent<RectTransform>();
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            go.AddComponent<LayoutElement>().minHeight = size + 6f;
            return label;
        }

        private void CreateSpacer(float height)
        {
            GameObject go = new GameObject("Spacer");
            go.transform.SetParent(detailContent, false);
            go.AddComponent<RectTransform>();
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
        }

        /// <summary>带游戏资源图标的行（图标来自 Assets.GetSprite，找不到时仅文本）。</summary>
        private GameObject CreateIconRow(string spriteKey, string text, Color textColor)
        {
            GameObject row = new GameObject("IconRow");
            row.transform.SetParent(detailContent, false);
            row.AddComponent<RectTransform>();
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(2, 2, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            GameObject iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(row.transform, false);
            iconObject.AddComponent<RectTransform>();
            LayoutElement iconLayout = iconObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 22f;
            iconLayout.preferredHeight = 22f;
            iconLayout.flexibleWidth = 0f;
            Image icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Sprite sprite = Assets.GetSprite(spriteKey);
            if (sprite != null)
            {
                icon.sprite = sprite;
            }
            else
            {
                icon.enabled = false;
            }

            TextMeshProUGUI label = CreateText("Label", row.transform, text, 12, TextAlignmentOptions.MidlineLeft);
            label.color = textColor;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            return row;
        }

        /// <summary>迷你进度条（挂在已有文本的 RectTransform 上）。</summary>
        private static Image CreateMiniBar(RectTransform parent, float fraction)
        {
            GameObject barObject = new GameObject("MiniBar");
            barObject.transform.SetParent(parent, false);
            RectTransform barRect = barObject.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(1f, 0.5f);
            barRect.anchorMax = new Vector2(1f, 0.5f);
            barRect.pivot = new Vector2(1f, 0.5f);
            Image background = barObject.AddComponent<Image>();
            background.color = BarBg;
            background.raycastTarget = false;

            GameObject fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(barRect, false);
            RectTransform fillRect = fillObject.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fill = fillObject.AddComponent<Image>();
            fill.color = BlueText;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = Mathf.Clamp01(fraction);
            fill.raycastTarget = false;
            return background;
        }

        /// <summary>视频分段行，点击跳转对应时刻。</summary>
        private void CreateKeyPointRow(string text, string url)
        {
            GameObject row = MakeFoldoutHeader("Segment", detailContent, "▶  " + text,
                () =>
                {
                    if (!string.IsNullOrEmpty(url))
                    {
                        Application.OpenURL(url);
                    }
                },
                RowBg, RowHover, 11, FontStyles.Normal, false);
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 22f;
            layout.minHeight = 22f;
            row.GetComponentInChildren<TextMeshProUGUI>().color = MutedText;
        }

        private static GameObject CreateBox(string name, Transform parent, Color color)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent, false);
            box.AddComponent<RectTransform>();
            Image image = box.AddComponent<Image>();
            image.color = color;
            image.type = Image.Type.Sliced;
            return box;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, int size,
            TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = size;
            textComponent.alignment = alignment;
            textComponent.color = BodyTextC;
            textComponent.raycastTarget = false;
            return textComponent;
        }

        /// <summary>
        /// 折叠头样式行（StorageNetwork CreateFoldoutHeader 模式）：
        /// KImage + ColorStyleSetting + KButton。textColorFollowStyle 会让文字跟随行状态色。
        /// </summary>
        private static GameObject MakeFoldoutHeader(
            string name, Transform parent, string label, System.Action onClick,
            Color normal, Color hover, int fontSize, FontStyles style, bool textColorFollowStyle)
        {
            GameObject rowObject = new GameObject(name);
            rowObject.transform.SetParent(parent, false);
            rowObject.AddComponent<RectTransform>();

            KImage image = rowObject.AddComponent<KImage>();
            image.type = Image.Type.Sliced;
            image.colorStyleSetting = CreateColorStyle(normal, hover, normal * 0.85f);
            image.ColorState = KImage.ColorSelector.Inactive;

            TextMeshProUGUI text = CreateText("Label", rowObject.transform, label, fontSize,
                TextAlignmentOptions.MidlineLeft);
            text.fontStyle = style;
            Stretch(text.rectTransform(), 8f, 0f);
            if (textColorFollowStyle)
            {
                // 阶段头文字跟随主题色（金色感）
                text.color = WarningText;
            }

            KButton button = rowObject.AddComponent<KButton>();
            button.bgImage = image;
            button.additionalKImages = new KImage[0];
            button.soundPlayer = new ButtonSoundPlayer();
            button.onClick += onClick;
            return rowObject;
        }

        /// <summary>薄按钮（web_button 精灵 + 蓝/粉按钮色）。</summary>
        private static GameObject MakeThinButton(
            string name, Transform parent, string label, System.Action onClick,
            Color normal, Color hover, int fontSize = 12, FontStyles style = FontStyles.Normal)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            buttonObject.AddComponent<RectTransform>();

            KImage image = buttonObject.AddComponent<KImage>();
            ApplyThinButtonSprite(image);
            image.colorStyleSetting = CreateColorStyle(normal, hover, normal * 0.8f);
            image.ColorState = KImage.ColorSelector.Inactive;

            TextMeshProUGUI text = CreateText("Label", buttonObject.transform, label, fontSize,
                TextAlignmentOptions.MidlineLeft);
            text.fontStyle = style;
            Stretch(text.rectTransform(), 8f, 0f);

            KButton button = buttonObject.AddComponent<KButton>();
            button.bgImage = image;
            button.additionalKImages = new KImage[0];
            button.soundPlayer = new ButtonSoundPlayer();
            button.onClick += onClick;
            return buttonObject;
        }

        private static ColorStyleSetting CreateColorStyle(Color normal, Color hover, Color pressed)
        {
            ColorStyleSetting setting = ScriptableObject.CreateInstance<ColorStyleSetting>();
            setting.inactiveColor = normal;
            setting.hoverColor = hover;
            setting.activeColor = pressed;
            setting.disabledColor = normal * 0.6f;
            setting.disabledActiveColor = setting.disabledColor;
            setting.disabledhoverColor = setting.disabledColor;
            return setting;
        }

        /// <summary>web_button 精灵薄按钮（StorageNetwork ApplyThinButtonSprite 模式）。</summary>
        private static void ApplyThinButtonSprite(KImage image)
        {
            if (image == null)
            {
                return;
            }

            Sprite sprite = Assets.GetSprite("web_button");
            if (sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            image.fillCenter = true;
        }

        /// <summary>web_box 精灵薄边框盒子（StorageNetwork ApplyThinBoxSprite 模式）。</summary>
        private static void ApplyThinBoxSprite(Image image)
        {
            if (image == null)
            {
                return;
            }

            Sprite sprite = Assets.GetSprite("web_box");
            if (sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            image.fillCenter = true;
        }

        private static void Stretch(RectTransform rect, float horizontal, float vertical)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        private static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTopStretch(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void ClearChildren(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }
    }
}