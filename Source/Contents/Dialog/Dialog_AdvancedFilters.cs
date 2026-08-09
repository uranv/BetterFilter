using System;
using System.Collections.Generic;
using System.Linq;
using BetterFilter.Contents.Filters;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BetterFilter.Contents.Dialog;

public class Dialog_AdvancedFilters : Window
{
    private readonly ThingFilter _sourceFilter;
    private readonly ThingFilter _workingFilter;
    private readonly ThingFilterUI.UIState _previewState = new();
    internal readonly List<FilterComponent> Components = new();

    /// <summary>Items relevant to this dialog context (null = all ever-storable).</summary>
    private readonly List<ThingDef> _contextDefs;

    private bool _dirty = true;
    internal void MarkDirty() => _dirty = true;
    private Vector2 _leftScroll = Vector2.zero;
    private Vector2 _overrideScroll = Vector2.zero;


    /// <summary>
    /// All ThingDefs that can ever appear in a real storage filter.
    /// Derived from vanilla's CreateOnlyEverStorableThingFilter — includes
    /// Items + minimizable Buildings, excludes pawns, plants, stumps, and
    /// special constructions like Character Editor tombs.
    /// </summary>
    private static readonly List<ThingDef> EverStorableDefs = new();

    /// <summary>Set while drawing the right-side preview, so patches can skip UI elements.</summary>
    internal static bool IsPreview;

    /// <summary>
    /// The parent filter used for the preview tree, so only ever-storable
    /// items appear (same behavior as a real storage zone).
    /// </summary>
    private static readonly ThingFilter EverStorableParent;

    /// <summary>
    /// Per-item overrides: true = force-allow, false = force-disallow.
    /// </summary>
    internal readonly Dictionary<ThingDef, bool> ManualOverrides = new();

    /// <summary>
    /// Snapshot of what the filter components alone produce (before manual overrides).
    /// </summary>
    private readonly HashSet<ThingDef> _componentAllowedDefs = new();

    static Dialog_AdvancedFilters()
    {
        // Use the same ever-storable filter as vanilla storage zones.
        EverStorableParent = ThingFilter.CreateOnlyEverStorableThingFilter();
        foreach (var def in EverStorableParent.AllowedThingDefs)
        {
            if (!def.PlayerAcquirable) continue;
            if (def.virtualDefParent != null) continue;
            EverStorableDefs.Add(def);
        }
    }

    public Dialog_AdvancedFilters(ThingFilter source, List<ThingDef> contextDefs = null)
    {
        forcePause = true;
        draggable = false;
        doCloseX = true;
        closeOnClickedOutside = false;
        closeOnAccept = false;
        absorbInputAroundWindow = true;
        layer = WindowLayer.Dialog;
        _sourceFilter = source;
        _contextDefs = contextDefs;
        _workingFilter = new ThingFilter();
        _workingFilter.SetDisallowAll();
        foreach (var def in (_contextDefs ?? EverStorableDefs))
            _workingFilter.SetAllow(def, true);

        // Sync HP / quality ranges from the real filter so the preview is faithful.
        _workingFilter.AllowedHitPointsPercents = source.AllowedHitPointsPercents;
        _workingFilter.AllowedQualityLevels = source.AllowedQualityLevels;

        // Sync non-whitelisted filter-worker states (the ones the dialog
        // does NOT manage — e.g. AllowDeadManApparel).  Whitelisted workers
        // are left at their default (allowed) because filter components are
        // empty at construction.
        var whitelistSet = new HashSet<string>(
            BetterFilterMod.Settings?.AllowedFilterDefNames ?? new List<string>());
        foreach (var sfDef in DefDatabase<SpecialThingFilterDef>.AllDefsListForReading)
        {
            if (!sfDef.configurable) continue;
            if (whitelistSet.Contains(sfDef.defName)) continue;
            _workingFilter.SetAllow(sfDef, source.Allows(sfDef));
        }
    }

    public override Vector2 InitialSize => new Vector2(960f, 640f);

