using HarmonyLib;
using Verse;

namespace BetterFilter.Contents.Patches;

/// <summary>
/// Makes UR_BetterFilter's dummy ThingDefs pass EVERY visibility check in
/// Listing_TreeThingFilter.Visible(ThingDef):
///   1. parentFilter.Allows(ThingDef)
///   2. parentFilter.IsAlwaysDisallowedDueToSpecialFilters(ThingDef)
///
/// Together these guarantee UR_BetterFilter is always considered "has visible
/// content" by the vanilla tree, even when the storage container restricts by
/// category or filter (weapons-only, adult-only apparel, etc.).
///
/// The dummies are still hidden from the actual item list via forceHiddenDefs.
/// </summary>
[HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.Allows), typeof(ThingDef))]
public static class Patch_DummyAlwaysVisible_Allows
{
    static bool Prefix(ThingDef def, ref bool __result)
    {
        if (ModDefOf.IsBetterFilterDummy(def)) { __result = true; return false; }
        return true;
    }
}

[HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.IsAlwaysDisallowedDueToSpecialFilters))]
public static class Patch_DummyAlwaysVisible_AlwaysDisallowed
{
    static bool Prefix(ThingDef def, ref bool __result)
    {
        if (ModDefOf.IsBetterFilterDummy(def)) { __result = false; return false; }
        return true;
    }
}
