using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Patches;

/// <summary>
/// Injects the HP and Quality sliders at the top of the expanded UR_BetterFilter
/// category.  Visibility mirrors vanilla: parentFilter.allowedHitPointsConfigurable
/// and parentFilter.allowedQualitiesConfigurable (default true when no parentFilter).
///
/// Note: the vanilla slider hiding is handled by Patch_UIButtonOperations, which only
/// hides them in storage-zone contexts (not apparel/food policy dialogs).
/// </summary>
[HarmonyPatch(typeof(Listing_TreeThingFilter), "DoCategoryChildren")]
public static class Patch_InjectFilterSliders
{
    private const string FilterDefName = "UR_BetterFilter";

    private static readonly FieldInfo FilterField =
        AccessTools.Field(typeof(Listing_TreeThingFilter), "filter");
    private static readonly FieldInfo CurYField =
        AccessTools.Field(typeof(Listing), "curY");
    private static readonly FieldInfo VisibleRectField =
        AccessTools.Field(typeof(Listing_TreeThingFilter), "visibleRect");
    private static readonly MethodInfo XAtIndentLevelMethod =
        AccessTools.Method(typeof(Listing_Tree), "XAtIndentLevel");

    /// <summary>Render sliders BEFORE the original method so they appear at the top.</summary>
    static void Prefix(Listing_TreeThingFilter __instance, TreeNode_ThingCategory node, int indentLevel)
    {
        if (node.catDef.defName != FilterDefName) return;

        var filter = FilterField.GetValue(__instance) as ThingFilter;
        if (filter == null) return;

        // —— Visibility: mirror vanilla ThingFilterUI checks ——
        var parentFilter = Patch_FilterCategoryOrder.GetParentFilter(__instance);
        bool hpFlag = parentFilter == null || parentFilter.allowedHitPointsConfigurable;
        bool qFlag = parentFilter == null || parentFilter.allowedQualitiesConfigurable;

        if (!hpFlag && !qFlag) return;

        var visibleRect = (Rect)VisibleRectField.GetValue(__instance);
        float curY = (float)CurYField.GetValue(__instance);
        float columnWidth = __instance.ColumnWidth;
        float indentX = (float)XAtIndentLevelMethod.Invoke(__instance, new object[] { indentLevel + 1 });
        float sliderWidth = columnWidth - 2f * indentX;

        // --- Hit Points slider ---
        if (hpFlag)
        {
            Rect hpRow = new Rect(0f, curY, columnWidth, 37f);
            if (visibleRect.Overlaps(hpRow))
            {
                Rect hpRect = new Rect(indentX, curY, sliderWidth, 32f);
                FloatRange hpRange = filter.AllowedHitPointsPercents;
                Widgets.FloatRange(hpRect, 1, ref hpRange, 0f, 1f,
                    "HitPoints", ToStringStyle.PercentZero, 0f, GameFont.Small, null, 0.01f);
                filter.AllowedHitPointsPercents = hpRange;
            }
            curY += 37f;
        }

        // --- Quality slider ---
        if (qFlag)
        {
            Rect qRow = new Rect(0f, curY, columnWidth, 37f);
            if (visibleRect.Overlaps(qRow))
            {
                Rect qRect = new Rect(indentX, curY, sliderWidth, 32f);
                QualityRange qRange = filter.AllowedQualityLevels;
                Widgets.QualityRange(qRect, 876813230, ref qRange);
                filter.AllowedQualityLevels = qRange;
            }
            curY += 37f;
        }

        // Small gap before the filter toggles
        curY += 4f;

        CurYField.SetValue(__instance, curY);
    }
}
