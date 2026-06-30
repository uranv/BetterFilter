using System.Collections.Generic;
using System.Linq;
using BetterFilter.Contents.Utils;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BetterFilter.Contents.Patches;

/// <summary>
/// Replaces vanilla DoThingFilterConfigWindow header:
/// - 3 equal buttons (Clear / Invert / Allow) instead of vanilla 2
/// - Advanced / Copy / Paste buttons in apparel/food policy dialogs
/// - Expand/collapse icons next to search bar
/// - Context-aware HP/Quality slider hiding
/// - Per-state view-height tracking
/// </summary>
[HarmonyPatch(typeof(ThingFilterUI), nameof(ThingFilterUI.DoThingFilterConfigWindow))]
public static class Patch_UIButtonOperations
{
    private const float IconSize = 18f;

    // ── Invert ──────────────────────────────────────────────────────────

    private static void InvertThingDefs(ThingFilter filter, ThingFilter parentFilter,
        IEnumerable<ThingDef> forceHiddenDefs)
    {
        var hiddenSet = forceHiddenDefs as HashSet<ThingDef>
            ?? (forceHiddenDefs != null ? new HashSet<ThingDef>(forceHiddenDefs) : null);

        foreach (ThingDef td in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (!td.PlayerAcquirable) continue;
            if (td.virtualDefParent != null) continue;
            if (hiddenSet != null && hiddenSet.Contains(td)) continue;
            if (parentFilter != null && !parentFilter.Allows(td)) continue;
            filter.SetAllow(td, !filter.Allows(td));
        }
    }

    // ── Per-state view height tracking ──────────────────────────────────

    private static readonly Dictionary<ThingFilterUI.UIState, float> PerStateViewHeight = new();

    // ── Prefix ──────────────────────────────────────────────────────────

