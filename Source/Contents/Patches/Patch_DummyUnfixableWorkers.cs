using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterFilter.Contents.Patches;

/// <summary>
/// Eleven vanilla SpecialThingFilterWorkers that cannot rely on dummy
/// ThingDef properties alone.  burnableByRecipe and smeltable are public
/// *fields* on ThingDef (set via XML tags), not the similarly-named computed
/// properties.  Without those tags the fields default to false, so Burnable
/// and Smeltable need patches too.
///
/// Each patch simply returns true when the def is one of our dummies.
/// </summary>

// ── Already covered by dummy comps / useHitPoints ─────────────────────
// Fresh     → CompProperties_Rottable
// Rotten    → CompProperties_Rottable(rotDestroys=false)
// NonSmeltable, NonBurnable → default CanEverMatch (not overridden)
// ───────────────────────────────────────────────────────────────────────

// ── Weapon / apparel workers that need patches ──

[HarmonyPatch(typeof(SpecialThingFilterWorker_DeadmansApparel), nameof(SpecialThingFilterWorker_DeadmansApparel.CanEverMatch))]
public static class Patch_DummyDeadmansApparel
{
    static bool Prefix(ThingDef def, ref bool __result)
    { if (ModDefOf.IsBetterFilterDummy(def)) { __result = true; return false; } return true; }
}

[HarmonyPatch(typeof(SpecialThingFilterWorker_NonDeadmansApparel), nameof(SpecialThingFilterWorker_NonDeadmansApparel.CanEverMatch))]
public static class Patch_DummyNonDeadmansApparel
{
    static bool Prefix(ThingDef def, ref bool __result)
    { if (ModDefOf.IsBetterFilterDummy(def)) { __result = true; return false; } return true; }
}

[HarmonyPatch(typeof(SpecialThingFilterWorker_BiocodedApparel), nameof(SpecialThingFilterWorker_BiocodedApparel.CanEverMatch))]
public static class Patch_DummyBiocodedApparel
{
    static bool Prefix(ThingDef def, ref bool __result)
    { if (ModDefOf.IsBetterFilterDummy(def)) { __result = true; return false; } return true; }
}

[HarmonyPatch(typeof(SpecialThingFilterWorker_NonBiocodedApparel), nameof(SpecialThingFilterWorker_NonBiocodedApparel.CanEverMatch))]
public static class Patch_DummyNonBiocodedApparel
{
    static bool Prefix(ThingDef def, ref bool __result)
    { if (ModDefOf.IsBetterFilterDummy(def)) { __result = true; return false; } return true; }
}

[HarmonyPatch(typeof(SpecialThingFilterWorker_BiocodedWeapons), nameof(SpecialThingFilterWorker_BiocodedWeapons.CanEverMatch))]
public static class Patch_DummyBiocodedWeapons
{
    static bool Prefix(ThingDef def, ref bool __result)
    { if (ModDefOf.IsBetterFilterDummy(def)) { __result = true; return false; } return true; }
}

[HarmonyPatch(typeof(SpecialThingFilterWorker_NonBiocodedWeapons), nameof(SpecialThingFilterWorker_NonBiocodedWeapons.CanEverMatch))]
public static class Patch_DummyNonBiocodedWeapons
{
    static bool Prefix(ThingDef def, ref bool __result)
    { if (ModDefOf.IsBetterFilterDummy(def)) { __result = true; return false; } return true; }
}

// ── Weapon-category / corpse workers ──

[HarmonyPatch(typeof(SpecialThingFilterWorker_NonBurnableWeapons), nameof(SpecialThingFilterWorker_NonBurnableWeapons.CanEverMatch))]
public static class Patch_DummyNonBurnableWeapons
{
    static bool Prefix(ThingDef def, ref bool __result)
    { if (ModDefOf.IsBetterFilterDummy(def)) { __result = true; return false; } return true; }
}

[HarmonyPatch(typeof(SpecialThingFilterWorker_NonSmeltableWeapons), nameof(SpecialThingFilterWorker_NonSmeltableWeapons.CanEverMatch))]
public static class Patch_DummyNonSmeltableWeapons
{
    static bool Prefix(ThingDef def, ref bool __result)
    { if (ModDefOf.IsBetterFilterDummy(def)) { __result = true; return false; } return true; }
}

[HarmonyPatch(typeof(SpecialThingFilterWorker_CorpsesLarge), nameof(SpecialThingFilterWorker_CorpsesLarge.CanEverMatch))]
public static class Patch_DummyCorpsesLarge
{
    static bool Prefix(ThingDef def, ref bool __result)
    { if (ModDefOf.IsBetterFilterDummy(def)) { __result = true; return false; } return true; }
}

// ── Field-dependent workers (burnableByRecipe / smeltable are ThingDef fields) ──

[HarmonyPatch(typeof(SpecialThingFilterWorker_Burnable), nameof(SpecialThingFilterWorker_Burnable.CanEverMatch))]
public static class Patch_DummyBurnable
{
    static bool Prefix(ThingDef def, ref bool __result)
    { if (ModDefOf.IsBetterFilterDummy(def)) { __result = true; return false; } return true; }
}

[HarmonyPatch(typeof(SpecialThingFilterWorker_Smeltable), nameof(SpecialThingFilterWorker_Smeltable.CanEverMatch))]
public static class Patch_DummySmeltable
{
    static bool Prefix(ThingDef def, ref bool __result)
    { if (ModDefOf.IsBetterFilterDummy(def)) { __result = true; return false; } return true; }
}
