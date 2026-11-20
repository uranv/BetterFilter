using System.Collections.Generic;
using System.Linq;
using BetterFilter.Contents.Dialog;
using BetterFilter.Contents.Utils;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents;

public class FilterTargetDefinition : IExposable
{
    public string Type = "Numeric";
    public string XPath = "";
    public string Name = "";
    public bool Enabled = true;

    public FilterTargetDefinition Clone() => new()
    {
        Type = Type,
        XPath = XPath,
        Name = Name,
        Enabled = Enabled
    };

    public void ExposeData()
    {
        Scribe_Values.Look(ref Type, "type", "Numeric");
        Scribe_Values.Look(ref XPath, "xpath", "");
        Scribe_Values.Look(ref Name, "name", "");
        Scribe_Values.Look(ref Enabled, "enabled", true);
    }
}

public class BetterFilterMod : Mod
{
    private const float RowHeight = 30f;
    private const float BottomBarHeight = 32f;
    private const float Gap = 8f;
    private const float ScrollbarWidth = 16f;

    public static BetterFilterSettings Settings;
    public static ModContentPack ContentPack;

    public BetterFilterMod(ModContentPack content) : base(content)
    {
        ContentPack = content;
        Settings = GetSettings<BetterFilterSettings>();
    }

    public override string SettingsCategory() => "[UR] Better Filter Settings";

    private enum SettingsTab
    {
        Introduction,
        FilterSettings,
        FilterTargets
    }

    private SettingsTab _currentTab = SettingsTab.Introduction;

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var contentRect = inRect;
        contentRect.yMin += 32f;

        var tabs = new List<TabRecord>
        {
            new TabRecord(
                "UR.BetterFilter.TabIntroduction".Translate(),
                delegate { _currentTab = SettingsTab.Introduction; },
                _currentTab == SettingsTab.Introduction
            ),
            new TabRecord(
                "UR.BetterFilter.TabFilterSettings".Translate(),
                delegate { _currentTab = SettingsTab.FilterSettings; },
                _currentTab == SettingsTab.FilterSettings
            ),
            new TabRecord(
                "UR.BetterFilter.TabFilterTargets".Translate(),
                delegate { _currentTab = SettingsTab.FilterTargets; },
                _currentTab == SettingsTab.FilterTargets
            ),
        };
        TabDrawer.DrawTabs(contentRect, tabs);

        Widgets.DrawMenuSection(contentRect);
        var innerRect = contentRect.ContractedBy(15f);

