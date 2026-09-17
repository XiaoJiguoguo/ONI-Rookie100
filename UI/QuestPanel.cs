using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private bool sidebarExpanded;
        private Dictionary<string, int> currentCounts;
        private TextMeshProUGUI headerTitle;
        private float liveRefreshTimer;
        private string lastCountsSignature = string.Empty;

        private RectTransform windowRect;
        private RectTransform treeContent;
        private RectTransform detailContent;
        private RectTransform sidebarContent;
        private GameObject sidebarPanel;
        private GameObject guideModal;
        private object guideCurrent;
        private readonly List<object> guideBackStack = new List<object>();

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

        /// <summary>
        /// 面板打开期间周期刷新。事件只能覆盖"目标全部达成/领取"两类变化；
        /// "已接取→进行中"这类部分进度推进不产生事件（QuestTracker 只在全部达成时通知），
        /// 若开着面板建造，任务树徽章与迷你进度条会停在旧状态。
        /// 故每 2s 重扫建筑计数做签名比对，仅签名变化才整体刷新（无变化时不重建 UI，避免滚动位置被重置）。
        /// </summary>
        private void Update()
        {
            if (!IsOpen())
            {
                return;
            }

            liveRefreshTimer += Time.deltaTime;
            if (liveRefreshTimer < 2f)
            {
                return;
            }

            liveRefreshTimer = 0f;
            var counts = QuestScanner.CountAllBuildings();
            string signature = CountsSignature(counts);
            if (!string.Equals(signature, lastCountsSignature, StringComparison.Ordinal))
            {
                lastCountsSignature = signature;
                currentCounts = counts;
                RefreshAll();
            }
        }

        /// <summary>建筑计数签名：仅用于检测"部分进度推进"是否需要刷新 UI。</summary>
        private static string CountsSignature(Dictionary<string, int> counts)
        {
            var keys = new List<string>(counts.Keys);
            keys.Sort(StringComparer.Ordinal);
            return string.Join(",", keys.Select(k => k + "=" + counts[k]));
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
                if (guideModal != null)
                {
                    CloseGuideModal();
                    return;
                }

                Close();
            }
        }

        // ---- 状态展示 ----

        private struct StatusStyle
        {
            /// <summary>状态徽章精灵 key（StatusIcons 常量），Image 渲染，不用字体 glyph。</summary>
            public string Icon;
            public Color Color;
        }

        private static StatusStyle StyleOf(QuestStatus status)
        {
            switch (status)
            {
                case QuestStatus.Locked:
                    return new StatusStyle { Icon = StatusIcons.Locked, Color = MutedText };
                case QuestStatus.Available:
                    return new StatusStyle { Icon = StatusIcons.Available, Color = BodyTextC };
                case QuestStatus.Accepted:
                    return new StatusStyle { Icon = StatusIcons.Accepted, Color = BlueText };
                case QuestStatus.InProgress:
                    return new StatusStyle { Icon = StatusIcons.InProgress, Color = BlueText };
                case QuestStatus.Completed:
                    return new StatusStyle { Icon = StatusIcons.ReadyClaim, Color = WarningText };
                case QuestStatus.Claimed:
                    return new StatusStyle { Icon = StatusIcons.Claimed, Color = PositiveText };
                default:
                    // 未来新增枚举的兜底：按"可接取"外观显示，绝不误显示为已领取
                    return new StatusStyle { Icon = StatusIcons.Available, Color = MutedText };
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
            // 默认尺寸随屏幕缩放（小分辨率自动收缩，保持响应式）
            float scale = Mathf.Min(1f, (Screen.width - 32f) / 960f, (Screen.height - 32f) / 640f);
            var defaultSize = new Vector2(960f * scale, 640f * scale);
            windowRect.sizeDelta = defaultSize;
            if (!WindowDrag.TryApplyLayout(windowRect, defaultSize))
            {
                windowRect.sizeDelta = defaultSize;
            }

            // 标题栏（可拖拽，酒红色与游戏 HUD 一致；顶部 2px 红线 + 高 28 对齐 StorageNetwork）
            GameObject header = CreateBox("Header", window.transform, HeaderBg);
            SetTopStretch(header.GetComponent<RectTransform>(), 2f, 2f, 2f, 28f);
            header.AddComponent<WindowDrag>().Configure(windowRect);

            headerTitle = CreateText("Title", header.transform, Lang.T("缺氧新手百天 · 任务指引"), 14,
                TextAlignmentOptions.MidlineLeft);
            headerTitle.fontStyle = FontStyles.Bold;
            headerTitle.color = HeaderText;
            headerTitle.raycastTarget = false;
            Stretch(headerTitle.rectTransform(), 12f, 0f);
            headerTitle.rectTransform().offsetMax = new Vector2(-150f, 0f);

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
            resetRect.anchoredPosition = new Vector2(-112f, 0f);
            resetRect.sizeDelta = new Vector2(28f, 22f);
            resetButton.GetComponentInChildren<TextMeshProUGUI>().color = HeaderText;
            resetButton.AddComponent<ToolTip>().SetSimpleTooltip(Lang.T("重置全部任务进度（开始新一轮任务时使用）"));

            // 退出按钮
            GameObject exitButton = MakeThinButton("ExitButton", header.transform, Lang.T("✕ 退出"),
                Close, PinkBtn, PinkBtnHover, 12);
            RectTransform exitRect = exitButton.GetComponent<RectTransform>();
            exitRect.anchorMin = new Vector2(1f, 0.5f);
            exitRect.anchorMax = new Vector2(1f, 0.5f);
            exitRect.pivot = new Vector2(1f, 0.5f);
            exitRect.anchoredPosition = new Vector2(-56f, 0f);
            exitRect.sizeDelta = new Vector2(52f, 22f);
            exitButton.GetComponentInChildren<TextMeshProUGUI>().color = HeaderText;
            exitButton.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            exitButton.AddComponent<ToolTip>().SetSimpleTooltip(Lang.T("关闭面板（Esc）"));

            // 主内容区（浅色，内缩露出红色描边；底部预留叠甲声明栏高度）
            GameObject content = CreateBox("Content", window.transform, ContentBg);
            SetStretch(content.GetComponent<RectTransform>(), 2f, 2f, 2f, 66f);
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

            // 右栏：任务详情（左：详情滚动列；右：材料与布局侧栏 280px）
            GameObject detail = CreateBox("Detail", content.transform, DetailBg);
            detail.AddComponent<LayoutElement>().flexibleWidth = 1f;
            HorizontalLayoutGroup detailColumns = detail.AddComponent<HorizontalLayoutGroup>();
            detailColumns.padding = new RectOffset(4, 4, 4, 4);
            detailColumns.spacing = 4f;
            detailColumns.childControlWidth = true;
            detailColumns.childControlHeight = true;
            detailColumns.childForceExpandWidth = true;
            detailColumns.childForceExpandHeight = true;

            GameObject detailViewportObject = new GameObject("DetailViewport");
            detailViewportObject.transform.SetParent(detail.transform, false);
            detailViewportObject.AddComponent<RectTransform>();
            LayoutElement detailViewportLayout = detailViewportObject.AddComponent<LayoutElement>();
            detailViewportLayout.flexibleWidth = 1f;
            detailViewportLayout.minWidth = 400f;
            detailViewportObject.AddComponent<RectMask2D>();
            RectTransform detailViewport = detailViewportObject.GetComponent<RectTransform>();

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

            // 材料与布局侧栏（默认折叠成 30px 图标条，展开 240px；知识任务整栏隐藏）——底色与详情区一致
            GameObject sidebar = CreateBox("Sidebar", detail.transform, DetailBg);
            sidebarPanel = sidebar;
            LayoutElement sidebarLayout = sidebar.AddComponent<LayoutElement>();
            sidebarLayout.minWidth = 30f;
            sidebarLayout.preferredWidth = 30f;
            sidebarLayout.flexibleWidth = 0f;

            GameObject sidebarViewportObject = new GameObject("SidebarViewport");
            sidebarViewportObject.transform.SetParent(sidebar.transform, false);
            RectTransform sidebarViewport = sidebarViewportObject.AddComponent<RectTransform>();
            SetStretch(sidebarViewport, 4f, 4f, 4f, 4f);
            sidebarViewportObject.AddComponent<RectMask2D>();

            GameObject sidebarScrollObject = new GameObject("SidebarContent");
            sidebarScrollObject.transform.SetParent(sidebarViewportObject.transform, false);
            sidebarContent = sidebarScrollObject.AddComponent<RectTransform>();
            sidebarContent.anchorMin = new Vector2(0f, 1f);
            sidebarContent.anchorMax = new Vector2(1f, 1f);
            sidebarContent.pivot = new Vector2(0.5f, 1f);
            sidebarContent.offsetMin = Vector2.zero;
            sidebarContent.offsetMax = Vector2.zero;
            VerticalLayoutGroup sidebarLayoutGroup = sidebarScrollObject.AddComponent<VerticalLayoutGroup>();
            sidebarLayoutGroup.spacing = 2f;
            sidebarLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
            sidebarLayoutGroup.childControlWidth = true;
            sidebarLayoutGroup.childControlHeight = true;
            sidebarLayoutGroup.childForceExpandWidth = true;
            sidebarLayoutGroup.childForceExpandHeight = false;
            sidebarScrollObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sidebarScroll = sidebar.AddComponent<ScrollRect>();
            sidebarScroll.viewport = sidebarViewport;
            sidebarScroll.content = sidebarContent;
            sidebarScroll.horizontal = false;
            sidebarScroll.scrollSensitivity = 25f;

            // 底部叠甲声明栏（常驻可见，给官方/权利方看本意）
            GameObject disclaimer = CreateBox("Disclaimer", window.transform, ContentBg);
            SetBottomStretch(disclaimer.GetComponent<RectTransform>(), 2f, 2f, 2f, 60f);
            VerticalLayoutGroup disclaimerLayout = disclaimer.AddComponent<VerticalLayoutGroup>();
            disclaimerLayout.padding = new RectOffset(8, 8, 4, 4);
            disclaimerLayout.spacing = 1f;
            disclaimerLayout.childControlWidth = true;
            disclaimerLayout.childControlHeight = true;
            disclaimerLayout.childForceExpandWidth = true;
            disclaimerLayout.childForceExpandHeight = false;

            TextMeshProUGUI disclaimerCn = CreateText("DisclaimerCn", disclaimer.transform,
                "免责声明：本模组为无偿、非商业的教学辅助工具；任务内容仅对B站UP主「大叔追云彩」《缺氧新手活过100天》系列视频作提纲式引用，不含画面/音频/完整文案转载，著作权归原作者；本模组与 Klei Entertainment 无隶属关系，不修改付费内容；若涉侵权，请通过工坊留言或 GitHub Issues 联系，本人将立即删除相应功能。",
                9, TextAlignmentOptions.TopLeft);
            disclaimerCn.color = MutedText;

            TextMeshProUGUI disclaimerEn = CreateText("DisclaimerEn", disclaimer.transform,
                "Disclaimer: This is a free, non-commercial educational aid. Quest texts briefly reference (in outline form only) the video series by Bilibili creator \"Uncle Chasing Clouds\"; no footage, audio or full transcripts are reproduced and all rights belong to the creator. Not affiliated with Klei Entertainment; no paid-content modification. If any rights holder claims infringement, contact me via Workshop comments or GitHub Issues and the feature will be removed immediately.",
                9, TextAlignmentOptions.TopLeft);
            disclaimerEn.color = MutedText;

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
            lastCountsSignature = CountsSignature(currentCounts);
            if (headerTitle != null)
            {
                headerTitle.text = $"{Lang.T("缺氧新手百天 · 任务指引")}（{Lang.T("已领取")} {QuestStore.ClaimedCount}/{QuestStore.Quests.Count}）";
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
                    $"{arrow} {phase.TitleDisp}  ({claimed}/{quests.Count})",
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

            // 行 = 背景 KImage + 水平布局（建筑图标 | 状态徽章+标题 | 迷你进度条）
            GameObject rowObject = new GameObject($"Quest_{quest.Id}");
            rowObject.transform.SetParent(treeContent, false);
            rowObject.AddComponent<RectTransform>();
            KImage bg = rowObject.AddComponent<KImage>();
            bg.type = Image.Type.Sliced;
            bg.colorStyleSetting = CreateColorStyle(RowColorOf(status, selected), RowHover,
                RowColorOf(status, selected) * 0.85f);
            bg.ColorState = KImage.ColorSelector.Inactive;

            HorizontalLayoutGroup layout = rowObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(3, 3, 0, 0);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // 建筑图标（目标建筑原版 Sprite，找不到则用奖励元素图标）
            GameObject iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(rowObject.transform, false);
            iconObject.AddComponent<RectTransform>();
            LayoutElement iconLayout = iconObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 22f;
            iconLayout.preferredHeight = 22f;
            iconLayout.flexibleWidth = 0f;
            Image icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Sprite sprite = ResolveQuestIcon(quest, out Color tint);
            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.color = tint;
            }
            else
            {
                icon.enabled = false;
            }

            // 状态徽章（游戏原生精灵 Image，替代 emoji 文本前缀）
            Image badge = StatusIcons.CreateIconImage("StatusBadge", rowObject.transform, style.Icon, 15f);
            badge.color = style.Color;

            TextMeshProUGUI label = CreateText("Label", rowObject.transform,
                $"#{quest.Order:d2} {quest.TitleDisp}", 11, TextAlignmentOptions.MidlineLeft);
            label.color = selected ? HeadingText : style.Color;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            KButton button = rowObject.AddComponent<KButton>();
            button.bgImage = bg;
            button.additionalKImages = new KImage[0];
            button.soundPlayer = new ButtonSoundPlayer();
            string questId = quest.Id;
            button.onClick += () => SelectQuest(questId);

            rowObject.AddComponent<LayoutElement>().preferredHeight = 25f;

            // 迷你进度条（进行中任务的右侧展示）
            var total = quest.Objectives.Count;
            if (status == QuestStatus.InProgress && total > 0)
            {
                int met = quest.Objectives.Count(o => QuestStore.IsObjectiveMet(o, currentCounts));
                float fraction = total > 0 ? (float)met / total : 0f;
                Image bar = CreateMiniBar(label.rectTransform, fraction);
                bar.rectTransform.anchoredPosition = new Vector2(-10f, 0f);
                bar.rectTransform.sizeDelta = new Vector2(52f, 5f);
            }
        }

        /// <summary>任务图标：优先目标建筑 Sprite，其次奖励元素 Sprite。</summary>
        private static Sprite ResolveQuestIcon(QuestDef quest, out Color tint)
        {
            if (quest.Objectives != null)
            {
                foreach (var objective in quest.Objectives)
                {
                    Sprite sprite = TryResolveSprite(objective.Tag, out tint);
                    if (sprite != null)
                    {
                        return sprite;
                    }
                }
            }

            if (quest.Rewards != null)
            {
                foreach (var reward in quest.Rewards)
                {
                    Sprite sprite = TryResolveSprite(reward.Element, out tint);
                    if (sprite != null)
                    {
                        return sprite;
                    }
                }
            }

            tint = Color.white;
            return null;
        }

        private static readonly HashSet<string> spriteDiagnosed = new HashSet<string>();

        /// <summary>
        /// 精灵解析链：元素 id 别名纠正 → Assets.GetSprite（UI/元素图标） → Def.GetUISprite（建筑 UI 白模图标+着色）。
        /// Def 命中的白模必须应用返回的第二项 Color 才能显示游戏内正常颜色；
        /// 名为 unknown 的灰色兜底图视为未命中（它伴随 Missing prefab 警告，不能当真图标）。
        /// 每个 key 只诊断一次（写日志），便于确认解析命中率。
        /// </summary>
        private static Sprite TryResolveSprite(string rawKey, out Color tint)
        {
            tint = Color.white;
            if (string.IsNullOrEmpty(rawKey))
            {
                return null;
            }

            string key = QuestStore.CanonicalElementId(rawKey);

            Sprite sprite = Assets.GetSprite(key);
            string source = sprite != null ? "Assets" : null;
            if (sprite == null)
            {
                try
                {
                    var pair = Def.GetUISprite(key);
                    if (pair != null && pair.first != null && pair.first.name != "unknown")
                    {
                        sprite = pair.first;
                        tint = pair.second;
                        source = "Def";
                    }
                }
                catch
                {
                    // Def.GetUISprite 不可用时忽略
                }
            }

            if (!spriteDiagnosed.Contains(rawKey))
            {
                spriteDiagnosed.Add(rawKey);
                ModLogger.Log($"图标解析: {rawKey}{(rawKey != key ? " -> " + key : "")} -> {(sprite != null ? source + " 命中" : "全部未命中")}");
            }

            return sprite;
        }

        private void RefreshDetail()
        {
            ClearChildren(detailContent);
            ClearChildren(sidebarContent);

            var quest = QuestStore.GetQuest(selectedQuestId);
            if (quest == null)
            {
                CreateSectionTitle(Lang.T("暂无任务数据"), MutedText);
                return;
            }

            var status = QuestStore.GetStatus(quest, currentCounts);
            var style = StyleOf(status);
            var phase = QuestStore.Phases.FirstOrDefault(p => p.Id == quest.Phase);

            // 标题区（大图标 + 标题/阶段）
            CreateDetailHeader(quest, style, phase);

            // 任务说明
            CreateSectionTitle(StatusIcons.Brief, Lang.T("任务说明"), HeadingText);
            CreateBodyText(quest.DescDisp, BodyTextC);

            // 目标进度
            if (quest.Objectives.Count > 0)
            {
                CreateSectionTitle(StatusIcons.Objectives, Lang.T("任务目标"), HeadingText);
                foreach (var objective in quest.Objectives)
                {
                    bool met = QuestStore.IsObjectiveMet(objective, currentCounts);
                    GameObject row = CreateIconRow(objective.Tag,
                        QuestStore.GetObjectiveStatusText(objective, currentCounts),
                        met ? PositiveText : BlueText,
                        met ? StatusIcons.ObjectiveDone : StatusIcons.ObjectiveTodo);
                    row.AddComponent<LayoutElement>().preferredHeight = 26f;

                    // 建筑引导按钮：查看大图标与解锁所需的前置科技
                    QuestObjectiveDef guideObjective = objective;
                    GameObject guideButton = MakeIconButton($"Guide_{objective.Tag}", row.transform,
                        StatusIcons.GuideBuilding,
                        () => OpenGuideModal(guideObjective), BlueBtn, BlueBtnHover);
                    LayoutElement guideLayout = guideButton.AddComponent<LayoutElement>();
                    guideLayout.preferredWidth = 28f;
                    guideLayout.preferredHeight = 22f;
                    guideLayout.flexibleWidth = 0f;
                    guideButton.AddComponent<ToolTip>().SetSimpleTooltip(Lang.T("查看建筑大图标与解锁所需科技"));
                }
            }
            else
            {
                CreateSectionTitle(StatusIcons.Objectives, Lang.T("任务目标"), HeadingText);
                CreateIconTextLine(StatusIcons.Knowledge, Lang.T("知识任务：接取后即可领取奖励"), MutedText);
            }

            // 奖励
            CreateSectionTitle(StatusIcons.Rewards, Lang.T("打印舱奖励"), HeadingText);
            foreach (var reward in quest.Rewards)
            {
                GameObject row = CreateIconRow(reward.Element, reward.LabelDisp, WarningText, null);
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
                                    ? $"「#{req.Order:d2} {req.TitleDisp}」"
                                    : reqId);
                            }
                        }

                        CreateIconTextLine(StatusIcons.Locked,
                            Lang.T("需要先完成并领取：") + string.Join("、", missing), MutedText);
                        break;
                    }
                case QuestStatus.Available:
                    CreateAcceptButton(quest);
                    break;
                case QuestStatus.Accepted:
                    CreateIconTextLine(StatusIcons.Accepted, Lang.T("已接取，按目标开始建造吧"), BlueText);
                    break;
                case QuestStatus.InProgress:
                    CreateIconTextLine(StatusIcons.InProgress, Lang.T("任务进行中，继续完成剩余目标"), BlueText);
                    break;
                case QuestStatus.Completed:
                    CreateClaimButton(quest);
                    break;
                case QuestStatus.Claimed:
                    CreateIconTextLine(StatusIcons.Claimed, Lang.T("任务已完成，继续下一个任务！"), PositiveText);
                    break;
            }

            // 视频参考章节
            CreateSpacer(4f);
            CreateSectionTitle(StatusIcons.Video, Lang.T("视频讲解章节"), MutedText);
            CreateBodyText(Lang.T("内容来源：B站UP主「大叔追云彩」《缺氧新手活过100天》合辑 · 仅提纲式引用，非转载；如侵权我将删除该功能"), MutedText, 10);
            CreateBodyText("Source: Bilibili creator \"Uncle Chasing Clouds\" — outline reference only; will be removed upon infringement claim.", MutedText, 10);
            GameObject videoButton = MakeThinButton("VideoToggle", detailContent,
                videoExpanded ? Lang.T("▼ 收起章节") : Lang.T("▶ 展开章节（") + quest.Segments.Count + Lang.T(" 段，点击跳转对应时刻）"),
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
                bool useEnSegments = Lang.English && quest.SegmentsEn != null &&
                                     quest.SegmentsEn.Count == quest.Segments.Count;
                var segments = useEnSegments ? quest.SegmentsEn : quest.Segments;
                foreach (var segment in segments.Take(12))
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

            // 材料与布局侧栏
            BuildSidebar(quest);
        }

        // ---- 材料与布局侧栏 ----

        private static List<object> cachedElementList;
        private static bool elementListLoaded;

        /// <summary>全元素目录（ElementLoader.elements，反射缓存）。</summary>
        private static List<object> GetAllElements()
        {
            if (elementListLoaded)
            {
                return cachedElementList;
            }

            elementListLoaded = true;
            cachedElementList = new List<object>();
            try
            {
                var field = typeof(ElementLoader).GetField("elements", BindingFlags.Public | BindingFlags.Static);
                var list = field?.GetValue(null) as IEnumerable;
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        if (item != null)
                        {
                            cachedElementList.Add(item);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ModLogger.Warn("读取 ElementLoader.elements 失败: " + e.Message);
            }

            return cachedElementList;
        }

        /// <summary>元素携带的全部材料标签名（主 tag + oreTags）。</summary>
        private static List<string> GetElementTagNames(object element)
        {
            var names = new List<string>();
            try
            {
                var type = element.GetType();
                var tag = type.GetField("tag", BindingFlags.Public | BindingFlags.Instance);
                string primary = tag != null ? GetTagName(tag.GetValue(element)) : null;
                if (!string.IsNullOrEmpty(primary))
                {
                    names.Add(primary);
                }

                var oreTags = type.GetField("oreTags", BindingFlags.Public | BindingFlags.Instance);
                var oreList = oreTags?.GetValue(element) as IEnumerable;
                if (oreList != null)
                {
                    foreach (var t in oreList)
                    {
                        string n = GetTagName(t);
                        if (!string.IsNullOrEmpty(n))
                        {
                            names.Add(n);
                        }
                    }
                }
            }
            catch
            {
            }

            return names;
        }

        private static string GetTagName(object tag)
        {
            if (tag == null)
            {
                return null;
            }

            try
            {
                var prop = tag.GetType().GetProperty("Name");
                if (prop != null)
                {
                    return System.Convert.ToString(prop.GetValue(tag, null));
                }

                return System.Convert.ToString(tag);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>元素显示名（U59 的 Element.name 本身即当前游戏语言的本地化串，剥掉 link 标记即可）。</summary>
        private static string GetElementName(object element)
        {
            try
            {
                string raw = GetElementRawName(element);
                if (string.IsNullOrEmpty(raw))
                {
                    return "?";
                }

                return StripLinkMarkup(raw);
            }
            catch
            {
                return "?";
            }
        }

        /// <summary>剥离 &lt;link&gt; 等富文本标记。</summary>
        private static string StripLinkMarkup(string text)
        {
            return string.IsNullOrEmpty(text)
                ? text
                : System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "");
        }

        /// <summary>从 &lt;link="KEY"&gt; 标记中提取元素 key（如 ZINCORE），用于查官方描述。</summary>
        private static string ExtractLinkKey(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var m = System.Text.RegularExpressions.Regex.Match(text, "link=\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>元素官方描述（获取方式）：按元素名内 link 标记的 key 查当前游戏语言的 STRINGS。</summary>
        private static string GetElementDesc(object element)
        {
            try
            {
                string key = ExtractLinkKey(GetElementRawName(element));
                if (string.IsNullOrEmpty(key))
                {
                    return null;
                }

                return LocalizeString("STRINGS.ELEMENTS." + key + ".DESC");
            }
            catch
            {
                return null;
            }
        }

        private static string GetElementRawName(object element)
        {
            try
            {
                string name = null;
                var field = element.GetType().GetField("name", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    name = System.Convert.ToString(field.GetValue(element));
                }

                if (string.IsNullOrEmpty(name))
                {
                    var prop = element.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                    {
                        name = System.Convert.ToString(prop.GetValue(element, null));
                    }
                }

                return name;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>按材料类别字符串（"BuildableRaw&BuildingWood" 式 TagSet）解析可选材料。</summary>
        private static List<object> ResolveMaterialOptions(string categoryString)
        {
            var options = new List<object>();
            if (string.IsNullOrEmpty(categoryString))
            {
                return options;
            }

            var wanted = new HashSet<string>();
            foreach (var part in categoryString.Split('&'))
            {
                if (!string.IsNullOrEmpty(part))
                {
                    wanted.Add(part);
                }
            }

            foreach (var element in GetAllElements())
            {
                var tags = GetElementTagNames(element);
                bool hit = false;
                foreach (var t in tags)
                {
                    if (wanted.Contains(t))
                    {
                        hit = true;
                        break;
                    }
                }

                if (hit)
                {
                    options.Add(element);
                }
            }

            return options;
        }

        private static string FormatMass(float kg)
        {
            if (kg >= 1000f)
            {
                return (kg / 1000f).ToString("0.##") + Lang.T(" 吨");
            }

            return kg.ToString("0.#") + " kg";
        }

        private void BuildSidebar(QuestDef quest)
        {
            bool hasMaterials = quest.Objectives.Any(o => o.Type == "buildingBuilt" && !string.IsNullOrEmpty(o.Tag));

            // 知识任务（无建造目标）：整栏隐藏，详情区恢复全宽
            if (sidebarPanel != null)
            {
                sidebarPanel.SetActive(hasMaterials);
            }

            if (!hasMaterials)
            {
                return;
            }

            // 折叠宽度切换：折叠 30px 图标条 / 展开 240px 清单
            LayoutElement panelLayout = sidebarPanel != null ? sidebarPanel.GetComponent<LayoutElement>() : null;
            if (panelLayout != null)
            {
                float width = sidebarExpanded ? 240f : 30f;
                panelLayout.minWidth = width;
                panelLayout.preferredWidth = width;
            }

            // 折叠头：30px 宽时只显示材料图标，展开态显示图标+完整标题
            GameObject toggleRow = MakeFoldoutHeader("SidebarToggle", sidebarContent,
                sidebarExpanded ? Lang.T("建造材料　▼") : string.Empty, () =>
                {
                    sidebarExpanded = !sidebarExpanded;
                    RefreshDetail();
                },
                RowBg, RowHover, 11, FontStyles.Bold, false);
            TextMeshProUGUI toggleLabelText = toggleRow.GetComponentInChildren<TextMeshProUGUI>();
            toggleLabelText.color = HeadingText;
            toggleLabelText.overflowMode = TextOverflowModes.Ellipsis;

            // 材料图标：折叠态居中，展开态靠左
            Image toggleIcon = StatusIcons.CreateIconImage("HeadIcon", toggleRow.transform,
                StatusIcons.Materials, 15f);
            toggleIcon.color = HeadingText;
            RectTransform toggleIconRect = toggleIcon.rectTransform();
            if (sidebarExpanded)
            {
                toggleIconRect.anchorMin = new Vector2(0f, 0.5f);
                toggleIconRect.anchorMax = new Vector2(0f, 0.5f);
                toggleIconRect.pivot = new Vector2(0f, 0.5f);
                toggleIconRect.anchoredPosition = new Vector2(7f, 0f);
                toggleLabelText.rectTransform().offsetMin = new Vector2(28f, 0f);
            }
            else
            {
                toggleIconRect.anchorMin = new Vector2(0.5f, 0.5f);
                toggleIconRect.anchorMax = new Vector2(0.5f, 0.5f);
                toggleIconRect.pivot = new Vector2(0.5f, 0.5f);
                toggleIconRect.anchoredPosition = Vector2.zero;
                toggleLabelText.alignment = TextAlignmentOptions.Center;
            }

            toggleRow.AddComponent<LayoutElement>().preferredHeight = 24f;
            toggleRow.AddComponent<ToolTip>().SetSimpleTooltip(
                sidebarExpanded ? Lang.T("收起建造材料清单") : Lang.T("展开建造材料清单"));

            if (!sidebarExpanded)
            {
                return;
            }

            foreach (var objective in quest.Objectives)
            {
                if (string.IsNullOrEmpty(objective.Tag) || objective.Type != "buildingBuilt")
                {
                    continue;
                }

                BuildMaterialBlock(objective);
            }

            // 推荐布局（quests.json 可选 Layout 字段：等宽文字示意图；无内容时隐藏）
            if (quest.Layout != null && quest.Layout.Count > 0)
            {
                GameObject layHead = CreateIconTitleIn(sidebarContent, StatusIcons.Layout,
                    Lang.T("推荐布局"), HeadingText, 12);
                layHead.AddComponent<LayoutElement>().minHeight = 20f;

                foreach (var line in quest.Layout)
                {
                    TextMeshProUGUI row = CreateBodyTextIn(sidebarContent, line, BodyTextC, 10);
                    row.gameObject.AddComponent<LayoutElement>().minHeight = 14f;
                }
            }
        }

        private static readonly HashSet<string> materialDiagDone = new HashSet<string>();

        private void BuildMaterialBlock(QuestObjectiveDef objective)
        {
            // 分组标题条：与"展开章节"相同的 RowBg 头条样式
            string buildingName = string.IsNullOrEmpty(objective.LabelDisp) ? objective.Tag : objective.LabelDisp;
            GameObject headRow = MakeFoldoutHeader("MaterialHead", sidebarContent,
                $"▪ {buildingName}", () => { }, RowBg, RowHover, 12, FontStyles.Bold, false);
            headRow.GetComponentInChildren<TextMeshProUGUI>().color = BodyTextC;
            headRow.GetComponentInChildren<TextMeshProUGUI>().overflowMode = TextOverflowModes.Ellipsis;
            headRow.AddComponent<LayoutElement>().preferredHeight = 22f;

            try
            {
                BuildingDef bd = Assets.GetBuildingDef(objective.Tag);
                if (bd == null)
                {
                    TextMeshProUGUI miss = CreateBodyTextIn(sidebarContent, Lang.T("造价数据不可用。"), MutedText, 11);
                    miss.gameObject.AddComponent<LayoutElement>().minHeight = 16f;
                    return;
                }

                string[] cats = bd.MaterialCategory;
                float[] mass = bd.Mass;
                if (cats == null || cats.Length == 0)
                {
                    TextMeshProUGUI free = CreateBodyTextIn(sidebarContent, Lang.T("无需建造材料。"), MutedText, 11);
                    free.gameObject.AddComponent<LayoutElement>().minHeight = 16f;
                    return;
                }

                for (int i = 0; i < cats.Length; i++)
                {
                    float m = mass != null && i < mass.Length ? mass[i] : 0f;
                    var options = ResolveMaterialOptions(cats[i]);

                    // 一次性诊断：核对材料类别标签解析结果（登录 Player.log）
                    string diagKey = objective.Tag + "|" + cats[i];
                    if (materialDiagDone.Add(diagKey))
                    {
                        var sample = new List<string>();
                        foreach (var el in options.Take(3))
                        {
                            sample.Add(GetElementName(el));
                        }

                        ModLogger.Log($"[MatDiag] {objective.Tag} 类别[{cats[i]}] -> 匹配 {options.Count} 种：{string.Join(",", sample)}");
                    }

                    string optionText;
                    if (options.Count == 0)
                    {
                        optionText = Lang.T("（材料目录未匹配到可用元素）");
                    }
                    else
                    {
                        var names = new List<string>();
                        foreach (var el in options.Take(6))
                        {
                            names.Add(GetElementName(el));
                        }

                        optionText = string.Join("、", names);
                        if (options.Count > 6)
                        {
                            optionText += Lang.T(" 等") + options.Count + Lang.T("种");
                        }
                    }

                    TextMeshProUGUI line = CreateBodyTextIn(sidebarContent,
                        FormatMass(m) + Lang.T("　可用：") + optionText, MutedText, 11);
                    line.gameObject.AddComponent<LayoutElement>().minHeight = 16f;

                    // 获取方式：官方元素描述悬停提示
                    string tooltip = BuildMaterialTooltip(options);
                    if (!string.IsNullOrEmpty(tooltip))
                    {
                        line.raycastTarget = true;
                        line.gameObject.AddComponent<ToolTip>().SetSimpleTooltip(tooltip);
                    }
                }
            }
            catch (Exception e)
            {
                ModLogger.Warn("材料块构建失败(" + objective.Tag + "): " + e);
            }
        }

        /// <summary>材料获取方式提示：前 4 种可选材料的官方描述（来源/用途）。</summary>
        private static string BuildMaterialTooltip(List<object> options)
        {
            try
            {
                var entries = new List<string>();
                foreach (var el in options.Take(4))
                {
                    string desc = GetElementDesc(el);
                    string display = GetElementName(el);
                    entries.Add(desc == null ? $"{display}：{Lang.T("（获取方式见游戏内元素数据库）")}" : $"{display}：{desc}");
                }

                return entries.Count == 0 ? null : Lang.T("获取方式") + "\n" + string.Join("\n", entries);
            }
            catch
            {
                return null;
            }
        }

        // ---- 建筑引导模态窗 ----

        private sealed class TechGuideEntry
        {
            public string Id;
            public string DisplayName;
            public bool Complete;
            public bool PrereqComplete;
            public Tech TechRef;
        }

        /// <summary>打开建筑引导卡（重置导航栈）。</summary>
        private void OpenGuideModal(QuestObjectiveDef objective)
        {
            CloseGuideModal();
            guideBackStack.Clear();
            if (objective == null || string.IsNullOrEmpty(objective.Tag))
            {
                return;
            }

            guideCurrent = objective;
            BuildGuideCard();
        }

        /// <summary>打开科技详情卡（推入导航栈）。</summary>
        private void OpenGuideTechCard(TechGuideEntry entry)
        {
            if (entry == null || entry.TechRef == null)
            {
                return;
            }

            guideBackStack.Add(guideCurrent);
            guideCurrent = entry;
            CloseGuideModal();
            BuildGuideCard();
        }

        /// <summary>返回上一张引导卡。</summary>
        private void GoGuideBack()
        {
            if (guideBackStack.Count == 0)
            {
                return;
            }

            guideCurrent = guideBackStack[guideBackStack.Count - 1];
            guideBackStack.RemoveAt(guideBackStack.Count - 1);
            CloseGuideModal();
            BuildGuideCard();
        }

        private static string CardTitleOf(object card)
        {
            if (card is QuestObjectiveDef objective)
            {
                return string.IsNullOrEmpty(objective.LabelDisp) ? objective.Tag : objective.LabelDisp;
            }

            if (card is TechGuideEntry entry)
            {
                return entry.DisplayName;
            }

            return "";
        }

        /// <summary>
        /// 构建引导卡：浅色底 + 2px 酒红细描边（与主窗口一致的配色结构），
        /// 全透明挡板拦截下方面板点击但不遮暗；卡片高度自适应内容。
        /// </summary>
        private void BuildGuideCard()
        {
            if (guideCurrent == null)
            {
                return;
            }

            GameObject modal = new GameObject("GuideModal");
            modal.transform.SetParent(windowRect, false);
            modal.AddComponent<RectTransform>();
            Stretch(modal.GetComponent<RectTransform>(), 0f, 0f);
            Image shade = modal.AddComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.18f);
            shade.raycastTarget = true;
            guideModal = modal;

            // 点击卡片外遮罩处关闭（避免"点哪都没反应"的冻结感）
            Button shadeButton = modal.AddComponent<Button>();
            shadeButton.targetGraphic = shade;
            shadeButton.onClick.AddListener(CloseGuideModal);

            // 外框：酒红底色 + 2px 内边距露细描边；内层浅色内容区
            GameObject frame = CreateBox("GuideFrame", modal.transform, FrameBg);
            RectTransform frameRect = frame.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.anchoredPosition = Vector2.zero;
            frameRect.sizeDelta = new Vector2(440f, 0f);
            VerticalLayoutGroup frameGroup = frame.AddComponent<VerticalLayoutGroup>();
            frameGroup.padding = new RectOffset(2, 2, 2, 2);
            frameGroup.childControlWidth = true;
            frameGroup.childControlHeight = true;
            frameGroup.childForceExpandWidth = true;
            frameGroup.childForceExpandHeight = false;
            frame.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject inner = CreateBox("GuideInner", frame.transform, ContentBg);
            VerticalLayoutGroup layout = inner.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            inner.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 标题行：标题 + ✕
            GameObject titleRow = new GameObject("TitleRow");
            titleRow.transform.SetParent(inner.transform, false);
            titleRow.AddComponent<RectTransform>();
            HorizontalLayoutGroup titleLayout = titleRow.AddComponent<HorizontalLayoutGroup>();
            titleLayout.spacing = 4f;
            titleLayout.childControlWidth = true;
            titleLayout.childControlHeight = true;
            titleLayout.childForceExpandWidth = true;
            titleLayout.childForceExpandHeight = true;
            titleLayout.childAlignment = TextAnchor.MiddleLeft;
            titleRow.AddComponent<LayoutElement>().preferredHeight = 26f;

            string cardTitle = guideCurrent is TechGuideEntry ? Lang.T("科技详情") : Lang.T("建筑引导");
            string cardIcon = guideCurrent is TechGuideEntry ? StatusIcons.GuideTech : StatusIcons.GuideBuilding;
            Image titleIcon = StatusIcons.CreateIconImage("CardIcon", titleRow.transform, cardIcon, 16f);
            titleIcon.color = HeadingText;

            TextMeshProUGUI title = CreateText("Title", titleRow.transform,
                $"{cardTitle} · {CardTitleOf(guideCurrent)}", 14, TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            title.color = HeadingText;
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            GameObject closeButton = MakeThinButton("GuideClose", titleRow.transform, "✕",
                CloseGuideModal, PinkBtn, PinkBtnHover, 12);
            LayoutElement closeLayout = closeButton.AddComponent<LayoutElement>();
            closeLayout.preferredWidth = 26f;
            closeLayout.preferredHeight = 22f;
            closeLayout.flexibleWidth = 0f;
            closeButton.GetComponentInChildren<TextMeshProUGUI>().color = WhiteText;
            closeButton.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            // 返回按钮（有导航历史时显示）
            if (guideBackStack.Count > 0)
            {
                GameObject backButton = MakeThinButton("GuideBack", inner.transform,
                    Lang.T("← 返回「") + CardTitleOf(guideBackStack[guideBackStack.Count - 1]) + "」",
                    GoGuideBack, BlueBtn, BlueBtnHover, 11);
                backButton.AddComponent<LayoutElement>().preferredHeight = 24f;
                TextMeshProUGUI backLabel = backButton.GetComponentInChildren<TextMeshProUGUI>();
                backLabel.color = WhiteText;
                backLabel.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (guideCurrent is QuestObjectiveDef currentObjective)
            {
                BuildGuideBuildingContent(inner.transform, currentObjective);
            }
            else if (guideCurrent is TechGuideEntry currentTech)
            {
                BuildGuideTechContent(inner.transform, currentTech);
            }

            GameObject doneButton = MakeThinButton("GuideDone", inner.transform, Lang.T("知道了"),
                CloseGuideModal, BlueBtn, BlueBtnHover, 12, FontStyles.Bold);
            doneButton.AddComponent<LayoutElement>().preferredHeight = 30f;
            doneButton.GetComponentInChildren<TextMeshProUGUI>().color = WhiteText;
            doneButton.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }

        /// <summary>建筑卡内容：64px 大图标 + 名称 + 可点击的科技链。</summary>
        private void BuildGuideBuildingContent(Transform parent, QuestObjectiveDef objective)
        {
            GameObject iconHolder = new GameObject("IconHolder");
            iconHolder.transform.SetParent(parent, false);
            iconHolder.AddComponent<RectTransform>();
            iconHolder.AddComponent<LayoutElement>().preferredHeight = 72f;
            GameObject iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(iconHolder.transform, false);
            RectTransform iconRect = iconObject.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(64f, 64f);
            Image icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Sprite sprite = TryResolveSprite(objective.Tag, out Color tint);
            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.color = tint;
            }
            else
            {
                icon.enabled = false;
            }

            string buildingName = string.IsNullOrEmpty(objective.LabelDisp) ? objective.Tag : objective.LabelDisp;
            string nameLine = string.Equals(buildingName, objective.Tag, StringComparison.Ordinal)
                ? buildingName
                : $"{buildingName}（{objective.Tag}）";
            TextMeshProUGUI nameText = CreateText("BuildingName", parent, nameLine, 13, TextAlignmentOptions.Center);
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = HeadingText;
            nameText.gameObject.AddComponent<LayoutElement>().minHeight = 20f;

            GameObject chainTitleRow = CreateIconTitleIn(parent, StatusIcons.GuideTech,
                Lang.T("解锁该建筑所需的研究（点击查看详情）："), MutedText, 12);
            chainTitleRow.AddComponent<LayoutElement>().minHeight = 18f;

            var chain = new List<TechGuideEntry>();
            TryCollectTechChain(objective.Tag, chain);

            if (chain.Count == 0)
            {
                GameObject none = CreateIconTextLineIn(parent, StatusIcons.Positive,
                    Lang.T("无前置科技：开局即可建造。"), PositiveText, 12);
                none.AddComponent<LayoutElement>().minHeight = 20f;
                return;
            }

            foreach (var entry in chain)
            {
                string state;
                Color color;
                string stateIcon;
                if (entry.Complete)
                {
                    state = Lang.T("已研究");
                    color = PositiveText;
                    stateIcon = StatusIcons.Claimed;
                }
                else if (entry.PrereqComplete)
                {
                    state = Lang.T("可研究");
                    color = BlueText;
                    stateIcon = StatusIcons.ObjectiveTodo;
                }
                else
                {
                    state = Lang.T("先决未达成");
                    color = WarningText;
                    stateIcon = StatusIcons.Locked;
                }

                GameObject row = MakeFoldoutHeader("TechRow", parent,
                    $"{entry.DisplayName}　{state}　▶", () => OpenGuideTechCard(entry),
                    RowBg, RowHover, 11, FontStyles.Normal, false);
                AddLeadingStatusIcon(row, stateIcon, color);
                row.GetComponentInChildren<TextMeshProUGUI>().color = color;
                row.GetComponentInChildren<TextMeshProUGUI>().overflowMode = TextOverflowModes.Ellipsis;
                row.AddComponent<LayoutElement>().preferredHeight = 24f;
            }
        }

        /// <summary>科技卡内容：研究状态 + 官方描述 + 研究点需求 + 该科技解锁的建筑列表。</summary>
        private void BuildGuideTechContent(Transform parent, TechGuideEntry tech)
        {
            TryAppendTechIcon(parent, tech);

            string state;
            Color color;
            string stateIcon;
            if (tech.Complete)
            {
                state = Lang.T("已研究");
                color = PositiveText;
                stateIcon = StatusIcons.Claimed;
            }
            else if (tech.PrereqComplete)
            {
                state = Lang.T("前置满足，可开始研究");
                color = BlueText;
                stateIcon = StatusIcons.ObjectiveTodo;
            }
            else
            {
                state = Lang.T("前置未完成，请先研究链上科技");
                color = WarningText;
                stateIcon = StatusIcons.Locked;
            }

            var guideCounts = QuestScanner.CountAllBuildings();

            GameObject stateRow = CreateIconTextLineIn(parent, stateIcon,
                $"{state}　|　{GetTechCostText(tech.TechRef, guideCounts)}", color, 12);
            stateRow.AddComponent<LayoutElement>().minHeight = 20f;

            // 打开游戏研究面板；若所需研究站未建造，先弹提醒指明建筑
            GameObject researchButton = MakeThinButton("OpenResearch", parent,
                Lang.T("打开研究面板"), () =>
                {
                    var missing = GetMissingResearchBuildings(tech.TechRef, QuestScanner.CountAllBuildings());
                    if (missing.Count > 0)
                    {
                        QuestTracker.Notify(Lang.T("研究「") + tech.DisplayName + Lang.T("」需要先建造研究站：") +
                                            string.Join(Lang.T("、"), missing) + Lang.T("。请立即建造以开启研究！"), NotificationType.Bad);
                    }

                    CloseGuideModal();
                    TryOpenResearchScreen();
                },
                BlueBtn, BlueBtnHover, 12, FontStyles.Bold);
            researchButton.AddComponent<LayoutElement>().preferredHeight = 28f;
            TextMeshProUGUI researchLabel = researchButton.GetComponentInChildren<TextMeshProUGUI>();
            researchLabel.color = WhiteText;
            researchLabel.alignment = TextAlignmentOptions.Center;
            AddButtonLeadingIcon(researchButton, StatusIcons.GuideTech, 15f, 14f);
            researchButton.AddComponent<ToolTip>().SetSimpleTooltip(Lang.T("打开研究面板"));

            string descEn = ReadFieldString(tech.TechRef, "desc");
            string desc = LocalizeTechDesc(tech.Id, descEn);
            if (!string.IsNullOrEmpty(desc))
            {
                GameObject descRow = CreateIconTextLineIn(parent, StatusIcons.Knowledge, desc, MutedText, 11);
                descRow.AddComponent<LayoutElement>().minHeight = 20f;
            }

            GameObject unlockTitleRow = CreateIconTitleIn(parent, StatusIcons.Unlocks,
                Lang.T("该科技解锁的建筑："), BodyTextC, 12);
            unlockTitleRow.AddComponent<LayoutElement>().minHeight = 20f;

            var ids = new List<string>();
            try
            {
                if (tech.TechRef.unlockedItemIDs != null)
                {
                    ids.AddRange(tech.TechRef.unlockedItemIDs);
                }
            }
            catch
            {
            }

            int shown = 0;
            foreach (var id in ids)
            {
                if (Assets.GetBuildingDef(id) == null)
                {
                    continue;
                }

                shown++;
                string buildingName = LocalizeBuildingName(id);
                GameObject row = MakeFoldoutHeader("UnlockRow", parent,
                    $"{buildingName}（{id}）　▶", () => OpenGuideModal(ObjectiveForBuilding(id)),
                    RowBg, RowHover, 11, FontStyles.Normal, false);
                Sprite buildingSprite = TryResolveSprite(id, out Color buildingTint);
                if (buildingSprite != null)
                {
                    AddLeadingIcon(row, buildingSprite, buildingTint, 15f);
                }

                row.GetComponentInChildren<TextMeshProUGUI>().color = BodyTextC;
                row.GetComponentInChildren<TextMeshProUGUI>().overflowMode = TextOverflowModes.Ellipsis;
                row.AddComponent<LayoutElement>().preferredHeight = 24f;
                if (shown >= 10)
                {
                    break;
                }
            }

            if (shown == 0)
            {
                TextMeshProUGUI none = CreateBodyTextIn(parent, Lang.T("无建造类解锁项。"), MutedText, 11);
                none.gameObject.AddComponent<LayoutElement>().minHeight = 18f;
            }
        }

        private void CloseGuideModal()
        {
            if (guideModal != null)
            {
                Destroy(guideModal);
                guideModal = null;
            }
        }

        /// <summary>
        /// 打开游戏研究面板：直接调用 ManagementMenu.OpenResearch()
        /// （H 段诊断实锤的公开方法；此前反射循环误命中属性 getter 导致假成功）。
        /// </summary>
        private static void TryOpenResearchScreen()
        {
            try
            {
                var menu = ManagementMenu.Instance;
                if (menu == null)
                {
                    ModLogger.Warn("研究面板打开失败：ManagementMenu.Instance 为空");
                    return;
                }

                menu.OpenResearch();
                ModLogger.Log("研究面板打开成功（ManagementMenu.OpenResearch）");
            }
            catch (Exception e)
            {
                ModLogger.Warn("研究面板打开失败: " + e.Message);
            }
        }

        /// <summary>建筑 → 科技链（Db.Techs 扫描 unlockedItemIDs，未命中走 TechItems.TryGet 兜底）。</summary>
        private void TryCollectTechChain(string buildingTag, List<TechGuideEntry> chain)
        {
            try
            {
                var db = Db.Get();
                var techs = db != null ? db.Techs : null;
                if (techs == null || techs.resources == null)
                {
                    return;
                }

                Tech found = null;
                foreach (var tech in techs.resources)
                {
                    if (tech == null || tech.unlockedItemIDs == null)
                    {
                        continue;
                    }

                    foreach (var id in tech.unlockedItemIDs)
                    {
                        if (id == buildingTag)
                        {
                            found = tech;
                            break;
                        }
                    }

                    if (found != null)
                    {
                        break;
                    }
                }

                if (found == null && db.TechItems != null)
                {
                    var item = db.TechItems.TryGet(buildingTag);
                    if (item != null && !string.IsNullOrEmpty(item.parentTechId))
                    {
                        found = techs.TryGet(item.parentTechId);
                    }
                }

                if (found != null)
                {
                    CollectTechChain(found, chain, new HashSet<Tech>());
                }
            }
            catch (Exception e)
            {
                ModLogger.Warn("建筑科技链解析失败(" + buildingTag + "): " + e);
            }
        }

        private void CollectTechChain(Tech tech, List<TechGuideEntry> chain, HashSet<Tech> visited)
        {
            if (tech == null || !visited.Add(tech))
            {
                return;
            }

            bool complete = false;
            bool prereq = false;
            try
            {
                complete = tech.IsComplete();
            }
            catch
            {
            }

            try
            {
                prereq = tech.ArePrerequisitesComplete();
            }
            catch
            {
            }

            chain.Add(new TechGuideEntry
            {
                Id = tech.Id,
                DisplayName = LocalizeTechName(tech.Id),
                Complete = complete,
                PrereqComplete = prereq,
                TechRef = tech
            });

            if (tech.requiredTech == null)
            {
                return;
            }

            foreach (var req in tech.requiredTech)
            {
                CollectTechChain(req, chain, visited);
            }
        }

        /// <summary>科技本地化名（当前游戏语言）。</summary>
        private static string LocalizeTechName(string techId)
        {
            if (string.IsNullOrEmpty(techId))
            {
                return techId;
            }

            return LocalizeString("STRINGS.RESEARCH.TECHS." + techId.ToUpperInvariant() + ".NAME") ?? techId;
        }

        /// <summary>科技官方描述（当前游戏语言；取不到时回退调用方给定串）。</summary>
        private static string LocalizeTechDesc(string techId, string fallback)
        {
            if (string.IsNullOrEmpty(techId))
            {
                return fallback;
            }

            return LocalizeString("STRINGS.RESEARCH.TECHS." + techId.ToUpperInvariant() + ".DESC") ?? fallback;
        }

        /// <summary>建筑显示名：直接跟随游戏语言。</summary>
        private static string LocalizeBuildingName(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
            {
                return buildingId;
            }

            return LocalizeString("STRINGS.BUILDINGS.PREFABS." + buildingId.ToUpperInvariant() + ".NAME") ?? buildingId;
        }

        /// <summary>通用本地化取串（剥离 &lt;link&gt; 富文本标记；未命中返回 null）。</summary>
        private static string LocalizeString(string key)
        {
            try
            {
                var stringsType = FindStringsType();
                var get = stringsType?.GetMethod("Get", new[] { typeof(string) });
                if (get != null)
                {
                    string value = get.Invoke(null, new object[] { key }) as string;
                    if (!string.IsNullOrEmpty(value) && !value.StartsWith("MISSING"))
                    {
                        value = System.Text.RegularExpressions.Regex.Replace(value, "</?link[^>]*>", "");
                        if (!string.IsNullOrEmpty(value))
                        {
                            return value;
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string ReadFieldString(object target, string fieldName)
        {
            if (target == null)
            {
                return null;
            }

            try
            {
                var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                return field == null ? null : System.Convert.ToString(field.GetValue(target));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>研究类型 key → 中文名（costsByResearchTypeID 的 key 为 lower-case 字符串）。</summary>
        private static string ResearchTypeName(string key)
        {
            switch (key == null ? null : key.ToLowerInvariant())
            {
                case "basic": return Lang.T("基础研究");
                case "advanced": return Lang.T("高级研究");
                case "space": return Lang.T("星际研究");
                case "nuclear": return Lang.T("辐射研究");
                case "orbital": return Lang.T("轨道研究");
                case "applied": return Lang.T("应用研究");
                default: return key ?? "?";
            }
        }

        /// <summary>研究类型 key → 供电研究的建筑 PrefabID（二进制字符串+运行时实锤；启发式映射）。</summary>
        private static readonly Dictionary<string, string> ResearchBuildingOf = new Dictionary<string, string>
        {
            { "basic", "ResearchCenter" },
            { "advanced", "AdvancedResearchCenter" },
            { "space", "Telescope" },
            { "nuclear", "NuclearResearchCenter" },
            { "orbital", "OrbitalResearchCenter" },
        };

        /// <summary>研究点需求文本：类型 x点数，并标注所需研究站及是否已建成。</summary>
        private static string GetTechCostText(Tech tech, Dictionary<string, int> builtCounts)
        {
            try
            {
                var costs = typeof(Tech).GetField("costsByResearchTypeID", BindingFlags.Public | BindingFlags.Instance)
                            ?.GetValue(tech) as IDictionary;
                if (costs == null || costs.Count == 0)
                {
                    return Lang.T("研究点需求：") + "—";
                }

                var parts = new List<string>();
                foreach (DictionaryEntry entry in costs)
                {
                    string key = (entry.Key as string)?.ToLowerInvariant();
                    string part = $"{ResearchTypeName(entry.Key as string)} x{entry.Value}";

                    if (key != null && ResearchBuildingOf.TryGetValue(key, out var buildingId))
                    {
                        string buildingName = LocalizeBuildingName(buildingId);
                        bool built = builtCounts.TryGetValue(buildingId, out int c) && c > 0;
                        part += built
                            ? $"（{buildingName} · {Lang.T("已建造")}）"
                            : $"（{Lang.T("需要")} {buildingName}，{Lang.T("未建造")}）";
                    }

                    parts.Add(part);
                    if (parts.Count >= 4)
                    {
                        break;
                    }
                }

                return Lang.T("研究点需求：") + string.Join("，", parts);
            }
            catch
            {
                return Lang.T("研究点需求：") + "—";
            }
        }

        /// <summary>该科技所需但尚未建造的研究站名称列表（用于提醒）。</summary>
        private static List<string> GetMissingResearchBuildings(Tech tech, Dictionary<string, int> builtCounts)
        {
            var missing = new List<string>();
            try
            {
                var costs = typeof(Tech).GetField("costsByResearchTypeID", BindingFlags.Public | BindingFlags.Instance)
                            ?.GetValue(tech) as IDictionary;
                if (costs == null)
                {
                    return missing;
                }

                foreach (DictionaryEntry entry in costs)
                {
                    string key = (entry.Key as string)?.ToLowerInvariant();
                    if (key == null || !ResearchBuildingOf.TryGetValue(key, out var buildingId))
                    {
                        continue;
                    }

                    if (!builtCounts.TryGetValue(buildingId, out int c) || c <= 0)
                    {
                        missing.Add(LocalizeBuildingName(buildingId));
                    }
                }
            }
            catch
            {
            }

            return missing;
        }

        private static bool techIconDiagDone;

        /// <summary>
        /// 科技卡片图标：用 Def.GetUISprite（白模+着色的 Tuple&lt;Sprite,Color&gt;）——
        /// 与建筑图标同一条已验证路径；候选 key：TechItem.Id（建筑 prefab id）→ 科技 Id。
        /// </summary>
        private static void TryAppendTechIcon(Transform parent, TechGuideEntry tech)
        {
            try
            {
                if (tech == null || tech.TechRef == null)
                {
                    return;
                }

                var unlocked = tech.TechRef.GetType().GetField("unlockedItems",
                    BindingFlags.Public | BindingFlags.Instance)?.GetValue(tech.TechRef) as IEnumerable;
                if (unlocked == null)
                {
                    return;
                }

                foreach (var item in unlocked)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    string itemId = item.GetType().GetField("Id",
                        BindingFlags.Public | BindingFlags.Instance)?.GetValue(item) as string;

                    Sprite sprite = null;
                    Color tint = Color.white;
                    string hitKey = null;
                    foreach (var key in new[] { itemId, tech.Id })
                    {
                        if (string.IsNullOrEmpty(key))
                        {
                            continue;
                        }

                        try
                        {
                            var pair = Def.GetUISprite(key);
                            if (pair != null && pair.first != null)
                            {
                                sprite = pair.first;
                                tint = pair.second;
                                hitKey = key;
                                break;
                            }
                        }
                        catch
                        {
                        }
                    }

                    if (sprite == null)
                    {
                        ModLogger.Warn($"[TechDiag] 科技图标(Def)未命中 tech={tech.Id} item={itemId}");
                        continue;
                    }

                    if (!techIconDiagDone)
                    {
                        techIconDiagDone = true;
                        ModLogger.Log($"[TechDiag] 科技图标(Def)取图成功 tech={tech.Id} key={hitKey}");
                    }

                    GameObject holder = new GameObject("TechIcon");
                    holder.transform.SetParent(parent, false);
                    holder.AddComponent<RectTransform>();
                    holder.AddComponent<LayoutElement>().preferredHeight = 52f;
                    GameObject iconObject = new GameObject("Icon");
                    iconObject.transform.SetParent(holder.transform, false);
                    RectTransform iconRect = iconObject.AddComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                    iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.pivot = new Vector2(0.5f, 0.5f);
                    iconRect.anchoredPosition = Vector2.zero;
                    iconRect.sizeDelta = new Vector2(48f, 48f);
                    Image icon = iconObject.AddComponent<Image>();
                    icon.sprite = sprite;
                    icon.color = tint;
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    return;
                }
            }
            catch
            {
            }
        }

        /// <summary>由建筑 PrefabID 构造引导目标（Label 使用官方本地化名）。</summary>
        private static QuestObjectiveDef ObjectiveForBuilding(string buildingId)
        {
            return new QuestObjectiveDef
            {
                Type = "buildingBuilt",
                Tag = buildingId,
                Count = 1,
                Label = LocalizeBuildingName(buildingId)
            };
        }

        private static System.Type cachedStringsType;
        private static bool stringsTypeSearched;

        private static System.Type FindStringsType()
        {
            if (stringsTypeSearched)
            {
                return cachedStringsType;
            }

            stringsTypeSearched = true;
            var assemblies = new[] { typeof(Db).Assembly, typeof(KPrefabID).Assembly };
            foreach (var asm in assemblies)
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == "Strings")
                        {
                            return cachedStringsType = t;
                        }
                    }
                }
                catch
                {
                }
            }

            return cachedStringsType;
        }

        /// <summary>挂在指定父节点下的正文文本（模态窗用，默认 Body 挂 detailContent）。</summary>
        private static TextMeshProUGUI CreateBodyTextIn(Transform parent, string text, Color color, int size = 12)
        {
            GameObject go = new GameObject("Body");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        /// <summary>任意父节点下的图标+粗体标题行（侧栏/弹窗通用，替代 emoji 前缀文本）。</summary>
        private static GameObject CreateIconTitleIn(Transform parent, string iconKey, string text, Color color, int size = 12)
        {
            GameObject row = new GameObject("IconTitle");
            row.transform.SetParent(parent, false);
            row.AddComponent<RectTransform>();
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            Image icon = StatusIcons.CreateIconImage("Icon", row.transform, iconKey, size + 2f);
            icon.color = color;

            TextMeshProUGUI label = CreateText("Label", row.transform, text, size, TextAlignmentOptions.MidlineLeft);
            label.fontStyle = FontStyles.Bold;
            label.color = color;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            return row;
        }

        /// <summary>在 MakeFoldoutHeader 生成的行最左侧加一个图标，并把文字右移（替代行首 emoji）。</summary>
        private static Image AddLeadingIcon(GameObject row, Sprite sprite, Color tint, float size = 15f, float left = 7f)
        {
            GameObject iconObject = new GameObject("LeadIcon");
            iconObject.transform.SetParent(row.transform, false);
            Image icon = iconObject.AddComponent<Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = tint;
            RectTransform rect = icon.rectTransform();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(size, size);

            TextMeshProUGUI label = row.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                Vector2 offsetMin = label.rectTransform().offsetMin;
                offsetMin.x = left + size + 5f;
                label.rectTransform().offsetMin = offsetMin;
            }

            return icon;
        }

        /// <summary>状态精灵版行首图标。</summary>
        private static Image AddLeadingStatusIcon(GameObject row, string iconKey, Color color, float size = 14f)
        {
            Sprite sp = StatusIcons.Get(iconKey);
            if (sp == null)
            {
                return null;
            }

            return AddLeadingIcon(row, sp, color, size);
        }

        /// <summary>任意父节点下的图标+自动换行正文（弹窗用，替代 emoji 前缀段落）。</summary>
        private static GameObject CreateIconTextLineIn(Transform parent, string iconKey, string text, Color color, int size = 12)
        {
            GameObject row = new GameObject("IconTextLine");
            row.transform.SetParent(parent, false);
            row.AddComponent<RectTransform>();
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            Image icon = StatusIcons.CreateIconImage("Icon", row.transform, iconKey, size + 2f);
            icon.color = color;

            TextMeshProUGUI label = CreateText("Label", row.transform, text, size, TextAlignmentOptions.TopLeft);
            label.color = color;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            row.AddComponent<LayoutElement>().minHeight = size + 6f;
            return row;
        }

        /// <summary>在宽按钮左侧加一个状态/功能图标（按钮文字本身居中）。</summary>
        private static void AddButtonLeadingIcon(GameObject button, string iconKey, float size = 16f, float left = 12f)
        {
            Image icon = StatusIcons.CreateIconImage("LeadIcon", button.transform, iconKey, size);
            icon.color = WhiteText;
            RectTransform rect = icon.rectTransform();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(size, size);
        }

        private void CreateAcceptButton(QuestDef quest)
        {
            GameObject button = MakeThinButton("AcceptButton", detailContent,
                Lang.T("▶ 接取任务"),
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
                Lang.T("领取奖励！"),
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

            // 打印舱奖励图标（替代 🎁 emoji）
            Image giftIcon = StatusIcons.CreateIconImage("GiftIcon", button.transform, StatusIcons.Rewards, 20f);
            giftIcon.color = WhiteText;
            RectTransform giftRect = giftIcon.rectTransform();
            giftRect.anchorMin = new Vector2(0f, 0.5f);
            giftRect.anchorMax = new Vector2(0f, 0.5f);
            giftRect.pivot = new Vector2(0f, 0.5f);
            giftRect.anchoredPosition = new Vector2(14f, 0f);
            giftRect.sizeDelta = new Vector2(20f, 20f);
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

        /// <summary>详情顶部：大建筑图标 + 标题 + 阶段。</summary>
        private void CreateDetailHeader(QuestDef quest, StatusStyle style, QuestPhaseDef phase)
        {
            GameObject headRow = new GameObject("DetailHeader");
            headRow.transform.SetParent(detailContent, false);
            headRow.AddComponent<RectTransform>();
            HorizontalLayoutGroup layout = headRow.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            headRow.AddComponent<LayoutElement>().preferredHeight = 50f;

            // 大图标（44px）
            GameObject iconObject = new GameObject("BigIcon");
            iconObject.transform.SetParent(headRow.transform, false);
            iconObject.AddComponent<RectTransform>();
            LayoutElement iconLayout = iconObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 44f;
            iconLayout.preferredHeight = 44f;
            iconLayout.flexibleWidth = 0f;
            Image icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Sprite sprite = ResolveQuestIcon(quest, out Color tint);
            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.color = tint;
            }
            else
            {
                icon.enabled = false;
            }

            // 标题列
            GameObject textColumn = new GameObject("TitleColumn");
            textColumn.transform.SetParent(headRow.transform, false);
            textColumn.AddComponent<RectTransform>();
            VerticalLayoutGroup columnLayout = textColumn.AddComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 0f;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;
            textColumn.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // 标题行：状态徽章 + 标题文本
            GameObject titleRow = new GameObject("TitleRow");
            titleRow.transform.SetParent(textColumn.transform, false);
            titleRow.AddComponent<RectTransform>();
            HorizontalLayoutGroup titleRowLayout = titleRow.AddComponent<HorizontalLayoutGroup>();
            titleRowLayout.spacing = 6f;
            titleRowLayout.childControlWidth = true;
            titleRowLayout.childControlHeight = true;
            titleRowLayout.childForceExpandWidth = false;
            titleRowLayout.childForceExpandHeight = true;
            titleRowLayout.childAlignment = TextAnchor.MiddleLeft;
            titleRow.AddComponent<LayoutElement>().preferredHeight = 28f;

            Image badge = StatusIcons.CreateIconImage("StatusBadge", titleRow.transform, style.Icon, 16f);
            badge.color = style.Color;

            TextMeshProUGUI title = CreateText("QuestTitle", titleRow.transform,
                $"#{quest.Order:d2}  {quest.TitleDisp}", 16, TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            title.color = HeadingText;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            TextMeshProUGUI phaseText = CreateText("PhaseLine", textColumn.transform,
                phase != null ? phase.Title : quest.Phase, 12, TextAlignmentOptions.MidlineLeft);
            phaseText.color = MutedText;
            phaseText.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;
        }

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

        /// <summary>带游戏精灵图标的段落标题（图标 + 粗体文本一行，替代 emoji 前缀）。</summary>
        private void CreateSectionTitle(string iconKey, string text, Color color, int size = 13)
        {
            GameObject row = new GameObject("SectionTitle");
            row.transform.SetParent(detailContent, false);
            row.AddComponent<RectTransform>();
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            Image icon = StatusIcons.CreateIconImage("Icon", row.transform, iconKey, size + 3f);
            icon.color = color;

            TextMeshProUGUI label = CreateText("Label", row.transform, text, size, TextAlignmentOptions.MidlineLeft);
            label.fontStyle = FontStyles.Bold;
            label.color = color;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = size + 8f;
            rowLayout.minHeight = size + 8f;
        }

        /// <summary>状态/提示行：小图标 + 自动换行正文（替代 emoji 前缀文本）。</summary>
        private void CreateIconTextLine(string iconKey, string text, Color color, int size = 12)
        {
            GameObject row = new GameObject("IconTextLine");
            row.transform.SetParent(detailContent, false);
            row.AddComponent<RectTransform>();
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            Image icon = StatusIcons.CreateIconImage("Icon", row.transform, iconKey, size + 2f);
            icon.color = color;

            TextMeshProUGUI label = CreateText("Label", row.transform, text, size, TextAlignmentOptions.TopLeft);
            label.color = color;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            row.AddComponent<LayoutElement>().minHeight = size + 6f;
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
        private GameObject CreateIconRow(string spriteKey, string text, Color textColor, string badgeKey = null)
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
            Sprite sprite = TryResolveSprite(spriteKey, out Color tint);
            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.color = tint;
            }
            else
            {
                icon.enabled = false;
            }

            // 目标达成状态徽章（无 key 则不占位）
            if (!string.IsNullOrEmpty(badgeKey))
            {
                Image badge = StatusIcons.CreateIconImage("Badge", row.transform, badgeKey, 14f);
                badge.color = textColor;
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

        /// <summary>纯图标薄按钮（web_button 底板 + 居中游戏精灵），尺寸由外层 LayoutElement 控制。</summary>
        private static GameObject MakeIconButton(
            string name, Transform parent, string iconKey, System.Action onClick,
            Color normal, Color hover)
        {
            GameObject buttonObject = MakeThinButton(name, parent, string.Empty, onClick, normal, hover);

            Image icon = StatusIcons.CreateIconImage("Icon", buttonObject.transform, iconKey, 16f);
            icon.color = WhiteText;
            icon.rectTransform().anchorMin = Vector2.zero;
            icon.rectTransform().anchorMax = Vector2.one;
            icon.rectTransform().offsetMin = new Vector2(4f, 3f);
            icon.rectTransform().offsetMax = new Vector2(-4f, -3f);
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

        private static void SetBottomStretch(RectTransform rect, float left, float right, float bottom, float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
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