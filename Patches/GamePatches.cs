using System;
using System.Reflection;
using HarmonyLib;

namespace Rookie100.Patches
{
    /// <summary>
    /// 游戏生命周期补丁：进存档时挂载任务追踪器（周期检测任务目标），
    /// 并按存档（殖民地）key 激活该存档专属的任务进度档案（多存档进度隔离）。
    /// </summary>
    public static class GamePatches
    {
        [HarmonyPatch(typeof(Game), "OnSpawn")]
        public static class Game_OnSpawn_Patch
        {
            public static void Postfix(Game __instance)
            {
                var tracker = __instance.gameObject.AddOrGet<QuestTracker>();
                tracker.ResetForNewColony();

                string colonyKey = ResolveColonyKey();
                Content.QuestStore.ActivateColony(colonyKey);
                ModLogger.Log($"殖民地已生成，任务追踪器就位 (已领取 {Content.QuestStore.ClaimedCount}/{Content.QuestStore.Quests.Count})");
            }
        }

        /// <summary>
        /// 解析当前存档的唯一标识（存档 key）。
        /// 候选链：SaveLoader(类型/实例).saveFolder 属性或字段 → 取路径末段。
        /// 全部失败退回 'default_colony'（此时无法保证同名殖民地隔离，日志会警告）。
        /// 每次命中来源都会写日志（运行时证据）。
        /// </summary>
        private static string ResolveColonyKey()
        {
            try
            {
                // 1) 定位 SaveLoader 类型（编译期已知类型，找不到时按名反射兜底）
                Type slType = typeof(SaveLoader);
                if (slType == null)
                {
                    slType = typeof(Game).Assembly.GetType("SaveLoader");
                }

                if (slType == null)
                {
                    ModLogger.Warn("存档 key 解析: 找不到 SaveLoader 类型");
                    return null;
                }

                // 2) 取单例实例（静态属性 Instance）
                object instance = null;
                var instanceProp = slType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (instanceProp != null)
                {
                    instance = instanceProp.GetValue(null, null);
                }

                if (instance == null)
                {
                    ModLogger.Warn("存档 key 解析: SaveLoader.Instance 为空（可能尚未进入殖民地）");
                    return null;
                }

                Type instType = instance.GetType();

                // 3) saveFolder 属性/字段（存档目录名 = 殖民地名称，创建后不变）
                var folderProp = instType.GetProperty("saveFolder",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (folderProp != null && folderProp.PropertyType == typeof(string))
                {
                    string folder = folderProp.GetValue(instance, null) as string;
                    if (!string.IsNullOrEmpty(folder))
                    {
                        string key = System.IO.Path.GetFileName(folder.TrimEnd('\\', '/'));
                        ModLogger.Log($"存档 key 解析: SaveLoader 实例属性 saveFolder = '{key}'");
                        return key;
                    }
                }

                var folderField = instType.GetField("saveFolder",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (folderField != null && folderField.FieldType == typeof(string))
                {
                    string folder = folderField.GetValue(instance) as string;
                    if (!string.IsNullOrEmpty(folder))
                    {
                        string key = System.IO.Path.GetFileName(folder.TrimEnd('\\', '/'));
                        ModLogger.Log($"存档 key 解析: SaveLoader 实例字段 saveFolder = '{key}'");
                        return key;
                    }
                }

                // 4) 兜底：SaveLoader 上的 clusterId / colonyName 候选（未知版本可用性）
                foreach (var member in new[]
                {
                    instType.GetProperty("clusterId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    instType.GetProperty("colonyName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                })
                {
                    if (member != null && member.PropertyType == typeof(string))
                    {
                        string value = member.GetValue(instance, null) as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            ModLogger.Log($"存档 key 解析: SaveLoader 实例候选成员 {member.Name} = '{value}'");
                            return value;
                        }
                    }
                }

                ModLogger.Warn("存档 key 解析: SaveLoader 上未找到 saveFolder/clusterId/colonyName 候选成员");
                return null;
            }
            catch (Exception e)
            {
                ModLogger.Warn("存档 key 解析异常: " + e.Message);
                return null;
            }
        }
    }
}