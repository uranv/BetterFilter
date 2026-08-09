using BetterFilter.Contents.Utils;
using Verse;

namespace BetterFilter.Contents.SpecialThingFilterWorker;

public class SpecialThingFilterWorker_AllowStackable : Verse.SpecialThingFilterWorker
{
    public override bool Matches(Thing t) => MatchFunction.MatchStackable(t.def);

    public override bool CanEverMatch(ThingDef def) => ModDefOf.IsBetterFilterDummy(def) || MatchFunction.MatchStackable(def);
}
