using Verse;

namespace BetterFilter.Contents.SpecialThingFilterWorker;

public class SpecialThingFilterWorker_AllowNoHitPoints : Verse.SpecialThingFilterWorker
{
    public override bool Matches(Thing t)
    {
        return !t.def.useHitPoints;
    }

    public override bool CanEverMatch(ThingDef def)
    {
        return ModDefOf.IsBetterFilterDummy(def)
            || !def.useHitPoints;
    }
}
