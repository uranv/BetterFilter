using UnityEngine;
using Verse;

namespace BetterFilter.Contents;

public class BetterFilterMod : Mod
{
    public static BetterFilterSettings Settings;

    public BetterFilterMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<BetterFilterSettings>();
    }

    public override string SettingsCategory() => "[UR] Better Filter Settings";

    private enum SettingsTab { Introduction, AppliableFilterWorker }

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
        }
    }

    private Vector2 _scrollPos;
    private Vector2 _introScrollPos;

    private void DrawIntroductionTab(Rect rect)
    {
        string text = "UR.BetterFilter.IntroductionContent".Translate();
        float textH = Text.CalcHeight(text, rect.width - 16f);
        float totalH = Mathf.Max(textH + 20f, rect.height);

        Rect viewRect = new Rect(0f, 0f, rect.width - 16f, totalH);
        Widgets.BeginScrollView(rect, ref _introScrollPos, viewRect);
        Widgets.Label(new Rect(0f, 0f, viewRect.width, textH), text);
        Widgets.EndScrollView();
    }

    private void DrawAppliableTab(Rect rect)
    {
        // Lazy validation — DefDatabase is now guaranteed loaded
        Settings.Validate();

        // Ensure defaults are populated on first run (old saves may have empty list)
        if (Settings.allowedFilterDefNames.Count == 0)
            Settings.allowedFilterDefNames = new List<string>(BetterFilterSettings.Defaults);

        Widgets.Label(rect.TopPartPixels(22f), "UR.BetterFilter.TabAppliableDesc".Translate());
        rect.yMin += 28f;

        var allDefs = DefDatabase<SpecialThingFilterDef>.AllDefsListForReading
            .OrderBy(d => d.LabelCap.ToString()).ToList();
        float rowH = 28f;
        float btnH = 32f;
        float totalH = allDefs.Count * rowH;

        Rect scrollRect = new Rect(rect.x, rect.y, rect.width, rect.height - btnH - 12f);

        var allowedSet = new HashSet<string>(Settings.allowedFilterDefNames);
        Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, totalH);
        Widgets.BeginScrollView(scrollRect, ref _scrollPos, viewRect);
        float y = 0f;
        foreach (var def in allDefs)
        {
            Rect rowRect = new Rect(0f, y, viewRect.width, rowH);
            bool isAllowed = allowedSet.Contains(def.defName);
            Widgets.CheckboxLabeled(rowRect, (string)def.LabelCap, ref isAllowed);
            if (isAllowed && !allowedSet.Contains(def.defName))
                Settings.allowedFilterDefNames.Add(def.defName);
            else if (!isAllowed && allowedSet.Contains(def.defName))
                Settings.allowedFilterDefNames.Remove(def.defName);
            y += rowH;
        }
        Widgets.EndScrollView();

        // Reset defaults button — centered at bottom
        Rect resetRect = new Rect(rect.x + (rect.width - 120f) / 2f,
            scrollRect.yMax + 4f, 120f, btnH);
        if (Widgets.ButtonText(resetRect, "UR.BetterFilter.ResetDefault".Translate()))
        {
            Settings.allowedFilterDefNames = new List<string>(BetterFilterSettings.Defaults);
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

    public List<string> allowedFilterDefNames = new(Defaults);

    public List<string> EffectiveAllowedDefNames =>
        allowedFilterDefNames != null && allowedFilterDefNames.Count > 0
            ? allowedFilterDefNames
            : Defaults;

    public IEnumerable<SpecialThingFilterDef> AllowedFilters =>
        EffectiveAllowedDefNames
            .Select(n => DefDatabase<SpecialThingFilterDef>.GetNamed(n, errorOnFail: false))
            .Where(d => d != null);

    public void Validate()
    {
        allowedFilterDefNames.RemoveAll(n =>
            DefDatabase<SpecialThingFilterDef>.GetNamed(n, errorOnFail: false) == null);
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref allowedFilterDefNames, "allowedFilterDefNames", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.LoadingVars
            && (allowedFilterDefNames == null || allowedFilterDefNames.Count == 0))
            allowedFilterDefNames = new List<string>(Defaults);
        allowedFilterDefNames ??= new List<string>();
    }
}
