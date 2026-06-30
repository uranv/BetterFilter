using RimWorld;
using Verse;

namespace BetterFilter.Contents.SpecialThingFilterWorker;

public class SpecialThingFilterWorker_AllowQuality : Verse.SpecialThingFilterWorker
{
    public override bool Matches(Thing t)
    {
        return t.TryGetComp<CompQuality>() != null;
    }

    public override bool CanEverMatch(ThingDef def)
    {
        return ModDefOf.IsBetterFilterDummy(def) || def.HasComp<CompQuality>();
    }
}
