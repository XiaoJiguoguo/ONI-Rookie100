using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Rookie100.Content
{
    /// <summary>
    /// 任务仓库：加载 quests.json，维护任务链与领取状态。
    /// 进度由 SaveGame 上的 RookieProgressTracker 持久化；旧 JSON 仅作为导入来源。
    /// 每个游戏存档有独立的 claimed/accepted 集合，载入时绑定已恢复的组件，
    /// 所有进度读写只作用于该组件，不再写回 legacy JSON。
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

        private static RookieProgressTracker progress;
        private static string activeKey;
        private static string legacyReadError;
        private static bool hasUnscopedLegacy;

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

        // Legacy JSON remains on disk, unchanged, for import and rollback.
        private static string ProgressPath =>
            contentPath == null ? null : Path.Combine(contentPath, "quest_progress.json");

        private static void LoadProgress()
        {
            registry = new ProgressRegistry();
            legacyReadError = null;
            hasUnscopedLegacy = false;
            DeactivateColony();
            try
            {
                var path = ProgressPath;
                if (path == null || !File.Exists(path))
                    return;

                var token = Newtonsoft.Json.Linq.JToken.Parse(File.ReadAllText(path));
                if (token.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                {
                    // Validate the historical list, but do not guess its colony.
                    token.ToObject<List<string>>();
                    hasUnscopedLegacy = true;
                    return;
                }
                var obj = token as Newtonsoft.Json.Linq.JObject;
                if (obj == null)
                    throw new InvalidDataException("Progress JSON must be an object or a list.");
                if (obj["Profiles"] != null)
                {
                    if (obj["Version"] == null || obj["Version"].ToObject<int>() != 3)
                        throw new InvalidDataException("Unsupported legacy progress version.");
                    registry = obj.ToObject<ProgressRegistry>();
                    if (registry == null || registry.Profiles == null)
                        throw new InvalidDataException("Legacy Profiles is null.");
                    return;
                }
                if (obj["Claimed"] != null || obj["Accepted"] != null)
                {
                    obj.ToObject<ColonyProgress>();
                    hasUnscopedLegacy = true;
                    return;
                }
                throw new InvalidDataException("Unrecognized legacy progress format.");
            }
            catch (Exception e)
            {
                legacyReadError = e.Message;
                ModLogger.Warn("Legacy progress import unavailable: " + e.Message);
            }
        }

        /// <summary>
        /// Bind the restored native component. Only schema 0 imports JSON;
        /// there is no JSON write-back or second active progress object.
        /// </summary>
        public static void ActivateColony(string colonyKey)
        {
            DeactivateColony();
            var owner = SaveGame.Instance;
            var tracker = owner == null ? null : owner.GetComponent<RookieProgressTracker>();
            if (tracker == null)
                throw new InvalidOperationException("RookieProgressTracker was not attached before save restoration.");

            ColonyProgress legacy = null;
            if (tracker.SchemaVersion == 0)
            {
                if (legacyReadError != null)
                    throw new InvalidDataException("Cannot migrate legacy progress: " + legacyReadError);
                if (hasUnscopedLegacy)
                    throw new InvalidDataException("Legacy progress has no colony identity. Map it to a v3 Profiles entry before importing.");
                if (registry.Profiles.Count > 0)
                {
                    if (string.IsNullOrEmpty(colonyKey) || colonyKey == "default_colony")
                        throw new InvalidDataException("Cannot determine colony identity for legacy import.");
                    if (registry.Profiles.TryGetValue(colonyKey, out legacy))
                    {
                        if (legacy == null)
                            throw new InvalidDataException("Matched legacy colony profile is null.");
                    }
                    else if (registry.Profiles.ContainsKey("default_colony"))
                        throw new InvalidDataException("Unassigned default_colony legacy progress requires an explicit colony mapping.");
                }
            }

            tracker.Initialize(legacy == null ? null : legacy.Claimed,
                legacy == null ? null : legacy.Accepted);
            progress = tracker;
            activeKey = colonyKey;
            ModLogger.Log("Native progress active: schema=" + tracker.SchemaVersion +
                ", colony=" + colonyKey + ", claimed=" + tracker.ClaimedCount +
                ", importedLegacy=" + (legacy != null));
        }

        public static void DeactivateColony(RookieProgressTracker expectedOwner = null)
        {
            // A late cleanup of colony A must not detach already-bound colony B.
            if (!ReferenceEquals(expectedOwner, null) && !ReferenceEquals(progress, expectedOwner))
                return;
            progress = null;
            activeKey = null;
        }

        public static string ActiveColonyKey => activeKey;
        public static bool IsProgressReady => progress != null && progress.IsReady;

        public static bool IsClaimed(string questId)
        {
            return IsProgressReady && progress.IsClaimed(questId);
        }

        public static void MarkClaimed(string questId)
        {
            if (IsProgressReady)
                progress.MarkClaimed(questId);
        }

        public static bool IsAccepted(string questId)
        {
            return IsProgressReady && progress.IsAccepted(questId);
        }

        public static bool AcceptQuest(string questId)
        {
            return IsProgressReady && progress.AcceptQuest(questId);
        }

        public static void ResetAll()
        {
            if (IsProgressReady)
                progress.ResetAll();
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

        public static int ClaimedCount => IsProgressReady ? progress.ClaimedCount : 0;

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
            return QuestBuildingSnapshot.IsMet(objective, counts);
        }

        /// <summary>目标进度文本，如 "户外厕所 1/1"。</summary>
        public static string GetObjectiveStatusText(QuestObjectiveDef objective, Dictionary<string, int> counts)
        {
            int count = QuestBuildingSnapshot.ObjectiveCount(objective, counts);
            string label = string.IsNullOrEmpty(objective.LabelDisp) ? objective.Tag : objective.LabelDisp;
            return $"{label} {Math.Min(count, objective.Count)}/{objective.Count}";
        }
    }
}