using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Rookie100.UI
{
    public sealed class LessonFilmPlayer : MonoBehaviour
    {
        private VideoPlayer player;
        private RenderTexture texture;
        private RawImage screen;
        private TextMeshProUGUI subtitle,status;
        private LessonFilmDefinition film;
        private DemoPlayback state;
        private bool ready,seeking,failed,primingFrame;
        private float preparedAt;
        public Action<bool> PlaybackChanged;
        public bool IsPlaying=>state!=null&&state.Playing;
        public static LessonFilmDefinition DefinitionFor(string questId)
        {
            string root=Path.GetDirectoryName(typeof(LessonFilmPlayer).Assembly.Location);
            return LessonFilmDefinition.ForQuest(root,questId);
        }
        public void Initialize(LessonFilmDefinition definition,DemoPlayback saved,TextMeshProUGUI subtitleLabel,TextMeshProUGUI statusLabel)
        {
            film=definition;film.Validate();state=saved?.Copy()??new DemoPlayback();
            state.EndSeconds=film.Duration;state.Seconds=Mathf.Clamp(state.Seconds,0f,film.Duration);
            subtitle=subtitleLabel;status=statusLabel;
            string root=Path.GetDirectoryName(typeof(LessonFilmPlayer).Assembly.Location);
            string path=Path.Combine(root,"Videos",film.File);
            if(!File.Exists(path))throw new FileNotFoundException("Lesson film is missing",path);
            var imageObject=new GameObject("LessonFilmScreen",typeof(RectTransform),typeof(RawImage));
            imageObject.transform.SetParent(transform,false);screen=imageObject.GetComponent<RawImage>();screen.raycastTarget=false;
            var rect=imageObject.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=rect.offsetMax=Vector2.zero;
            var fit=imageObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=(float)film.Width/film.Height;
            texture=new RenderTexture(film.Width,film.Height,0,RenderTextureFormat.ARGB32);texture.Create();screen.texture=texture;
            player=gameObject.AddComponent<VideoPlayer>();player.playOnAwake=false;player.isLooping=false;
            player.source=VideoSource.Url;player.url=new Uri(path).AbsoluteUri;
            player.renderMode=VideoRenderMode.RenderTexture;player.targetTexture=texture;player.audioOutputMode=VideoAudioOutputMode.None;
            player.timeUpdateMode=VideoTimeUpdateMode.UnscaledGameTime;player.waitForFirstFrame=true;
            player.sendFrameReadyEvents=true;player.frameReady+=FrameReady;
            player.prepareCompleted+=Prepared;player.seekCompleted+=SeekCompleted;
            player.loopPointReached+=Ended;player.errorReceived+=Error;
            preparedAt=Time.unscaledTime;player.Prepare();UpdateLabels();
        }
        private void Prepared(VideoPlayer source)
        {
            ready=true;failed=false;
            if(state.Seconds>0){seeking=true;source.time=Math.Min(state.Seconds,film.Duration-.04f);}
            // Decode one frame even when restoring a paused lesson at the beginning.
            primingFrame=!state.Playing;source.Play();
            UpdateLabels();PlaybackChanged?.Invoke(state.Playing);
        }
        private void SeekCompleted(VideoPlayer source)
        {
            seeking=false;if(!state.Playing&&!primingFrame)source.Pause();UpdateLabels();
        }
        private void FrameReady(VideoPlayer source,long frame)
        {
            if(!state.Playing)source.Pause();primingFrame=false;
        }
        private void Ended(VideoPlayer source)
        {
            state.Seconds=film.Duration;state.Pause();source.Pause();UpdateLabels();PlaybackChanged?.Invoke(false);
        }
        private void Error(VideoPlayer source,string message)
        {
            failed=true;ready=false;state.Pause();source.Stop();PlaybackChanged?.Invoke(false);
            ModLogger.Warn("Lesson film playback: "+message);UpdateLabels();
        }
        public DemoPlayback Capture()
        {
            if(ready&&!seeking&&player!=null&&state.Seconds<film.Duration)state.Seconds=Mathf.Clamp((float)player.time,0f,film.Duration);
            return state?.Copy();
        }
        public void Pause()
        {
            state?.Pause();if(player!=null)player.Pause();PlaybackChanged?.Invoke(false);
        }
        public void TogglePlay()
        {
            if(state==null)return;
            if(failed){Replay();return;}
            if(state.Seconds>=film.Duration){Replay();return;}
            state.Playing=!state.Playing;
            if(ready){if(state.Playing)player.Play();else player.Pause();}
            UpdateLabels();PlaybackChanged?.Invoke(state.Playing);
        }
        public void Replay()
        {
            state.Seconds=0;state.Playing=true;
            if(failed){failed=false;preparedAt=Time.unscaledTime;player.Prepare();}
            else if(ready){seeking=true;player.time=0;player.Play();}
            UpdateLabels();PlaybackChanged?.Invoke(true);
        }
        public void SeekStep(int index)
        {
            state.Seconds=film.Steps[Mathf.Clamp(index,0,film.Steps.Count-1)].Start;state.Pause();
            if(ready){player.Pause();seeking=true;player.time=state.Seconds;}
            UpdateLabels();PlaybackChanged?.Invoke(false);
        }
        private void Update()
        {
            if(state==null)return;
            if(!ready&&!failed&&Time.unscaledTime-preparedAt>20f){Error(player,"Video preparation timed out");return;}
            if(ready&&!seeking&&state.Playing)state.Seconds=Mathf.Clamp((float)player.time,0f,film.Duration);
            UpdateLabels();
        }
        private void UpdateLabels()
        {
            if(state==null)return;
            if(subtitle!=null)subtitle.text=failed?(Lang.English?"Video unavailable. Retry playback; details are in the log.":"短片未能播放，请重播尝试；详情见游戏日志。"):
                !ready?(Lang.English?"Loading lesson film…":"正在加载教程短片……"):film.SubtitleAt(state.Seconds,Lang.English);
            if(status!=null)status.text=film.StepAt(state.Seconds,Lang.English)+"  ·  "+state.Seconds.ToString("0")+" / "+film.Duration.ToString("0")+" s";
        }
        private void OnDisable(){Pause();}
        private void OnDestroy()
        {
            PlaybackChanged=null;
            if(player!=null)
            {
                player.prepareCompleted-=Prepared;player.seekCompleted-=SeekCompleted;
                player.frameReady-=FrameReady;
                player.loopPointReached-=Ended;player.errorReceived-=Error;player.Stop();player.targetTexture=null;
            }
            if(screen!=null)screen.texture=null;
            if(texture!=null){texture.Release();Destroy(texture);}
        }
    }
}
