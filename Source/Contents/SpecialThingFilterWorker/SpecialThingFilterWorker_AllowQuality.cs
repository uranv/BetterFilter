using BetterFilter.Contents.Utils;
using Verse;

namespace BetterFilter.Contents.SpecialThingFilterWorker;

public class SpecialThingFilterWorker_AllowQuality : Verse.SpecialThingFilterWorker
{
    public override bool Matches(Thing t) => MatchFunction.MatchQuality(t.def);

    public override bool CanEverMatch(ThingDef def) => ModDefOf.IsBetterFilterDummy(def) || MatchFunction.MatchQuality(def);
}
