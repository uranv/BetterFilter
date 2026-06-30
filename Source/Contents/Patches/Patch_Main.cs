using Verse;

namespace BetterFilter.Contents.Patches;

[StaticConstructorOnStartup]
public static class HarmonyPatchesMain
{
    static HarmonyPatchesMain()
    {
        var harmony = new HarmonyLib.Harmony("com.ur.BetterFilter.patches");
        harmony.PatchAll();
    }
}