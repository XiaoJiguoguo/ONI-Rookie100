using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Rookie100.Content;
using Rookie100.UI;
using UnityEngine;

namespace Rookie100
{
    /// <summary>
    /// 任务追踪器：挂在 Game 对象上周期检测。
    /// 任务目标达成 → 事件 + 游戏内通知；领取奖励由 RewardsService 投放打印舱。
    /// </summary>
    public class QuestTracker : KMonoBehaviour
    {
        public static QuestTracker Instance { get; private set; }

        /// <summary>任务目标刚刚达成的通知（面板订阅刷新）。</summary>
        public static event Action<QuestDef> QuestCompleted;

        /// <summary>领取奖励完成的事件（面板订阅刷新）。</summary>
        public static event Action<QuestDef> QuestClaimed;

        /// <summary>已通知过的完成任务（避免重复通知轰炸）。</summary>
        private readonly HashSet<string> notifiedCompleted = new HashSet<string>();

        private float checkTimer;
        private const float CheckInterval = 2f;
        private bool diagnosticsDone;

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Instance = this;
            ModLogger.Log($"任务追踪器初始化，已领取 {QuestStore.ClaimedCount} 个任务");
        }

        protected override void OnCleanUp()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            base.OnCleanUp();
        }

        private void Update()
        {
            checkTimer += Time.deltaTime;
            if (checkTimer < CheckInterval)
            {
                return;
            }

            checkTimer = 0f;
            try
            {
                CheckQuests();
                if (!diagnosticsDone)
                {
                    diagnosticsDone = true;
                    DumpDiagnostics();
                }
            }
            catch (Exception e)
            {
                ModLogger.Error("任务检测异常", e);
            }
        }

        /// <summary>一次性场景诊断：列出前 40 种已建成建筑 PrefabTag（核对任务目标 ID）。</summary>
        private void DumpDiagnostics()
        {
            var counts = QuestScanner.CountAllBuildings();
            ModLogger.Log($"场景建筑诊断: 共 {counts.Count} 种 / 总数 {counts.Values.Sum()}");
            foreach (var pair in counts.OrderByDescending(kv => kv.Value).Take(40))
            {
                ModLogger.Log($"  [{pair.Key}] x{pair.Value}");
            }

            try
            {
                DumpTechDiagnostics();
            }
            catch (Exception e)
            {
                ModLogger.Warn("[TechDiag] 科技树诊断异常: " + e);
            }
        }

        /// <summary>
        /// 一次性科技树诊断 v2（全反射）：确认真实 API 形态——
        /// Db/Research 真实基类与字段、Techs/TechItems 表、任务建筑科技链、本地化入口、材料造价结构。
        /// 校准"建筑引导模态窗"正式代码。
        /// </summary>
        private void DumpTechDiagnostics()
        {
            const string p = "[TechDiag] ";

            // A) Db 类型与实例形态
            var dbType = typeof(Db);
            ModLogger.Log($"{p}A) typeof(Db) FullName={dbType.FullName} Asm={dbType.Assembly.GetName().Name} Base={dbType.BaseType?.FullName}");
            var staticFieldNames = new List<string>();
            foreach (var f in dbType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                staticFieldNames.Add(f.Name + "(" + f.FieldType.Name + ")");
            }

            ModLogger.Log($"{p}A) Db 静态字段: {string.Join(", ", staticFieldNames)}");
            var db = Db.Get();
            if (db == null)
            {
                ModLogger.Warn(p + "A) Db.Get() == null");
                return;
            }

            ModLogger.Log($"{p}A) Db.Get() 实例类型={db.GetType().FullName} Base={db.GetType().BaseType?.FullName}");
            var dbFieldNames = new List<string>();
            foreach (var f in db.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                dbFieldNames.Add(f.Name + ":" + f.FieldType.Name);
            }

            ModLogger.Log($"{p}A) Db 公共实例字段({dbFieldNames.Count}): {string.Join(", ", dbFieldNames)}");

            // B) Techs 表
            var techsTable = GetFieldValue(db, "Techs");
            var techList = GetFieldValue(techsTable, "resources") as IList;
            if (techList == null)
            {
                ModLogger.Warn(p + "B) Techs.resources 读取失败");
            }
            else
            {
                var elemType = techList.GetType().IsGenericType
                    ? techList.GetType().GenericTypeArguments[0].FullName
                    : "?";
                ModLogger.Log($"{p}B) Techs.resources 共 {techList.Count} 项，元素类型 {elemType}");
                foreach (var tech in techList)
                {
                    if (tech != null)
                    {
                        ModLogger.Log(p + "B) 首个科技示例: " + DescribeTech(tech));
                        break;
                    }
                }

                var buildingTags = QuestStore.Quests
                    .SelectMany(q => q.Objectives)
                    .Where(o => o.Type == "buildingBuilt" && !string.IsNullOrEmpty(o.Tag))
                    .Select(o => o.Tag)
                    .Distinct()
                    .OrderBy(t => t, StringComparer.Ordinal)
                    .ToList();
                ModLogger.Log($"{p}B) 任务建筑({buildingTags.Count}): {string.Join(", ", buildingTags)}");

                foreach (var tag in buildingTags)
                {
                    object found = null;
                    foreach (var tech in techList)
                    {
                        if (tech == null)
                        {
                            continue;
                        }

                        var ids = GetFieldValue(tech, "unlockedItemIDs") as IEnumerable;
                        if (ids == null)
                        {
                            continue;
                        }

                        foreach (var item in ids)
                        {
                            if (string.Equals(item as string, tag, StringComparison.Ordinal))
                            {
                                found = tech;
                                break;
                            }
                        }

                        if (found != null)
                        {
                            break;
                        }
                    }

                    if (found == null)
                    {
                        ModLogger.Log($"{p}B) {tag}: 未命中任何科技（开局可用）");
                    }
                    else
                    {
                        StringBuilder chain = new StringBuilder();
                        AppendTechChain(found, chain, new HashSet<object>());
                        ModLogger.Log($"{p}B) {tag}: " + chain);
                    }
                }
            }

            // C) TechItems 表（Id 是否=建筑 prefab id）
            var techItemsTable = GetFieldValue(db, "TechItems");
            if (techItemsTable == null)
            {
                ModLogger.Warn(p + "C) Db 上无 TechItems 字段");
            }
            else
            {
                var items = GetFieldValue(techItemsTable, "resources") as IList;
                if (items != null && items.Count > 0)
                {
                    var first = items[0];
                    var ft = first.GetType();
                    var fields = new List<string>();
                    foreach (var f in ft.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        fields.Add(f.Name);
                    }

                    var props = new List<string>();
                    foreach (var pr in ft.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        props.Add(pr.Name);
                    }

                    ModLogger.Log($"{p}C) TechItems.resources 共 {items.Count} 项，元素 {ft.FullName}；字段: {string.Join(", ", fields)}；属性: {string.Join(", ", props)}");
                    ModLogger.Log($"{p}C) 首个 TechItem: Id={GetStringId(first)} parentTechId={GetFieldValue(first, "parentTechId")}");
                }

                var tryGet = techItemsTable.GetType().GetMethod("TryGet", new[] { typeof(string) });
                if (tryGet == null)
                {
                    ModLogger.Warn(p + "C) TechItems 无 TryGet(string) 重载");
                }
                else
                {
                    foreach (var tag in new[] { "Outhouse", "SteamTurbine", "StorageLocker", "Tile" })
                    {
                        object item = null;
                        try
                        {
                            item = tryGet.Invoke(techItemsTable, new object[] { tag });
                        }
                        catch
                        {
                        }

                        ModLogger.Log(item == null
                            ? $"{p}C) TryGet({tag}) 未命中"
                            : $"{p}C) TryGet({tag}) 命中: Id={GetStringId(item)} parentTechId={GetFieldValue(item, "parentTechId")}");
                    }
                }
            }

            // D) Research 管理器
            var researchType = typeof(Db).Assembly.GetType("Research");
            if (researchType == null)
            {
                ModLogger.Warn(p + "D) 找不到 Research 类型");
            }
            else
            {
                ModLogger.Log($"{p}D) Research FullName={researchType.FullName} Base={researchType.BaseType?.FullName}");
                object instance = null;
                try
                {
                    var instProp = researchType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instProp != null)
                    {
                        instance = instProp.GetValue(null, null);
                    }
                }
                catch (Exception e)
                {
                    ModLogger.Warn(p + "D) Research.Instance 异常: " + e.Message);
                }

                if (instance == null)
                {
                    object game = null;
                    try
                    {
                        var gameInst = typeof(Game).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        if (gameInst != null)
                        {
                            game = gameInst.GetValue(null, null);
                        }
                    }
                    catch
                    {
                    }

                    if (game != null)
                    {
                        try
                        {
                            var getComp = typeof(Game).GetMethod("GetComponent",
                                BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                            instance = getComp.MakeGenericMethod(researchType).Invoke(game, null);
                        }
                        catch (Exception e)
                        {
                            ModLogger.Warn(p + "D) Game.GetComponent<Research> 异常: " + e.Message);
                        }
                    }
                }

                ModLogger.Log(p + "D) Research 实例 = " + (instance != null));
                if (instance != null)
                {
                    var instFields = new List<string>();
                    foreach (var f in researchType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        instFields.Add(f.Name);
                    }

                    ModLogger.Log($"{p}D) Research 公共字段: {string.Join(", ", instFields)}");
                }
            }

            // E) 本地化入口
            DumpLocalizationProbe();

            // F) 造价与材料分类
            DumpBuildingDefProbe();

            // G) Tech 详情与 TechItem 图标结构
            DumpTechDetailProbe();

            // H) 屏幕系统：研究面板打开路径
            DumpScreenSystemProbe();
        }

        private static string DescribeTech(object tech)
        {
            string id = GetStringId(tech);
            var ids = GetFieldValue(tech, "unlockedItemIDs") as IEnumerable;
            var reqs = GetFieldValue(tech, "requiredTech") as IEnumerable;
            int idCount = 0;
            int reqCount = 0;
            if (ids != null)
            {
                foreach (var _ in ids)
                {
                    idCount++;
                }
            }

            if (reqs != null)
            {
                foreach (var _ in reqs)
                {
                    reqCount++;
                }
            }

            return $"{id} (解锁项x{idCount}, 前置x{reqCount}, 完成={InvokeBoolMethod(tech, "IsComplete")}, 先决={InvokeBoolMethod(tech, "ArePrerequisitesComplete")})";
        }

        private static string GetStringId(object target)
        {
            if (target == null)
            {
                return "?";
            }

            try
            {
                // Resource 系类的 Id/Name 是公共字段（不是属性）
                var field = target.GetType().GetField("Id")
                            ?? target.GetType().GetField("id")
                            ?? target.GetType().GetField("Name");
                if (field != null)
                {
                    return System.Convert.ToString(field.GetValue(target));
                }
            }
            catch
            {
            }

            try
            {
                var prop = target.GetType().GetProperty("Id")
                           ?? target.GetType().GetProperty("id")
                           ?? target.GetType().GetProperty("Name");
                return prop == null ? "?" : System.Convert.ToString(prop.GetValue(target, null));
            }
            catch
            {
                return "?";
            }
        }

        private static System.Type FindTypeByName(string name)
        {
            var assemblies = new[] { typeof(Db).Assembly, typeof(KPrefabID).Assembly };
            foreach (var asm in assemblies)
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == name)
                        {
                            return t;
                        }
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private void DumpLocalizationProbe()
        {
            const string p = "[TechDiag] ";
            var stringsType = FindTypeByName("Strings");
            if (stringsType == null)
            {
                ModLogger.Warn(p + "E) 找不到 Strings 类型");
                return;
            }

            var methods = new List<string>();
            foreach (var m in stringsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                methods.Add(m.Name);
            }

            ModLogger.Log($"{p}E) Strings({stringsType.FullName}) 静态方法: {string.Join(", ", methods.Distinct())}");

            foreach (var key in new[] { "STRINGS.RESEARCH.TECHS.SANITATION.NAME", "STRINGS.RESEARCH.TECHS.PLUMBING.NAME" })
            {
                try
                {
                    var get = stringsType.GetMethod("Get", new[] { typeof(string) });
                    if (get != null)
                    {
                        ModLogger.Log($"{p}E) Strings.Get({key}) = {get.Invoke(null, new object[] { key })}");
                    }
                    else
                    {
                        ModLogger.Warn(p + "E) Strings 无 Get(string) 重载");
                    }
                }
                catch (Exception e)
                {
                    ModLogger.Warn(p + "E) 本地化探测异常: " + e.Message);
                }
            }
        }

        private void DumpBuildingDefProbe()
        {
            const string p = "[TechDiag] ";
            var getBuildingDef = typeof(Assets).GetMethod("GetBuildingDef");
            if (getBuildingDef == null)
            {
                ModLogger.Warn(p + "F) Assets 无 GetBuildingDef");
                return;
            }

            foreach (var id in new[] { "Outhouse", "FlushToilet", "ManualGenerator", "Bed" })
            {
                object bd = null;
                try
                {
                    bd = getBuildingDef.Invoke(null, new object[] { id });
                }
                catch
                {
                }

                if (bd == null || bd.Equals(null))
                {
                    ModLogger.Log(p + $"F) {id}: GetBuildingDef 未命中");
                    continue;
                }

                var mass = GetFieldValue(bd, "Mass") as IEnumerable;
                var cats = GetFieldValue(bd, "MaterialCategory") as IEnumerable;
                var parts = new List<string>();
                if (cats != null)
                {
                    foreach (var c in cats)
                    {
                        parts.Add("[" + System.Convert.ToString(c) + "]");
                    }
                }

                ModLogger.Log(p + $"F) {id}: Mass=[{(mass == null ? "null" : string.Join(",", mass.Cast<object>()))}] " +
                              $"MaterialCategory={string.Join("|", parts)}");
            }

            try
            {
                var gt = typeof(GameTags);
                var names = new List<string>();
                foreach (var f in gt.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (f.Name.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0
                        || f.Name.IndexOf("Buildable", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        names.Add(f.Name);
                    }
                }

                ModLogger.Log($"{p}F) GameTags 材料相关字段: {string.Join(", ", names)}");
            }
            catch
            {
            }

            try
            {
                var el = typeof(ElementLoader);
                var methods = new List<string>();
                foreach (var m in el.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    methods.Add(m.Name);
                }

                ModLogger.Log($"{p}F) ElementLoader 静态方法: {string.Join(", ", methods.Distinct())}");
            }
            catch
            {
            }
        }

        /// <summary>
        /// G) Tech 详情样例：desc 字段、costsByResearchTypeID（研究类型→点数）、TechItem.getUISprite 类型。
        /// 校准科技详情卡（研究点需求/科技图标）正式代码。
        /// </summary>
        private void DumpTechDetailProbe()
        {
            const string p = "[TechDiag] ";
            try
            {
                var db = Db.Get();
                var techList = GetFieldValue(GetFieldValue(db, "Techs"), "resources") as IList;
                if (techList == null || techList.Count == 0)
                {
                    return;
                }

                object sampleTech = null;
                foreach (var tech in techList)
                {
                    var ids = GetFieldValue(tech, "unlockedItemIDs") as IEnumerable;
                    if (ids == null)
                    {
                        continue;
                    }

                    foreach (var item in ids)
                    {
                        if (string.Equals(item as string, "FlushToilet", StringComparison.Ordinal))
                        {
                            sampleTech = tech;
                            break;
                        }
                    }

                    if (sampleTech != null)
                    {
                        break;
                    }
                }

                if (sampleTech == null)
                {
                    sampleTech = techList[0];
                }

                ModLogger.Log($"{p}G) 样例科技 Id={GetStringId(sampleTech)} desc=[{GetFieldValue(sampleTech, "desc")}]");

                var costs = GetFieldValue(sampleTech, "costsByResearchTypeID") as IDictionary;
                if (costs == null)
                {
                    object rawCosts = GetFieldValue(sampleTech, "costsByResearchTypeID");
                    ModLogger.Log($"{p}G) costsByResearchTypeID 类型: {(rawCosts == null ? "null" : rawCosts.GetType().FullName)}");
                }
                else
                {
                    var pairs = new List<string>();
                    foreach (DictionaryEntry entry in costs)
                    {
                        pairs.Add($"{entry.Key}({entry.Key.GetType().Name})={entry.Value}({(entry.Value == null ? "null" : entry.Value.GetType().Name)})");
                        if (pairs.Count >= 6)
                        {
                            break;
                        }
                    }

                    ModLogger.Log($"{p}G) costsByResearchTypeID (前6): {string.Join(" | ", pairs)}");
                }

                var unlockedItems = GetFieldValue(sampleTech, "unlockedItems") as IEnumerable;
                if (unlockedItems != null)
                {
                    foreach (var ti in unlockedItems)
                    {
                        if (ti == null)
                        {
                            continue;
                        }

                        var spriteValue = GetFieldValue(ti, "getUISprite");
                        string spriteDesc;
                        if (spriteValue == null)
                        {
                            spriteDesc = "null";
                        }
                        else
                        {
                            var firstProp = spriteValue.GetType().GetProperty("first");
                            spriteDesc = spriteValue.GetType().FullName + (firstProp != null ? " [有first] " + firstProp.PropertyType.Name : " [无first]");
                        }

                        ModLogger.Log($"{p}G) TechItem Id={GetStringId(ti)} getUISprite={spriteDesc}");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                ModLogger.Warn(p + "G) 科技详情探测异常: " + e);
            }
        }

        /// <summary>
        /// H) 屏幕系统运行时形态：GameScreenManager/ManagementMenu 上与 Research 相关的成员、
        /// ResearchScreen.Instance 与公开方法——校准"打开研究面板"导航。
        /// </summary>
        private void DumpScreenSystemProbe()
        {
            const string p = "[TechDiag] ";
            try
            {
                var mgr = GameScreenManager.Instance;
                ModLogger.Log($"{p}H) GameScreenManager.Instance={mgr != null}");
                if (mgr != null)
                {
                    var names = new List<string>();
                    foreach (var f in typeof(GameScreenManager).GetFields(
                                 BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (f.Name.IndexOf("Research", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            names.Add("F:" + f.Name);
                        }
                    }

                    foreach (var pr in typeof(GameScreenManager).GetProperties(
                                 BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (pr.Name.IndexOf("Research", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            names.Add("P:" + pr.Name);
                        }
                    }

                    ModLogger.Log($"{p}H) GameScreenManager Research 成员: {(names.Count == 0 ? "无" : string.Join(", ", names))}");
                }

                var menu = ManagementMenu.Instance;
                ModLogger.Log($"{p}H) ManagementMenu.Instance={menu != null}");
                if (menu != null)
                {
                    var members = new List<string>();
                    foreach (var m in typeof(ManagementMenu).GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (m.Name.IndexOf("Research", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            members.Add("M:" + m.Name);
                        }
                    }

                    foreach (var f in typeof(ManagementMenu).GetFields(
                                 BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (f.Name.IndexOf("Research", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            members.Add("F:" + f.Name);
                        }
                    }

                    foreach (var pr in typeof(ManagementMenu).GetProperties(
                                 BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (pr.Name.IndexOf("Research", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            members.Add("P:" + pr.Name);
                        }
                    }

                    ModLogger.Log($"{p}H) ManagementMenu Research 成员: {(members.Count == 0 ? "无" : string.Join(", ", members))}");
                }

                var rsType = typeof(Db).Assembly.GetType("ResearchScreen");
                if (rsType == null)
                {
                    ModLogger.Warn(p + "H) ResearchScreen 类型不存在");
                }
                else
                {
                    object inst = null;
                    try
                    {
                        var instProp = rsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        inst = instProp?.GetValue(null, null);
                    }
                    catch
                    {
                    }

                    ModLogger.Log($"{p}H) ResearchScreen.Instance={inst != null}");
                    var methods = new List<string>();
                    foreach (var m in rsType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        methods.Add(m.Name);
                    }

                    ModLogger.Log($"{p}H) ResearchScreen 公开方法: {string.Join(", ", methods.Distinct().Take(30))}");
                }
            }
            catch (Exception e)
            {
                ModLogger.Warn(p + "H) 屏幕系统探测异常: " + e);
            }
        }

        /// <summary>科技链递归打印：科技(是否完成, 先决是否满足) &lt;- 前置...</summary>
        private void AppendTechChain(object tech, StringBuilder sb, HashSet<object> visited)
        {
            if (tech == null || !visited.Add(tech))
            {
                sb.Append("(...循环)");
                return;
            }

            string id = GetStringId(tech);

            sb.Append(id)
              .Append("(完成=").Append(InvokeBoolMethod(tech, "IsComplete"))
              .Append(",先决=").Append(InvokeBoolMethod(tech, "ArePrerequisitesComplete"))
              .Append(")");

            var reqs = GetFieldValue(tech, "requiredTech") as IEnumerable;
            if (reqs == null)
            {
                return;
            }

            foreach (var req in reqs)
            {
                sb.Append(" <- ");
                AppendTechChain(req, sb, visited);
            }
        }

        private static object GetPropertyValue(object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            try
            {
                var prop = target.GetType().GetProperty(name);
                return prop == null ? null : prop.GetValue(target, null);
            }
            catch
            {
                return null;
            }
        }

        private static object GetFieldValue(object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            try
            {
                var field = target.GetType().GetField(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return field == null ? null : field.GetValue(target);
            }
            catch
            {
                return null;
            }
        }

        private static bool InvokeBoolMethod(object target, string methodName)
        {
            if (target == null)
            {
                return false;
            }

            try
            {
                var method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                return method != null && (bool)method.Invoke(target, null);
            }
            catch
            {
                return false;
            }
        }

        private void CheckQuests()
        {
            var counts = QuestScanner.CountAllBuildings();
            foreach (var quest in QuestStore.OrderedQuests)
            {
                if (QuestStore.IsClaimed(quest.Id) || notifiedCompleted.Contains(quest.Id))
                {
                    continue;
                }

                // 只追踪已接取的任务
                if (!QuestStore.IsAccepted(quest.Id))
                {
                    continue;
                }

                bool locked = false;
                foreach (var reqId in quest.Requires)
                {
                    if (!QuestStore.IsClaimed(reqId))
                    {
                        locked = true;
                        break;
                    }
                }

                if (locked)
                {
                    continue;
                }

                bool met = quest.Objectives.Count > 0;
                foreach (var objective in quest.Objectives)
                {
                    if (!QuestStore.IsObjectiveMet(objective, counts))
                    {
                        met = false;
                        break;
                    }
                }

                // 知识任务（无建造目标）在解锁后视为立即完成（可领取）
                if (quest.Objectives.Count == 0)
                {
                    met = true;
                }

                if (met)
                {
                    notifiedCompleted.Add(quest.Id);
                    ModLogger.Log($"任务目标达成: #{quest.Order:d2} {quest.Title}");
                    ShowNotification(Lang.T("任务完成：「") + quest.TitleDisp + Lang.T("」——到打印舱面板领取奖励！"), NotificationType.Good);
                    QuestCompleted?.Invoke(quest);
                }
            }
        }

        /// <summary>领取任务奖励：投放物资到打印舱旁，标记已领取。</summary>
        public bool ClaimRewards(QuestDef quest)
        {
            if (quest == null || QuestStore.IsClaimed(quest.Id))
            {
                return false;
            }

            var counts = QuestScanner.CountAllBuildings();
            foreach (var objective in quest.Objectives)
            {
                if (!QuestStore.IsObjectiveMet(objective, counts))
                {
                    ModLogger.Warn($"领取失败：目标未达成 ({quest.Id})");
                    return false;
                }
            }

            bool delivered = RewardsService.Deliver(quest);
            if (!delivered)
            {
                return false;
            }

            QuestStore.MarkClaimed(quest.Id);
            notifiedCompleted.Remove(quest.Id);
            ModLogger.Log($"任务奖励已领取: #{quest.Order:d2} {quest.Title}");
            ShowNotification(Lang.T("已领取「") + quest.TitleDisp + Lang.T("」奖励！下一任务已解锁。"), NotificationType.Good);
            QuestClaimed?.Invoke(quest);
            return true;
        }

        /// <summary>外部模块发游戏内通知（未进存档时静默忽略）。</summary>
        public static void Notify(string message, NotificationType type = NotificationType.Good)
        {
            if (Instance != null)
            {
                Instance.ShowNotification(message, type);
            }
        }

        private void ShowNotification(string message, NotificationType type)
        {
            try
            {
                GameObject owner = Game.Instance != null ? Game.Instance.gameObject : gameObject;
                var notification = new Notification(
                    message,
                    type,
                    (notifications, data) => message + notifications.ReduceMessages(false),
                    null,
                    true,
                    0f,
                    null,
                    null,
                    null,
                    true,
                    false,
                    false);
                owner.AddOrGet<Notifier>().Add(notification, string.Empty);
            }
            catch (Exception e)
            {
                ModLogger.Warn("通知发送失败: " + e.Message);
            }
        }
    }
}