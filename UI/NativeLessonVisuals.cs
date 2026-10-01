using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Rookie100.UI
{
    // Visual-only UI objects: never instantiate Minion or a complete building prefab.
    // The paused native controller is sampled from the lesson clock, including seeks.
    internal sealed class NativeLessonVisuals
    {
        private GameObject portraitObject, bedObject;
        private KBatchedAnimController portrait, bed;
        private string idle, walk, carry, workPre, workLoop, workPost;
        private string pickupPre, pickupPost, placePre, placePost;
        private string currentClip;
        public bool Available { get; private set; }

        public static NativeLessonVisuals Create(RectTransform actorHost, RectTransform bedHost)
        {
            var visuals=new NativeLessonVisuals();
            try { visuals.Initialize(actorHost,bedHost); }
            catch(Exception e)
            {
                visuals.Disable();
                ModLogger.Error("Native bedroom lesson unavailable; retaining illustrated lesson",e);
            }
            return visuals;
        }

        private static KAnimFile RequireFile(string name)
        {
            if(!Assets.TryGetAnim(name,out var file)||file==null)
                throw new InvalidOperationException("Missing native animation file: "+name);
            return file;
        }
        private static string FindClip(KAnimFile file,string[] candidates,Func<string,bool> match=null)
        {
            var data=file.GetData();
            var clips=new List<KAnim.Anim>();
            for(int i=0;i<data.animCount;i++)clips.Add(data.GetAnim(i));
            // KAnimFileData.GetAnim(string) searches the entire batch, not this file.
            foreach(var name in candidates)if(clips.Any(a=>a!=null&&a.name==name))return name;
            if(match!=null)
                foreach(var anim in clips)
                {
                    if(anim!=null&&match(anim.name.ToLowerInvariant()))return anim.name;
                }
            throw new InvalidOperationException("No matching clip in "+data.name+": "+string.Join(", ",candidates));
        }
        private void Initialize(RectTransform actorHost,RectTransform bedHost)
        {
            var body=RequireFile("body_comp_default_kanim");
            var idles=RequireFile("anim_idles_default_kanim");
            var locomotion=RequireFile("anim_loco_new_kanim");
            var construction=RequireFile("anim_construction_default_kanim");
            var bedFile=RequireFile("bedlg_kanim");
            idle=FindClip(idles,new[]{"idle_default","idle_loop","idle"});
            walk=FindClip(locomotion,new[]{"floor_floor_1_0_loop","walk_loop","walk"});
            // Use a carry pose only when it exists; hand attachment remains independent.
            try { carry=FindClip(locomotion,new[]{"walk_carry_loop","walk_carry","carry_walk_loop"},n=>n.Contains("carry")&&n.Contains("walk")&&!n.Contains("pre")&&!n.Contains("pst")); }
            catch(InvalidOperationException) { carry=walk; }
            workPre=FindClip(construction,new[]{"build_in_place_pre","build_antic"});
            workLoop=FindClip(construction,new[]{"build_in_place_loop","build_loop"});
            workPost=FindClip(construction,new[]{"build_in_place_pst","build_settle"});
            pickupPre=FindClip(construction,new[]{"pickup_pre"});pickupPost=FindClip(construction,new[]{"pickup_pst"});
            placePre=FindClip(construction,new[]{"place_pre"});placePost=FindClip(construction,new[]{"place_pst"});
            string bedClip=FindClip(bedFile,new[]{"off","idle"});

            var prefab=Assets.TryGetPrefab(new Tag(FullMinionUIPortrait.ID));
            if(prefab==null)throw new InvalidOperationException("FullMinionUIPortrait prefab not loaded");
            portraitObject=Util.KInstantiateUI(prefab,actorHost.gameObject,false);
            portraitObject.SetActive(false);
            portraitObject.name="Rookie100NativeLessonDuplicant";
            portrait=portraitObject.GetComponent<KBatchedAnimController>();
            if(portrait==null)throw new InvalidOperationException("Native portrait has no animation controller");
            portrait.AnimFiles=new[]{body,idles,locomotion,construction};
            portrait.initialAnim=idle;portrait.initialMode=KAnim.PlayMode.Paused;
            portrait.randomiseLoopedOffset=false;portrait.forceUseGameTime=false;
            var rt=portraitObject.GetComponent<RectTransform>();
            rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,0f);
            rt.offsetMin=rt.offsetMax=Vector2.zero;
            portraitObject.SetActive(true);
            portrait.SwapAnims(new[]{body,idles,locomotion,construction});
            // Clone only the game's UI portrait. It has no AI, identity or colony registration.
            var personality=Db.Get().Personalities.resources.FirstOrDefault(p=>p.model==MinionConfig.MODEL);
            if(personality==null)throw new InvalidOperationException("Native personalities not loaded");
            portraitObject.GetComponent<Accessorizer>().ApplyMinionPersonality(personality);
            portrait.Play(idle,KAnim.PlayMode.Paused,0f);
            foreach(var clip in new[]{idle,walk,carry,workPre,workLoop,workPost,pickupPre,pickupPost,placePre,placePost})
                if(!portrait.HasAnimation(clip))throw new InvalidOperationException("Portrait cannot play "+clip);
            currentClip=idle;

            bedObject=new GameObject("Rookie100NativeLessonBed",typeof(RectTransform));
            bedObject.SetActive(false);bedObject.transform.SetParent(bedHost,false);
            bedObject.layer=bedHost.gameObject.layer;
            var br=bedObject.GetComponent<RectTransform>();
            br.anchorMin=Vector2.zero;br.anchorMax=Vector2.one;br.pivot=new Vector2(.5f,0f);
            br.offsetMin=br.offsetMax=Vector2.zero;
            bed=bedObject.AddComponent<KBatchedAnimController>();
            bed.materialType=KAnimBatchGroup.MaterialType.UI;
            bed.AnimFiles=new[]{bedFile};bed.initialAnim=bedClip;bed.initialMode=KAnim.PlayMode.Paused;
            bed.animScale=1f;bed.setScaleFromAnim=true;
            bed.forceUseGameTime=false;bed.randomiseLoopedOffset=false;
            bedObject.SetActive(true);bed.Play(bedClip,KAnim.PlayMode.Paused,0f);
            bedObject.SetActive(false);
            Available=true;
            ModLogger.Log("Native bedroom lesson ready: idle="+idle+", walk="+walk+", carry="+carry+
                ", construction="+workPre+"/"+workLoop+"/"+workPost+", bed="+bedClip);
        }
        private static void Sample(KBatchedAnimController controller,string clip,float seconds)
        {
            var anim=controller.GetAnim(clip);
            float duration=anim==null?0f:anim.totalTime;
            controller.SetElapsedTime(duration>0f?Mathf.Repeat(Mathf.Max(0f,seconds),duration):0f);
            controller.SetDirty();controller.UpdateAnim(0f);
        }
        public bool Draw(float seconds,bool walking,bool carrying,bool constructing,bool bedComplete,RectTransform parcel)
        {
            if(!Available)return false;
            try
            {
                float local=seconds;
                string clip=walking?(carrying?carry:walk):idle;
                if(seconds>=4f&&seconds<4.5f)
                {
                    local=seconds-4f;float preLength=portrait.GetAnim(pickupPre).totalTime;
                    if(local<preLength)clip=pickupPre;
                    else { clip=pickupPost;local-=preLength; }
                }
                else if(seconds>=7.5f&&seconds<8f)
                {
                    local=seconds-7.5f;float preLength=portrait.GetAnim(placePre).totalTime;
                    if(local<preLength)clip=placePre;
                    else { clip=placePost;local-=preLength; }
                }
                else if(constructing)
                {
                    local=seconds-8f;
                    var pre=portrait.GetAnim(workPre);
                    float preLength=pre?.totalTime??0f;
                    if(local<preLength)clip=workPre;
                    else { clip=workLoop;local-=preLength; }
                }
                else if(seconds>=11f&&seconds<11.35f) { clip=workPost;local=seconds-11f; }
                if(clip!=currentClip)
                {
                    portrait.Play(clip,KAnim.PlayMode.Paused,0f);currentClip=clip;
                }
                Sample(portrait,clip,local);
                bedObject.SetActive(bedComplete);
                if(bedComplete)Sample(bed,bed.CurrentAnim.name,seconds-11f);
                if(carrying)
                {
                    var hand=portrait.GetSymbolTransform("snapTo_rgtHand",out bool visible);
                    if(visible)
                    {
                        // Native UI symbol matrices use centered canvas coordinates.
                        var canvas=portrait.GetComponentInParent<Canvas>()?.rootCanvas;
                        if(canvas!=null)
                        {
                            var scaler=canvas.GetComponent<CanvasScaler>();
                            float scale=scaler!=null?scaler.scaleFactor:1f;
                            var rect=canvas.GetComponent<RectTransform>();
                            var offset=canvas.renderMode==RenderMode.WorldSpace?Vector3.zero:
                                new Vector3(rect.rect.width/2f,rect.rect.height/2f,0f);
                            parcel.position=(hand.MultiplyPoint3x4(Vector3.zero)+offset)*scale;
                        }
                    }
                }
                return true;
            }
            catch(Exception e)
            {
                Disable();ModLogger.Error("Native bedroom lesson playback failed; retaining illustrated lesson",e);
                return false;
            }
        }
        private void Disable()
        {
            Available=false;
            if(portraitObject!=null){portraitObject.SetActive(false);UnityEngine.Object.Destroy(portraitObject);}
            if(bedObject!=null){bedObject.SetActive(false);UnityEngine.Object.Destroy(bedObject);}
        }
    }
}