    public override void DoWindowContents(Rect inRect)
    {
        if (_dirty)
        {
            RebuildWorkingFilter();
            _dirty = false;
        }

        var topBarH = 32f;
        var topRect = inRect.TopPartPixels(topBarH);
        DrawTopBar(topRect);
        inRect.yMin += topBarH + 6f;

        var leftRect = inRect.LeftPart(0.55f).ContractedBy(4f);
        DrawLeftPanel(leftRect);

        var leftW = inRect.width * 0.55f;
        inRect.xMin += leftW + 8f;

        var rightRect = inRect.ContractedBy(4f);
        DrawRightPanel(rightRect);

    }

    // ── Top bar ────────────────────────────────────────────────────────

    private void DrawTopBar(Rect rect)
    {
        var btnW = (rect.width - 18f) / 4f;
        var x = rect.x;

        if (Widgets.ButtonText(new Rect(x, rect.y, btnW, rect.height), "UR.BetterFilter.New".Translate()))
        {
            var opts = new List<FloatMenuOption>
            {
                new FloatMenuOption("UR.BetterFilter.FilterWorker".Translate(), () =>
                    { Components.Add(new FilterComponent_Worker()); _dirty = true; }),
                new FloatMenuOption("UR.BetterFilter.ModFilter".Translate(), () =>
                    { Components.Add(new FilterComponent_Mod()); _dirty = true; }),
                new FloatMenuOption("UR.BetterFilter.NumericFilter".Translate(), () =>
                    { Components.Add(new FilterComponent_Numeric()); _dirty = true; }),
                new FloatMenuOption("UR.BetterFilter.CategoryFilter".Translate(), () =>
                    { Components.Add(new FilterComponent_Category()); _dirty = true; }),
                new FloatMenuOption("UR.BetterFilter.EnumFilter".Translate(), () =>
                    { Components.Add(new FilterComponent_Enum()); _dirty = true; }),
                new FloatMenuOption("UR.BetterFilter.CommandInput".Translate(), () =>
                    Find.WindowStack.Add(new Dialog_CommandInput(this)))
            };
            Find.WindowStack.Add(new FloatMenu(opts));
        }
        x += btnW + 6f;
        if (Widgets.ButtonText(new Rect(x, rect.y, btnW, rect.height), "UR.BetterFilter.Save".Translate()))
        {
            Find.WindowStack.Add(new Dialog_SaveFilter(this, Components));
        }
        x += btnW + 6f;
        if (Widgets.ButtonText(new Rect(x, rect.y, btnW, rect.height), "UR.BetterFilter.Load".Translate()))
        {
            Find.WindowStack.Add(new Dialog_LoadFilter(this));
        }
        x += btnW + 6f;

        if (Widgets.ButtonText(new Rect(x, rect.y, btnW, rect.height), "UR.BetterFilter.Apply".Translate()))
        {
            // Synced on open — user modifications to non-whitelisted filter
            // workers are intentional.  Full copy is correct.
            _sourceFilter.CopyAllowancesFrom(_workingFilter);
            var cb = AccessTools.FieldRefAccess<ThingFilter, Action>(
                "settingsChangedCallback")(_sourceFilter);
            cb?.Invoke();
            Messages.Message("UR.BetterFilter.ApplyMessage".Translate(),
                MessageTypeDefOf.NeutralEvent, false);
        }
    }

    // ── Filter logic ───────────────────────────────────────────────────

    /// <summary>
    /// Compute what the filter components alone produce (before manual overrides).
    /// All filters are combined with AND: for any item, result = x_1 AND x_2 AND ...
    /// </summary>
    private bool ComputeComponentResult(ThingDef def)
    {
        foreach (var c in Components)
        {
            if (c is FilterComponent_Item) continue; // items are manual overrides
            if (!c.AllowsDef(def))
                return false;
        }
        return true; // passed all filters (or no active filters)
    }

    private void RebuildWorkingFilter()
    {
        // 0. Rebuild manualOverrides entirely from FilterComponent_Item instances.
        ManualOverrides.Clear();
        foreach (var c in Components)
        {
            if (c is FilterComponent_Item { SelectedDef: not null } it)
                ManualOverrides[it.SelectedDef] = it.IsWhitelist;
        }

        // 1. Compute what the filter components alone produce.
        var relevantDefs = _contextDefs ?? EverStorableDefs;
        _componentAllowedDefs.Clear();
        foreach (var def in relevantDefs)
        {
            if (ComputeComponentResult(def))
                _componentAllowedDefs.Add(def);
        }

        // 2. Apply manual overrides on top: they take the highest priority.
        foreach (var def in relevantDefs)
        {
            var allow = ManualOverrides.TryGetValue(def, out var manual) ? manual : // manual override wins
                _componentAllowedDefs.Contains(def); // component result

            _workingFilter.SetAllow(def, allow);
        }
    }

