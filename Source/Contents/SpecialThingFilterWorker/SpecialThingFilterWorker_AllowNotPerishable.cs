using RimWorld;
using Verse;

namespace BetterFilter.Contents.SpecialThingFilterWorker;

public class SpecialThingFilterWorker_AllowNotPerishable : Verse.SpecialThingFilterWorker
{
    public override bool Matches(Thing t)
    {
        return t.TryGetComp<CompRottable>() == null;
    }

    public override bool CanEverMatch(ThingDef def)
    {
        return ModDefOf.IsBetterFilterDummy(def)
            || def.GetCompProperties<CompProperties_Rottable>() == null;
    }
}
