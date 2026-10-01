using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rookie100;
using Rookie100.Content;
using Rookie100.UI;
using Rookie100.Monitoring;

namespace Rookie100 { public static class ModLogger { public static void Log(string s){} public static void Warn(string s){} public static void Error(string s, Exception e){} } public static class QuestScanner { public static Dictionary<string,int> CountAllBuildings()=>new Dictionary<string,int>(); } public static class SanitationWitness { public static void Clear(){} } }
namespace Rookie100.UI { public static class Lang { public static bool English => false; } }
class Program
{
    sealed class Entry { readonly string value; public Entry(string text){value=text;} public override string ToString()=>value; }
    static int checks;
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
    static void Main(string[] args)
    {
        Check(LocalizedUiText.FromEntry(new Entry("<link=\"BED\">床铺</link>"),"key")=="床铺", "boxed native-style StringEntry preserves Chinese localization");
        Check(LocalizedUiText.FromEntry(new Entry("<link=\"BED\">Cot</link>"),"key")=="Cot", "English localization stays English without a second name or ID");
        Check(LocalizedUiText.FromEntry(new Entry("MISSING.STRING"),"key")==null&&LocalizedUiText.FromEntry(null,"key")==null&&LocalizedUiText.FromEntry("key","key")==null,"missing localization cannot masquerade as a display name");
        var toilet = new SanitationRules.Facility { Kind="Outhouse",World=1,Ready=true,Room=2,Workers=new HashSet<int>{10} };
        var basin = new SanitationRules.Facility { Kind="WashBasin",World=1,Ready=true,Room=2,Workers=new HashSet<int>{10} };
        var pump = new SanitationRules.Facility { Kind="LiquidPumpingStation",World=1,Ready=true,Workers=new HashSet<int>{10} };
        var f=new[]{toilet,basin,pump};
        Check(SanitationRules.Evaluate(f,1,true).Values.All(v=>v==1),"complete sanitation chain");
        basin.Workers=new HashSet<int>{11}; Check(SanitationRules.Evaluate(f,1,true)["sanitation:Connected"]==0,"different workers cannot satisfy chain"); basin.Workers=new HashSet<int>{10};
        basin.Room=3;Check(SanitationRules.Evaluate(f,1,true)["sanitation:Connected"]==0,"different rooms cannot satisfy chain");basin.Room=2;
        Check(SanitationRules.Evaluate(f,2,false).Values.All(v=>v==0),"world isolation");
        Check(SanitationRules.Evaluate(f,1,false)["sanitation:Trial"]==0,"facilities alone cannot satisfy trial");
        var cot=new LivingRoomRules.Facility{Kind="Bed",World=1,Usable=true,Reachable=true,Recognized=false};
        var rooms=LivingRoomRules.Evaluate(new[]{cot},1);Check(rooms["livingRoom:BedsUsable"]==1&&rooms["livingRoom:Barracks"]==0,"usable bed is separate from native room");
        cot.Recognized=true;cot.Reachable=false;Check(LivingRoomRules.Evaluate(new[]{cot},1)["livingRoom:Barracks"]==0,"unreachable bed cannot complete bedroom");
        cot.Reachable=true;
        var table=new LivingRoomRules.Facility{Kind="DiningTable",World=1,Usable=true,Reachable=true,Recognized=true};
        var nativeLiving=LivingRoomRules.Evaluate(new[]{cot,table},1);
        Check(nativeLiving.Values.All(v=>v==1),"native Bed and DiningTable IDs complete living facilities");
        Check(LivingRoomRules.Evaluate(new[]{cot,table},2).Values.All(v=>v==0),"living facilities on other asteroid cannot grant progress");
        var counts=SanitationRules.Evaluate(f,1,true);counts["sanitation:OuthouseBuilt"]=1;counts["sanitation:WashBasinBuilt"]=1;counts["sanitation:Room"]=0;counts["sanitation:Connected"]=0;
        Check(BeginnerGuidance.Get("q01",counts,true).Key=="room"&&BeginnerGuidance.Get("q01",counts,true).NeedsRepair,"room repair precedes route guidance");
        counts["sanitation:Outhouse"]=0;Check(BeginnerGuidance.Get("q01",counts,false).Key=="inventory-unknown","unknown inventory is not missing dirt");
        counts["sanitation:DirtKnown"]=1;counts["sanitation:DirtAccessible"]=1;Check(BeginnerGuidance.Get("q01",counts,false).Key=="delivery","existing dirt directs delivery instead of digging");
        var playback=new DemoPlayback();playback.Advance(20);Check(playback.Seconds==10&&!playback.Playing,"demo stops independently");
        var bedroomPlayback=new DemoPlayback { EndSeconds=20f };bedroomPlayback.Advance(12f);
        Check(bedroomPlayback.Playing&&bedroomPlayback.Seconds==12f,"extended bedroom lesson continues past the original duration");
        bedroomPlayback.Pause();bedroomPlayback.Advance(5f);
        var restoredPlayback=bedroomPlayback.Copy();
        Check(restoredPlayback.EndSeconds==20f&&restoredPlayback.Seconds==12f&&!restoredPlayback.Playing,"paused extended lesson retains its timeline across panel refresh");
        restoredPlayback.Toggle();restoredPlayback.Advance(20f);
        Check(restoredPlayback.Seconds==20f&&!restoredPlayback.Playing,"extended lesson stops at its own end");
        restoredPlayback.Toggle();restoredPlayback.Advance(1f);
        Check(restoredPlayback.Seconds==1f&&restoredPlayback.Playing,"playing completed extended lesson restarts from the beginning");
        var film=LessonFilmDefinition.Load(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0])),"Videos","bedroom.lesson.json"));
        Check(film.Duration==36f&&film.Steps.Count==5,"bedroom film exposes its complete five-shot timeline");
        Check(film.SubtitleAt(6f,false)==film.Subtitles[2].Zh&&film.SubtitleAt(6f,true)==film.Subtitles[2].En,"subtitle boundary selects exactly one language at the new shot");
        Check(film.StepAt(20f,false)==film.Steps[3].Zh&&film.StepAt(28f,true)==film.Steps[4].En,"chapter seeks align with enclosure and room inspection shots");
        Check(film.SubtitleAt(film.Duration,true)==film.Subtitles.Last().En,"final paused frame retains the completion guidance");
        var invalidFilm=LessonFilmDefinition.Load(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0])),"Videos","bedroom.lesson.json"));
        invalidFilm.Subtitles[1].Start+=.5f;bool gapRejected=false;
        try{invalidFilm.Validate();}catch(InvalidDataException){gapRejected=true;}
        Check(gapRejected,"film rejects subtitle gaps instead of silently showing stale guidance");
        string path=Path.Combine(Path.GetTempPath(),"rookie100-tests-"+Guid.NewGuid());Directory.CreateDirectory(path);
        try
        {
            File.Copy(args[0],Path.Combine(path,"quests.json"));QuestStore.SetContentPath(path);QuestStore.Load();QuestStore.ActivateColony("A");
            var qs=QuestStore.OrderedQuests;Check(qs.Count==36&&qs.Take(3).Select(q=>q.Id).SequenceEqual(new[]{"q01","q01_bedroom","q01_dining"}),"content has three living-space main quests");
            Check(QuestStore.GetQuest("q01_bedroom").Objectives.All(o=>o.Tag=="Bed")&&QuestStore.GetQuest("q01_dining").Objectives.All(o=>o.Tag=="DiningTable"),"living objective icon and guide tags use native prefab IDs");
            Check(QuestStore.AreCurrentObjectivesMet(QuestStore.GetQuest("q01_bedroom"),nativeLiving)&&QuestStore.AreCurrentObjectivesMet(QuestStore.GetQuest("q01_dining"),nativeLiving),"living objectives evaluate corrected native observations");
            Check(qs.All(q=>q.Requires.All(id=>qs.Any(x=>x.Id==id)&&qs.First(x=>x.Id==id).Order<q.Order)),"dependency graph resolves and is ordered");
            QuestStore.ActivateColony("chapter-one-flow");
            var allReady=SanitationRules.Evaluate(f,1,true);foreach(var pair in nativeLiving)allReady[pair.Key]=pair.Value;
            Check(QuestStore.GetStatus(QuestStore.GetQuest("q01"),allReady)==QuestStatus.Available&&QuestStore.GetStatus(QuestStore.GetQuest("q01_bedroom"),allReady)==QuestStatus.Locked,"fresh colony starts at sanitation and gates bedroom");
            QuestStore.RecordLearning(allReady);
            foreach(var id in new[]{"q01","q01_bedroom","q01_dining"})
            {
                Check(QuestStore.GetStatus(QuestStore.GetQuest(id),allReady)==QuestStatus.Available,"chapter-one next task unlocks: "+id);
                QuestStore.AcceptQuest(id);Check(QuestStore.GetStatus(QuestStore.GetQuest(id),allReady)==QuestStatus.Completed,"existing facilities complete accepted chapter-one task: "+id);
                QuestStore.MarkClaimed(id);
            }
            allReady["livingRoom:MessHall"]=0;
            Check(QuestStore.IsClaimed("q01_dining")&&!QuestStore.AreCurrentObjectivesMet(QuestStore.GetQuest("q01_dining"),allReady),"completed first chapter retains history while exposing later room damage");
            QuestStore.ActivateColony("A");
            counts=SanitationRules.Evaluate(f,1,true);QuestStore.RecordLearning(counts);Check(QuestStore.IsLearned("q01")&&!QuestStore.IsAccepted("q01"),"working facilities credited before acceptance");
            QuestStore.AcceptQuest("q01");counts["sanitation:Room"]=0;Check(QuestStore.GetStatus(QuestStore.GetQuest("q01"),counts)==QuestStatus.Completed&&!QuestStore.AreCurrentObjectivesMet(QuestStore.GetQuest("q01"),counts),"history retained while current reward check fails");
            QuestStore.MarkRewardReceived("q01");QuestStore.ResetAll();Check(!QuestStore.IsLearned("q01")&&QuestStore.HasReceivedReward("q01"),"learning reset preserves issued rewards");
            QuestStore.MarkSanitationTrial(1);Check(QuestStore.HasSanitationTrial(1),"actual trial may be recorded before acceptance");
            QuestStore.ActivateColony("B");Check(!QuestStore.HasSanitationTrial(1)&&!QuestStore.IsLearned("q01"),"save profiles isolated");
            QuestStore.ActivateColony("A");Check(QuestStore.HasSanitationTrial(1)&&QuestStore.HasReceivedReward("q01"),"save profile restoration");
            File.WriteAllText(Path.Combine(path,"quest_progress.json"),"{\"Version\":4,\"Profiles\":{\"old\":{\"Claimed\":[\"q01\"],\"Accepted\":[]}}}");QuestStore.Load();QuestStore.ActivateColony("old");Check(QuestStore.IsLearned("q01")&&QuestStore.HasReceivedReward("q01"),"v4 claimed progress migrates to learning and reward history");
        }
        finally{Directory.Delete(path,true);}
        var monitor = new MonitorState();
        var a = new DuplicantReading { Id=1, World=1, Name="Ada", Health=72f, Stress=null };
        var b = new DuplicantReading { Id=2, World=1, Name="Burt", Health=100f, Stress=40f };
        var other = new DuplicantReading { Id=3, World=2, Name="Meep" };
        monitor.Apply("A",1,new[]{b,a,other});
        Check(monitor.Count==2 && monitor.Current.Id==1,"monitor filters current world and orders roster");
        a.Health=0f;Check(monitor.Current.Health==72f,"monitor copies incoming readings");
        var detached=monitor.Current;detached.Health=0f;Check(monitor.Current.Health==72f,"monitor returns detached readings to views");
        Check(monitor.Current.Stress==null,"unknown stress remains unknown");
        monitor.Move(-1);Check(monitor.Current.Id==2,"previous selection wraps to last duplicant");
        monitor.Move(1);Check(monitor.Current.Id==1,"next selection wraps to first duplicant");
        monitor.Move(1);monitor.Apply("A",1,new[]{a,b});Check(monitor.Current.Id==2,"sampling preserves selected duplicant");
        monitor.Apply("A",1,new[]{a});Check(monitor.Current.Id==1,"removed duplicant selects valid survivor");
        monitor.Apply("A",2,new[]{a,other});Check(monitor.Count==1&&monitor.Current.Id==3,"asteroid switch filters and updates selection");
        monitor.Apply("A",1,new[]{a,b});monitor.Move(1);monitor.Apply("B",1,new[]{a,b});Check(monitor.Current.Id==1,"new colony clears old monitor selection");
        monitor.Apply("B",1,new[]{a,a,b});Check(monitor.Count==2,"duplicate roster entries do not duplicate monitoring rows");
        monitor.Apply("B",1,null);Check(monitor.Current==null&&!monitor.Move(1),"empty roster clears view and disables selection");
        Check(MonitorState.Percent(50f,200f)==25f&&MonitorState.Percent(300f,200f)==100f&&MonitorState.Percent(-5f,100f)==0f,"native amounts normalize with bounds");
        Check(MonitorState.Percent(1f,0f)==null&&MonitorState.Percent(float.NaN,100f)==null&&MonitorState.Percent(1f,float.PositiveInfinity)==null,"invalid native amounts remain unknown");
        var link = new QuestNoticeLink("q01","A");
        Check(link.CanOpen("A",id=>id=="q01"),"notification routes known task within same colony");
        Check(!link.CanOpen("B",id=>true),"old colony notification cannot jump into another save");
        Check(!link.CanOpen("A",id=>false)&&!new QuestNoticeLink(null,"A").CanOpen("A",id=>true),"removed or missing notification task is rejected");
        Check(!new QuestNoticeLink("q01",null).CanOpen(null,id=>true),"notification without active colony is rejected");
        var catalogPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0])), "Curriculum", "catalog.v1.json");
        var catalog = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(catalogPath));
        Rookie100.Curriculum.CurriculumCatalog.Validate(catalog);
        Check(catalog["tasks"].Count()==45 && catalog["chapters"].Count()==6,"six chapters and 45 tasks validate");
        var runtimeContent=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(args[0]));
        var runtimeTasks=runtimeContent["Quests"].ToDictionary(t=>(string)t["Id"]);
        var learningTasks=catalog["tasks"].ToDictionary(t=>(string)t["id"]);
        Check(runtimeContent["Phases"].Select(t=>(string)t["Id"]).SequenceEqual(catalog["chapters"].Select(t=>(string)t["id"])),"runtime task list follows all six chapters");
        Check(catalog["tasks"].All(t=>t["teaching"]?["steps"] is Newtonsoft.Json.Linq.JArray steps && steps.Count>=3 && !string.IsNullOrEmpty((string)t["teaching"]["completion"])),"all 45 teaching items have actionable steps and explicit completion limits");
        Check(runtimeTasks.Values.All(t=>t["Teaching"]!=null&&(string)t["Phase"]==(string)learningTasks[(string)t["Id"]]["chapterId"]),"all runtime tasks expose teaching in their correct chapter");
        Check(runtimeTasks.Values.Where(t=>(string)learningTasks[(string)t["Id"]]["track"]=="main").All(t=>t["Requires"].All(dep=>(string)learningTasks[(string)dep]["track"]=="main")),"optional branches cannot block runtime main tasks");
        Check(catalog["chapters"].All(c=>(string)c["backgroundStatus"]=="implementedNativeScene"&&c["backgroundIcons"].Count()==3),"all six chapter backgrounds have native scene designs");
        var cyclic=(Newtonsoft.Json.Linq.JObject)catalog.DeepClone();cyclic["tasks"][0]["prerequisites"]=new Newtonsoft.Json.Linq.JArray("q01");
        bool rejects=false;try{Rookie100.Curriculum.CurriculumCatalog.Validate(cyclic);}catch(InvalidDataException){rejects=true;}
        Check(rejects,"curriculum rejects circular prerequisites");
        Rookie100.Curriculum.CurriculumCatalog.Initialize(Path.GetDirectoryName(Path.GetFullPath(args[0])));
        var curriculumCounts=new Dictionary<string,int>{{"sanitation:Room",0}};
        var observed=Rookie100.Curriculum.CurriculumCatalog.ObserveFacts(curriculumCounts,1,1,DateTime.UtcNow,DateTime.UtcNow);
        Check((int)observed["sanitation.Room"]["value"]==0 && observed["livingRoom.BedsUsable"]==null,"zero is observed but missing rooms remain unknown");
        Check(Rookie100.Curriculum.CurriculumCatalog.ObserveFacts(curriculumCounts,1,2,DateTime.UtcNow,DateTime.UtcNow)["sanitation.Room"]==null,"curriculum rejects readings from previous asteroid");
        Check(Rookie100.Curriculum.CurriculumCatalog.ObserveFacts(null,1,1,null,DateTime.UtcNow)["sanitation.Room"]==null,"curriculum does not invent measurements before scanning");
        var lessonRoot=Path.GetDirectoryName(Path.GetFullPath(args[0]));
        var lessonIndex=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(lessonRoot,"Videos","lessons.index.json")));
        var lessonIds=lessonIndex["lessons"].Select(x=>(string)x["id"]).ToList();
        Check(lessonIds.Count==45&&lessonIds.Distinct().Count()==45&&learningTasks.Keys.All(lessonIds.Contains),"every teaching task has a unique lesson film mapping");
        Check(runtimeTasks.Keys.All(lessonIds.Contains),"every runtime task has a tutorial film");
        var allFilms=lessonIds.Select(id=>LessonFilmDefinition.ForQuest(lessonRoot,id)).ToList();
        Check(allFilms.Select(x=>x.File).Distinct().Count()==45,"tasks load distinct films rather than reusing the bedroom movie");
        Check(allFilms.All(x=>x.Steps.Count>=4&&x.Subtitles.All(c=>c.Zh.Any(ch=>ch>127)&&c.En.All(ch=>ch<128))),"all task films have complete Chinese and English teaching timelines");
        Check(allFilms.All(x=>x.SubtitleAt(x.Duration,true)==x.Subtitles.Last().En),"all film final frames retain their own final guidance");
        bool badLessonId=false;try{LessonFilmDefinition.ForQuest(lessonRoot,"../q01");}catch(InvalidDataException){badLessonId=true;}
        Check(badLessonId,"lesson lookup rejects traversal identifiers");
        Console.WriteLine(checks+" checks passed. Game adapter and Unity UI require game DLL validation.");
    }
}
