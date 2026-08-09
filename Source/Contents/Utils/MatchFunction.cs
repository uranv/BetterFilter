using RimWorld;
using Verse;

namespace BetterFilter.Contents.Utils;

public static class MatchFunction
{
    // 室外老化
    public static bool MatchDeteriorate(ThingDef def)
    {
        return def.CanEverDeteriorate && def.GetStatValueAbstract(StatDefOf.DeteriorationRate) > 0f;
    }
    // 高温腐坏（含有毒垃圾溶化，含腐坏变质的可腐坏物品，含腐坏消失的可腐坏物品）
    public static bool MatchPerishable(ThingDef def)
    {
        // var comp1 = def.GetCompProperties<CompProperties_Rottable>()?.rotDestroys ?? false;
        var comp1 = def.GetCompProperties<CompProperties_Rottable>();
        var comp2 = def.GetCompProperties<CompProperties_Dissolution>();
        return comp1 != null || comp2 != null;
    }
    // 使用耐久
    public static bool MatchHitPoints(ThingDef def) => def.useHitPoints;
    // 使用品质
    public static bool MatchQuality(ThingDef def) => def.HasComp<CompQuality>();
    // 使用堆叠
    public static bool MatchStackable(ThingDef def) => def.stackLimit >= 2;

}