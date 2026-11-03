using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BetterFilter.Contents.Utils;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BetterFilter.Contents.Patches;

/// <summary>
/// Changes vanilla DoThingFilterConfigWindow without replacing it, so other
/// mods' transpilers on the same method still run (e.g. Stack Gap's slider):
/// - Transpiler swaps the vanilla header (Clear / Allow + search bar) for
///   3 equal buttons (Clear / Invert / Allow), Advanced / Copy / Paste in
///   apparel/food policy dialogs, and expand/collapse icons next to search
/// - Prefix hides HP/Quality sliders in storage contexts and dummy items +
///   mutex pairs in policy dialogs
/// - Prefix/Postfix keep the scroll view height per UIState
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

        foreach (var td in DefDatabase<ThingDef>.AllDefsListForReading)
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

    private static readonly AccessTools.FieldRef<float> ViewHeight =
        AccessTools.StaticFieldRefAccess<float>(AccessTools.Field(typeof(ThingFilterUI), "viewHeight"));

    // ── Prefix ──────────────────────────────────────────────────────────

    [HarmonyPrefix]
    static void Prefix(ThingFilterUI.UIState state, ThingFilter filter, ThingFilter parentFilter,
        ref IEnumerable<ThingDef> forceHiddenDefs, ref IEnumerable<SpecialThingFilterDef> forceHiddenFilters,
        ref bool forceHideHitPointsConfig, ref bool forceHideQualityConfig)
    {
        if (PerStateViewHeight.TryGetValue(state, out var vh))
            ViewHeight() = vh;

        if (Dialog.Dialog_AdvancedFilters.IsPreview) return;

        // Hide vanilla HP/Quality sliders — Patch_InjectFilterSliders will
        // place them inside UR_BetterFilter.
        if (!PolicyFilterClipboard.IsOutfitOrFoodDialog())
        {
            forceHideHitPointsConfig = true;
            forceHideQualityConfig = true;
            return;
        }

        // ── Mutex pairs + dummy hiding in policy dialogs ──
        // (Patch_FilterCategoryOrder is skipped here, so UR_BetterFilter stays empty)
        // Hide dummies → UR_BetterFilter has no visible children → hidden
        var combinedHiddenDefs = forceHiddenDefs?.ToList() ?? new List<ThingDef>();
        combinedHiddenDefs.Add(ModDefOf.UR_DummyFilterItem);
        combinedHiddenDefs.Add(ModDefOf.UR_DummyApplyFilterItem);
        forceHiddenDefs = combinedHiddenDefs;

        var displayRootCat = parentFilter != null
            ? parentFilter.DisplayRootCategory.catDef
            : filter.RootNode.catDef;
        var mutexHidden = Patch_FilterCategoryOrder.GetMutexHiddenDefNames(displayRootCat, parentFilter);
        if (mutexHidden.Count == 0) return;
        var combinedHiddenFilters = forceHiddenFilters?.ToList() ?? new List<SpecialThingFilterDef>();
        combinedHiddenFilters.AddRange(mutexHidden
            .Select(n => DefDatabase<SpecialThingFilterDef>.GetNamed(n, errorOnFail: false))
            .Where(d => d != null));
        forceHiddenFilters = combinedHiddenFilters;
    }

    [HarmonyPostfix]
    static void Postfix(ThingFilterUI.UIState state)
    {
        if (Event.current.type == EventType.Layout)
            PerStateViewHeight[state] = ViewHeight();
    }

    // ── Transpiler: vanilla header → DrawHeader ─────────────────────────

    /// <summary>
    /// Replaces the IL from method start to <c>rect.yMin = searchRect.yMax + 3f</c>
    /// (the set_yMin call right after QuickSearchWidget.OnGUI) with one
    /// DrawHeader call. Everything after the header stays vanilla.
    /// </summary>
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var onGui = AccessTools.Method(typeof(QuickSearchWidget), nameof(QuickSearchWidget.OnGUI));
        var setYMin = AccessTools.PropertySetter(typeof(Rect), nameof(Rect.yMin));
        var onGuiIndex = codes.FindIndex(c => c.Calls(onGui));
        var headerEnd = onGuiIndex < 0 ? -1 : codes.FindIndex(onGuiIndex, c => c.Calls(setYMin));
        if (headerEnd < 0)
        {
            Log.Warning("[BetterFilter] ThingFilterUI header not found, vanilla header kept.");
            return codes;
        }

        codes.RemoveRange(0, headerEnd + 1);
        codes.InsertRange(0, new[]
        {
            new CodeInstruction(OpCodes.Ldarga_S, (byte)0),
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Ldarg_2),
            new CodeInstruction(OpCodes.Ldarg_3),
            new CodeInstruction(OpCodes.Ldarg_S, (byte)4),
            new CodeInstruction(OpCodes.Ldarg_S, (byte)5),
            new CodeInstruction(OpCodes.Ldarg_S, (byte)6),
            CodeInstruction.Call(typeof(Patch_UIButtonOperations), nameof(DrawHeader)),
        });
        return codes;
    }

    private static void DrawHeader(ref Rect rect, ThingFilterUI.UIState state, ThingFilter filter,
        ThingFilter parentFilter, int openMask, IEnumerable<ThingDef> forceHiddenDefs,
        IEnumerable<SpecialThingFilterDef> forceHiddenFilters)
    {
        Widgets.DrawMenuSection(rect);
        var num = rect.width - 2f;

        if (!Dialog.Dialog_AdvancedFilters.IsPreview)
        {
            var btnWidth = (num - 12f) / 3f;
            var y = rect.y + 3f;

            // ── Clear / Invert / Allow ──
            var clearRect = new Rect(rect.x + 3f, y, btnWidth, 24f);
            if (Widgets.ButtonText(clearRect, "ClearAll".Translate()))
            {
                filter.SetDisallowAll(forceHiddenDefs, forceHiddenFilters);
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
            var invertRect = new Rect(clearRect.xMax + 3f, y, btnWidth, 24f);
            if (Widgets.ButtonText(invertRect, "UR.BetterFilter.Invert".Translate()))
            {
                InvertThingDefs(filter, parentFilter, forceHiddenDefs);
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
            var allowRect = new Rect(invertRect.xMax + 3f, y, btnWidth, 24f);
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
        var contentWidth = rect.width - 16f;
        var interGap = 6f;
        var iconGap = 8f;
        var iconTotal = IconSize * 2f + interGap + iconGap + 3f;
        var searchRect = new Rect(rect.x + 3f, rect.yMin + 3f,
            contentWidth - 3f - iconTotal, 24f);
        state.quickSearch.OnGUI(searchRect);

        var expandRect = new Rect(searchRect.xMax + interGap, searchRect.y + 3f, IconSize, IconSize);
        var displayRoot = parentFilter != null
            ? parentFilter.DisplayRootCategory
            : filter.RootNode;
        if (TexBetterFilter.ExpandIcon != null && Widgets.ButtonImage(expandRect, TexBetterFilter.ExpandIcon))
            TreeExpandCollapse.ToggleOneLevel(displayRoot.catDef, openMask, true);
        TooltipHandler.TipRegion(expandRect, "ExpandOneLevel".Translate());

        var compressRect = new Rect(expandRect.xMax + iconGap, searchRect.y + 3f, IconSize, IconSize);
        if (TexBetterFilter.CollapseIcon != null && Widgets.ButtonImage(compressRect, TexBetterFilter.CollapseIcon))
            TreeExpandCollapse.ToggleOneLevel(displayRoot.catDef, openMask, false);
        TooltipHandler.TipRegion(compressRect, "CollapseOneLevel".Translate());

        rect.yMin = searchRect.yMax + 3f;
    }

    // ── Policy buttons ──────────────────────────────────────────────────

    private static void DrawPolicyButtons(ref Rect rect, ThingFilter filter, float btnWidth)
    {
        if (!PolicyFilterClipboard.IsOutfitOrFoodDialog()) return;

        var isApparel = PolicyFilterClipboard.IsApparelDialog();
        var y = rect.yMin + 4f;

        var advRect = new Rect(rect.x + 3f, y, btnWidth, 24f);
        if (Widgets.ButtonText(advRect, "UR.BetterFilter.AdvancedFilter".Translate()))
        {
            var ctxDefs = PolicyFilterClipboard.GetContextDefs();
            Find.WindowStack.Add(new Dialog.Dialog_AdvancedFilters(filter, ctxDefs));
        }

        var copyRect = new Rect(advRect.xMax + 3f, y, btnWidth, 24f);
        if (Widgets.ButtonText(copyRect, "UR.BetterFilter.Copy".Translate()))
        {
            PolicyFilterClipboard.Copy(isApparel, filter);
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            Messages.Message(isApparel
                ? "UR.BetterFilter.CopiedApparel".Translate()
                : "UR.BetterFilter.CopiedFood".Translate(),
                MessageTypeDefOf.NeutralEvent, false);
        }

        var pasteRect = new Rect(copyRect.xMax + 3f, y, btnWidth, 24f);
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
}
