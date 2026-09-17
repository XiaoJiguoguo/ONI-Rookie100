using HarmonyLib;
using Rookie100.UI;

namespace Rookie100.Patches
{
    /// <summary>管理菜单初始化时注入"百天助手"按钮。</summary>
    public static class ManagementMenuPatch
    {
        [HarmonyPatch(typeof(ManagementMenu), "OnPrefabInit")]
        public static class ManagementMenuOnPrefabInitPatch
        {
            public static void Postfix(ManagementMenu __instance)
            {
                if (__instance == null)
                {
                    return;
                }

                try
                {
                    ManagementMenuInstaller.Install(__instance);
                }
                catch (System.Exception exception)
                {
                    ModLogger.Error("管理菜单按钮注入失败", exception);
                }
            }
        }
    }
}
