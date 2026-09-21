using HarmonyLib;

namespace Rookie100.Patches
{
    // These declared lifecycle methods were verified in the target game's DLL.
    // Callbacks only mark the snapshot dirty; they never scan or update quest state.
    [HarmonyPatch(typeof(BuildingComplete), "OnSpawn")]
    public static class BuildingComplete_OnSpawn_Patch
    {
        public static void Postfix()
        {
            QuestScanner.InvalidateBuildings();
        }
    }

    [HarmonyPatch(typeof(BuildingComplete), "OnCleanUp")]
    public static class BuildingComplete_OnCleanUp_Patch
    {
        public static void Postfix()
        {
            QuestScanner.InvalidateBuildings();
        }
    }
}
