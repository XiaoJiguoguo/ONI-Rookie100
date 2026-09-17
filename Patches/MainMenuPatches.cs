using HarmonyLib;

namespace Rookie100.Patches
{
    /// <summary>
    /// 示例补丁：主菜单激活时打日志。
    /// 不用进存档就能在 Player.log 里确认模组已加载、Harmony 已生效。
    /// </summary>
    [HarmonyPatch(typeof(MainMenu), "OnActivate")]
    public static class MainMenu_OnActivate_Patch
    {
        public static void Postfix()
        {
            ModLogger.Log("主菜单已激活 —— 模组存活确认 (Harmony patch OK)");
        }
    }
}
