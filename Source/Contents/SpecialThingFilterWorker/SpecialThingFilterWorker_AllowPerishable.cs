using RimWorld;
using Verse;

namespace BetterFilter.Contents.SpecialThingFilterWorker;

public class SpecialThingFilterWorker_AllowPerishable : Verse.SpecialThingFilterWorker
{
    public override bool Matches(Thing t)
    {
        CompRottable comp = ThingCompUtility.TryGetComp<CompRottable>(t);
        return comp != null && comp.PropsRot.rotDestroys;
    }

    public override bool CanEverMatch(ThingDef def)
    {
        return ModDefOf.IsBetterFilterDummy(def) || (def.GetCompProperties<CompProperties_Rottable>()?.rotDestroys ?? false);
    }
}
