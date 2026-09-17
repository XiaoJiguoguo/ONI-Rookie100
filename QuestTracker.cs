using System;
using System.Collections.Generic;
using System.Linq;
using Rookie100.Content;
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
                    ShowNotification($"任务完成：「{quest.Title}」——到打印舱面板领取奖励！", NotificationType.Good);
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
            ShowNotification($"已领取「{quest.Title}」奖励！下一任务已解锁。", NotificationType.Good);
            QuestClaimed?.Invoke(quest);
            return true;
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