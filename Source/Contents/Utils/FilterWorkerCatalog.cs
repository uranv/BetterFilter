using System.Reflection;
using System.Reflection.Emit;
using BetterFilter.Contents.Dialog;
using HarmonyLib;
using Verse;

namespace BetterFilter.Contents.Utils;

/// <summary>
/// Static: the worker reads only the ThingDef, so its result can be written
/// into per-item permissions. Dynamic: the worker reads the Thing instance.
/// </summary>
public enum FilterWorkerKind
{
    Static,
    Dynamic
}

public class FilterWorkerConfig : IExposable
{
    public string DefName;
    public FilterWorkerKind Kind;
    public bool Move;
    public bool Apply;

    public void Reset(SpecialThingFilterDef def)
    {
        DefName = def.defName;
        Kind = FilterWorkerCatalog.DefaultKind(def);
        Move = Apply = FilterWorkerCatalog.IsBuiltIn(def);
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref DefName, "defName");
        Scribe_Values.Look(ref Kind, "kind");
        Scribe_Values.Look(ref Move, "move");
        Scribe_Values.Look(ref Apply, "apply");
    }
}

public static class FilterWorkerCatalog
{
    // Official filters whose result depends only on the ThingDef (and its stuff).
    private static readonly HashSet<string> OfficialStatic = new()
    {
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
        "AllowUnfinished",
        "AllowPlantFood"
    };

    private static readonly HashSet<OpCode> ArgumentOps = new()
    {
        OpCodes.Ldarg_S, OpCodes.Ldarg, OpCodes.Ldarga_S, OpCodes.Ldarga, OpCodes.Starg_S, OpCodes.Starg
    };

    private static readonly HashSet<OpCode> NullTestOps = new()
    {
        OpCodes.Brtrue, OpCodes.Brtrue_S, OpCodes.Brfalse, OpCodes.Brfalse_S, OpCodes.Ldnull
    };

    private static readonly Dictionary<SpecialThingFilterDef, FilterWorkerKind> DetectedKinds = new();
    private static readonly Dictionary<SpecialThingFilterDef, int> MatchCounts = new();

    /// <summary>Official (Core/DLC) and BetterFilter filters have a fixed kind.</summary>
    public static bool IsBuiltIn(SpecialThingFilterDef def)
        => def.modContentPack is { IsOfficialMod: true } || def.modContentPack == BetterFilterMod.ContentPack;

    public static FilterWorkerKind DefaultKind(SpecialThingFilterDef def)
    {
        if (def.modContentPack == BetterFilterMod.ContentPack) return FilterWorkerKind.Static;
        if (def.modContentPack is { IsOfficialMod: true })
            return OfficialStatic.Contains(def.defName) ? FilterWorkerKind.Static : FilterWorkerKind.Dynamic;
        return DetectKind(def);
    }

    /// <summary>
    /// Reads the IL of the worker's Matches(Thing). Static when the Thing
    /// argument is used only as <c>t.def</c> or in a null test.
    /// </summary>
    public static FilterWorkerKind DetectKind(SpecialThingFilterDef def)
    {
        if (DetectedKinds.TryGetValue(def, out var kind)) return kind;
        var code = PatchProcessor.ReadMethodBody(
            AccessTools.Method(def.workerClass, nameof(Verse.SpecialThingFilterWorker.Matches), new[] { typeof(Thing) })).ToList();
        return DetectedKinds[def] = Enumerable.Range(0, code.Count).All(i => code[i].Key == OpCodes.Ldarg_1
            ? i + 1 < code.Count && (NullTestOps.Contains(code[i + 1].Key)
                || code[i + 1].Key == OpCodes.Ldfld && code[i + 1].Value is FieldInfo { Name: "def" } field
                && field.DeclaringType == typeof(Thing))
            : !ArgumentOps.Contains(code[i].Key))
            ? FilterWorkerKind.Static
            : FilterWorkerKind.Dynamic;
    }

    /// <summary>Number of storable items the worker can match. The game hides a filter with none.</summary>
    public static int MatchCount(SpecialThingFilterDef def)
        => MatchCounts.TryGetValue(def, out var count)
            ? count
            : MatchCounts[def] = Dialog_AdvancedFilters.EverStorableDefs.Count(d =>
                !ModDefOf.IsBetterFilterDummy(d) && def.Worker.CanEverMatch(d));

    public static string KindLabel(FilterWorkerKind kind)
        => (kind == FilterWorkerKind.Static ? "UR.BetterFilter.KindStatic" : "UR.BetterFilter.KindDynamic").Translate();

    public static string SourceLabel(SpecialThingFilterDef def)
        => def.modContentPack == null ? "UR.BetterFilter.SourceUnknown".Translate()
            : def.modContentPack.IsOfficialMod ? "UR.BetterFilter.SourceOfficial".Translate()
            : $"{def.modContentPack.Name} ({def.modContentPack.PackageIdPlayerFacing})";
}
