using HarmonyLib;

namespace Rookie100.Patches
{
    /// <summary>
    /// 游戏生命周期补丁：进存档时挂载任务追踪器（周期检测任务目标）。
    /// </summary>
    public static class GamePatches
    {
        [HarmonyPatch(typeof(Game), "OnSpawn")]
        public static class Game_OnSpawn_Patch
        {
            public static void Postfix(Game __instance)
            {
                __instance.gameObject.AddOrGet<QuestTracker>();
                ModLogger.Log($"殖民地已生成，任务追踪器就位 (已领取 {Content.QuestStore.ClaimedCount}/{Content.QuestStore.Quests.Count})");
            }
        }
    }
}