        switch (_currentTab)
        {
            case SettingsTab.Introduction:
                DrawIntroductionTab(innerRect);
                break;
            case SettingsTab.FilterSettings:
                DrawFilterSettingsTab(innerRect);
                break;
            case SettingsTab.FilterTargets:
                DrawFilterTargetsTab(innerRect);
                break;
        }
    }

    // Every tab has the same layout: option or description at the top,
    // a scroll list in the middle, one button in the bottom bar.

    /// <summary>Draws the bottom-bar button and removes the bar from the tab area.</summary>
    private static bool BottomButton(ref Rect rect, string label)
    {
        var clicked = Widgets.ButtonText(
            new Rect(rect.x, rect.yMax - BottomBarHeight, 220f, BottomBarHeight), label);
        rect.yMax -= BottomBarHeight + Gap;
        return clicked;
    }

    /// <summary>Draws the description and removes it from the tab area.</summary>
    private static void Description(ref Rect rect, string text)
    {
        var height = Text.CalcHeight(text, rect.width);
        GUI.color = ColoredText.SubtleGrayColor;
        Widgets.Label(new Rect(rect.x, rect.y, rect.width, height), text);
        GUI.color = Color.white;
        rect.yMin += height + Gap;
    }

    private Vector2 _introScrollPos;
    private Vector2 _filterScrollPos;
    private Vector2 _targetScrollPos;
    private float _filterViewHeight;
    private float _targetViewHeight;
    private List<(string header, List<SpecialThingFilterDef> defs)> _filterGroups;

    private void DrawIntroductionTab(Rect rect)
    {
        if (BottomButton(ref rect, "UR.BetterFilter.ConvertLegacyFilters".Translate()))
        {
            var result = FilterSetStorage.ConvertLegacyTxtFiles();
            Messages.Message(
                "UR.BetterFilter.LegacyConversionMessage".Translate(
                    result.Converted, result.Skipped, result.Failed),
                MessageTypeDefOf.NeutralEvent, false);
        }

        var optionRect = new Rect(rect.x, rect.y, rect.width, RowHeight);
        Widgets.CheckboxLabeled(optionRect, "UR.BetterFilter.NotifyNewFilters".Translate(),
            ref Settings.NotifyNewFilters, placeCheckboxNearText: true);
        TooltipHandler.TipRegion(optionRect, "UR.BetterFilter.NotifyNewFiltersTip".Translate());
        rect.yMin += RowHeight + Gap;

        string text = "UR.BetterFilter.IntroductionContent".Translate();
        var viewRect = new Rect(0f, 0f, rect.width - ScrollbarWidth,
            Text.CalcHeight(text, rect.width - ScrollbarWidth));
        Widgets.BeginScrollView(rect, ref _introScrollPos, viewRect);
        Widgets.Label(viewRect, text);
        Widgets.EndScrollView();
    }

    private static void SplitFilterRow(Rect row, out Rect label, out Rect category,
        out Rect kind, out Rect move, out Rect apply)
    {
        apply = new Rect(row.xMax - 80f, row.y, 80f, row.height);
        move = new Rect(apply.x - 100f, row.y, 100f, row.height);
        kind = new Rect(move.x - 90f, row.y, 90f, row.height);
        category = new Rect(kind.x - 160f, row.y, 154f, row.height);
        label = new Rect(row.x + 6f, row.y, category.x - row.x - 12f, row.height);
    }

    private void DrawFilterSettingsTab(Rect rect)
    {
        if (BottomButton(ref rect, "UR.BetterFilter.ResetDefault".Translate()))
            Settings.ResetFilterConfigs();
        Description(ref rect, "UR.BetterFilter.FilterSettingsDesc".Translate());

        // Official (Core + DLC) first, then this mod, then other mods by name.
        _filterGroups ??= DefDatabase<SpecialThingFilterDef>.AllDefsListForReading
            .GroupBy(FilterWorkerCatalog.SourceLabel)
            .OrderBy(g => g.First().modContentPack is { IsOfficialMod: true } ? 0
                : g.First().modContentPack == ContentPack ? 1 : 2)
            .ThenBy(g => g.Key)
            .Select(g => (g.Key, g.ToList()))
            .ToList();

        var headerRow = new Rect(rect.x, rect.y, rect.width - ScrollbarWidth, RowHeight);
        SplitFilterRow(headerRow, out var labelRect, out var categoryRect,
            out var kindRect, out var moveRect, out var applyRect);
        Widgets.DrawLightHighlight(headerRow);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, "UR.BetterFilter.ColumnFilter".Translate());
        Widgets.Label(categoryRect, "UR.BetterFilter.ColumnCategory".Translate());
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(kindRect, "UR.BetterFilter.ColumnKind".Translate());
        Widgets.Label(moveRect, "UR.BetterFilter.ColumnMove".Translate());
        Widgets.Label(applyRect, "UR.BetterFilter.ColumnApply".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(kindRect, "UR.BetterFilter.ColumnKindTip".Translate());
        TooltipHandler.TipRegion(moveRect, "UR.BetterFilter.ColumnMoveTip".Translate());
        TooltipHandler.TipRegion(applyRect, "UR.BetterFilter.ColumnApplyTip".Translate());
        rect.yMin += RowHeight;

        var viewRect = new Rect(0f, 0f, rect.width - ScrollbarWidth, _filterViewHeight);
        Widgets.BeginScrollView(rect, ref _filterScrollPos, viewRect);
        var y = 0f;
        foreach (var (header, defs) in _filterGroups)
        {
            Widgets.ListSeparator(ref y, viewRect.width, header);
            for (var i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                var row = new Rect(0f, y, viewRect.width, RowHeight);
                y += RowHeight;
                if (row.yMax < _filterScrollPos.y || row.y > _filterScrollPos.y + rect.height) continue;

                SplitFilterRow(row, out labelRect, out categoryRect, out kindRect, out moveRect, out applyRect);
                var builtIn = FilterWorkerCatalog.IsBuiltIn(def);
                var kind = Settings.KindOf(def);
                var matchCount = FilterWorkerCatalog.MatchCount(def);
                // The game draws a filter only when it is configurable and matches a storable item.
                var shown = def.configurable && matchCount > 0;

                if (i % 2 == 1) Widgets.DrawAltRect(row);
                if (Mouse.IsOver(row))
                {
                    Widgets.DrawHighlight(row);
                    var tip = (def.description.NullOrEmpty() ? "" : def.description + "\n\n")
                        + "UR.BetterFilter.FilterTip".Translate(def.defName, def.workerClass.FullName,
                        builtIn
                            ? "UR.BetterFilter.KindFixed".Translate(FilterWorkerCatalog.KindLabel(kind))
                            : "UR.BetterFilter.KindDetected".Translate(FilterWorkerCatalog.KindLabel(kind),
                                FilterWorkerCatalog.KindLabel(FilterWorkerCatalog.DetectKind(def))),
                        matchCount);
                    if (!def.configurable) tip += "\n\n" + "UR.BetterFilter.FilterTipNotConfigurable".Translate();
                    if (matchCount == 0) tip += "\n\n" + "UR.BetterFilter.FilterTipNoMatch".Translate();
                    TooltipHandler.TipRegion(new Rect(row.x, row.y, kindRect.x - row.x, row.height), tip);
                }

                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = shown ? Color.white : ColoredText.SubtleGrayColor;
                Widgets.Label(labelRect, def.LabelCap.ToString().Truncate(labelRect.width));
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(categoryRect,
                    (def.parentCategory?.LabelCap.ToString() ?? "").Truncate(categoryRect.width));
                if (builtIn)
                {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(kindRect, FilterWorkerCatalog.KindLabel(kind));
                }
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (!builtIn && Widgets.ButtonText(kindRect.ContractedBy(2f), FilterWorkerCatalog.KindLabel(kind)))
                    Settings.GetOrCreateConfig(def).Kind = kind == FilterWorkerKind.Static
                        ? FilterWorkerKind.Dynamic
                        : FilterWorkerKind.Static;

                var move = shown && Settings.Moves(def);
                var moveNow = move;
                Widgets.Checkbox(new Vector2(moveRect.center.x - 12f, row.y + 3f), ref moveNow, disabled: !shown);
                if (moveNow != move) Settings.GetOrCreateConfig(def).Move = moveNow;

                var apply = Settings.Applies(def);
                var applyNow = apply;
                Widgets.Checkbox(new Vector2(applyRect.center.x - 12f, row.y + 3f), ref applyNow,
                    disabled: kind == FilterWorkerKind.Dynamic);
                if (applyNow != apply) Settings.GetOrCreateConfig(def).Apply = applyNow;
            }
            y += Gap;
        }
        Widgets.EndScrollView();
        _filterViewHeight = y;
    }

    private const float TargetEditWidth = 70f;

    /// <summary>Draws one match-property row. Returns the mouse button that clicked it, or -1.</summary>
    private static int TargetRow(Rect row, int index, string name, string xpath, bool enabled, string tip)
    {
        var clickRect = new Rect(row.x, row.y, row.width - TargetEditWidth - 4f, row.height);
        var xpathRect = new Rect(row.x + 280f, row.y, clickRect.xMax - row.x - 284f, row.height);
        if (index % 2 == 1) Widgets.DrawAltRect(row);
        Widgets.DrawHighlightIfMouseover(clickRect);
        Widgets.CheckboxDraw(row.x + 4f, row.y + 3f, enabled, false);
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = enabled ? Color.white : ColoredText.SubtleGrayColor;
        Widgets.Label(new Rect(row.x + 34f, row.y, 240f, row.height), name.Truncate(240f));
        GUI.color = ColoredText.SubtleGrayColor;
        Widgets.Label(xpathRect, xpath.Truncate(xpathRect.width));
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(clickRect, xpath + "\n\n" + tip);
        return Widgets.ButtonInvisible(clickRect) ? Event.current.button : -1;
    }

    private static bool IsDuplicateTarget(FilterTargetDefinition candidate, FilterTargetDefinition except)
        => Settings.CustomFilterTargets.Any(existing => existing != except
            && string.Equals(existing.Type, candidate.Type, System.StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.XPath, candidate.XPath, System.StringComparison.Ordinal));

    private void DrawFilterTargetsTab(Rect rect)
    {
        if (BottomButton(ref rect, "UR.BetterFilter.AddFilterTarget".Translate()))
            Find.WindowStack.Add(new Dialog_EditFilterTarget(
                null,
                target => Settings.CustomFilterTargets.Add(target),
                candidate => IsDuplicateTarget(candidate, null)));
        Description(ref rect, "UR.BetterFilter.FilterTargetsDesc".Translate());

        string builtInTip = "UR.BetterFilter.TargetRowTip".Translate();
        var customTip = builtInTip + "\n" + "UR.BetterFilter.TargetRowTipDelete".Translate();
        FilterTargetDefinition removed = null;

        var viewRect = new Rect(0f, 0f, rect.width - ScrollbarWidth, _targetViewHeight);
        Widgets.BeginScrollView(rect, ref _targetScrollPos, viewRect);
        var y = 0f;
        foreach (var kind in new[] { FilterTargetKind.Numeric, FilterTargetKind.Enum })
        {
            Widgets.ListSeparator(ref y, viewRect.width, (kind == FilterTargetKind.Numeric
                ? "UR.BetterFilter.TargetSectionNumeric"
                : "UR.BetterFilter.TargetSectionEnum").Translate());
            var index = 0;
            foreach (var option in FilterTargetCatalog.BuiltIns.Where(option => option.Kind == kind))
            {
                var disabled = Settings.DisabledBuiltInTargets.Contains(option.XPath);
                if (TargetRow(new Rect(0f, y, viewRect.width, RowHeight), index++,
                        option.Name.Translate(), option.XPath, !disabled, builtInTip) == 0)
                {
                    if (disabled) Settings.DisabledBuiltInTargets.Remove(option.XPath);
                    else Settings.DisabledBuiltInTargets.Add(option.XPath);
                }
                y += RowHeight;
            }

            foreach (var target in Settings.CustomFilterTargets.Where(target =>
                         FilterTargetCatalog.TryParseKind(target.Type, out var parsed) && parsed == kind))
            {
                var row = new Rect(0f, y, viewRect.width, RowHeight);
                var click = TargetRow(row, index++, target.Name, target.XPath, target.Enabled, customTip);
                if (click == 0) target.Enabled = !target.Enabled;
                else if (click == 1 && !target.Enabled) removed = target;

                if (Widgets.ButtonText(new Rect(row.xMax - TargetEditWidth, row.y + 2f, TargetEditWidth, row.height - 4f),
                        "UR.BetterFilter.Edit".Translate()))
                    Find.WindowStack.Add(new Dialog_EditFilterTarget(
                        target,
                        updated => Settings.CustomFilterTargets[Settings.CustomFilterTargets.IndexOf(target)] = updated,
                        candidate => IsDuplicateTarget(candidate, target)));
                y += RowHeight;
            }
            y += Gap;
        }
        Widgets.EndScrollView();
        _targetViewHeight = y;

        if (removed != null) Settings.CustomFilterTargets.Remove(removed);
    }
}