    [HarmonyPrefix]
    static bool Prefix(Rect rect, ThingFilterUI.UIState state, ThingFilter filter,
        ThingFilter parentFilter, int openMask, IEnumerable<ThingDef> forceHiddenDefs,
        IEnumerable<SpecialThingFilterDef> forceHiddenFilters,
        ref bool forceHideHitPointsConfig, ref bool forceHideQualityConfig,
        bool showMentalBreakChanceRange, List<ThingDef> suppressSmallVolumeTags, Map map)
    {
        bool isPreview = Dialog.Dialog_AdvancedFilters.IsPreview;

        // Hide vanilla HP/Quality sliders — Patch_InjectFilterSliders will
        // place them inside UR_BetterFilter.  Dummy items are now in all
        // common ThingCategories so UR_BetterFilter is never hidden.
        if (!isPreview && !PolicyFilterClipboard.IsOutfitOrFoodDialog())
        {
            forceHideHitPointsConfig = true;
            forceHideQualityConfig = true;
        }

        Widgets.DrawMenuSection(rect);
        float num = rect.width - 2f;

        if (!isPreview)
        {
            float btnWidth = (num - 12f) / 3f;
            float y = rect.y + 3f;

            // ── Clear / Invert / Allow ──
            Rect clearRect = new Rect(rect.x + 3f, y, btnWidth, 24f);
            if (Widgets.ButtonText(clearRect, "ClearAll".Translate()))
            {
                filter.SetDisallowAll(forceHiddenDefs, forceHiddenFilters);
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
            Rect invertRect = new Rect(clearRect.xMax + 3f, y, btnWidth, 24f);
            if (Widgets.ButtonText(invertRect, "UR.BetterFilter.Invert".Translate()))
            {
                InvertThingDefs(filter, parentFilter, forceHiddenDefs);
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
            Rect allowRect = new Rect(invertRect.xMax + 3f, y, btnWidth, 24f);
            if (Widgets.ButtonText(allowRect, "AllowAll".Translate()))
            {
                filter.SetAllowAll(parentFilter);
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
            rect.yMin = clearRect.yMax;

            // ── Advanced / Copy / Paste (policy dialogs only) ──
            DrawPolicyButtons(ref rect, filter, btnWidth);
        }

        // ── Search bar + expand/collapse ──
        float contentWidth = rect.width - 16f;
        float interGap = 6f;
        float iconGap = 8f;
        float iconTotal = IconSize * 2f + interGap + iconGap + 3f;
        Rect searchRect = new Rect(rect.x + 3f, rect.yMin + 3f,
            contentWidth - 3f - iconTotal, 24f);
        state.quickSearch.OnGUI(searchRect);

        Rect expandRect = new Rect(searchRect.xMax + interGap, searchRect.y + 3f, IconSize, IconSize);
        var displayRoot = parentFilter != null
            ? parentFilter.DisplayRootCategory
            : filter.RootNode;
        if (TexBetterFilter.ExpandIcon != null && Widgets.ButtonImage(expandRect, TexBetterFilter.ExpandIcon))
            TreeExpandCollapse.ToggleOneLevel(displayRoot.catDef, openMask, true);
        TooltipHandler.TipRegion(expandRect, "ExpandOneLevel".Translate());

        Rect compressRect = new Rect(expandRect.xMax + iconGap, searchRect.y + 3f, IconSize, IconSize);
        if (TexBetterFilter.CollapseIcon != null && Widgets.ButtonImage(compressRect, TexBetterFilter.CollapseIcon))
            TreeExpandCollapse.ToggleOneLevel(displayRoot.catDef, openMask, false);
        TooltipHandler.TipRegion(compressRect, "CollapseOneLevel".Translate());

        rect.yMin = searchRect.yMax + 3f;

        // ── Mutex pairs + dummy hiding in policy dialogs ──
        // (Patch_FilterCategoryOrder is skipped here, so UR_BetterFilter stays empty)
        var combinedHiddenFilters = forceHiddenFilters?.ToList()
            ?? new List<SpecialThingFilterDef>();
        var combinedHiddenDefs = forceHiddenDefs?.ToList() ?? new List<ThingDef>();
        if (!isPreview && PolicyFilterClipboard.IsOutfitOrFoodDialog())
        {
            // Hide dummies → UR_BetterFilter has no visible children → hidden
            combinedHiddenDefs.Add(ModDefOf.UR_DummyFilterItem);
            combinedHiddenDefs.Add(ModDefOf.UR_DummyApplyFilterItem);

            var displayRootCat = (parentFilter != null
                ? parentFilter.DisplayRootCategory.catDef
                : filter.RootNode.catDef);
            var mutexHidden = Patch_FilterCategoryOrder.GetMutexHiddenDefNames(
                displayRootCat, parentFilter);
            if (mutexHidden.Count > 0)
            {
                combinedHiddenFilters.AddRange(
                    mutexHidden.Select(n =>
                        DefDatabase<SpecialThingFilterDef>.GetNamed(n, errorOnFail: false))
                    .Where(d => d != null));
            }
        }

        // ── Tree ──
        DrawFilterTree(rect, state, filter, parentFilter, openMask, combinedHiddenDefs,
            combinedHiddenFilters, forceHideHitPointsConfig, forceHideQualityConfig,
            showMentalBreakChanceRange, suppressSmallVolumeTags, map);
        return false;
    }

    // ── Policy buttons ──────────────────────────────────────────────────

    private static void DrawPolicyButtons(ref Rect rect, ThingFilter filter, float btnWidth)
    {
        if (!PolicyFilterClipboard.IsOutfitOrFoodDialog()) return;

        bool isApparel = PolicyFilterClipboard.IsApparelDialog();
        float y = rect.yMin + 4f;

        Rect advRect = new Rect(rect.x + 3f, y, btnWidth, 24f);
        if (Widgets.ButtonText(advRect, "UR.BetterFilter.AdvancedFilter".Translate()))
        {
            var ctxDefs = PolicyFilterClipboard.GetContextDefs();
            Find.WindowStack.Add(new Dialog.Dialog_AdvancedFilters(filter, ctxDefs));
        }

        Rect copyRect = new Rect(advRect.xMax + 3f, y, btnWidth, 24f);
        if (Widgets.ButtonText(copyRect, "UR.BetterFilter.Copy".Translate()))
        {
            PolicyFilterClipboard.Copy(isApparel, filter);
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            Messages.Message(isApparel
                ? "UR.BetterFilter.CopiedApparel".Translate()
                : "UR.BetterFilter.CopiedFood".Translate(),
                MessageTypeDefOf.NeutralEvent, false);
        }

        Rect pasteRect = new Rect(copyRect.xMax + 3f, y, btnWidth, 24f);
        if (Widgets.ButtonText(pasteRect, "UR.BetterFilter.Paste".Translate()))
        {
            if (PolicyFilterClipboard.HasClipboard(isApparel))
                PolicyFilterClipboard.ShowPasteMenu(filter, isApparel);
            else
                Messages.Message("UR.BetterFilter.PasteEmpty".Translate(),
                    MessageTypeDefOf.RejectInput, false);
        }

        rect.yMin = advRect.yMax;
    }

    // ── Tree rendering ──────────────────────────────────────────────────

    private static void DrawFilterTree(Rect rect, ThingFilterUI.UIState state,
        ThingFilter filter, ThingFilter parentFilter, int openMask,
        IEnumerable<ThingDef> forceHiddenDefs,
        IEnumerable<SpecialThingFilterDef> forceHiddenFilters,
        bool forceHideHitPointsConfig, bool forceHideQualityConfig,
        bool showMentalBreakChanceRange, List<ThingDef> suppressSmallVolumeTags, Map map)
    {
        TreeNode_ThingCategory node = filter.RootNode;
        bool hpFlag = true;
        bool qualityFlag = true;
        if (parentFilter != null)
        {
            node = parentFilter.DisplayRootCategory;
            hpFlag = parentFilter.allowedHitPointsConfigurable;
            qualityFlag = parentFilter.allowedQualitiesConfigurable;
        }
        rect.xMax -= 4f;
        rect.yMax -= 6f;

        if (!PerStateViewHeight.TryGetValue(state, out float vh))
            vh = rect.height;
        Rect viewRect = new Rect(0f, 0f, rect.width - 16f, vh);
        Rect visibleRect = new Rect(0f, 0f, rect.width, rect.height);
        visibleRect.position += state.scrollPosition;
        Widgets.BeginScrollView(rect, ref state.scrollPosition, viewRect);
        float sy = 2f;
        if (hpFlag && !forceHideHitPointsConfig)
            ThingFilterUI_DrawHitPoints(ref sy, viewRect.width, filter);
        if (qualityFlag && !forceHideQualityConfig)
            ThingFilterUI_DrawQuality(ref sy, viewRect.width, filter);
        if (ModsConfig.AnomalyActive && showMentalBreakChanceRange)
            ThingFilterUI_DrawMentalBreak(ref sy, viewRect.width, filter);
        float num2 = sy;
        Rect rect4 = new Rect(0f, sy, viewRect.width, 9999f);
        visibleRect.position -= rect4.position;
        Listing_TreeThingFilter listing = new Listing_TreeThingFilter(filter, parentFilter,
            forceHiddenDefs, forceHiddenFilters, suppressSmallVolumeTags, state.quickSearch.filter);
        listing.Begin(rect4);
        listing.ListCategoryChildren(node, openMask, map, visibleRect);
        listing.End();
        state.quickSearch.noResultsMatched = listing.matchCount == 0;
        if (Event.current.type == EventType.Layout)
        {
            float computed = num2 + listing.CurHeight + 90f;
            PerStateViewHeight[state] = computed;
            ThingFilterUI_SetViewHeight(computed);
        }
        Widgets.EndScrollView();
    }

    // ── Reflection helpers (private ThingFilterUI members) ──────────────

    private static void ThingFilterUI_SetViewHeight(float val) =>
        AccessTools.Field(typeof(ThingFilterUI), "viewHeight").SetValue(null, val);

    private delegate void DrawConfigDelegate(ref float y, float width, ThingFilter filter);

    private static readonly DrawConfigDelegate ThingFilterUI_DrawHitPoints =
        AccessTools.MethodDelegate<DrawConfigDelegate>(
            AccessTools.Method(typeof(ThingFilterUI), "DrawHitPointsFilterConfig"));
    private static readonly DrawConfigDelegate ThingFilterUI_DrawQuality =
        AccessTools.MethodDelegate<DrawConfigDelegate>(
            AccessTools.Method(typeof(ThingFilterUI), "DrawQualityFilterConfig"));
    private static readonly DrawConfigDelegate ThingFilterUI_DrawMentalBreak =
        AccessTools.MethodDelegate<DrawConfigDelegate>(
            AccessTools.Method(typeof(ThingFilterUI), "DrawMentalBreakFilterConfig"));
}
