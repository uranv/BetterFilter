using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Patches;

/// <summary>
/// Replaces the regular checkbox on UR_DummyFilterItem with an "Advanced" button
/// that opens the Advanced Filters dialog.
/// </summary>
[HarmonyPatch(typeof(Listing_TreeThingFilter), "DoThingDef")]
public static class Patch_DummyAdvancedFiltersButton
{
    private const string DummyDefName = "UR_DummyFilterItem";

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

        if (Dialog.Dialog_AdvancedFilters.IsPreview)
        {
            // Preview: render label only, no button, no checkbox.
            LabelLeftMethod.Invoke(__instance,
                new object[] { (string)tDef.LabelCap, (string)tDef.description, nestLevel, 0f, null, 0f });
            EndLineMethod.Invoke(__instance, null);
            return false;
        }

        // Normal (storage filter): render label + Advanced button instead of checkbox.
        LabelLeftMethod.Invoke(__instance,
            new object[] { (string)tDef.LabelCap, (string)tDef.description, nestLevel, 0f, null, 0f });

        float btnWidth = 90f;
        Rect btnRect = new Rect(__instance.ColumnWidth - btnWidth - 4f, curY, btnWidth,
            __instance.lineHeight);
        if (Widgets.ButtonText(btnRect, "UR.BetterFilter.OpenAdvanced".Translate()))
        {
            var filter = FilterField.GetValue(__instance) as ThingFilter;
            if (filter != null)
            {
                var pf = Patch_FilterCategoryOrder.GetParentFilter(__instance);
                var ctxDefs = GetContextDefsFromParentFilter(pf);
                Find.WindowStack.Add(new Dialog.Dialog_AdvancedFilters(filter, ctxDefs));
            }
        }

        EndLineMethod.Invoke(__instance, null);
        return false;
    }

    private static List<ThingDef> GetContextDefsFromParentFilter(ThingFilter parentFilter)
    {
        if (parentFilter == null) return null;
        var allowed = parentFilter.AllowedThingDefs
            .Where(d => d.PlayerAcquirable && d.virtualDefParent == null)
            .ToList();
        // Non-empty restricted set → use it; empty (all allowed) → null → fall back
        return allowed.Count > 0 ? allowed : null;
    }
}
