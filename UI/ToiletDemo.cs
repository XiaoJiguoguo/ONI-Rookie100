using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rookie100.UI
{
    /// <summary>Illustrated layout lesson, independent of colony simulation and quest progress.</summary>
    public sealed class ToiletDemo : MonoBehaviour
    {
        private DemoPlayback state = new DemoPlayback();
        private RectTransform actor;
        private RawImage actorImage;
        private Image toilet, basin, pump;
        private RectTransform water, carriedWater, roomTop, roomLeft, roomRight;
        private TextMeshProUGUI toiletLabel;
        private TextMeshProUGUI caption;
        private Texture2D atlas;
        private float lastTime;
        private int lastFrame = -1;
        public Action<bool> PlaybackChanged;
        public bool IsPlaying => state.Playing;
        public DemoPlayback Capture() => state.Copy();
        private static string L(string zh, string en) => Lang.English ? en : zh;

        public void Initialize(Sprite toiletSprite, Color toiletTint, Sprite basinSprite, Color basinTint,
            Sprite pumpSprite, Color pumpTint, DemoPlayback saved, TMP_FontAsset font)
        {
            state = saved ?? new DemoPlayback();
            var floor = Box("Floor", transform, new Color(.4f,.43f,.42f,1f));
            Place(floor, .04f, .96f, 28f, 7f);
            water = Box("WaterSource", transform, new Color(.25f,.65f,.85f,.8f));
            Place(water, .08f, .36f, 31f, 17f);
            roomTop = Box("RoomTop", transform, new Color(.5f,.5f,.46f,1f));
            Place(roomTop, .04f,.96f,137f,4f);
            roomLeft = Box("RoomLeft", transform, new Color(.5f,.5f,.46f,1f));
            Place(roomLeft,.04f,.052f,35f,102f);
            roomRight = Box("RoomDoor", transform, new Color(.65f,.52f,.37f,.5f));
            Place(roomRight,.94f,.955f,35f,78f);
            toilet = Building("Outhouse", toiletSprite, toiletTint, .22f);
            pump = Building("PitcherPump", pumpSprite, pumpTint, .22f);
            basin = Building("WashBasin", basinSprite, basinTint, .64f);
            toiletLabel = Label("ToiletLabel", L("户外厕所", "Outhouse"), font, 11);
            Place(toiletLabel.rectTransform, .02f, .43f, 5f, 20f);
            var basinLabel = Label("BasinLabel", L("洗手盆 / 朝出口", "Wash basin / toward exit"), font, 11);
            Place(basinLabel.rectTransform, .43f, .88f, 5f, 20f);
            var exit = Label("Exit", L("出口", "Exit"), font, 11);
            Place(exit.rectTransform, .86f, 1f, 36f, 20f);
            caption = Label("Step", "", font, 12);
            caption.alignment = TextAlignmentOptions.TopLeft;
            var cr = caption.rectTransform;
            cr.anchorMin = new Vector2(0f,1f);cr.anchorMax = Vector2.one;
            cr.pivot = new Vector2(.5f,1f);cr.offsetMin = new Vector2(10f,-55f);cr.offsetMax = new Vector2(-10f,-6f);
            var actorObject = new GameObject("DemoDuplicant", typeof(RectTransform), typeof(RawImage));
            actorObject.transform.SetParent(transform, false);
            actor = actorObject.GetComponent<RectTransform>();actor.anchorMin=actor.anchorMax=new Vector2(.22f,0f);
            actor.pivot = new Vector2(.5f,0f);actor.sizeDelta = new Vector2(64f,96f);actor.anchoredPosition=new Vector2(0f,35f);
            actorImage = actorObject.GetComponent<RawImage>();actorImage.raycastTarget=false;
            carriedWater = Box("CarriedWater", actor, new Color(.25f,.65f,.95f,1f));
            carriedWater.anchorMin=carriedWater.anchorMax=new Vector2(.78f,.4f);
            carriedWater.sizeDelta=new Vector2(10f,16f);carriedWater.anchoredPosition=Vector2.zero;
            try
            {
                using(var stream=typeof(ToiletDemo).Assembly.GetManifestResourceStream("Rookie100.Resources.demo_walk.png"))
                using(var bytes=new MemoryStream())
                {
                    if(stream==null)throw new FileNotFoundException("Demo atlas missing");
                    stream.CopyTo(bytes);atlas=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    if(!ImageConversion.LoadImage(atlas,bytes.ToArray(),true)||atlas.width!=2048||atlas.height!=960)
                        throw new InvalidDataException("Unexpected demo atlas");
                    atlas.filterMode=FilterMode.Bilinear;atlas.wrapMode=TextureWrapMode.Clamp;
                    actorImage.texture=atlas;
                }
            }
            catch(Exception e)
            {
                actorImage.enabled=false;
                ModLogger.Warn("Demo actor unavailable: "+e.Message);
            }
            lastTime=Time.unscaledTime;
            Render();
        }

        public void TogglePlay()
        {
            state.Toggle();lastTime=Time.unscaledTime;
            PlaybackChanged?.Invoke(state.Playing);
        }
        public void Replay() { state.Replay();lastTime=Time.unscaledTime;Render();PlaybackChanged?.Invoke(true); }
        public void Pause() { state.Pause();PlaybackChanged?.Invoke(false); }
        private void OnEnable() { lastTime=Time.unscaledTime; }
        private void OnDisable() { Pause(); }
        private void Update()
        {
            float now=Time.unscaledTime;
            if(state.Playing)
            {
                state.Advance(Mathf.Max(0f,now-lastTime));
                if(!state.Playing)PlaybackChanged?.Invoke(false);
                Render();
            }
            lastTime=now;
        }
        private void Render()
        {
            if(caption==null)return;
            float t=state.Seconds;
            toilet.enabled=t>=3f && toilet.sprite!=null;
            pump.enabled=t<3f && pump.sprite!=null;basin.enabled=basin.sprite!=null;
            water.gameObject.SetActive(t<3f);carriedWater.gameObject.SetActive(t>=1.5f&&t<3f);
            roomTop.gameObject.SetActive(t>=4.5f);roomLeft.gameObject.SetActive(t>=4.5f);roomRight.gameObject.SetActive(t>=4.5f);
            toiletLabel.text=t<3f?L("水池 / 手压泵","Water / pitcher pump"):L("户外厕所","Outhouse");
            caption.text=t<1.5f?L("1. 在水池上方建手压泵，确保能取水。", "1. Place a pitcher pump above reachable water."):
                t<3f?L("2. 复制人运水到洗手盆，补水后才能洗手。", "2. Deliver water to the wash basin."):
                t<4.5f?L("3. 厕所补入泥土，清理后保持可用。", "3. Supply dirt and keep the outhouse usable."):
                t<6f?L("4. 用墙和门围合，让原版识别为公共厕所。", "4. Enclose with walls and a door: a recognized latrine."):
                t<7.5f?L("5. 路线连通；离开厕所时经过洗手盆。", "5. Connect the route; pass the basin when leaving."):
                t<8.7f?L("6. 实际如厕后洗手，完成一次试运行。", "6. Complete a real toilet-then-wash trial."):
                L("洗手后离开。六项检查全通过，才可领取奖励。", "Leave after washing. All six checks are required.");
            float x=t<1.5f?.22f:t<3f?Mathf.Lerp(.22f,.64f,(t-1.5f)/1.5f):
                t<6f?.22f:t<7.5f?Mathf.Lerp(.22f,.64f,(t-6f)/1.5f):t<8.7f?.64f:Mathf.Lerp(.64f,.9f,(t-8.7f)/1.3f);
            actor.anchorMin=actor.anchorMax=new Vector2(x,0f);
            bool walking=(t>=1.5f&&t<3f)||(t>=6f&&t<7.5f)||(t>=8.7f&&t<10f);
            int frame=walking?41+Mathf.FloorToInt(t*30f)%36:Mathf.FloorToInt(t*30f)%41;
            if(frame!=lastFrame && atlas!=null)
            {
                actorImage.uvRect=new Rect((frame%16)/16f,1f-(frame/16+1)/5f,1f/16f,1f/5f);
                lastFrame=frame;
            }
        }
        private Image Building(string name,Sprite sprite,Color tint,float x)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(transform,false);
            var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(x,0f);r.pivot=new Vector2(.5f,0f);
            r.sizeDelta=new Vector2(78f,94f);r.anchoredPosition=new Vector2(0f,35f);
            var im=go.GetComponent<Image>();im.sprite=sprite;im.color=tint;im.preserveAspect=true;im.raycastTarget=false;
            return im;
        }
        private TextMeshProUGUI Label(string name,string text,TMP_FontAsset font,int size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(transform,false);
            var label=go.GetComponent<TextMeshProUGUI>();if(font!=null)label.font=font;
            label.text=text;label.fontSize=size;label.color=new Color(.18f,.22f,.24f,1f);label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
            return label;
        }
        private static RectTransform Box(string name,Transform parent,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return go.GetComponent<RectTransform>();
        }
        private static void Place(RectTransform r,float left,float right,float bottom,float height)
        {
            r.anchorMin=new Vector2(left,0f);r.anchorMax=new Vector2(right,0f);r.offsetMin=new Vector2(0f,bottom);r.offsetMax=new Vector2(0f,bottom+height);
        }
        private void OnDestroy() { PlaybackChanged=null;if(actorImage!=null)actorImage.texture=null;if(atlas!=null)Destroy(atlas); }
    }
}
