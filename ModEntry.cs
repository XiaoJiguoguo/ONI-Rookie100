using System;
using HarmonyLib;
using KMod;

namespace Rookie100
{
    /// <summary>
    /// 模组入口。加载器扫描所有继承 KMod.UserMod2 的类（类名任意）。
    /// </summary>
    public class ModEntry : KMod.UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);

            var version = typeof(ModEntry).Assembly.GetName().Version;
            ModLogger.Log($"v{version} 开始加载 (content path: {mod.ContentPath})");

            try
            {
                // 内容与 UI 初始化
                Content.QuestStore.SetContentPath(mod.ContentPath);
                UI.LayoutStore.SetContentPath(mod.ContentPath);
                Content.QuestStore.Load();

                // 应用所有 [HarmonyPatch] 注解补丁
                harmony.PatchAll(typeof(ModEntry).Assembly);
                ModLogger.Log("Harmony 补丁已全部应用");
            }
            catch (Exception e)
            {
                ModLogger.Error("初始化失败", e);
            }

            ModLogger.Log("加载完成");
        }
    }
}
