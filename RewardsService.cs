using System;
using System.Linq;
using Rookie100.Content;
using UnityEngine;

namespace Rookie100
{
    /// <summary>
    /// 奖励投放服务：把任务物资以"打印舱补给包"形式投放到打印舱旁。
    /// 核心 API 为游戏原生的 CarePackageInfo.Deliver（这就是补给包机制本身），
    /// 投放位置 + 弹出动画 + 音效模式取自 StorageNetwork 的 TelepadBonusDeliveryPatch。
    /// </summary>
    public static class RewardsService
    {
        /// <summary>把任务的全部奖励投放到打印舱。</summary>
        public static bool Deliver(QuestDef quest)
        {
            if (quest == null || quest.Rewards == null || quest.Rewards.Count == 0)
            {
                ModLogger.Warn($"任务 {quest?.Id} 没有配置奖励");
                return true; // 无奖励任务视为投放成功
            }

            Vector3 spawnPosition = ResolveSpawnPosition();
            foreach (var reward in quest.Rewards)
            {
                DeliverOne(reward, spawnPosition);
            }

            return true;
        }

        private static void DeliverOne(QuestRewardDef reward, Vector3 spawnPosition)
        {
            // 元素 id 纠正（Coal 是 oreTag，真实元素 id 为 Carbon；错误 id 会静默投放失败）
            string elementId = QuestStore.CanonicalElementId(reward.Element);
            try
            {
                // 游戏原生补给包：在指定位置生成对应物资的包裹
                var info = new CarePackageInfo(elementId, reward.Amount, () => true, null);
                GameObject delivered = info.Deliver(spawnPosition);

                if (delivered == null)
                {
                    ModLogger.Warn($"奖励投放失败: {elementId} x{reward.Amount}");
                    return;
                }

                // 弹出 + 音效反馈
                PopFXManager.Instance.SpawnFX(
                    PopFXManager.Instance.sprite_Plus,
                    reward.Label,
                    delivered.transform,
                    new Vector3(0f, 0.5f, 0f),
                    1.5f,
                    false,
                    false);
                KMonoBehaviour.PlaySound(GlobalAssets.GetSound("SandboxTool_Spawner", false));
                ModLogger.Log($"奖励投放: {reward.Label} → 打印舱");
            }
            catch (Exception e)
            {
                ModLogger.Error($"奖励投放异常 {elementId}", e);
            }
        }

        /// <summary>打印舱旁的投放位置（找不到打印舱则用屏幕中心地面兜底）。</summary>
        private static Vector3 ResolveSpawnPosition()
        {
            Telepad telepad = null;
            try
            {
                var telepads = UnityEngine.Object.FindObjectsByType<Telepad>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                telepad = telepads.FirstOrDefault(t => t != null && t.gameObject != null);
            }
            catch (Exception e)
            {
                ModLogger.Warn("查找打印舱失败: " + e.Message);
            }

            if (telepad == null)
            {
                // 兜底：世界原点上方
                var fallback = new Vector3(0f, 3f, Grid.GetLayerZ(Grid.SceneLayer.Front));
                ModLogger.Warn("未找到打印舱，奖励投放在世界原点");
                return fallback;
            }

            int cell = Grid.OffsetCell(Grid.PosToCell(telepad.gameObject), 1, 1);
            Vector3 position = Grid.CellToPosCBC(cell, Grid.SceneLayer.Front) - Vector3.right / 2f;
            return position;
        }
    }
}