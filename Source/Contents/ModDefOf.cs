using RimWorld;
using Verse;

namespace BetterFilter.Contents;

[DefOf]
public static class ModDefOf
{
    public static ThingDef UR_DummyFilterItem;
    public static ThingDef UR_DummyApplyFilterItem;

    public static bool IsBetterFilterDummy(ThingDef def) =>
        def == UR_DummyFilterItem || def == UR_DummyApplyFilterItem;

    static ModDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ModDefOf));
    }
}