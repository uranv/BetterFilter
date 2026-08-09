using BetterFilter.Contents.Utils;
using Verse;

namespace BetterFilter.Contents.SpecialThingFilterWorker;

public class SpecialThingFilterWorker_AllowDeteriorating : Verse.SpecialThingFilterWorker
{
    public override bool Matches(Thing t) => MatchFunction.MatchDeteriorate(t.def);

    public override bool CanEverMatch(ThingDef def) => ModDefOf.IsBetterFilterDummy(def) || MatchFunction.MatchDeteriorate(def);
}
