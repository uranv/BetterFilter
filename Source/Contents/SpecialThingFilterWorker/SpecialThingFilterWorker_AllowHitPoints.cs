using BetterFilter.Contents.Utils;
using Verse;

namespace BetterFilter.Contents.SpecialThingFilterWorker;

public class SpecialThingFilterWorker_AllowHitPoints : Verse.SpecialThingFilterWorker
{
    public override bool Matches(Thing t) => MatchFunction.MatchHitPoints(t.def);

    public override bool CanEverMatch(ThingDef def) => ModDefOf.IsBetterFilterDummy(def) || MatchFunction.MatchHitPoints(def);
}
