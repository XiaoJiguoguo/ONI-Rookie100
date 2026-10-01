using System;
using Rookie100.Monitoring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rookie100.UI
{
    // Both views subscribe to one source and share selection. Unknown metrics stay unknown.
    public sealed class DuplicantMonitorView : MonoBehaviour
    {
        private DuplicantMonitor source;
        private TextMeshProUGUI title, values, fallback;
        private Transform portraitHost;
        private Image portrait;
        private bool compact;
        private int portraitId = int.MinValue;
        private bool portraitWarned;
        private float retryPortraitAfter;
        private static readonly Color Background = new Color(.17f,.19f,.25f,.96f);
        private static readonly Color ButtonColor = new Color(.29f,.31f,.37f,1f);
        public static GameObject CreateHud(Transform parent, DuplicantMonitor monitor)
        {
            var root = Box("Rookie100DuplicantHUD", parent);
            var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f,1f); rect.pivot = new Vector2(.5f,.5f);
            rect.anchoredPosition = new Vector2(0f,-110f); rect.sizeDelta = new Vector2(480f,44f);
            var canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            var gameCanvas = parent.GetComponentInParent<Canvas>();
            canvas.sortingOrder = (gameCanvas != null ? gameCanvas.sortingOrder : 0) + 1;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<MonitorHudDrag>();
            var view = root.AddComponent<DuplicantMonitorView>();view.Initialize(monitor, true);
            return root;
        }
        public static GameObject CreateDetail(Transform parent, DuplicantMonitor monitor)
        {
            var root = Box("Rookie100DuplicantDetail", parent);
            root.AddComponent<LayoutElement>().preferredHeight = 210f;
            root.AddComponent<DuplicantMonitorView>().Initialize(monitor, false);
            return root;
        }
        private void Initialize(DuplicantMonitor monitor, bool small)
        {
            source = monitor; compact = small;
            title = Text("Title", transform, small ? 12 : 15);
            Place(title.rectTransform, new Vector2(small ? 66f : 8f, -4f), new Vector2(small ? 334f : 230f, 24f));
            if (small) title.raycastTarget = true;
            var avatar = Box("Portrait", transform); portraitHost = avatar.transform;
            Place(avatar.GetComponent<RectTransform>(), new Vector2(small ? 27f : 8f, small ? -5f : -40f), new Vector2(small ? 34f : 62f, small ? 34f : 66f));
            portrait = avatar.GetComponent<Image>(); portrait.preserveAspect = true; portrait.raycastTarget = small;
            fallback = Text("AvatarFallback", avatar.transform, 16); Stretch(fallback.rectTransform); fallback.alignment = TextAlignmentOptions.Center;
            values = Text("Values", transform, small ? 11 : 13);
            if (small)
            {
                values.enableAutoSizing = true; values.fontSizeMin = 9f; values.fontSizeMax = 11f;
                values.textWrappingMode = TextWrappingModes.NoWrap;
                values.overflowMode = TextOverflowModes.Ellipsis;
            }
            Place(values.rectTransform, new Vector2(small ? 66f : 78f, small ? -22f : -40f), new Vector2(small ? 338f : 450f, small ? 19f : 132f));
            Button("Previous", transform, small ? "<" : (Lang.English ? "Prev" : "上一个"), new Vector2(small ? 4f : 8f, small ? -9f : -174f), new Vector2(small ? 20f : 64f, 26f), () => source?.Move(-1));
            Button("Next", transform, small ? ">" : (Lang.English ? "Next" : "下一个"), new Vector2(small ? 409f : 78f, small ? -9f : -174f), new Vector2(small ? 20f : 64f, 26f), () => source?.Move(1));
            if (small)
            {
                Button("Details", transform, Lang.English ? "Info" : "详情", new Vector2(432f,-2f), new Vector2(44f,19f), () => QuestPanel.ShowMonitor());
                Button("Reset", transform, Lang.English ? "Reset" : "复位", new Vector2(432f,-23f), new Vector2(44f,19f), () => GetComponent<MonitorHudDrag>().ResetPosition());
            }
            if (source != null) source.Changed += Refresh;
            Refresh();
        }
        private void OnDestroy() { if(source!=null)source.Changed-=Refresh; }
        private static string Percent(float? x) => x.HasValue?Math.Round(x.Value)+"%":(Lang.English?"unknown":"未知");
        private void Refresh()
        {
            var row=source?.State.Current;
            title.text=(Lang.English?"Duplicants":"复制人监测")+" · "+(source?.State.Count??0);
            if(source==null || !source.Available)
            {
                values.text=Lang.English?"Monitoring temporarily unavailable.":"复制人数据暂不可用，等待下一次读取。";
                fallback.text="?";if(portrait!=null)portrait.enabled=false;portraitId=int.MinValue;return;
            }
            if(row==null){values.text=Lang.English?"No living duplicants on this asteroid.":"当前星体没有可监测的存活复制人。";fallback.text="?";if(portrait!=null)portrait.enabled=false;portraitId=int.MinValue;return;}
            values.text=(row.Name??"")+"\n"+(Lang.English?"Health ":"生命 ")+Percent(row.Health)+"  "+(Lang.English?"Stress ":"压力 ")+Percent(row.Stress)+"\n"+(Lang.English?"Breath ":"呼吸 ")+Percent(row.Breath)+"\n"+(Lang.English?"Calories ":"热量 ")+(row.Calories.HasValue?Math.Round(row.Calories.Value)+" kcal":(Lang.English?"not applicable / unknown":"不适用 / 未知"));
            if(!compact)
            {
                var rect = values.rectTransform;
                rect.anchorMin = new Vector2(0f,0f); rect.anchorMax = new Vector2(1f,1f);
                rect.offsetMin = new Vector2(78f,38f); rect.offsetMax = new Vector2(-8f,-40f);
            }
            if (compact)
            {
                title.text = (row.Name ?? "") + " · " + (Lang.English ? "Duplicants " : "复制人 ") + (source?.State.Count ?? 0);
                values.text = (Lang.English ? "HP " : "生命 ") + Percent(row.Health) + "  " + (Lang.English ? "Stress " : "压力 ") + Percent(row.Stress) + "  " + (Lang.English ? "Breath " : "呼吸 ") + Percent(row.Breath) + "  " + (row.Calories.HasValue ? Math.Round(row.Calories.Value) + " kcal" : (Lang.English ? "unknown" : "未知"));
            }
            if(!compact)values.text+="\n"+(Lang.English?"Current asteroid; shared read-only observations.":"当前星体 · 与游戏头像面板同步 · 只读监测");
            if(portraitId!=row.Id && Time.unscaledTime >= retryPortraitAfter)BindPortrait(row);
        }
        private void BindPortrait(DuplicantReading row)
        {
            retryPortraitAfter = Time.unscaledTime + 3f;
            fallback.text = string.IsNullOrEmpty(row.Name) ? "?" : row.Name.Substring(0,1);
            portrait.enabled = false;
            try
            {
                var identity = source.Resolve(row.Id);
                if (identity == null) return;
                // Game-owned, personality-specific portrait sprite. No prefab cloning,
                // animation atlases, or external portrait assets are required.
                var personality = Db.Get().Personalities.Get(identity.personalityResourceId);
                if (personality == null) personality = Db.Get().Personalities.GetPersonalityFromNameStringKey(identity.nameStringKey);
                var sprite = personality?.GetMiniIcon();
                if (sprite == null) return;
                portrait.sprite = sprite; portrait.color = Color.white; portrait.enabled = true;
                fallback.text = ""; portraitId = row.Id; retryPortraitAfter = 0f;
            }
            catch (Exception e)
            {
                if (!portraitWarned) { portraitWarned = true; ModLogger.Warn("原版复制人头像读取失败: " + e.Message); }
            }
        }
        public void LocateCurrent()
        {
            var row = source?.State.Current;
            var identity = row == null ? null : source.Resolve(row.Id);
            if (identity == null || identity.GetMyWorldId() != ClusterManager.Instance?.activeWorld?.id) return;
            var selectable = identity.GetComponent<KSelectable>();
            if (selectable != null && SelectTool.Instance != null)
                SelectTool.Instance.SelectAndFocus(identity.transform.position, selectable);
        }
        private static GameObject Box(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);go.GetComponent<Image>().color=Background;return go;}
        private static TextMeshProUGUI Text(string name,Transform parent,int size){var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var text=go.GetComponent<TextMeshProUGUI>();text.fontSize=size;text.color=Color.white;text.raycastTarget=false;text.alignment=TextAlignmentOptions.TopLeft;return text;}
        private static void Place(RectTransform rect,Vector2 position,Vector2 size,bool right=false){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(right?1f:0f,1f);rect.anchoredPosition=position;rect.sizeDelta=size;}
        private static void Stretch(RectTransform rect){if(rect==null)return;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static void Button(string name,Transform parent,string caption,Vector2 pos,Vector2 size,System.Action click,bool right=false){var go=Box(name,parent);Place(go.GetComponent<RectTransform>(),pos,size,right);go.GetComponent<Image>().color=ButtonColor;var label=Text("Label",go.transform,11);Stretch(label.rectTransform);label.text=caption;label.alignment=TextAlignmentOptions.Center;var button=go.AddComponent<Button>();button.targetGraphic=go.GetComponent<Image>();button.onClick.AddListener(()=>click());}
    }
}
