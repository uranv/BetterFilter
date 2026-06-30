using RimWorld;
using Verse;

namespace BetterFilter.Contents.SpecialThingFilterWorker;

public class SpecialThingFilterWorker_AllowNotDeteriorating : Verse.SpecialThingFilterWorker
{
    public override bool Matches(Thing t)
    {
        return !t.def.CanEverDeteriorate
            || t.GetStatValue(StatDefOf.DeteriorationRate) <= 0f;
    }

    public override bool CanEverMatch(ThingDef def)
    {
        return ModDefOf.IsBetterFilterDummy(def)
            || !def.CanEverDeteriorate
            || def.GetStatValueAbstract(StatDefOf.DeteriorationRate) <= 0f;
    }
}
