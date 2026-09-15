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

    public FilterTargetDefinition Clone() => new()
    {
        Type = Type,
        XPath = XPath,
        Name = Name
    };

    public void ExposeData()
    {
        Scribe_Values.Look(ref Type, "type", "Numeric");
        Scribe_Values.Look(ref XPath, "xpath", "");
        Scribe_Values.Look(ref Name, "name", "");
    }
}

public class BetterFilterMod : Mod
{
    public static BetterFilterSettings Settings;

    public BetterFilterMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<BetterFilterSettings>();
    }

    public override string SettingsCategory() => "[UR] Better Filter Settings";

    private enum SettingsTab
    {
        Introduction,
        AppliableFilterWorker,
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
                "UR.BetterFilter.TabAppliable".Translate(),
                delegate { _currentTab = SettingsTab.AppliableFilterWorker; },
                _currentTab == SettingsTab.AppliableFilterWorker
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
            case SettingsTab.AppliableFilterWorker:
                DrawAppliableTab(innerRect);
                break;
            case SettingsTab.FilterTargets:
                DrawFilterTargetsTab(innerRect);
                break;
        }
    }

    private Vector2 _scrollPos;
    private Vector2 _targetScrollPos;
    private Vector2 _introScrollPos;

    private void DrawIntroductionTab(Rect rect)
    {
        string text = "UR.BetterFilter.IntroductionContent".Translate();
        var textH = Text.CalcHeight(text, rect.width - 16f);
        var totalH = Mathf.Max(textH + 20f, rect.height);

        var viewRect = new Rect(0f, 0f, rect.width - 16f, totalH);
        Widgets.BeginScrollView(rect, ref _introScrollPos, viewRect);
        Widgets.Label(new Rect(0f, 0f, viewRect.width, textH), text);
        Widgets.EndScrollView();
    }

    private void DrawFilterTargetsTab(Rect rect)
    {
        Settings.CustomFilterTargets ??= new List<FilterTargetDefinition>();
        Widgets.Label(rect.TopPartPixels(22f), "UR.BetterFilter.FilterTargetsDesc".Translate());
        rect.yMin += 28f;

        var buttonH = 32f;
        var scrollRect = new Rect(rect.x, rect.y, rect.width,
            rect.height - buttonH - 8f);
        var rowH = 58f;
        var viewRect = new Rect(0f, 0f, scrollRect.width - 16f,
            Mathf.Max(scrollRect.height, Settings.CustomFilterTargets.Count * rowH));

        Widgets.BeginScrollView(scrollRect, ref _targetScrollPos, viewRect);
        var y = 0f;
        for (var i = 0; i < Settings.CustomFilterTargets.Count; i++)
        {
            var target = Settings.CustomFilterTargets[i];
            var row = new Rect(0f, y, viewRect.width, rowH - 4f);
            var type = FilterTargetCatalog.TryParseKind(target.Type, out var kind)
                ? kind.ToString()
                : target.Type;
            Widgets.Label(new Rect(row.x, row.y, 90f, 22f), type);
            Widgets.Label(new Rect(row.x + 92f, row.y, row.width - 220f, 22f),
                target.Name);
            Widgets.Label(new Rect(row.x + 92f, row.y + 23f, row.width - 220f, 22f),
                target.XPath);

            var editRect = new Rect(row.xMax - 116f, row.y, 54f, 24f);
            if (Widgets.ButtonText(editRect, "UR.BetterFilter.Edit".Translate()))
            {
                var index = i;
                var original = target;
                Find.WindowStack.Add(new Dialog_EditFilterTarget(
                    original,
                    updated => Settings.CustomFilterTargets[index] = updated,
                    candidate => Settings.CustomFilterTargets
                        .Where((_, n) => n != index)
                        .Any(existing => string.Equals(existing.Type, candidate.Type,
                                System.StringComparison.OrdinalIgnoreCase)
                            && string.Equals(existing.XPath, candidate.XPath,
                                System.StringComparison.Ordinal))));
            }

            var deleteRect = new Rect(row.xMax - 56f, row.y, 54f, 24f);
            if (Widgets.ButtonText(deleteRect, "Delete"))
                Settings.CustomFilterTargets.RemoveAt(i--);
            y += rowH;
        }
        Widgets.EndScrollView();

        var addRect = new Rect(rect.x, scrollRect.yMax + 4f, rect.width, buttonH);
        if (Widgets.ButtonText(addRect, "UR.BetterFilter.AddFilterTarget".Translate()))
        {
            Find.WindowStack.Add(new Dialog_EditFilterTarget(
                null,
                target => Settings.CustomFilterTargets.Add(target),
                candidate => Settings.CustomFilterTargets.Any(existing =>
                    string.Equals(existing.Type, candidate.Type,
                        System.StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.XPath, candidate.XPath,
                        System.StringComparison.Ordinal))));
        }
    }

    private void DrawAppliableTab(Rect rect)
    {
        // Lazy validation — DefDatabase is now guaranteed loaded
        Settings.Validate();

        // Ensure defaults are populated on first run (old saves may have empty list)
        if (Settings.AllowedFilterDefNames.Count == 0)
            Settings.AllowedFilterDefNames = new List<string>(BetterFilterSettings.Defaults);

        Widgets.Label(rect.TopPartPixels(22f), "UR.BetterFilter.TabAppliableDesc".Translate());
        rect.yMin += 28f;

        var allDefs = DefDatabase<SpecialThingFilterDef>.AllDefsListForReading
            .OrderBy(d => d.LabelCap.ToString()).ToList();
        var rowH = 28f;
        var btnH = 32f;
        var buttonGap = 6f;
        var totalH = allDefs.Count * rowH;

        var scrollRect = new Rect(rect.x, rect.y, rect.width,
            rect.height - btnH - 12f);

        var allowedSet = new HashSet<string>(Settings.AllowedFilterDefNames);
        var viewRect = new Rect(0f, 0f, scrollRect.width - 16f, totalH);
        Widgets.BeginScrollView(scrollRect, ref _scrollPos, viewRect);
        var y = 0f;
        foreach (var def in allDefs)
        {
            var rowRect = new Rect(0f, y, viewRect.width, rowH);
            var isAllowed = allowedSet.Contains(def.defName);
            Widgets.CheckboxLabeled(rowRect, (string)def.LabelCap, ref isAllowed);
            if (isAllowed && !allowedSet.Contains(def.defName))
                Settings.AllowedFilterDefNames.Add(def.defName);
            else if (!isAllowed && allowedSet.Contains(def.defName))
                Settings.AllowedFilterDefNames.Remove(def.defName);
            y += rowH;
        }
        Widgets.EndScrollView();

        // Settings actions — reset defaults and migrate legacy TXT filter sets.
        var actionY = scrollRect.yMax + 4f;
        var actionW = (rect.width - buttonGap) / 2f;
        var resetRect = new Rect(rect.x, actionY, actionW, btnH);
        if (Widgets.ButtonText(resetRect, "UR.BetterFilter.ResetDefault".Translate()))
        {
            Settings.AllowedFilterDefNames = new List<string>(BetterFilterSettings.Defaults);
        }

        var migrateRect = new Rect(resetRect.xMax + buttonGap, actionY, actionW, btnH);
        if (Widgets.ButtonText(migrateRect, "UR.BetterFilter.ConvertLegacyFilters".Translate()))
        {
            var result = FilterSetStorage.ConvertLegacyTxtFiles();
            Messages.Message("UR.BetterFilter.LegacyConversionMessage".Translate(
                    result.Converted, result.Skipped, result.Failed),
                MessageTypeDefOf.NeutralEvent, false);
        }
    }
}

