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
        private Image toilet, basin, pump, pumpBlueprint, basinBlueprint, toiletBlueprint;
        private RectTransform roomHeader, roomOverlay, buildTrack, buildFill;
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
            state = saved?.Copy() ?? new DemoPlayback();
            state.EndSeconds=24f;
            roomOverlay=Box("RoomOverlay",transform,new Color(.38f,.74f,.51f,.19f));
            Place(roomOverlay,.052f,.94f,35f,102f);
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
            roomHeader=Box("WallAboveDoor",transform,new Color(.5f,.5f,.46f,1f));
            Place(roomHeader,.94f,.955f,113f,24f);
            toilet = Building("Outhouse", toiletSprite, toiletTint, .22f);
            pump = Building("PitcherPump", pumpSprite, pumpTint, .22f);
            basin = Building("WashBasin", basinSprite, basinTint, .64f);
            var blue=new Color(.28f,.68f,.91f,.65f);
            pumpBlueprint=Building("PumpBlueprint",pumpSprite,blue,.22f);
            basinBlueprint=Building("BasinBlueprint",basinSprite,blue,.64f);
            toiletBlueprint=Building("ToiletBlueprint",toiletSprite,blue,.22f);
            buildTrack=Box("ConstructionTrack",transform,new Color(.55f,.59f,.60f));
            Place(buildTrack,.12f,.32f,132f,4f);
            buildFill=Box("ConstructionProgress",transform,new Color(.24f,.65f,.85f));
            Place(buildFill,.12f,.12f,132f,4f);
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
                    if(!TextureLoader.LoadImage(atlas,bytes.ToArray(),true)||atlas.width!=2048||atlas.height!=960)
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
        public void SeekStep(int step) { state.Seconds=Mathf.Clamp(step,0,5)*4f;state.Pause();Render();PlaybackChanged?.Invoke(false); }
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
            pumpBlueprint.enabled=t<3f&&pumpBlueprint.sprite!=null;
            pump.enabled=t>=3f&&t<8f&&pump.sprite!=null;
            basinBlueprint.enabled=t>=4f&&t<5.5f&&basinBlueprint.sprite!=null;
            basin.enabled=t>=5.5f&&basin.sprite!=null;
            toiletBlueprint.enabled=t>=8f&&t<11f&&toiletBlueprint.sprite!=null;
            toilet.enabled=t>=11f&&toilet.sprite!=null;
            water.gameObject.SetActive(t<8f);
            roomTop.gameObject.SetActive(t>=12f);roomLeft.gameObject.SetActive(t>=13f);
            roomRight.gameObject.SetActive(t>=14f);roomHeader.gameObject.SetActive(t>=14f);
            roomOverlay.gameObject.SetActive(t>=15f&&t<16f||t>=23f);
            bool building=t>=1.5f&&t<3f||t>=4f&&t<5.5f||t>=9.5f&&t<11f;
            float left=t>=4f&&t<5.5f?.54f:.12f;
            Place(buildTrack,left,left+.2f,132f,4f);
            Place(buildFill,left,left+.2f*Mathf.Clamp01((t-(t<4f?1.5f:t<8f?4f:9.5f))/1.5f),132f,4f);
            buildTrack.gameObject.SetActive(building);buildFill.gameObject.SetActive(building);
            carriedWater.gameObject.SetActive(t>=6.5f&&t<9.5f);
            carriedWater.GetComponent<Image>().color=t<8f?new Color(.25f,.65f,.95f):new Color(.65f,.48f,.30f);
            toiletLabel.text=t<8f?L("水池 / 手压泵","Water / pitcher pump"):L("户外厕所","Outhouse");
            int step=Mathf.Min(5,Mathf.FloorToInt(t/4f));
            caption.text=Lang.English ? new[]{
                "1. Place a pump blueprint above reachable water; construct it.",
                "2. Build the basin facing the exit; deliver water from the pump.",
                "3. Place an outhouse blueprint; deliver material and supply dirt.",
                "4. Enclose with walls and a door; inspect the latrine room overlay.",
                "5. Keep the exit route through the basin; demonstrate toilet use.",
                "6. Wash after toilet use, then leave; perform the trial in your colony."}[step] : new[]{
                "1. 在可达水池上方放手压泵蓝图，复制人搬料施工。",
                "2. 建洗手盆并朝向出口，从手压泵取水送到洗手盆。",
                "3. 放户外厕所蓝图，搬料施工并补充泥土。",
                "4. 用墙和门围合，在房间叠层检查公共厕所。",
                "5. 出口路线经过洗手盆，演示复制人如厕。",
                "6. 如厕后洗手再离开；实际试运行需在存档中完成。"}[step];
            float x=t<1.5f?Mathf.Lerp(.08f,.22f,Ease(t/1.5f)):
                t<3f?.22f:t<4f?Mathf.Lerp(.22f,.64f,Ease(t-3f)):
                t<5.5f?.64f:t<6.5f?Mathf.Lerp(.64f,.22f,Ease(t-5.5f)):
                t<8f?Mathf.Lerp(.22f,.64f,Ease((t-6.5f)/1.5f)):
                t<9.5f?Mathf.Lerp(.64f,.22f,Ease((t-8f)/1.5f)):
                t<18f?.22f:t<20f?Mathf.Lerp(.22f,.64f,Ease((t-18f)/2f)):
                t<22f?.64f:Mathf.Lerp(.64f,.9f,Ease((t-22f)/2f));
            actor.anchorMin=actor.anchorMax=new Vector2(x,0f);
            actor.localScale=new Vector3((t>=5.5f&&t<6.5f)||(t>=8f&&t<9.5f)?-1f:1f,1f,1f);
            actor.anchoredPosition=new Vector2(0f,35f+(building?Mathf.Sin(t*12f)*1.5f:0f));
            bool walking=t<1.5f||t>=3f&&t<4f||t>=5.5f&&t<9.5f||t>=18f&&t<20f||t>=22f&&t<24f;
            int frame=walking?41+Mathf.FloorToInt(t*24f)%36:Mathf.FloorToInt(t*24f)%41;
            if(frame!=lastFrame && atlas!=null)
            {
                actorImage.uvRect=new Rect((frame%16)/16f,1f-(frame/16+1)/5f,1f/16f,1f/5f);
                lastFrame=frame;
            }
        }
        private static float Ease(float value) { value=Mathf.Clamp01(value);return value*value*(3f-2f*value); }
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
