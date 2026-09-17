using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Rookie100.Content
{
    /// <summary>
    /// 任务仓库：加载 quests.json，维护任务链与领取状态。
    /// 领取状态持久化到模组目录 quest_progress.json（claimed 列表）。
    /// 其他状态（锁定/进行中/待领取）由前置与目标计数实时推导。
    /// </summary>
    public static class QuestStore
    {
        private static string contentPath;
        private static QuestContent content = new QuestContent();
        private static readonly HashSet<string> claimedIds = new HashSet<string>();
        private static readonly HashSet<string> acceptedIds = new HashSet<string>();

        public static void SetContentPath(string path)
        {
            contentPath = path;
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

        // ---- 进度持久化（claimed + accepted） ----

        private static string ProgressPath =>
            contentPath == null ? null : Path.Combine(contentPath, "quest_progress.json");

        private class ProgressFile
        {
            public List<string> Claimed { get; set; } = new List<string>();
            public List<string> Accepted { get; set; } = new List<string>();
        }

        private static void LoadProgress()
        {
            claimedIds.Clear();
            acceptedIds.Clear();
            try
            {
                var path = ProgressPath;
                if (path == null || !File.Exists(path))
                {
                    return;
                }

                string raw = File.ReadAllText(path);
                // 新版格式 {"Claimed":[...],"Accepted":[...]}
                var progress = JsonConvert.DeserializeObject<ProgressFile>(raw);
                if (progress != null)
                {
                    foreach (var id in progress.Claimed ?? new List<string>())
                    {
                        claimedIds.Add(id);
                    }

                    foreach (var id in progress.Accepted ?? new List<string>())
                    {
                        acceptedIds.Add(id);
                    }
                }
                else
                {
                    // 旧版格式（纯列表）视为 claimed
                    var list = JsonConvert.DeserializeObject<List<string>>(raw);
                    foreach (var id in list ?? new List<string>())
                    {
                        claimedIds.Add(id);
                    }
                }
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

                var progress = new ProgressFile
                {
                    Claimed = claimedIds.OrderBy(x => x).ToList(),
                    Accepted = acceptedIds.OrderBy(x => x).ToList()
                };
                File.WriteAllText(path, JsonConvert.SerializeObject(progress, Formatting.Indented));
            }
            catch (Exception e)
            {
                ModLogger.Warn("写入 quest_progress.json 失败: " + e.Message);
            }
        }

        public static bool IsClaimed(string questId)
        {
            return questId != null && claimedIds.Contains(questId);
        }

        public static void MarkClaimed(string questId)
        {
            if (questId == null || !claimedIds.Add(questId))
            {
                return;
            }

            SaveProgress();
        }

        public static bool IsAccepted(string questId)
        {
            return questId != null && acceptedIds.Contains(questId);
        }

        /// <summary>接取任务（Available → Accepted）。</summary>
        public static bool AcceptQuest(string questId)
        {
            if (questId == null || IsClaimed(questId) || !acceptedIds.Add(questId))
            {
                return false;
            }

            SaveProgress();
            ModLogger.Log($"任务已接取: {questId}");
            return true;
        }

        public static void ResetAll()
        {
            claimedIds.Clear();
            acceptedIds.Clear();
            SaveProgress();
            ModLogger.Log("全部任务进度已重置");
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

        public static int ClaimedCount => claimedIds.Count;

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
            string label = string.IsNullOrEmpty(objective.Label) ? objective.Tag : objective.Label;
            return $"{label} {Math.Min(count, objective.Count)}/{objective.Count}";
        }
    }
}