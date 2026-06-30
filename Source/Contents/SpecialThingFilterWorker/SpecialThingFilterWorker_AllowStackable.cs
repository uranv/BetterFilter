using Verse;

namespace BetterFilter.Contents.SpecialThingFilterWorker;

public class SpecialThingFilterWorker_AllowStackable : Verse.SpecialThingFilterWorker
{
    public override bool Matches(Thing t)
    {
        return t.def.stackLimit >= 2;
    }

    public override bool CanEverMatch(ThingDef def)
    {
        return ModDefOf.IsBetterFilterDummy(def) || def.stackLimit >= 2;
    }
}
