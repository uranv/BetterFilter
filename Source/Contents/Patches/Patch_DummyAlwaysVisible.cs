using HarmonyLib;
using Verse;

namespace BetterFilter.Contents.Patches;

/// <summary>
/// Makes UR_BetterFilter's dummy ThingDefs pass EVERY visibility check in
/// Listing_TreeThingFilter.Visible(ThingDef), but ONLY during tree UI rendering.
///
/// Gated by Patch_FilterCategoryOrder.IsRenderingTreeUI — outside of tree
/// painting (e.g. TurretGunUtility.TryFindRandomShellDef, PawnApparelGenerator)
/// the dummies behave like normal ThingDefs and never pollute game logic.
/// </summary>
[HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.Allows), typeof(ThingDef))]
public static class Patch_DummyAlwaysVisible_Allows
{
    static bool Prefix(ThingDef def, ref bool __result)
    {
        if (Patch_FilterCategoryOrder.IsRenderingTreeUI && ModDefOf.IsBetterFilterDummy(def))
        { __result = true; return false; }
        return true;
    }
}

[HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.IsAlwaysDisallowedDueToSpecialFilters))]
public static class Patch_DummyAlwaysVisible_AlwaysDisallowed
{
    static bool Prefix(ThingDef def, ref bool __result)
    {
        if (Patch_FilterCategoryOrder.IsRenderingTreeUI && ModDefOf.IsBetterFilterDummy(def))
        { __result = false; return false; }
        return true;
    }
}