public class BetterFilterSettings : ModSettings
{
    /// <summary>Stored per-filter options. A filter without an entry uses its defaults.</summary>
    public List<FilterWorkerConfig> FilterConfigs = new();
    public List<FilterTargetDefinition> CustomFilterTargets = new();
    /// <summary>XPaths of the built-in match properties that are hidden from the pickers.</summary>
    public List<string> DisabledBuiltInTargets = new();
    public bool NotifyNewFilters = true;

    private Dictionary<string, FilterWorkerConfig> _configByDefName;

    public FilterWorkerConfig FindConfig(SpecialThingFilterDef def)
        => (_configByDefName ??= FilterConfigs.ToDictionary(config => config.DefName))
            .TryGetValue(def.defName, out var found) ? found : null;

    public FilterWorkerConfig GetOrCreateConfig(SpecialThingFilterDef def)
    {
        var config = FindConfig(def);
        if (config != null) return config;
        config = new FilterWorkerConfig();
        config.Reset(def);
        FilterConfigs.Add(config);
        return _configByDefName[def.defName] = config;
    }

    /// <summary>Built-in filters have a fixed kind. Other filters use the stored kind, then the detected kind.</summary>
    public FilterWorkerKind KindOf(SpecialThingFilterDef def)
        => FilterWorkerCatalog.IsBuiltIn(def)
            ? FilterWorkerCatalog.DefaultKind(def)
            : FindConfig(def)?.Kind ?? FilterWorkerCatalog.DefaultKind(def);

