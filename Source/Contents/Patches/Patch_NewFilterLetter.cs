using System.Linq;
using BetterFilter.Contents.Utils;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterFilter.Contents.Patches;

/// <summary>
/// After a game starts or loads, records the filters of other mods that have
/// no entry in mod settings, and sends one letter that lists them.
/// </summary>
[HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
public static class Patch_NewFilterLetter
{
    static void Postfix()
    {
        var settings = BetterFilterMod.Settings;
        var found = DefDatabase<SpecialThingFilterDef>.AllDefsListForReading
            .Where(def => !FilterWorkerCatalog.IsBuiltIn(def) && settings.FindConfig(def) == null)
            .ToList();
        if (found.Count == 0) return;

        // Runs on the main thread after the load, when the letter stack is ready.
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            foreach (var def in found) settings.GetOrCreateConfig(def);
            settings.Write();
            if (!settings.NotifyNewFilters) return;

            Find.LetterStack.ReceiveLetter(
                "UR.BetterFilter.NewFiltersLetterLabel".Translate(),
                "UR.BetterFilter.NewFiltersLetterText".Translate(found
                    .Select(def => $"{def.LabelCap} [{FilterWorkerCatalog.KindLabel(settings.KindOf(def))}] - {FilterWorkerCatalog.SourceLabel(def)}")
                    .ToLineList("  - ")),
                LetterDefOf.NeutralEvent);
        });
    }
}