    // ── Left panel ─────────────────────────────────────────────────────

    private void DrawLeftPanel(Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        var inner = rect.ContractedBy(6f);

        // Reserve space for the per-item override panel at the bottom.
        var overrideH = 120f;
        var overrideRect = inner.BottomPartPixels(overrideH);
        inner.yMax -= overrideH + 6f;

        // Separate item components (rendered in override panel, not here).
        var itemComps = Components.OfType<FilterComponent_Item>().ToList();
        var regularComps = Components.Where(c => c is not FilterComponent_Item).ToList();

        // Filter components area.
        var blockW = inner.width - 16f;
        foreach (var t in regularComps)
            t.LayoutWidth = blockW;

        var totalH = 0f;
        foreach (var t in regularComps)
            totalH += t.BlockHeight + 4f;

        if (totalH < inner.height) totalH = inner.height;

        var viewRect = new Rect(0f, 0f, blockW, totalH);
        Widgets.BeginScrollView(inner, ref _leftScroll, viewRect);
        var cy = 0f;
        for (var i = 0; i < regularComps.Count; i++)
        {
            var blockRect = new Rect(0f, cy, blockW, regularComps[i].BlockHeight);
            regularComps[i].Draw(blockRect, this, i);
            cy += regularComps[i].BlockHeight + 4f;
        }
        // Deferred deletion (all filter types except item, handled in override panel).
        var removed = Components.RemoveAll(c =>
            c is not FilterComponent_Item && (
            c is FilterComponent_Worker { PendingDelete: true } ||
            c is FilterComponent_Mod { PendingDelete: true } ||
            c is FilterComponent_Numeric { PendingDelete: true } ||
            c is FilterComponent_Category { PendingDelete: true } ||
            c is FilterComponent_Enum { PendingDelete: true }));
        if (removed > 0) _dirty = true;
        Widgets.EndScrollView();

        DrawPerItemOverride(overrideRect, itemComps);
    }

    // ── Per-item override panel ────────────────────────────────────────

