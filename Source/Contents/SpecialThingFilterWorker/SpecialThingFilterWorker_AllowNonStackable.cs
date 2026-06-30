using Verse;

namespace BetterFilter.Contents.SpecialThingFilterWorker;

public class SpecialThingFilterWorker_AllowNonStackable : Verse.SpecialThingFilterWorker
{
    public override bool Matches(Thing t)
    {
        return t.def.stackLimit <= 1;
    }

    public override bool CanEverMatch(ThingDef def)
    {
        return ModDefOf.IsBetterFilterDummy(def) || def.stackLimit <= 1;
    }
}
