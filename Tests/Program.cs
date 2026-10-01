using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rookie100;
using Rookie100.Content;
using Rookie100.UI;

namespace Rookie100 { public static class ModLogger { public static void Log(string s){} public static void Warn(string s){} public static void Error(string s, Exception e){} } public static class QuestScanner { public static Dictionary<string,int> CountAllBuildings()=>new Dictionary<string,int>(); } public static class SanitationWitness { public static void Clear(){} } }
namespace Rookie100.UI { public static class Lang { public static bool English => false; } }
class Program
{
    static int checks;
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
    static void Main(string[] args)
    {
        var toilet = new SanitationRules.Facility { Kind="Outhouse",World=1,Ready=true,Room=2,Workers=new HashSet<int>{10} };
        var basin = new SanitationRules.Facility { Kind="WashBasin",World=1,Ready=true,Room=2,Workers=new HashSet<int>{10} };
        var pump = new SanitationRules.Facility { Kind="LiquidPumpingStation",World=1,Ready=true,Workers=new HashSet<int>{10} };
        var f=new[]{toilet,basin,pump};
        Check(SanitationRules.Evaluate(f,1,true).Values.All(v=>v==1),"complete sanitation chain");
        basin.Workers=new HashSet<int>{11}; Check(SanitationRules.Evaluate(f,1,true)["sanitation:Connected"]==0,"different workers cannot satisfy chain"); basin.Workers=new HashSet<int>{10};
        basin.Room=3;Check(SanitationRules.Evaluate(f,1,true)["sanitation:Connected"]==0,"different rooms cannot satisfy chain");basin.Room=2;
        Check(SanitationRules.Evaluate(f,2,false).Values.All(v=>v==0),"world isolation");
        Check(SanitationRules.Evaluate(f,1,false)["sanitation:Trial"]==0,"facilities alone cannot satisfy trial");
        var cot=new LivingRoomRules.Facility{Kind="Cot",World=1,Usable=true,Reachable=true,Recognized=false};
        var rooms=LivingRoomRules.Evaluate(new[]{cot},1);Check(rooms["livingRoom:BedsUsable"]==1&&rooms["livingRoom:Barracks"]==0,"usable bed is separate from native room");
        cot.Recognized=true;cot.Reachable=false;Check(LivingRoomRules.Evaluate(new[]{cot},1)["livingRoom:Barracks"]==0,"unreachable bed cannot complete bedroom");
        var counts=SanitationRules.Evaluate(f,1,true);counts["sanitation:OuthouseBuilt"]=1;counts["sanitation:WashBasinBuilt"]=1;counts["sanitation:Room"]=0;counts["sanitation:Connected"]=0;
        Check(BeginnerGuidance.Get("q01",counts,true).Key=="room"&&BeginnerGuidance.Get("q01",counts,true).NeedsRepair,"room repair precedes route guidance");
        counts["sanitation:Outhouse"]=0;Check(BeginnerGuidance.Get("q01",counts,false).Key=="inventory-unknown","unknown inventory is not missing dirt");
        counts["sanitation:DirtKnown"]=1;counts["sanitation:DirtAccessible"]=1;Check(BeginnerGuidance.Get("q01",counts,false).Key=="delivery","existing dirt directs delivery instead of digging");
        var playback=new DemoPlayback();playback.Advance(20);Check(playback.Seconds==10&&!playback.Playing,"demo stops independently");
        string path=Path.Combine(Path.GetTempPath(),"rookie100-tests-"+Guid.NewGuid());Directory.CreateDirectory(path);
        try
        {
            File.Copy(args[0],Path.Combine(path,"quests.json"));QuestStore.SetContentPath(path);QuestStore.Load();QuestStore.ActivateColony("A");
            var qs=QuestStore.OrderedQuests;Check(qs.Count==36&&qs.Take(3).Select(q=>q.Id).SequenceEqual(new[]{"q01","q01_bedroom","q01_dining"}),"content has three living-space main quests");
            Check(qs.All(q=>q.Requires.All(id=>qs.Any(x=>x.Id==id)&&qs.First(x=>x.Id==id).Order<q.Order)),"dependency graph resolves and is ordered");
            counts=SanitationRules.Evaluate(f,1,true);QuestStore.RecordLearning(counts);Check(QuestStore.IsLearned("q01")&&!QuestStore.IsAccepted("q01"),"working facilities credited before acceptance");
            QuestStore.AcceptQuest("q01");counts["sanitation:Room"]=0;Check(QuestStore.GetStatus(QuestStore.GetQuest("q01"),counts)==QuestStatus.Completed&&!QuestStore.AreCurrentObjectivesMet(QuestStore.GetQuest("q01"),counts),"history retained while current reward check fails");
            QuestStore.MarkRewardReceived("q01");QuestStore.ResetAll();Check(!QuestStore.IsLearned("q01")&&QuestStore.HasReceivedReward("q01"),"learning reset preserves issued rewards");
            QuestStore.MarkSanitationTrial(1);Check(QuestStore.HasSanitationTrial(1),"actual trial may be recorded before acceptance");
            QuestStore.ActivateColony("B");Check(!QuestStore.HasSanitationTrial(1)&&!QuestStore.IsLearned("q01"),"save profiles isolated");
            QuestStore.ActivateColony("A");Check(QuestStore.HasSanitationTrial(1)&&QuestStore.HasReceivedReward("q01"),"save profile restoration");
            File.WriteAllText(Path.Combine(path,"quest_progress.json"),"{\"Version\":4,\"Profiles\":{\"old\":{\"Claimed\":[\"q01\"],\"Accepted\":[]}}}");QuestStore.Load();QuestStore.ActivateColony("old");Check(QuestStore.IsLearned("q01")&&QuestStore.HasReceivedReward("q01"),"v4 claimed progress migrates to learning and reward history");
        }
        finally{Directory.Delete(path,true);}
        Console.WriteLine(checks+" checks passed. Game adapter and Unity UI require game DLL validation.");
    }
}