    /// <summary>The filter is drawn in the UR_BetterFilter category.</summary>
    public bool Moves(SpecialThingFilterDef def)
        => FindConfig(def)?.Move ?? FilterWorkerCatalog.IsBuiltIn(def);

    /// <summary>The "Apply" button writes the filter into per-item permissions.</summary>
    public bool Applies(SpecialThingFilterDef def)
        => KindOf(def) == FilterWorkerKind.Static
           && (FindConfig(def)?.Apply ?? FilterWorkerCatalog.IsBuiltIn(def));

    public void ResetFilterConfigs()
    {
        foreach (var config in FilterConfigs)
        {
            if (DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail(config.DefName) is { } def)
                config.Reset(def);
        }
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref FilterConfigs, "filterConfigs", LookMode.Deep);
        Scribe_Collections.Look(ref CustomFilterTargets, "customFilterTargets", LookMode.Deep);
        Scribe_Collections.Look(ref DisabledBuiltInTargets, "disabledBuiltInTargets", LookMode.Value);
        Scribe_Values.Look(ref NotifyNewFilters, "notifyNewFilters", true);
        FilterConfigs ??= new List<FilterWorkerConfig>();
        CustomFilterTargets ??= new List<FilterTargetDefinition>();
        DisabledBuiltInTargets ??= new List<string>();
    }
}