public class BetterFilterSettings : ModSettings
{
    public static readonly List<string> Defaults = new()
    {
        // "AllowFresh",    // 这两个原版 Filter 虽然也会从 thingDef 过滤物品，但是仍然是依赖游戏内行动的非静态过滤器
        // "AllowRotten",
        "AllowBurnableApparel",
        "AllowNonBurnableApparel",
        "AllowBurnableWeapons",
        "AllowNonBurnableWeapons",
        "AllowSmeltableApparel",
        "AllowNonSmeltableApparel",
        "AllowSmeltable",
        "AllowNonSmeltableWeapons",
        "AllowAdultOnlyApparel",
        "AllowChildOnlyApparel",
        "UR_AllowPerishable",
        "UR_AllowNotPerishable",
        "UR_AllowDeteriorating",
        "UR_AllowNotDeteriorating",
        "UR_AllowQuality",
        "UR_AllowNoQuality",
        "UR_AllowHitPoints",
        "UR_AllowNoHitPoints",
        "UR_AllowNonStackable",
        "UR_AllowStackable"
    };

    public List<string> AllowedFilterDefNames = new(Defaults);
    public List<FilterTargetDefinition> CustomFilterTargets = new();

    public List<string> EffectiveAllowedDefNames =>
        AllowedFilterDefNames is { Count: > 0 }
            ? AllowedFilterDefNames
            : Defaults;

    public IEnumerable<SpecialThingFilterDef> AllowedFilters =>
        EffectiveAllowedDefNames
            .Select(n => DefDatabase<SpecialThingFilterDef>.GetNamed(n, errorOnFail: false))
            .Where(d => d != null);

    public void Validate()
    {
        AllowedFilterDefNames.RemoveAll(n =>
            DefDatabase<SpecialThingFilterDef>.GetNamed(n, errorOnFail: false) == null);
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref AllowedFilterDefNames, "allowedFilterDefNames", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.LoadingVars
            && (AllowedFilterDefNames == null || AllowedFilterDefNames.Count == 0))
            AllowedFilterDefNames = new List<string>(Defaults);
        AllowedFilterDefNames ??= new List<string>();
        Scribe_Collections.Look(ref CustomFilterTargets, "customFilterTargets", LookMode.Deep);
        CustomFilterTargets ??= new List<FilterTargetDefinition>();
    }
}
