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
    public FilterTargetKind Kind { get; }
    public string Name { get; }
    public string XPath { get; }
    public bool IsBuiltIn { get; }

    public FilterTargetOption(FilterTargetKind kind, string name, string xpath, bool isBuiltIn = false)
    {
        Kind = kind;
        Name = name;
        XPath = xpath;
        IsBuiltIn = isBuiltIn;
    }
}

public static class FilterTargetCatalog
{
    internal static readonly List<FilterTargetOption> BuiltIns = new()
    {
        new FilterTargetOption(FilterTargetKind.Numeric, "UR.BetterFilter.StackLimit", "Defs/ThingDef/stackLimit", isBuiltIn: true),
        new FilterTargetOption(FilterTargetKind.Numeric, "UR.BetterFilter.MassKg", "Defs/ThingDef/stats/Mass", isBuiltIn: true),
        new FilterTargetOption(FilterTargetKind.Numeric, "UR.BetterFilter.MarketValue", "Defs/ThingDef/stats/MarketValue", isBuiltIn: true),
        new FilterTargetOption(FilterTargetKind.Enum, "UR.BetterFilter.WeaponTags", "Defs/ThingDef/weaponTags/li", isBuiltIn: true),
        new FilterTargetOption(FilterTargetKind.Enum, "UR.BetterFilter.TradeTags", "Defs/ThingDef/tradeTags/li", isBuiltIn: true),
        new FilterTargetOption(FilterTargetKind.Enum, "UR.BetterFilter.TechLevel", "Defs/ThingDef/techLevel", isBuiltIn: true),
        new FilterTargetOption(FilterTargetKind.Enum, "UR.BetterFilter.ApparelTags", "Defs/ThingDef/apparel/tags/li", isBuiltIn: true),
        new FilterTargetOption(FilterTargetKind.Enum, "UR.BetterFilter.ApparelLayer", "Defs/ThingDef/apparel/layers/li", isBuiltIn: true),
        new FilterTargetOption(FilterTargetKind.Enum, "UR.BetterFilter.BodyCoverage", "Defs/ThingDef/apparel/bodyPartGroups/li", isBuiltIn: true)
    };

    /// <summary>Targets of the kind. The pickers pass enabledOnly; lookups of saved components do not.</summary>
    public static IEnumerable<FilterTargetOption> Get(FilterTargetKind kind, bool enabledOnly = true)
    {
        foreach (var option in BuiltIns)
        {
            if (option.Kind == kind
                && !(enabledOnly && BetterFilterMod.Settings.DisabledBuiltInTargets.Contains(option.XPath)))
                yield return option;
        }

        foreach (var definition in BetterFilterMod.Settings.CustomFilterTargets)
        {
            if (TryParseKind(definition.Type, out var parsedKind) && parsedKind == kind
                && !(enabledOnly && !definition.Enabled))
                yield return new FilterTargetOption(kind, definition.Name, definition.XPath, isBuiltIn: false);
        }
    }

    public static FilterTargetOption Find(FilterTargetKind kind, string xpath, string name = null)
    {
        var options = Get(kind, enabledOnly: false).ToList();
        if (!string.IsNullOrWhiteSpace(xpath))
        {
            var byPath = options.FirstOrDefault(option =>
                string.Equals(option.XPath, xpath, StringComparison.Ordinal));
            if (byPath != null)
            {
                return byPath;
            }
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            return options.FirstOrDefault(option =>
                string.Equals(option.Name, name, StringComparison.Ordinal));
        }

        return null;
    }

    public static string DisplayName(FilterTargetOption option)
    {
        return option == null ? null : option.IsBuiltIn ? option.Name.Translate() : option.Name;
    }

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

        kind = default;
        return false;
    }
}
