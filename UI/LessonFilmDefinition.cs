using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Rookie100.UI
{
    public sealed class LessonFilmText
    {
        public float Start;
        public float End;
        public string Zh;
        public string En;
        public string Text(bool english) => english ? En : Zh;
    }
    public sealed class LessonFilmDefinition
    {
        public int Version;
        public string Id;
        public string File;
        public float Duration;
        public int Width,Height;
        public List<LessonFilmText> Steps=new List<LessonFilmText>();
        public List<LessonFilmText> Subtitles=new List<LessonFilmText>();
        public static LessonFilmDefinition ForQuest(string root,string questId)
        {
            if(string.IsNullOrWhiteSpace(questId)||questId.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'))
                throw new InvalidDataException("Invalid lesson quest identifier");
            string name=questId=="q01_bedroom"?"bedroom":questId;
            var film=Load(Path.Combine(root,"Videos",name+".lesson.json"));
            if(film.Id!=questId)throw new InvalidDataException("Lesson does not belong to the selected quest");
            if(!System.IO.File.Exists(Path.Combine(root,"Videos",film.File)))throw new FileNotFoundException("Lesson film is missing",film.File);
            return film;
        }
        public static LessonFilmDefinition Load(string path)
        {
            var film=JsonConvert.DeserializeObject<LessonFilmDefinition>(System.IO.File.ReadAllText(path));
            film?.Validate();return film??throw new InvalidDataException("Empty lesson film definition");
        }
        public void Validate()
        {
            if(Version!=1||string.IsNullOrWhiteSpace(Id)||string.IsNullOrWhiteSpace(File)||
               Path.GetFileName(File)!=File||File.Contains("\\")||File.Contains("/")||!File.EndsWith(".mp4",StringComparison.OrdinalIgnoreCase)||
               float.IsNaN(Duration)||float.IsInfinity(Duration)||Duration<=0||Width<=0||Height<=0||Width>1920||Height>1080)
                throw new InvalidDataException("Invalid lesson film metadata");
            if(Steps==null||Steps.Count==0||Steps[0].Start!=0||Subtitles==null||Subtitles.Count==0)
                throw new InvalidDataException("Lesson film has no timeline");
            float previous=-1;
            foreach(var step in Steps)
            {
                if(step==null||float.IsNaN(step.Start)||step.Start<0||step.Start>=Duration||step.Start<=previous||string.IsNullOrWhiteSpace(step.Zh)||string.IsNullOrWhiteSpace(step.En))
                    throw new InvalidDataException("Invalid lesson step");
                previous=step.Start;
            }
            float end=0;
            foreach(var cue in Subtitles)
            {
                if(cue==null||float.IsNaN(cue.Start)||float.IsNaN(cue.End)||cue.Start!=end||cue.End<=cue.Start||cue.End>Duration||string.IsNullOrWhiteSpace(cue.Zh)||string.IsNullOrWhiteSpace(cue.En))
                    throw new InvalidDataException("Invalid or incomplete subtitle timeline");
                end=cue.End;
            }
            if(end!=Duration)throw new InvalidDataException("Subtitles do not cover the whole film");
        }
        public string SubtitleAt(float seconds,bool english)
        {
            float t=Math.Max(0,Math.Min(Duration,seconds));
            return (Subtitles.FirstOrDefault(c=>t>=c.Start&&t<c.End)??Subtitles.Last()).Text(english);
        }
        public string StepAt(float seconds,bool english) => Steps.LastOrDefault(s=>s.Start<=seconds)?.Text(english)??Steps[0].Text(english);
    }
}
