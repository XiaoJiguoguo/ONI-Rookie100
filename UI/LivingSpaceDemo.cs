using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rookie100.UI
{
    // A visual lesson only: no access to QuestStore, colony buildings or research mutation.
    public sealed class LivingSpaceDemo : MonoBehaviour
    {
        private DemoPlayback state;
        private bool dining;
        private Image station, facility, dirt;
        private RectTransform actor, wallTop, wallLeft, door, power, researchFill;
        private RawImage actorImage;
        private TextMeshProUGUI caption, status, researchChoice;
        private Texture2D atlas;
        private Image blueprint, cursor, stationBlueprint;
        private RectTransform material, buildTrack, buildFill, roomOverlay, route, doorHeader;
        private Color facilityColor;
        private NativeLessonVisuals nativeBedroom;
        private float lastTime;
        public Action<bool> PlaybackChanged;
        public bool IsPlaying => state != null && state.Playing;
        public DemoPlayback Capture() => state?.Copy();
        private static string L(string zh,string en) => Lang.English ? en : zh;
        public void Initialize(bool isDining, Sprite stationSprite, Color stationTint,
            Sprite facilitySprite, Color facilityTint, Sprite dirtSprite, Color dirtTint,
            DemoPlayback saved, TMP_FontAsset font)
        {
            dining=isDining; state=saved?.Copy() ?? new DemoPlayback();
            state.EndSeconds=dining ? 24f : 20f;
            facilityColor=facilityTint;
            roomOverlay=Box("RoomOverlay",.06f,.92f,44f,103f,new Color(.38f,.74f,.51f,.19f));
            route=Box("ReachableRoute",.12f,.47f,39f,3f,new Color(.24f,.58f,.70f,.8f));
            Box("Floor",.04f,.96f,37f,7f,new Color(.40f,.43f,.42f));
            wallTop=Box("RoomTop",.05f,.95f,147f,5f,new Color(.50f,.50f,.46f));
            wallLeft=Box("RoomLeft",.05f,.06f,43f,104f,new Color(.50f,.50f,.46f));
            door=Box("Door",.92f,.935f,43f,78f,new Color(.66f,.52f,.37f));
            doorHeader=Box("WallAboveDoor",.92f,.935f,121f,26f,new Color(.50f,.50f,.46f));
            power=Box("PowerWire",.14f,.52f,49f,4f,new Color(.79f,.61f,.24f));
            var track=Box("ResearchTrack",.25f,.72f,10f,5f,new Color(.68f,.71f,.68f));track.gameObject.SetActive(dining);
            researchFill=Box("ResearchProgress",.25f,.25f,10f,5f,new Color(.37f,.57f,.40f));
            station=Building("ResearchStation",stationSprite,stationTint,.45f,92f,100f);
            facility=Building("LivingFacility",facilitySprite,facilityTint,.56f,110f,95f);
            dirt=Building("ResearchDirt",dirtSprite,dirtTint,.73f,30f,30f);
            stationBlueprint=Building("ResearchBlueprint",stationSprite,new Color(.28f,.68f,.91f,.65f),.45f,92f,100f);
            blueprint=Building("FacilityBlueprint",facilitySprite,new Color(.28f,.68f,.91f,.65f),.56f,110f,95f);
            buildTrack=Box("ConstructionTrack",.48f,.64f,139f,4f,new Color(.55f,.59f,.60f));
            buildFill=Box("ConstructionProgress",.48f,.48f,139f,4f,new Color(.24f,.65f,.85f));
            cursor=Box("PlacementCursor",.56f,.56f,75f,10f,Color.white).GetComponent<Image>();
            cursor.rectTransform.sizeDelta=new Vector2(7f,10f);
            cursor.rectTransform.localRotation=Quaternion.Euler(0f,0f,-30f);
            // Unity permits only one Graphic per object; Box already adds an Image.
            var actorObject=new GameObject("Duplicant",typeof(RectTransform),typeof(RawImage));
            actorObject.transform.SetParent(transform,false);
            actor=actorObject.GetComponent<RectTransform>();
            actor.anchorMin=actor.anchorMax=new Vector2(.18f,0f);
            actor.pivot=new Vector2(.5f,0f);actor.sizeDelta=new Vector2(76f,82f);
            actor.anchoredPosition=new Vector2(0f,43f);
            actorImage=actorObject.GetComponent<RawImage>();actorImage.raycastTarget=false;
            var parcel=new GameObject("CarriedMaterial",typeof(RectTransform),typeof(Image));
            parcel.transform.SetParent(actor,false);material=parcel.GetComponent<RectTransform>();
            material.anchorMin=material.anchorMax=new Vector2(.73f,.38f);
            material.sizeDelta=new Vector2(13f,12f);material.anchoredPosition=Vector2.zero;
            parcel.GetComponent<Image>().color=new Color(.65f,.48f,.30f);
            if(!dining&&dirtSprite!=null)
            {
                var materialImage=parcel.GetComponent<Image>();
                materialImage.sprite=dirtSprite;materialImage.color=dirtTint;materialImage.preserveAspect=true;
            }
            parcel.GetComponent<Image>().raycastTarget=false;
            try
            {
                using(var stream=typeof(LivingSpaceDemo).Assembly.GetManifestResourceStream("Rookie100.Resources.demo_walk.png"))
                using(var bytes=new MemoryStream())
                {
                    if(stream==null)throw new InvalidDataException("Missing lesson actor atlas");
                    stream.CopyTo(bytes);atlas=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    if(!TextureLoader.LoadImage(atlas,bytes.ToArray(),true)||atlas.width!=2048||atlas.height!=960)
                        throw new InvalidDataException("Unexpected lesson atlas");
                    atlas.filterMode=FilterMode.Bilinear;actorImage.texture=atlas;
                }
            }
            catch(Exception e){actorImage.enabled=false;ModLogger.Warn("Living lesson actor: "+e.Message);}
            caption=Text("Caption",font,13);caption.alignment=TextAlignmentOptions.TopLeft;
            var cr=caption.rectTransform;cr.anchorMin=new Vector2(0f,1f);cr.anchorMax=Vector2.one;
            cr.offsetMin=new Vector2(10f,-51f);cr.offsetMax=new Vector2(-10f,-7f);
            status=Text("Status",font,12);var sr=status.rectTransform;
            sr.anchorMin=new Vector2(.05f,0f);sr.anchorMax=new Vector2(.95f,0f);sr.offsetMin=new Vector2(0f,17f);sr.offsetMax=new Vector2(0f,37f);
            researchChoice=Text("ResearchSelection",font,12);
            var rr=researchChoice.rectTransform;rr.anchorMin=new Vector2(.1f,0f);rr.anchorMax=new Vector2(.9f,0f);
            rr.offsetMin=new Vector2(0f,113f);rr.offsetMax=new Vector2(0f,141f);
            if(!dining)nativeBedroom=NativeLessonVisuals.Create(actor,facility.rectTransform);
            lastTime=Time.unscaledTime;Render();
        }
        public void TogglePlay(){state.Toggle();lastTime=Time.unscaledTime;PlaybackChanged?.Invoke(state.Playing);}
        public void Replay(){state.Replay();lastTime=Time.unscaledTime;Render();PlaybackChanged?.Invoke(true);}
        public void Pause(){state?.Pause();PlaybackChanged?.Invoke(false);}
        public void SeekStep(int step){state.Seconds=dining ? new[]{0f,4f,8f,12f,20f}[Mathf.Clamp(step,0,4)] : Mathf.Clamp(step,0,4)*4f;state.Pause();Render();PlaybackChanged?.Invoke(false);}
        private void OnEnable(){lastTime=Time.unscaledTime;}
        private void OnDisable(){Pause();}
        private void Update()
        {
            float now=Time.unscaledTime;
            if(IsPlaying){state.Advance(Mathf.Max(0f,now-lastTime));Render();if(!state.Playing)PlaybackChanged?.Invoke(false);}
            lastTime=now;
        }
        private void Render()
        {
            if(caption==null)return;
            if(!dining){RenderBedroom();return;}
            RenderDining();
        }
        private void RenderDining()
        {
            float t=state.Seconds;
            researchChoice.gameObject.SetActive(t>=8f&&t<12f);
            researchChoice.text=L("研究选择：食物制备 → 解锁餐桌","Research: Meal Preparation → unlock tables");
            int step=t<4f?0:t<8f?1:t<12f?2:t<20f?3:4;
            caption.text=Lang.English ? new[]{
                "1. Place a research-station blueprint; deliver material and build it.",
                "2. Connect to a power source and supply dirt; keep the station reachable.",
                "3. Select Meal Preparation in Research; a duplicant completes research.",
                "4. Tables are unlocked: place a blueprint, deliver material and construct.",
                "5. Enclose with walls and a door; check the room overlay and access."}[step] : new[]{
                "1. 放置研究台蓝图，复制人搬料并完成施工。",
                "2. 连通电源，供应泥土，保持研究操作位置可达。",
                "3. 在研究界面选择食物制备，复制人进行研究。",
                "4. 餐桌解锁后放蓝图，复制人搬料并施工。",
                "5. 用墙和门围合，检查房间识别和餐桌通路。"}[step];
            stationBlueprint.enabled=t<3.5f&&stationBlueprint.sprite!=null;
            station.enabled=t>=3.5f&&t<12f&&station.sprite!=null;
            dirt.enabled=t>=4f&&t<12f&&dirt.sprite!=null;
            power.gameObject.SetActive(t>=4.5f&&t<12f);
            researchFill.gameObject.SetActive(t>=8f&&t<12f);
            researchFill.anchorMax=new Vector2(.25f+.47f*Mathf.Clamp01((t-8f)/3.5f),0f);
            blueprint.enabled=t>=12f&&t<18f&&blueprint.sprite!=null;
            blueprint.color=new Color(.28f,.68f,.91f,t<13.5f?.32f:.65f);
            facility.enabled=t>=18f&&facility.sprite!=null;facility.color=facilityColor;
            cursor.enabled=t<1f||(t>=8f&&t<9f)||(t>=12f&&t<13.5f);
            cursor.rectTransform.anchorMin=cursor.rectTransform.anchorMax=new Vector2(t<12f?.45f:.56f,0f);
            cursor.rectTransform.anchoredPosition=new Vector2(0f,75f);
            cursor.rectTransform.localScale=Vector3.one;
            bool firstDelivery=t>=1f&&t<2.5f;
            bool supply=t>=4f&&t<6f;
            bool tableDelivery=t>=13.5f&&t<16f;
            bool leave=t>=18f&&t<19f;
            float x=t<1f?.18f:t<2.5f?Mathf.Lerp(.18f,.36f,Ease((t-1f)/1.5f)):
                t<4f?.36f:t<6f?Mathf.Lerp(.36f,.68f,Ease((t-4f)/2f)):
                t<8f?Mathf.Lerp(.68f,.36f,Ease((t-6f)/2f)):
                t<12f?.36f:t<13.5f?Mathf.Lerp(.36f,.18f,Ease((t-12f)/1.5f)):
                t<16f?Mathf.Lerp(.18f,.45f,Ease((t-13.5f)/2.5f)):
                t<18f?.45f:Mathf.Lerp(.45f,.34f,Ease(t-18f));
            bool walking=firstDelivery||supply||(t>=6f&&t<8f)||(t>=12f&&t<13.5f)||tableDelivery||leave;
            bool building=(t>=2.5f&&t<3.5f)||(t>=16f&&t<18f);
            actor.anchorMin=actor.anchorMax=new Vector2(x,0f);
            actor.localScale=new Vector3((t>=6f&&t<8f)||(t>=12f&&t<13.5f)||t>=18f?-1f:1f,1f,1f);
            actor.anchoredPosition=new Vector2(0f,43f+(building?Mathf.Sin(t*12f)*1.5f:0f));
            if(atlas!=null)
            {
                int frame=walking?41+Mathf.FloorToInt(t*24f)%36:Mathf.FloorToInt(t*24f)%41;
                actorImage.uvRect=new Rect((frame%16)/16f,1f-(frame/16+1)/5f,1f/16f,1f/5f);
            }
            material.gameObject.SetActive(firstDelivery||(t>=6f&&t<8f)||tableDelivery);
            buildTrack.gameObject.SetActive(building);buildFill.gameObject.SetActive(building);
            buildFill.anchorMax=new Vector2(.48f+.16f*Mathf.Clamp01(t<4f?(t-2.5f):(t-16f)/2f),0f);
            wallTop.gameObject.SetActive(t>=20f);wallLeft.gameObject.SetActive(t>=20.5f);
            door.gameObject.SetActive(t>=21f);doorHeader.gameObject.SetActive(t>=21f);
            roomOverlay.gameObject.SetActive(t>=22f);route.gameObject.SetActive(t>=18f&&t<22f);
            status.text=t<4f?L("研究台：蓝图 → 搬料 → 施工","Research station: blueprint → delivery → construction"):
                t<8f?L("准备研究：电力、泥土与可达位置","Research preparation: power, dirt and access"):
                t<12f?L("选择食物制备 → 研究 → 解锁餐桌（示意）","Select Meal Preparation → research → unlock tables (illustration)"):
                t<20f?L("餐桌：解锁 → 蓝图 → 搬料 → 施工","Table: unlock → blueprint → delivery → construction"):
                L("房间叠层示意；实际任务以存档检测为准","Room overlay illustration; task checks use your colony");
        }
        private static float Ease(float value)
        {
            value=Mathf.Clamp01(value);return value*value*(3f-2f*value);
        }
        private void RenderBedroom()
        {
            researchChoice.gameObject.SetActive(false);
            float t=state.Seconds;
            int step=Mathf.Min(4,Mathf.FloorToInt(t/4f));
            caption.text=Lang.English ? new[]{
                "1. Choose a cot; preview placement while keeping the entrance clear.",
                "2. Confirm the blueprint; a duplicant delivers construction material.",
                "3. Construct the cot; leave its interaction position reachable.",
                "4. Build walls and a door to enclose the bedroom.",
                "5. Open the room overlay; verify barracks and cot assignments."}[step] : new[]{
                "1. 选择床铺，预览放置位置，保留入口和通路。",
                "2. 确认蓝图，复制人将建造材料搬到施工位置。",
                "3. 复制人施工完成床铺，保留可达的使用位置。",
                "4. 建造墙壁与门，围合宿舍并保留入口。",
                "5. 打开房间叠层确认宿舍，再检查床铺分配。"}[step];
            stationBlueprint.enabled=false;station.enabled=false;dirt.enabled=false;power.gameObject.SetActive(false);
            researchFill.gameObject.SetActive(false);
            blueprint.enabled=t<11f&&blueprint.sprite!=null;
            blueprint.color=new Color(.28f,.68f,.91f,t<4f?.32f:.65f);
            facility.enabled=t>=11f&&facility.sprite!=null;facility.color=facilityColor;
            cursor.enabled=t<4f;
            cursor.rectTransform.anchorMin=cursor.rectTransform.anchorMax=new Vector2(Mathf.Lerp(.76f,.56f,Ease(t/1.6f)),0f);
            cursor.rectTransform.anchoredPosition=new Vector2(0f,75f);
            float click=t>=2.2f&&t<2.7f ? 1f-.25f*Mathf.Sin((t-2.2f)/.5f*Mathf.PI) : 1f;
            cursor.rectTransform.localScale=Vector3.one*click;
            bool delivery=t>=4.5f&&t<7.5f;
            float x=t<4.5f?.18f:t<7.5f?Mathf.Lerp(.18f,.45f,Ease((t-4.5f)/3f)):.45f;
            bool exit=t>=11f&&t<12f;
            if(t>=11f)x=Mathf.Lerp(.45f,.34f,Ease(t-11f));
            actor.anchorMin=actor.anchorMax=new Vector2(x,0f);
            actor.localScale=new Vector3(t>=11f?-1f:1f,1f,1f);
            bool walking=delivery||exit;
            bool constructing=t>=8f&&t<11f;
            // The atlas contains idle and walking poses; tool feedback indicates construction.
            actor.anchoredPosition=new Vector2(0f,43f+(constructing?Mathf.Sin((t-8f)*12f)*1.5f:0f));
            if(atlas!=null)
            {
                int frame=walking?41+Mathf.FloorToInt(t*24f)%36:Mathf.FloorToInt(t*24f)%41;
                actorImage.uvRect=new Rect((frame%16)/16f,1f-(frame/16+1)/5f,1f/16f,1f/5f);
            }
            material.gameObject.SetActive(t>=4.5f&&t<8f);
            material.anchoredPosition=Vector2.zero;
            bool native=nativeBedroom!=null&&nativeBedroom.Draw(t,walking,material.gameObject.activeSelf,constructing,t>=11f,material);
            if(nativeBedroom!=null&&nativeBedroom.Available)actorImage.enabled=false;
            else actorImage.enabled=atlas!=null;
            if(native)facility.enabled=false;
            buildTrack.gameObject.SetActive(constructing);buildFill.gameObject.SetActive(constructing);
            buildFill.anchorMax=new Vector2(.48f+.16f*Mathf.Clamp01((t-8f)/3f),0f);
            wallTop.gameObject.SetActive(t>=12f);wallLeft.gameObject.SetActive(t>=13f);door.gameObject.SetActive(t>=14f);
            doorHeader.gameObject.SetActive(t>=14f);
            roomOverlay.gameObject.SetActive(t>=16f);route.gameObject.SetActive(t>=8f&&t<16f);
            status.text=t<4f?L("放置预览：先检查空间与通路","Placement preview: check space and access"):
                t<8f?L("蓝图等待搬料；有材料且可达才能施工","Blueprint: delivery requires materials and access"):
                t<11f?L("施工中（示意）","Construction in progress (illustration)"):
                t<12f?L("床铺建成，使用位置保持畅通","Cot complete; keep its interaction position clear"):
                t<16f?L("依次围合：墙壁 → 门 → 封闭房间","Enclose: walls → door → closed room"):
                L("房间叠层示意；实际任务以存档检测为准","Room overlay illustration; task checks use your colony");
            if(nativeBedroom!=null&&!nativeBedroom.Available)
                status.text=L("原版动画未加载，当前显示示意版；详情见日志","Native animation unavailable; illustration shown. See log.");
        }
        private Image Building(string name,Sprite sprite,Color tint,float x,float width,float height)
        {
            var r=Box(name,x,x,44f,height,Color.clear);r.pivot=new Vector2(.5f,0f);r.sizeDelta=new Vector2(width,height);
            var im=r.GetComponent<Image>();im.sprite=sprite;im.color=tint;im.preserveAspect=true;return im;
        }
        private RectTransform Box(string name,float left,float right,float bottom,float height,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(transform,false);
            var im=go.GetComponent<Image>();im.color=color;im.raycastTarget=false;
            var r=go.GetComponent<RectTransform>();r.anchorMin=new Vector2(left,0f);r.anchorMax=new Vector2(right,0f);
            r.offsetMin=new Vector2(0f,bottom);r.offsetMax=new Vector2(0f,bottom+height);return r;
        }
        private TextMeshProUGUI Text(string name,TMP_FontAsset font,int size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(transform,false);
            var text=go.GetComponent<TextMeshProUGUI>();if(font!=null)text.font=font;text.fontSize=size;
            text.color=new Color(.14f,.15f,.18f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;return text;
        }
        private void OnDestroy(){PlaybackChanged=null;if(actorImage!=null)actorImage.texture=null;if(atlas!=null)Destroy(atlas);}
    }
}