    private void DrawPerItemOverride(Rect rect, List<FilterComponent_Item> itemComps)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.15f));
        var inner = rect.ContractedBy(4f);

        // Split: valid-def components vs error-only components
        var validComps = itemComps.Where(it => it.SelectedDef != null).ToList();
        var errorComps = itemComps.Where(it => it.ErrorDefName != null).ToList();

        var totalRows = validComps.Count + errorComps.Count;
        Widgets.Label(inner.TopPartPixels(20f), "UR.BetterFilter.ManualOverrides".Translate(validComps.Count));
        inner.yMin += 22f;

        // "Add" button → creates FilterComponent_Item
        var addRect = new Rect(inner.xMax - 60f, inner.y - 20f, 60f, 20f);
        if (Widgets.ButtonText(addRect, "UR.BetterFilter.Add".Translate()))
        {
            var addDefs = _contextDefs ?? EverStorableDefs;
            var items = addDefs
                .Where(d => !validComps.Any(it => it.SelectedDef == d))
                .OrderBy(d => d.LabelCap.ToString())
                .Select(d => ((string) d.LabelCap, (Action)(() =>
                {
                    var componentAllows = _componentAllowedDefs.Contains(d);
                    Components.Add(new FilterComponent_Item
                    {
                        SelectedDef = d,
                        IsWhitelist = !componentAllows
                    });
                    _dirty = true;
                }))).ToList();
            Find.WindowStack.Add(new Dialog_SearchableSelect(
                "UR.BetterFilter.ItemFilter".Translate(), items));
        }

        // Scrollable override list
        var rowH = 24f;
        var listH = Mathf.Max(totalRows * (rowH + 2f), inner.height);
        var listView = new Rect(0f, 0f, inner.width - 16f, listH);
        Widgets.BeginScrollView(inner, ref _overrideScroll, listView);
        var ry = 0f;

        // 1. Valid-def components: checkbox + label + delete
        foreach (var it in validComps)
        {
            var def = it.SelectedDef;
            var curAllow = it.IsWhitelist;
            var isActive = curAllow != _componentAllowedDefs.Contains(def);

            var rowRect = new Rect(0f, ry, listView.width, rowH);

            var checkRect = new Rect(rowRect.x, rowRect.y, 24f, 24f);
            var tex = curAllow ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex;
            if (Widgets.ButtonImage(checkRect, tex))
            {
                it.IsWhitelist = !curAllow;
                _dirty = true;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }

            var oldColor = GUI.color;
            if (!isActive) GUI.color = Color.gray;
            var labelRect = new Rect(checkRect.xMax + 4f, rowRect.y,
                rowRect.width - 52f, rowH);
            Widgets.Label(labelRect, def.LabelCap);
            GUI.color = oldColor;

            var delRect = new Rect(rowRect.xMax - 20f, rowRect.y, 20f, 20f);
            if (Widgets.ButtonImage(delRect, TexButton.Delete))
            {
                it.PendingDelete = true;
                _dirty = true;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }

            ry += rowH + 2f;
        }

        // 2. Error-only components: gray *name
        if (errorComps.Count > 0)
        {
            var oldColor = GUI.color;
            GUI.color = Color.gray;
            foreach (var it in errorComps)
            {
                var rowRect = new Rect(0f, ry, listView.width, rowH);
                Widgets.Label(new Rect(rowRect.x + 28f, rowRect.y,
                    rowRect.width - 52f, rowH), "*" + it.ErrorDefName);
                ry += rowH + 2f;
            }
            GUI.color = oldColor;
        }

        Widgets.EndScrollView();

        // Deferred deletion of item components
        if (Components.RemoveAll(c => c is FilterComponent_Item { PendingDelete: true }) > 0)
            _dirty = true;

        if (validComps.Count == 0 && errorComps.Count == 0)
        {
            Widgets.Label(inner, "  " + "UR.BetterFilter.NoActiveOverrides".Translate());
        }
    }

    // ── Right panel (preview) ──────────────────────────────────────────

    private void DrawRightPanel(Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        var inner = rect.ContractedBy(4f);

        // Count allowed items in context (exclude our dummy items).
        var relevantDefs = _contextDefs ?? EverStorableDefs;
        var allowedCount = relevantDefs
            .Count(d => _workingFilter.Allows(d));
        Widgets.Label(inner.TopPartPixels(20f), "UR.BetterFilter.PreviewAllowItemCount".Translate(allowedCount));
        inner.yMin += 22f;

        IsPreview = true;
        var hiddenDefs = new List<ThingDef> {
            ModDefOf.UR_DummyFilterItem,
            ModDefOf.UR_DummyApplyFilterItem
        };

        var allowedList = BetterFilterMod.Settings?.EffectiveAllowedDefNames
            ?? BetterFilterSettings.Defaults;
        var allowedSet = new HashSet<string>(allowedList);
        var hiddenFilters = DefDatabase<SpecialThingFilterDef>.AllDefsListForReading
            .Where(sf => sf.configurable && allowedSet.Contains(sf.defName))
            .ToList();

        var treeParent = BuildContextParentFilter();

        ThingFilterUI.DoThingFilterConfigWindow(inner, _previewState, _workingFilter,
            parentFilter: treeParent,
            forceHiddenDefs: hiddenDefs,
            forceHiddenFilters: hiddenFilters,
            forceHideHitPointsConfig: false,
            forceHideQualityConfig: false);
        IsPreview = false;
    }

    /// <summary>
    /// Build a parent filter that restricts the preview tree to context-relevant items.
    /// null → all ever-storable (vanilla behavior).
    /// </summary>
    private ThingFilter BuildContextParentFilter()
    {
        if (_contextDefs == null)
            return EverStorableParent;

        var f = new ThingFilter();
        f.SetDisallowAll();
        foreach (var def in _contextDefs)
            f.SetAllow(def, true);
        return f;
    }
}
