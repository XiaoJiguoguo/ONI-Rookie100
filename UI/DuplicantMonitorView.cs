using System;
using System.Reflection;
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
        private CrewPortrait portrait;
        private bool compact, collapsed;
        private int portraitId = int.MinValue;
        private bool portraitWarned;
        private float retryPortraitAfter;
        private static readonly Color Background = new Color(.17f,.19f,.25f,.96f);
        private static readonly Color ButtonColor = new Color(.29f,.31f,.37f,1f);
        public static GameObject CreateHud(Transform parent, DuplicantMonitor monitor)
        {
            var root = Box("Rookie100DuplicantHUD", parent);
            var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f,1f);
            rect.anchoredPosition = new Vector2(-12f,-112f); rect.sizeDelta = new Vector2(266f,184f);
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
            title = Text("Title",transform,15); Place(title.rectTransform,new Vector2(8f,-8f),new Vector2(small?154f:230f,26f));
            if (small) Button("Fold",transform,Lang.English?"Fold":"展开 / 收起",new Vector2(-72f,-8f),new Vector2(64f,24f),()=>
            {
                collapsed = !collapsed;values.gameObject.SetActive(!collapsed);portraitHost.gameObject.SetActive(!collapsed);
                foreach (string child in new[] { "Previous", "Next", "Tasks" })
                    transform.Find(child)?.gameObject.SetActive(!collapsed);
                GetComponent<RectTransform>().sizeDelta = new Vector2(266f,collapsed?70f:184f);Refresh();
            },true);
            var avatar = Box("Portrait",transform);portraitHost=avatar.transform;
            Place(avatar.GetComponent<RectTransform>(),new Vector2(8f,-40f),new Vector2(62f,66f));
            fallback=Text("AvatarFallback",avatar.transform,20);Stretch(fallback.rectTransform);fallback.alignment=TextAlignmentOptions.Center;
            values=Text("Values",transform,small?12:13);Place(values.rectTransform,new Vector2(78f,-40f),new Vector2(small?180f:450f,small?102f:132f));
            Button("Previous",transform,Lang.English?"Prev":"上一个",new Vector2(8f,-(small?150f:174f)),new Vector2(64f,24f),()=>source?.Move(-1));
            Button("Next",transform,Lang.English?"Next":"下一个",new Vector2(78f,-(small?150f:174f)),new Vector2(64f,24f),()=>source?.Move(1));
            if(small)Button("Tasks",transform,Lang.English?"Details":"详览",new Vector2(150f,-150f),new Vector2(102f,24f),()=>QuestPanel.ShowMonitor());
            if(small)Button("TaskEntry",transform,Lang.English?"Tasks":"任务",new Vector2(-64f,-40f),new Vector2(56f,24f),()=>QuestPanel.Toggle(),true);
            if(source!=null)source.Changed+=Refresh;Refresh();
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
                fallback.text="?";if(portrait!=null)portrait.gameObject.SetActive(false);portraitId=int.MinValue;return;
            }
            if(row==null){values.text=Lang.English?"No living duplicants on this asteroid.":"当前星体没有可监测的存活复制人。";fallback.text="?";if(portrait!=null)portrait.gameObject.SetActive(false);portraitId=int.MinValue;return;}
            values.text=(row.Name??"")+"\n"+(Lang.English?"Health ":"生命 ")+Percent(row.Health)+"  "+(Lang.English?"Stress ":"压力 ")+Percent(row.Stress)+"\n"+(Lang.English?"Breath ":"呼吸 ")+Percent(row.Breath)+"\n"+(Lang.English?"Calories ":"热量 ")+(row.Calories.HasValue?Math.Round(row.Calories.Value)+" kcal":(Lang.English?"not applicable / unknown":"不适用 / 未知"));
            if(!compact)
            {
                var rect = values.rectTransform;
                rect.anchorMin = new Vector2(0f,0f); rect.anchorMax = new Vector2(1f,1f);
                rect.offsetMin = new Vector2(78f,38f); rect.offsetMax = new Vector2(-8f,-40f);
            }
            if(!compact)values.text+="\n"+(Lang.English?"Current asteroid; shared read-only observations.":"当前星体 · 与游戏头像面板同步 · 只读监测");
            if(!collapsed && portraitId!=row.Id && Time.unscaledTime >= retryPortraitAfter)BindPortrait(row);
        }
        private void BindPortrait(DuplicantReading row)
        {
            retryPortraitAfter = Time.unscaledTime + 10f;
            fallback.text=string.IsNullOrEmpty(row.Name)?"?":row.Name.Substring(0,1);
            if(portrait!=null)portrait.gameObject.SetActive(false);
            try
            {
                var identity=source.Resolve(row.Id);MinionAssignablesProxy proxy=null;
                foreach(var item in Components.MinionAssignablesProxy.Items)
                    if(item!=null&&item.GetTargetGameObject()==identity?.gameObject){proxy=item;break;}
                if(proxy==null)return;
                if(portrait==null)
                {
                    CrewPortrait template=null;
                    foreach(var ui in Resources.FindObjectsOfTypeAll<AssignableSideScreenRow>())
                    {
                        var field=typeof(AssignableSideScreenRow).GetField("crewPortraitPrefab",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        template=field?.GetValue(ui) as CrewPortrait;if(template!=null)break;
                    }
                    if(template==null)return; // Initials remain visible until native UI assets are available.
                    portrait=UnityEngine.Object.Instantiate(template,portraitHost,false);
                    Stretch(portrait.GetComponent<RectTransform>());
                    foreach(var graphic in portrait.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                }
                portrait.gameObject.SetActive(true);portrait.SetAlpha(1f);portrait.SetIdentityObject(proxy,false);
                fallback.text="";portraitId=row.Id;retryPortraitAfter=0f;
            }
            catch(Exception e){if(!portraitWarned){portraitWarned=true;ModLogger.Warn("原版复制人头像暂不可用，显示姓名首字: "+e.Message);}}
        }
        private static GameObject Box(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);go.GetComponent<Image>().color=Background;return go;}
        private static TextMeshProUGUI Text(string name,Transform parent,int size){var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var text=go.GetComponent<TextMeshProUGUI>();text.fontSize=size;text.color=Color.white;text.raycastTarget=false;text.alignment=TextAlignmentOptions.TopLeft;return text;}
        private static void Place(RectTransform rect,Vector2 position,Vector2 size,bool right=false){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(right?1f:0f,1f);rect.anchoredPosition=position;rect.sizeDelta=size;}
        private static void Stretch(RectTransform rect){if(rect==null)return;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static void Button(string name,Transform parent,string caption,Vector2 pos,Vector2 size,System.Action click,bool right=false){var go=Box(name,parent);Place(go.GetComponent<RectTransform>(),pos,size,right);go.GetComponent<Image>().color=ButtonColor;var label=Text("Label",go.transform,11);Stretch(label.rectTransform);label.text=caption;label.alignment=TextAlignmentOptions.Center;var button=go.AddComponent<Button>();button.targetGraphic=go.GetComponent<Image>();button.onClick.AddListener(()=>click());}
    }
}
