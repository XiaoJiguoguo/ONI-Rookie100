using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Rookie100.Content
{
    /// <summary>
    /// 任务仓库：加载 quests.json，维护任务链与领取状态。
    /// 进度持久化到模组目录 quest_progress.json，**按存档（殖民地）分档案存储**：
    /// 每个存档有独立的 claimed/accepted 集合，载入殖民地时激活对应档案，
    /// 所有读写只作用于当前激活档案，实现多存档进度隔离。
    /// 其他状态（锁定/进行中/待领取）由前置与目标计数实时推导。
    /// </summary>
    public static class QuestStore
    {
        private static string contentPath;
        private static QuestContent content = new QuestContent();

        // ---- 按存档隔离的进度档案 ----

        /// <summary>单个存档（殖民地）的进度档案。</summary>
        private class ColonyProgress
        {
            public List<string> Claimed { get; set; } = new List<string>();
            public List<string> Accepted { get; set; } = new List<string>();
        }

        /// <summary>quest_progress.json 顶层结构（v3：多档案注册表）。</summary>
        private class ProgressRegistry
        {
            public int Version { get; set; } = 3;
            public string Active { get; set; }
            public Dictionary<string, ColonyProgress> Profiles { get; set; } = new Dictionary<string, ColonyProgress>();
        }

        private static ProgressRegistry registry = new ProgressRegistry();

        /// <summary>当前激活的存档档案（未进入殖民地时为 null，读写动作不落盘）。</summary>
        private static ColonyProgress active;

        private static string activeKey;

        /// <summary>
        /// 旧版单文件进度（升级前所有存档共享的那一份）：
        /// 载入第一个殖民地时归并进该存档的档案，完成一次性迁移。
        /// </summary>
        private static ColonyProgress pendingLegacyMigration;

        public static void SetContentPath(string path)
        {
            contentPath = path;
        }

        /// <summary>
        /// 元素 id 别名纠正：奖励/图标取的是元素 SimHashes id（也是 CarePackageInfo 投放 id）。
        /// U59 实测煤的元素 id 是 Carbon（Coal 只是它的 oreTag），
        /// 用 "Coal" 走 Def.GetUISprite/CarePackageInfo 会报 Missing prefab 且投放失败。
        /// 数据层（quests.json）已修正，此表作为历史存档/手误的防御兜底。
        /// </summary>
        private static readonly Dictionary<string, string> elementIdAliases = new Dictionary<string, string>
        {
            { "Coal", "Carbon" },
        };

        public static string CanonicalElementId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }

            return elementIdAliases.TryGetValue(id, out string canonical) ? canonical : id;
        }

        public static void Load()
        {
            var jsonPath = contentPath == null ? null : Path.Combine(contentPath, "quests.json");
            if (jsonPath == null || !File.Exists(jsonPath))
            {
                ModLogger.Warn($"quests.json 不存在: {jsonPath}");
                content = new QuestContent();
            }
            else
            {
                try
                {
                    content = JsonConvert.DeserializeObject<QuestContent>(File.ReadAllText(jsonPath))
                              ?? new QuestContent();
                    ModLogger.Log($"任务包加载完成: {content.Phases.Count} 阶段 / {content.Quests.Count} 任务");
                }
                catch (Exception e)
                {
                    ModLogger.Error("quests.json 解析失败", e);
                    content = new QuestContent();
                }
            }

            LoadProgress();
        }

        // ---- 进度持久化（按存档分档案） ----

        private static string ProgressPath =>
            contentPath == null ? null : Path.Combine(contentPath, "quest_progress.json");

        private static void LoadProgress()
        {
            registry = new ProgressRegistry();
            pendingLegacyMigration = null;
            active = null;
            activeKey = null;
            try
            {
                var path = ProgressPath;
                if (path == null || !File.Exists(path))
                {
                    return;
                }

                string raw = File.ReadAllText(path);

                // v3：多档案注册表（本版本格式）
                var parsed = JsonConvert.DeserializeObject<ProgressRegistry>(raw);
                if (parsed != null && parsed.Profiles != null && parsed.Profiles.Count > 0)
                {
                    registry = parsed;
                    ModLogger.Log($"进度注册表加载: {registry.Profiles.Count} 个存档档案");
                    return;
                }

                // 旧版：单份平铺进度（所有存档共享）→ 待首个殖民地激活时迁移
                var legacy = JsonConvert.DeserializeObject<ColonyProgress>(raw);
                if (legacy == null)
                {
                    // 更旧的纯列表格式视为 claimed
                    legacy = new ColonyProgress
                    {
                        Claimed = JsonConvert.DeserializeObject<List<string>>(raw) ?? new List<string>()
                    };
                }

                pendingLegacyMigration = legacy;
                ModLogger.Log("检测到旧版单存档进度文件，将在载入第一个殖民地时迁移");
            }
            catch (Exception e)
            {
                ModLogger.Warn("读取 quest_progress.json 失败: " + e.Message);
            }
        }

        private static void SaveProgress()
        {
            try
            {
                var path = ProgressPath;
                if (path == null)
                {
                    return;
                }

                registry.Active = activeKey;
                File.WriteAllText(path, JsonConvert.SerializeObject(registry, Formatting.Indented));
            }
            catch (Exception e)
            {
                ModLogger.Warn("写入 quest_progress.json 失败: " + e.Message);
            }
        }

        /// <summary>
        /// 载入殖民地时调用：按存档 key 激活专属进度档案。
        /// 新存档（key 不在注册表中）= 全新进度；旧版单份进度在此一次性迁移进首个激活的档案。
        /// </summary>
        public static void ActivateColony(string colonyKey)
        {
            if (string.IsNullOrEmpty(colonyKey))
            {
                ModLogger.Warn("存档 key 为空，退回 'default_colony'（同名殖民地可能共享进度）");
                colonyKey = "default_colony";
            }

            if (activeKey == colonyKey && active != null)
            {
                ModLogger.Log($"任务进度档案已激活（同档重载）: {colonyKey}（已领取 {active.Claimed.Count}）");
                return;
            }

            if (!registry.Profiles.TryGetValue(colonyKey, out ColonyProgress profile))
            {
                // 新存档：初始化全新进度
                profile = new ColonyProgress();
                registry.Profiles[colonyKey] = profile;
                ModLogger.Log($"任务进度档案: 新存档 {colonyKey}，初始化全新进度");
            }
            else
            {
                ModLogger.Log($"任务进度档案: 载入存档 {colonyKey}（已领取 {profile.Claimed.Count}）");
            }

            // 旧版共享进度迁移：仅归并给第一个激活的存档档案
            if (pendingLegacyMigration != null)
            {
                if (profile.Claimed.Count == 0 && profile.Accepted.Count == 0)
                {
                    profile.Claimed.AddRange(pendingLegacyMigration.Claimed);
                    profile.Accepted.AddRange(pendingLegacyMigration.Accepted);
                    ModLogger.Log($"旧版进度已迁移至存档 {colonyKey}: 已领取 {profile.Claimed.Count}");
                }

                pendingLegacyMigration = null;
            }

            active = profile;
            activeKey = colonyKey;
            SaveProgress();
        }

        /// <summary>当前存档 key（未激活时为空）。</summary>
        public static string ActiveColonyKey => activeKey;

        private static ColonyProgress EnsureActive()
        {
            if (active == null)
            {
                // 未进入殖民地（主菜单等）：空档案兜底，不落盘
                active = new ColonyProgress();
                activeKey = "";
            }

            return active;
        }

        // ---- 进度读写（仅作用于当前激活档案） ----

        public static bool IsClaimed(string questId)
        {
            if (questId == null || active == null)
            {
                return false;
            }

            return active.Claimed.Contains(questId);
        }

        public static void MarkClaimed(string questId)
        {
            if (questId == null || active == null || active.Claimed.Contains(questId))
            {
                return;
            }

            active.Claimed.Add(questId);
            SaveProgress();
        }

        public static bool IsAccepted(string questId)
        {
            if (questId == null || active == null)
            {
                return false;
            }

            return active.Accepted.Contains(questId);
        }

        /// <summary>接取任务（Available → Accepted）。</summary>
        public static bool AcceptQuest(string questId)
        {
            var profile = EnsureActive();
            if (questId == null || profile.Claimed.Contains(questId) || profile.Accepted.Contains(questId))
            {
                return false;
            }

            profile.Accepted.Add(questId);
            SaveProgress();
            ModLogger.Log($"任务已接取: {questId}（存档 {activeKey}）");
            return true;
        }

        public static void ResetAll()
        {
            var profile = EnsureActive();
            profile.Claimed.Clear();
            profile.Accepted.Clear();
            SaveProgress();
            ModLogger.Log($"全部任务进度已重置（存档 {activeKey}）");
        }

        // ---- 查询 ----

        public static IReadOnlyList<QuestPhaseDef> Phases => content.Phases;
        public static IReadOnlyList<QuestDef> Quests => content.Quests;

        public static QuestDef GetQuest(string id)
        {
            return id == null ? null : content.Quests.FirstOrDefault(q => q.Id == id);
        }

        public static List<QuestDef> GetQuestsOfPhase(string phaseId)
        {
            return content.Quests.Where(q => q.Phase == phaseId).OrderBy(q => q.Order).ToList();
        }

        public static List<QuestDef> OrderedQuests => content.Quests.OrderBy(q => q.Order).ToList();

        public static int ClaimedCount => active == null ? 0 : active.Claimed.Count;

        // ---- 状态机 ----

        /// <summary>
        /// 任务状态。counts 为建筑计数缓存（调用方单次扫描后复用，避免每行重复全场景扫描）；
        /// 传 null 时内部扫描一次。
        /// </summary>
        public static QuestStatus GetStatus(QuestDef quest, Dictionary<string, int> counts = null)
        {
            if (IsClaimed(quest.Id))
            {
                return QuestStatus.Claimed;
            }

            foreach (var reqId in quest.Requires)
            {
                if (!IsClaimed(reqId))
                {
                    return QuestStatus.Locked;
                }
            }

            // 未接取的任务不算进度，保持可接取状态
            if (!IsAccepted(quest.Id))
            {
                return QuestStatus.Available;
            }

            counts = counts ?? Rookie100.QuestScanner.CountAllBuildings();
            int met = 0;
            int total = quest.Objectives.Count;
            foreach (var objective in quest.Objectives)
            {
                if (IsObjectiveMet(objective, counts))
                {
                    met++;
                }
            }

            if (total == 0)
            {
                // 知识任务：接取后即视为可领取
                return QuestStatus.Completed;
            }

            if (met >= total)
            {
                return QuestStatus.Completed;
            }

            return met > 0 ? QuestStatus.InProgress : QuestStatus.Accepted;
        }

        public static bool IsObjectiveMet(QuestObjectiveDef objective, Dictionary<string, int> counts)
        {
            switch (objective.Type)
            {
                case "buildingBuilt":
                    counts.TryGetValue(objective.Tag, out int count);
                    return count >= objective.Count;
                default:
                    return false;
            }
        }

        /// <summary>目标进度文本，如 "户外厕所 1/1"。</summary>
        public static string GetObjectiveStatusText(QuestObjectiveDef objective, Dictionary<string, int> counts)
        {
            counts.TryGetValue(objective.Tag, out int count);
            string label = string.IsNullOrEmpty(objective.LabelDisp) ? objective.Tag : objective.LabelDisp;
            return $"{label} {Math.Min(count, objective.Count)}/{objective.Count}";
        }
    }
}