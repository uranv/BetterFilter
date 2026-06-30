using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Patches;

/// <summary>
/// Hides the multi-checkbox for filter-only categories (no ThingDefs, no child
/// categories, only SpecialThingFilterDefs). Replicates vanilla DoCategory rendering
/// via reflection, but omits the checkbox.
/// </summary>
[HarmonyPatch(typeof(Listing_TreeThingFilter), "DoCategory")]
public static class Patch_HideFilterCheckbox
{
    private static readonly MethodInfo OpenCloseWidgetMethod =
        AccessTools.Method(typeof(Listing_Tree), "OpenCloseWidget");
    private static readonly MethodInfo LabelLeftMethod =
        AccessTools.Method(typeof(Listing_Tree), "LabelLeft");
    private static readonly MethodInfo EndLineMethod =
        AccessTools.Method(typeof(Listing_Lines), "EndLine");
    private static readonly MethodInfo DoCategoryChildrenMethod =
        AccessTools.Method(typeof(Listing_TreeThingFilter), "DoCategoryChildren");
    private static readonly MethodInfo CurrentRowVisibleOnScreenMethod =
        AccessTools.Method(typeof(Listing_TreeThingFilter), "CurrentRowVisibleOnScreen");
    private static readonly FieldInfo SearchFilterField =
        AccessTools.Field(typeof(Listing_TreeThingFilter), "searchFilter");
    private static readonly FieldInfo MatchCountField =
        AccessTools.Field(typeof(Listing_TreeThingFilter), "matchCount");

    private static readonly Color NoMatchColor = Color.grey;

    static bool Prefix(
        Listing_TreeThingFilter __instance,
        TreeNode_ThingCategory node,
        int indentLevel,
        int openMask,
        Map map,
        ref bool subtreeMatchedSearch)
    {
        // Only intercept filter-only categories
        bool isFilterCat = node.catDef.defName == "UR_BetterFilter";
        if (!isFilterCat && (node.catDef.childCategories.Count > 0
            || node.catDef.childSpecialFilters.Count == 0
            || HasNonDummyThingDefs(node.catDef)))
        {
            return true; // normal category — use vanilla rendering
        }

        // --- Replicate DoCategory rendering, minus the multi-checkbox ---
        var searchFilter = (QuickSearchFilter)SearchFilterField.GetValue(__instance);
        Color? textColor = null;

        if (searchFilter != null && searchFilter.Active)
        {
            if (searchFilter.Matches(node.catDef.label))
            {
                subtreeMatchedSearch = true;
                MatchCountField.SetValue(__instance, (int)MatchCountField.GetValue(__instance) + 1);
            }
            else
            {
                textColor = NoMatchColor;
            }
        }

        bool visibleOnScreen = (bool)CurrentRowVisibleOnScreenMethod.Invoke(__instance, null);
        if (visibleOnScreen)
        {
            OpenCloseWidgetMethod.Invoke(__instance, new object[] { node, indentLevel, openMask });
            LabelLeftMethod.Invoke(__instance,
                new object[] { node.LabelCap, node.catDef.description, indentLevel, 0f, textColor, 0f });
            // Intentionally no checkbox rendered
        }
        EndLineMethod.Invoke(__instance, null);

        if (__instance.IsOpen(node, openMask))
        {
            DoCategoryChildrenMethod.Invoke(__instance,
                new object[] { node, indentLevel + 1, openMask, map, false });
        }

        return false; // skip original rendering
    }

    /// <summary>
    /// Returns true if the category contains any ThingDef that is not one of our dummy items.
    /// </summary>
    private static bool HasNonDummyThingDefs(ThingCategoryDef catDef)
    {
        for (int i = 0; i < catDef.childThingDefs.Count; i++)
        {
            var name = catDef.childThingDefs[i].defName;
            if (name != "UR_DummyFilterItem" && name != "UR_DummyApplyFilterItem")
                return true;
        }
        return false;
    }
}
