using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BetterFilter.Contents.Patches;

/// <summary>
/// Replaces the checkbox on UR_DummyApplyFilterItem with an "Apply" button
/// that bakes in all currently-disallowed filter-worker decisions as explicit
/// ThingDef disallowances, then re-enables the filter workers.
/// </summary>
[HarmonyPatch(typeof(Listing_TreeThingFilter), "DoThingDef")]
public static class Patch_DummyApplyFilterButton
{
    private const string DummyDefName = "UR_DummyApplyFilterItem";

    private static readonly FieldInfo FilterField =
        AccessTools.Field(typeof(Listing_TreeThingFilter), "filter");
    private static readonly FieldInfo CurYField =
        AccessTools.Field(typeof(Listing), "curY");
    private static readonly MethodInfo LabelLeftMethod =
        AccessTools.Method(typeof(Listing_Tree), "LabelLeft");
    private static readonly MethodInfo EndLineMethod =
        AccessTools.Method(typeof(Listing_Lines), "EndLine");

    static bool Prefix(Listing_TreeThingFilter __instance, ThingDef tDef, int nestLevel, Map map)
    {
        if (tDef.defName != DummyDefName) return true;

        float curY = (float)CurYField.GetValue(__instance);
        var filter = FilterField.GetValue(__instance) as ThingFilter;

        if (Dialog.Dialog_AdvancedFilters.IsPreview)
        {
            // Preview: label only, no button, no checkbox.
            LabelLeftMethod.Invoke(__instance,
                new object[] { (string)tDef.LabelCap, (string)tDef.description, nestLevel, 0f, null, 0f });
            EndLineMethod.Invoke(__instance, null);
            return false;
        }

        // Normal: render label + Apply button instead of checkbox.
        LabelLeftMethod.Invoke(__instance,
            new object[] { (string)tDef.LabelCap, (string)tDef.description, nestLevel, 0f, null, 0f });

        float btnWidth = 90f;
        Rect btnRect = new Rect(__instance.ColumnWidth - btnWidth - 4f, curY, btnWidth,
            __instance.lineHeight);
        if (Widgets.ButtonText(btnRect, "UR.BetterFilter.ApplyFilter".Translate()))
        {
            if (filter != null)
                ApplyFilterWorkers(filter);
        }

        EndLineMethod.Invoke(__instance, null);
        return false;
    }

    /// <summary>
    /// For each whitelisted SpecialThingFilterWorker that is currently disallowed,
    /// disallow all matching ThingDefs, then re-allow the filter worker.
    /// </summary>
    private static void ApplyFilterWorkers(ThingFilter filter)
    {
        var allowedFilters = BetterFilterMod.Settings?.AllowedFilters?.ToList()
            ?? new List<SpecialThingFilterDef>();

        if (allowedFilters.Count == 0)
        {
            Messages.Message("UR.BetterFilter.ApplyEmptyWarning".Translate(),
                MessageTypeDefOf.RejectInput, false);
            return;
        }

        bool changed = false;
        foreach (var sfDef in allowedFilters)
        {
            if (filter.Allows(sfDef)) continue; // already allowed → skip

            // This filter worker is currently disallowed.
            // Disallow all ThingDefs that match it.
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!def.PlayerAcquirable || def.virtualDefParent != null) continue;
                if (!sfDef.Worker.CanEverMatch(def)) continue;
                filter.SetAllow(def, false);
            }

            // Re-allow the filter worker itself.
            filter.SetAllow(sfDef, true);
            changed = true;
        }

        if (changed)
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            var cb = AccessTools.FieldRefAccess<ThingFilter, System.Action>(
                "settingsChangedCallback")(filter);
            cb?.Invoke();
        }
    }
}
