using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace BetterFilter.Contents.Utils;

public enum FilterTargetKind
{
    Numeric,
    Enum
}

public sealed class FilterTargetOption
{
    public FilterTargetKind Kind;
    public string Name;
    public string XPath;
    public bool IsBuiltIn;

    public FilterTargetOption(FilterTargetKind kind, string name, string xpath,
        bool isBuiltIn = false)
    {
        Kind = kind;
        Name = name;
        XPath = xpath;
        IsBuiltIn = isBuiltIn;
    }
}

public static class FilterTargetCatalog
{
    private static readonly List<FilterTargetOption> BuiltIns = new()
    {
        new(FilterTargetKind.Numeric, "UR.BetterFilter.StackLimit", "Defs/ThingDef/stackLimit", true),
        new(FilterTargetKind.Numeric, "UR.BetterFilter.MassKg", "Defs/ThingDef/stats/Mass", true),
        new(FilterTargetKind.Numeric, "UR.BetterFilter.MarketValue", "Defs/ThingDef/stats/MarketValue", true),
        new(FilterTargetKind.Enum, "UR.BetterFilter.WeaponTags", "Defs/ThingDef/weaponTags/li", true),
        new(FilterTargetKind.Enum, "UR.BetterFilter.TradeTags", "Defs/ThingDef/tradeTags/li", true),
        new(FilterTargetKind.Enum, "UR.BetterFilter.TechLevel", "Defs/ThingDef/techLevel", true),
        new(FilterTargetKind.Enum, "UR.BetterFilter.ApparelTags", "Defs/ThingDef/apparel/tags/li", true),
        new(FilterTargetKind.Enum, "UR.BetterFilter.ApparelLayer", "Defs/ThingDef/apparel/layers/li", true),
        new(FilterTargetKind.Enum, "UR.BetterFilter.BodyCoverage", "Defs/ThingDef/apparel/bodyPartGroups/li", true)
    };

    public static IEnumerable<FilterTargetOption> Get(FilterTargetKind kind)
    {
        foreach (var builtIn in BuiltIns.Where(t => t.Kind == kind))
            yield return builtIn;

        var custom = BetterFilterMod.Settings?.CustomFilterTargets;
        if (custom == null) yield break;
        foreach (var definition in custom)
        {
            if (!TryParseKind(definition.Type, out var customKind) || customKind != kind)
                continue;
            yield return new FilterTargetOption(kind,
                definition.Name,
                definition.XPath,
                isBuiltIn: false);
        }
    }

    public static FilterTargetOption Find(FilterTargetKind kind, string xpath,
        string name = null)
    {
        if (!string.IsNullOrEmpty(xpath))
        {
            var byPath = Get(kind).FirstOrDefault(t =>
                string.Equals(t.XPath, xpath, StringComparison.Ordinal));
            if (byPath != null) return byPath;
        }
        if (!string.IsNullOrEmpty(name))
        {
            var byName = Get(kind).FirstOrDefault(t =>
                string.Equals(t.Name, name, StringComparison.Ordinal));
            if (byName != null) return byName;
        }
        return null;
    }

    public static string DisplayName(FilterTargetOption option) =>
        option == null ? null : option.IsBuiltIn ? option.Name.Translate() : option.Name;

    public static bool TryParseKind(string type, out FilterTargetKind kind)
    {
        if (string.Equals(type, "Numeric", StringComparison.OrdinalIgnoreCase))
        {
            kind = FilterTargetKind.Numeric;
            return true;
        }
        if (string.Equals(type, "Enum", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "Enumerative", StringComparison.OrdinalIgnoreCase))
        {
            kind = FilterTargetKind.Enum;
            return true;
        }
        kind = FilterTargetKind.Numeric;
        return false;
    }
}
