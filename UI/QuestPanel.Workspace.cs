using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Rookie100.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rookie100.UI
{
    public sealed partial class QuestPanel
    {
        private RectTransform chapterNavigation;
        private bool showOptional;
        private int detailTab;
        private LivingSpaceDemo currentLivingDemo;
        private LessonFilmPlayer currentFilm;

        private void BuildChapterNavigation(Transform parent, float height)
        {
            var row = CreateBox("ChapterNavigation", parent, new Color(.83f,.79f,.78f,1f));
            chapterNavigation = row.GetComponent<RectTransform>();
            SetTopStretch(chapterNavigation, FrameBorder, FrameBorder,
                FrameBorder + HeaderHeight + SectionGap,
                FrameBorder + HeaderHeight + SectionGap + height);
            var grid = row.AddComponent<GridLayoutGroup>();
            int columns = height > 50f ? 3 : 6;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns; grid.spacing = new Vector2(5f,4f);
            grid.padding = new RectOffset(6,6,4,4);
            grid.cellSize = new Vector2((windowRect.sizeDelta.x - FrameBorder * 2 - 12 - (columns-1)*5) / columns,
                height > 50f ? 28f : 32f);
        }
        private void RefreshChapterNavigation()
        {
            if (chapterNavigation == null) return;
            ClearChildren(chapterNavigation);
            var selected = QuestStore.GetQuest(selectedQuestId);
            foreach (var chapter in QuestStore.Phases)
            {
                bool active = selected?.Phase == chapter.Id;
                var button = MakeThinButton("Chapter_" + chapter.Id, chapterNavigation, chapter.TitleDisp,
                    () =>
                    {
                        var task = QuestStore.GetQuestsOfPhase(chapter.Id)
                            .FirstOrDefault(q => !QuestStore.IsClaimed(q.Id) && (string)q.Teaching?["requiredOrOptional"] == "本章主线")
                            ?? QuestStore.GetQuestsOfPhase(chapter.Id).FirstOrDefault();
                        if (task != null) { SelectQuest(task.Id); ScrollDetailToTop(); }
                    }, active ? HeaderBg : new Color(.87f,.84f,.81f,1f), active ? PinkBtnHover : RowHover, 12, FontStyles.Bold);
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.color = active ? WhiteText : HeadingText;
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true; label.fontSizeMin = 10f; label.fontSizeMax = 12f;
            }
        }
        private void RefreshTree()
        {
            ClearChildren(treeContent); RefreshChapterNavigation();
            var selected = QuestStore.GetQuest(selectedQuestId);
            var chapter = QuestStore.Phases.FirstOrDefault(p => p.Id == selected?.Phase);
            if (chapter == null) return;
            var tasks = QuestStore.GetQuestsOfPhase(chapter.Id);
            var heading = CreateText("ChapterHeading", treeContent, chapter.TitleDisp, 15, TextAlignmentOptions.TopLeft);
            heading.fontStyle = FontStyles.Bold; heading.gameObject.AddComponent<LayoutElement>().minHeight = 40f;
            var progress = CreateBodyTextIn(treeContent, Lang.T("已领取") + $" {tasks.Count(q=>QuestStore.IsClaimed(q.Id))}/{tasks.Count}", BodyTextC, 12);
            progress.gameObject.AddComponent<LayoutElement>().minHeight = 24f;
            var filter = MakeThinButton("TaskFilter", treeContent,
                showOptional ? (Lang.English ? "All tasks · show main" : "全部任务 · 切换主线") : (Lang.English ? "Main tasks · show all" : "主线任务 · 查看全部"),
                () => { showOptional = !showOptional; RefreshTree(); }, BlueBtn, BlueBtnHover, 12);
            filter.AddComponent<LayoutElement>().preferredHeight = 32f;
            foreach (var quest in tasks)
            {
                bool optional = (string)quest.Teaching?["requiredOrOptional"] != "本章主线";
                if (optional && !showOptional && quest.Id != selectedQuestId) continue;
                CreateQuestRow(quest, treeContent);
            }
            if (showOptional)
            {
                foreach (var lesson in Curriculum.CurriculumCatalog.Tasks.Where(t => (string)t["chapterId"] == chapter.Id && QuestStore.GetQuest((string)t["id"]) == null))
                {
                    var button = MakeFoldoutHeader("Lesson_" + (string)lesson["id"], treeContent,
                        (string)lesson["title"] + " · 教学自查", () => OpenLesson(lesson as JObject), RowBg, RowHover, 12, FontStyles.Normal, false);
                    button.AddComponent<LayoutElement>().minHeight = 44f;
                }
            }
        }
        private void OpenLesson(JObject lesson)
        {
            if (lesson == null) return;
            CloseGuideModal(); guideBackStack.Clear(); guideCurrent = lesson; BuildGuideCard();
        }
        private void OpenResource(Element element)
        {
            var inventory = ReadCurrentInventory();
            float stock = 0f; if (inventory != null) inventory.TryGetValue(element.tag, out stock);
            OpenLesson(new JObject {
                ["title"] = GetElementName(element),
                ["resourceDescription"] = (GetElementDesc(element) ?? Lang.T("获取方式见游戏内元素数据库")) + "\n\n" +
                    (inventory == null ? Lang.T("库存暂不可用") : Lang.T("当前世界可访问库存") + ": " + FormatMass(stock)) + "\n\n" +
                    (Lang.English ? "Related task: " : "相关任务：") + QuestStore.GetQuest(selectedQuestId)?.TitleDisp
            });
        }
        private void BuildDetailTabs()
        {
            var row = new GameObject("DetailTabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(detailContent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 5f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().preferredHeight = 50f;
            string[] labels = Lang.English ? new[] { "Objectives", "Learning steps", "Checks" } : new[] { "当前目标", "分步教学", "完成检查" };
            for (int i=0;i<labels.Length;i++)
            {
                int tab = i;
                var button = MakeThinButton("DetailTab"+i, row.transform, labels[i],
                    () => { detailTab = tab; RefreshDetail(); ScrollDetailToTop(); }, detailTab==i ? HeaderBg : new Color(.85f,.84f,.81f,1f), RowHover, 15, FontStyles.Bold);
                button.GetComponentInChildren<TextMeshProUGUI>().color = detailTab==i ? WhiteText : HeadingText;
                button.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
                button.AddComponent<LayoutElement>().flexibleWidth = 1f;
            }
        }
        private void RefreshDetail()
        {
            if (currentDemo != null) { demoPlayback = currentDemo.Capture(); currentDemo.Pause(); }
            if (currentFilm != null) { demoPlayback=currentFilm.Capture();currentFilm.Pause();currentFilm=null; }
            if (currentLivingDemo != null) { demoPlayback = currentLivingDemo.Capture(); currentLivingDemo.Pause(); currentLivingDemo = null; }
            currentDemo = null; materialInventoryLines.Clear(); ClearChildren(detailContent); ClearChildren(sidebarContent);
            var quest = QuestStore.GetQuest(selectedQuestId); if (quest == null) return;
            var status = QuestStore.GetStatus(quest,currentCounts);
            BuildCurriculumScene(quest);
            CreateDetailHeader(quest,StyleOf(status),QuestStore.Phases.FirstOrDefault(p=>p.Id==quest.Phase));
            CreateBodyText((string)quest.Teaching?["purpose"] ?? quest.DescDisp, BodyTextC,14);
            BuildDetailTabs();
            if (detailTab == 0)
            {
                var card = BeginDetailCard("NextStep", new Color(.91f,.85f,.89f,1f));
                CreateSectionTitle(StatusIcons.Brief, Lang.English ? "Next step" : "下一步 · 先完成这一件事", HeadingText,14);
                var hint = BeginnerGuidance.Get(quest.Id,currentCounts,QuestStore.IsLearned(quest.Id));
                CreateBodyText(hint != null ? (Lang.English ? hint.English : hint.Chinese) :
                    (QuestStore.IsClaimed(quest.Id) ? (string)quest.Teaching?["next"] : (string)quest.Teaching?["preparation"] ?? quest.DescDisp), BodyTextC,14);
                EndDetailCard(card);
                AppendObjectiveChecks(quest);
            }
            else if (detailTab == 1)
            {
                var card = BeginDetailCard("Teaching", new Color(.93f,.92f,.88f,1f));
                AppendTeaching(quest.Teaching); EndDetailCard(card);
                if (quest != null)
                {
                    var demo = MakeThinButton("DemoToggle",detailContent,Lang.T(demoExpanded ? "收起动画演示" : "动画演示"),
                        () => {demoExpanded=!demoExpanded;RefreshDetail();},BlueBtn,BlueBtnHover,13);
                    demo.AddComponent<LayoutElement>().preferredHeight=32f;
                    if(demoExpanded)
                    {
                        if (!CreateTaskFilm(quest))
                        {
                            if (quest.Id == "q01") CreateToiletDemo(detailContent);
                            else if (quest.Id == "q01_bedroom" || quest.Id == "q01_dining") CreateLivingDemo(quest);
                            else CreateBodyText(Lang.English?"The tutorial file is missing. Reinstall the complete package.":"教程资源缺失，请重新安装完整模组包。",MutedText,13);
                        }
                    }
                }
                AppendVideoReferences(quest);
            }
            else
            {
                AppendObjectiveChecks(quest);
                CreateSectionTitle(StatusIcons.Knowledge,Lang.English?"Running checks":"运行核对",HeadingText,14);
                CreateBodyText((string)quest.Teaching?["runningChecks"]??quest.DescDisp,BodyTextC,14);
                CreateBodyText((string)quest.Teaching?["completion"],MutedText,13);
                CreateSectionTitle(StatusIcons.Rewards,Lang.T("打印舱奖励"),HeadingText,14);
                foreach(var reward in quest.Rewards)CreateIconRow(reward.Element,reward.LabelDisp,BodyTextC);
                if(quest.Rewards.Count==0)CreateBodyText(Lang.T("无实物奖励，完成后解锁下一任务"),MutedText,13);
            }
            AppendQuestAction(quest,status);
            BuildPreparationSidebar(quest);
        }
        private void CreateLivingDemo(QuestDef quest)
        {
            bool dining=quest.Id=="q01_dining";
            var original=BeginDetailCard("LivingLesson",new Color(.83f,.87f,.88f,1f));
            var title=CreateBodyText(dining ? (Lang.English?"Dining room lesson":"餐厅与研究路线演示") : (Lang.English?"Bedroom: native animation prototype":"宿舍：原版动画样板"),HeadingText,15);
            title.fontStyle=FontStyles.Bold;
            var scene=CreateBox("LivingLessonScene",detailContent,new Color(.91f,.93f,.92f,1f));scene.AddComponent<LayoutElement>().preferredHeight=215f;
            var demo=scene.AddComponent<LivingSpaceDemo>();currentLivingDemo=demo;
            var station=TryResolveSprite("ResearchCenter",out var stationTint);
            var facility=TryResolveSprite(dining?"DiningTable":"Bed",out var facilityTint);
            var dirt=TryResolveSprite(dining?"Dirt":"SandStone",out var dirtTint);
            demo.Initialize(dining,station,stationTint,facility,facilityTint,dirt,dirtTint,demoPlayback,title.font);
            var row=new GameObject("LivingLessonControls",typeof(RectTransform),typeof(HorizontalLayoutGroup));row.transform.SetParent(detailContent,false);
            var layout=row.GetComponent<HorizontalLayoutGroup>();layout.spacing=6f;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;
            row.AddComponent<LayoutElement>().preferredHeight=34f;
            var play=MakeThinButton("PlayPause",row.transform,Lang.T(demo.IsPlaying?"暂停":"播放"),()=>demo.TogglePlay(),BlueBtn,BlueBtnHover,12);
            play.AddComponent<LayoutElement>().flexibleWidth=1f;var label=play.GetComponentInChildren<TextMeshProUGUI>();
            demo.PlaybackChanged=playing=>{if(label!=null)label.text=Lang.T(playing?"暂停":"播放");};
            var replay=MakeThinButton("Replay",row.transform,Lang.T("重播"),()=>demo.Replay(),BlueBtn,BlueBtnHover,12);replay.AddComponent<LayoutElement>().flexibleWidth=1f;
            var focus=MakeThinButton("CurrentStep",row.transform,Lang.English?"Current step":"当前步骤",()=>demo.SeekStep(CurrentLivingLessonStep(quest)),PinkBtn,PinkBtnHover,12);focus.AddComponent<LayoutElement>().flexibleWidth=1f;
            var steps=new GameObject("LivingLessonSteps",typeof(RectTransform),typeof(HorizontalLayoutGroup));steps.transform.SetParent(detailContent,false);
            var stepsLayout=steps.GetComponent<HorizontalLayoutGroup>();stepsLayout.spacing=5f;stepsLayout.childControlWidth=stepsLayout.childControlHeight=true;stepsLayout.childForceExpandWidth=true;
            steps.AddComponent<LayoutElement>().preferredHeight=30f;
            for(int i=0;i<5;i++)
            {
                int step=i;var button=MakeThinButton("Step"+i,steps.transform,(Lang.English?"Step ":"步骤 ")+(i+1),()=>demo.SeekStep(step),BlueBtn,BlueBtnHover,11);
                button.AddComponent<LayoutElement>().flexibleWidth=1f;
            }
            CreateBodyText(Lang.T("布局和动作示意，不会自动建造或完成任务"),MutedText,12);
            EndDetailCard(original);
        }
        private bool CreateTaskFilm(QuestDef quest)
        {
            LessonFilmDefinition definition;
            try { definition=LessonFilmPlayer.DefinitionFor(quest.Id); }
            catch(System.Exception e)
            {
                ModLogger.Error($"Task film definition unavailable: {quest.Id}",e);return false;
            }
            var original=BeginDetailCard("TaskFilm",new Color(.83f,.87f,.88f,1f));
            var title=CreateBodyText(quest.TitleDisp+(Lang.English?" - Tutorial film":" · 场景教程短片"),HeadingText,15);
            title.fontStyle=FontStyles.Bold;
            var scene=CreateBox("TaskFilmScene",detailContent,new Color(.12f,.12f,.15f,1f));
            scene.AddComponent<LayoutElement>().preferredHeight=300f;
            var subtitle=CreateBodyText("",HeadingText,14);subtitle.GetComponent<LayoutElement>().preferredHeight=96f;
            var status=CreateBodyText("",MutedText,12);status.GetComponent<LayoutElement>().preferredHeight=22f;
            var film=scene.AddComponent<LessonFilmPlayer>();currentFilm=film;
            try { film.Initialize(definition,demoPlayback,subtitle,status); }
            catch(System.Exception e)
            {
                currentFilm=null;Destroy(film);ModLogger.Error($"Task film player initialization failed: {quest.Id}",e);
                subtitle.text=Lang.English?"Lesson film could not load. See the game log.":"教程短片未能加载，请查看游戏日志。";
                EndDetailCard(original);return true;
            }
            var controls=new GameObject("FilmControls",typeof(RectTransform),typeof(HorizontalLayoutGroup));
            controls.transform.SetParent(detailContent,false);
            var layout=controls.GetComponent<HorizontalLayoutGroup>();layout.spacing=6f;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;
            controls.AddComponent<LayoutElement>().preferredHeight=34f;
            var play=MakeThinButton("PlayPause",controls.transform,Lang.T(film.IsPlaying?"暂停":"播放"),()=>film.TogglePlay(),BlueBtn,BlueBtnHover,12);
            play.AddComponent<LayoutElement>().flexibleWidth=1f;
            var playLabel=play.GetComponentInChildren<TextMeshProUGUI>();
            film.PlaybackChanged=playing=>{if(playLabel!=null)playLabel.text=Lang.T(playing?"暂停":"播放");};
            var replay=MakeThinButton("Replay",controls.transform,Lang.T("重播"),()=>film.Replay(),BlueBtn,BlueBtnHover,12);replay.AddComponent<LayoutElement>().flexibleWidth=1f;
            var focus=MakeThinButton("CurrentStep",controls.transform,quest.Id=="q01_bedroom"?(Lang.English?"Current step":"当前步骤"):(Lang.English?"Run checks":"运行核对"),()=>
            {
                int step=quest.Id=="q01_bedroom"?CurrentLivingLessonStep(quest):definition.Steps.Count-1;
                film.SeekStep(quest.Id=="q01_bedroom"&&step==2?0:step);
            },PinkBtn,PinkBtnHover,12);focus.AddComponent<LayoutElement>().flexibleWidth=1f;
            for(int first=0;first<definition.Steps.Count;first+=3)
            {
            var steps=new GameObject("FilmSteps"+first,typeof(RectTransform),typeof(HorizontalLayoutGroup));steps.transform.SetParent(detailContent,false);
            var stepLayout=steps.GetComponent<HorizontalLayoutGroup>();stepLayout.spacing=4f;stepLayout.childControlWidth=stepLayout.childControlHeight=true;stepLayout.childForceExpandWidth=true;
            steps.AddComponent<LayoutElement>().preferredHeight=38f;
            for(int i=first;i<System.Math.Min(first+3,definition.Steps.Count);i++)
            {
                int index=i;var button=MakeThinButton("FilmStep"+i,steps.transform,definition.Steps[i].Text(Lang.English),()=>film.SeekStep(index),BlueBtn,BlueBtnHover,11);
                button.AddComponent<LayoutElement>().flexibleWidth=1f;
            }
            }
            CreateBodyText(Lang.English?"Tutorial illustration; arrows describe flow, not exact utility ports. Complete colony checks or learning confirmation for this task.":"场景教学示意，流程箭头不代表精确接线端口；请在存档中完成检查或对应知识确认。",MutedText,12);
            EndDetailCard(original);return true;
        }
        private int CurrentLivingLessonStep(QuestDef quest)
        {
            bool Has(string key)=>currentCounts!=null&&currentCounts.TryGetValue(key,out var count)&&count>0;
            if(quest.Id=="q01_bedroom")
                return !Has("Bed")?0:!Has("livingRoom:BedsUsable")?2:!Has("livingRoom:Barracks")?3:4;
            if(!Has("DiningTable"))
            {
                var tech=Db.Get()?.Techs?.TryGet("FineDining");
                if(tech!=null&&tech.IsComplete())return 3;
                return Has("ResearchCenter")?1:0;
            }
            return Has("livingRoom:TablesUsable")?4:3;
        }
        private RectTransform BeginDetailCard(string name,Color color)
        {
            var original = detailContent;
            var box=CreateBox(name,original,color);var layout=box.AddComponent<VerticalLayoutGroup>();
            layout.padding=new RectOffset(12,12,12,12);layout.spacing=8f;
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            box.AddComponent<LayoutElement>(); detailContent=box.GetComponent<RectTransform>();return original;
        }
        private void EndDetailCard(RectTransform original) {detailContent=original;}
        private void AppendObjectiveChecks(QuestDef quest)
        {
            var card=BeginDetailCard("ObjectiveChecks",new Color(.94f,.93f,.88f,1f));
            CreateSectionTitle(StatusIcons.Objectives,Lang.T("任务目标"),HeadingText,14);
            foreach(var objective in quest.Objectives)
            {
                bool met=QuestStore.IsObjectiveMet(objective,currentCounts);
                var row=CreateIconRow(objective.Tag,QuestStore.GetObjectiveStatusText(objective,currentCounts),BodyTextC,met?StatusIcons.ObjectiveDone:StatusIcons.ObjectiveTodo);
                row.AddComponent<LayoutElement>().minHeight=44f;
                var button=MakeIconButton("Guide_"+objective.Tag,row.transform,StatusIcons.GuideBuilding,()=>OpenGuideModal(objective),BlueBtn,BlueBtnHover);
                var size=button.AddComponent<LayoutElement>();size.preferredWidth=30f;size.preferredHeight=26f;
            }
            if(quest.Objectives.Count==0)CreateBodyText(Lang.T("知识任务：接取后确认完成，解锁下一任务"),BodyTextC,14);
            EndDetailCard(card);
        }
        private void BuildPreparationSidebar(QuestDef quest)
        {
            sidebarPanel.SetActive(true);
            var element=sidebarPanel.GetComponent<LayoutElement>();
            float width=sidebarExpanded ? Mathf.Clamp(windowRect.sizeDelta.x*.22f,120f,270f) : 72f;
            if(windowRect.sizeDelta.x<600f)width=72f;
            element.minWidth=element.preferredWidth=width;
            var heading=MakeThinButton("PreparationToggle",sidebarContent,Lang.English?"Preparation":"建造准备",()=>{sidebarExpanded=!sidebarExpanded;RefreshDetail();},BlueBtn,BlueBtnHover,14,FontStyles.Bold);
            heading.AddComponent<LayoutElement>().preferredHeight=34f;
            var original=detailContent;detailContent=sidebarContent;
            if(sidebarExpanded && width>80f)
            {
                var card=BeginDetailCard("ResearchPreparation",new Color(.88f,.89f,.91f,1f));
                CreateSectionTitle(StatusIcons.GuideTech,Lang.English?"Buildings and research":"前置建筑与研究",HeadingText,13);
                CreateBodyText((string)quest.Teaching?["preparation"]??quest.DescDisp,BodyTextC,13);
                AppendResearchPrerequisites(quest);
                foreach(var objective in quest.Objectives.Where(o=>!string.IsNullOrEmpty(o.Tag)).GroupBy(o=>o.Tag).Select(g=>g.First()))
                {
                    var guide=MakeThinButton("BuildingGuide_"+objective.Tag,detailContent,LocalizeBuildingName(objective.Tag),()=>OpenGuideModal(objective),BlueBtn,BlueBtnHover,12);
                    guide.AddComponent<LayoutElement>().minHeight=32f;
                }
                EndDetailCard(card);
                CreateSectionTitle(StatusIcons.GuideBuilding,Lang.English?"Resources and materials":"资源与材料",HeadingText,14);
                foreach(var objective in quest.Objectives.Where(o=>!string.IsNullOrEmpty(o.Tag)).GroupBy(o=>o.Tag).Select(g=>g.First()))
                    BuildMaterialBlock(objective);
                if(quest.Objectives.Count==0)CreateBodyText((string)quest.Teaching?["preparation"],BodyTextC,13);
                if(quest.Layout!=null&&quest.Layout.Count>0)
                {
                    var notes=MakeThinButton("LayoutNotes",sidebarContent,Lang.T("推荐布局"),()=>OpenLesson(new JObject{["title"]=quest.TitleDisp,["teaching"]=new JObject{["steps"]=new JArray(quest.Layout)}}),BlueBtn,BlueBtnHover,12);
                    notes.AddComponent<LayoutElement>().minHeight=30f;
                }
            }
            var monitor=MakeThinButton("MonitorDetails",sidebarContent,Lang.English?"Duplicants":"复制人详情",()=>{monitorExpanded=!monitorExpanded;RefreshDetail();},BlueBtn,BlueBtnHover,12);
            monitor.AddComponent<LayoutElement>().minHeight=32f;
            if(monitorExpanded && width>80f)DuplicantMonitorView.CreateDetail(sidebarContent,Monitoring.DuplicantMonitor.Instance);
            detailContent=original;
        }
        private void AppendQuestAction(QuestDef quest, QuestStatus status)
        {
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
                    if (QuestStore.AreCurrentObjectivesMet(quest, currentCounts)) CreateClaimButton(quest);
                    else CreateBodyText(Lang.English ? "Learning recorded; repair the current facilities before claiming." : "教学记录已保留；当前设施需修复后再领取。", WarningText);
                    break;
                case QuestStatus.Claimed:
                    CreateIconTextLine(StatusIcons.Claimed, Lang.T("任务已完成，继续下一个任务！"), PositiveText);
                    break;
            }

        }
        private void AppendVideoReferences(QuestDef quest)
        {
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

        }
    }
}
