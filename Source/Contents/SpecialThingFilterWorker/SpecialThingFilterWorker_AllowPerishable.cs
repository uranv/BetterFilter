using BetterFilter.Contents.Utils;
using Verse;

namespace BetterFilter.Contents.SpecialThingFilterWorker;

public class SpecialThingFilterWorker_AllowPerishable : Verse.SpecialThingFilterWorker
{
    public override bool Matches(Thing t) => MatchFunction.MatchPerishable(t.def);

    public override bool CanEverMatch(ThingDef def) => ModDefOf.IsBetterFilterDummy(def) || MatchFunction.MatchPerishable(def);
}
