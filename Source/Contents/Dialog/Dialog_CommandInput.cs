using System;
using System.Collections.Generic;
using System.Linq;
using BetterFilter.Contents.Filters;
using BetterFilter.Contents.Utils;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Dialog;

/// <summary>
/// Legacy argparse parser used only by the one-click TXT-to-XML migration.
/// It is no longer exposed from the advanced-filter UI.
/// </summary>
public class Dialog_CommandInput : Window
{
    private readonly Dialog_AdvancedFilters _parent;
    private string _buffer = "";

    public Dialog_CommandInput(Dialog_AdvancedFilters parent)
    {
        _parent = parent;
        forcePause = true;
        draggable = true;
        doCloseX = true;
        closeOnClickedOutside = false;
        closeOnAccept = false;
        absorbInputAroundWindow = false;
        layer = WindowLayer.Super;
    }

    public override Vector2 InitialSize => new Vector2(500f, 180f);

    public override void DoWindowContents(Rect inRect)
    {
        var inner = inRect.ContractedBy(12f);

        Widgets.Label(inner.TopPartPixels(20f), "UR.BetterFilter.CommandPrompt".Translate());
        inner.yMin += 26f;

        var textRect = inner.TopPartPixels(30f);
        _buffer = Widgets.TextField(textRect, _buffer);

        inner.yMin += 40f;
        var btnW = (inner.width - 6f) / 2f;
        if (Widgets.ButtonText(new Rect(inner.x, inner.y, btnW, 30f), "OK"))
        {
            if (TryParseAndAdd(_buffer.Trim()))
            { _parent.MarkDirty(); Close(); }
        }
        if (Widgets.ButtonText(new Rect(inner.x + btnW + 6f, inner.y, btnW, 30f),
                "CancelButton".Translate()))
        {
            Close();
        }

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
            && GUI.GetNameOfFocusedControl().Contains("TextField"))
        {
            if (TryParseAndAdd(_buffer.Trim()))
            { _parent.MarkDirty(); Close(); }
            Event.current.Use();
        }
    }

    // ── Parser ────────────────────────────────────────────────────────

    private bool TryParseAndAdd(string cmd)
    {
        if (string.IsNullOrEmpty(cmd)) return false;
        return TryParseMulti(cmd, _parent.Components);
    }

    /// <summary>
    /// Parse a comma-separated list of filter commands and add results to target.
    /// Returns true if all commands parsed successfully.
    /// </summary>
    internal static bool TryParseMulti(string cmd, List<FilterComponent> target)
    {
        if (string.IsNullOrEmpty(cmd)) return false;
        var subCommands = cmd.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (subCommands.Count == 0) return false;
        foreach (var sub in subCommands)
        {
            if (!TryParseSingle(sub, target))
                return false;
        }
        return true;
    }

    internal static bool TryParseSingle(string cmd, List<FilterComponent> target)
    {
        var parts = cmd.Split(' ').Where(s => s.Length > 0).ToList();
        if (parts.Count == 0) return false;

        var type = parts[0].ToLowerInvariant();

        switch (type)
        {
            case "worker": return ParseWorker(parts, target);
            case "mod":    return ParseMod(parts, target);
            case "cat":    return ParseCat(parts, target);
            case "value":  return ParseValue(parts, target);
            case "enum":   return ParseEnum(parts, target);
            case "item":   return ParseItem(parts, target);
            default:
                Messages.Message("UR.BetterFilter.UnknownCommand".Translate(type),
                    MessageTypeDefOf.RejectInput, false);
                return false;
        }
    }

    internal static bool ParseWorker(List<string> parts, List<FilterComponent> target)
    {
        // State
        var defNames = new List<string>();
        bool? isBlacklist = null;
        var isUnsafe = false;
        var i = 1; // skip "filter"

        var hasD = false;

        while (i < parts.Count)
        {
            switch (parts[i])
            {
                case "-d":
                    if (hasD)
                    {
                        Messages.Message("UR.BetterFilter.CmdDupD".Translate(),
                            MessageTypeDefOf.RejectInput, false);
                        return false;
                    }
                    hasD = true;
                    i++;
                    while (i < parts.Count && !parts[i].StartsWith("-"))
                    {
                        defNames.Add(parts[i]);
                        i++;
                    }
                    break;

                case "-b":
                    if (isBlacklist.HasValue)
                    {
                        Messages.Message("UR.BetterFilter.CmdDupB".Translate(),
                            MessageTypeDefOf.RejectInput, false);
                        return false;
                    }
                    isBlacklist = true;
                    i++;
                    break;

                case "-u":
                    if (isUnsafe)
                    {
                        Messages.Message("UR.BetterFilter.CmdDupU".Translate(),
                            MessageTypeDefOf.RejectInput, false);
                        return false;
                    }
                    isUnsafe = true;
                    i++;
                    break;

                default:
                    i++;
                    break;
            }
        }

        // -d is mandatory
        if (!hasD)
        {
            Messages.Message("UR.BetterFilter.CmdNoD".Translate(),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }

        if (defNames.Count == 0)
        {
            Messages.Message("UR.BetterFilter.CmdNoDefNames".Translate(),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }

        // Resolve defNames
        var resolved = new List<SpecialThingFilterDef>();
        var errors = new List<string>();
        foreach (var name in defNames)
        {
            var def = DefDatabase<SpecialThingFilterDef>.GetNamed(name, errorOnFail: false);
            if (def != null)
                resolved.Add(def);
            else
                errors.Add(name);
        }

        if (errors.Count > 0 && !isUnsafe)
        {
            Messages.Message("UR.BetterFilter.CmdBadDefs".Translate(
                string.Join(", ", errors)), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        if (resolved.Count == 0 && errors.Count == 0)
        {
            Messages.Message("UR.BetterFilter.CmdNoValidDef".Translate(),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }

        var comp = new FilterComponent_Worker
        {
            IsWhitelist = !(isBlacklist ?? false)
        };
        comp.SelectedDefs.AddRange(resolved);
        comp.ErrorDefNames.AddRange(errors);
        target.Add(comp);
        return true;
    }

    internal static bool ParseMod(List<string> parts, List<FilterComponent> target)
    {
        var packageIds = new List<string>();
        bool? isBlacklist = null;
        var isUnsafe = false;
        var i = 1;
        var hasD = false;

        while (i < parts.Count)
        {
            switch (parts[i])
            {
                case "-d":
                    if (hasD) { MsgDup("D"); return false; }
                    hasD = true; i++;
                    while (i < parts.Count && !parts[i].StartsWith("-"))
                    { packageIds.Add(parts[i]); i++; }
                    break;
                case "-b":
                    if (isBlacklist.HasValue) { MsgDup("B"); return false; }
                    isBlacklist = true; i++;
                    break;
                case "-u":
                    if (isUnsafe) { MsgDup("U"); return false; }
                    isUnsafe = true; i++;
                    break;
                default: i++; break;
            }
        }

        if (!hasD) { MsgNoD(); return false; }
        if (packageIds.Count == 0) { MsgNoDef(); return false; }

        var runningIds = new HashSet<string>(
            LoadedModManager.RunningMods.Select(m => m.PackageId),
            StringComparer.OrdinalIgnoreCase);
        var valid = new List<string>();
        var errors = new List<string>();
        foreach (var pid in packageIds)
        {
            if (runningIds.TryGetValue(pid, out var actual))
                valid.Add(actual); // use canonical casing from the running mod
            else
                errors.Add(pid);
        }

        if (errors.Count > 0 && !isUnsafe)
        {
            Messages.Message("UR.BetterFilter.CmdBadMods".Translate(
                string.Join(", ", errors)), MessageTypeDefOf.RejectInput, false);
            return false;
        }
        if (valid.Count == 0 && errors.Count == 0)
        {
            Messages.Message("UR.BetterFilter.CmdNoValidMod".Translate(),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }

        var comp = new FilterComponent_Mod { IsWhitelist = !(isBlacklist ?? false) };
        comp.PackageIds.AddRange(valid);
        comp.ErrorPackageIds.AddRange(errors);
        target.Add(comp);
        return true;
    }

    internal static bool ParseCat(List<string> parts, List<FilterComponent> target)
    {
        var catNames = new List<string>();
        bool? isBlacklist = null;
        var isUnsafe = false;
        var i = 1;
        var hasD = false;

        while (i < parts.Count)
        {
            switch (parts[i])
            {
                case "-d":
                    if (hasD) { MsgDup("D"); return false; }
                    hasD = true; i++;
                    while (i < parts.Count && !parts[i].StartsWith("-"))
                    { catNames.Add(parts[i]); i++; }
                    break;
                case "-b":
                    if (isBlacklist.HasValue) { MsgDup("B"); return false; }
                    isBlacklist = true; i++;
                    break;
                case "-u":
                    if (isUnsafe) { MsgDup("U"); return false; }
                    isUnsafe = true; i++;
                    break;
                default: i++; break;
            }
        }

        if (!hasD) { MsgNoD(); return false; }
        if (catNames.Count == 0) { MsgNoDef(); return false; }

        var valid = new List<string>();
        var errors = new List<string>();
        foreach (var name in catNames)
        {
            var def = DefDatabase<ThingCategoryDef>.GetNamed(name, errorOnFail: false);
            if (def != null)
                valid.Add(name);
            else
                errors.Add(name);
        }

        if (errors.Count > 0 && !isUnsafe)
        {
            Messages.Message("UR.BetterFilter.CmdBadCats".Translate(
                string.Join(", ", errors)), MessageTypeDefOf.RejectInput, false);
            return false;
        }
        if (valid.Count == 0 && errors.Count == 0)
            return false;

        var comp = new FilterComponent_Category { IsWhitelist = !(isBlacklist ?? false) };
        comp.CategoryDefNames.AddRange(valid);
        comp.ErrorCategoryNames.AddRange(errors);
        target.Add(comp);
        return true;
    }

    internal static bool ParseValue(List<string> parts, List<FilterComponent> target)
    {
        string propName = null;
        bool? isBlacklist = null;
        bool? isLoose = null;
        var isUnsafe = false;
        float? vMin = null;
        float? vMax = null;
        var i = 1;
        bool hasD = false, hasV = false;

        while (i < parts.Count)
        {
            switch (parts[i])
            {
                case "-d":
                    if (hasD) { MsgDup("D"); return false; }
                    hasD = true; i++;
                    if (i < parts.Count && !parts[i].StartsWith("-"))
                    { propName = parts[i]; i++; }
                    // Exactly one argument required
                    if (i < parts.Count && !parts[i].StartsWith("-"))
                    {
                        Messages.Message("UR.BetterFilter.CmdValueMultiD".Translate(),
                            MessageTypeDefOf.RejectInput, false);
                        return false;
                    }
                    break;
                case "-b":
                    if (isBlacklist.HasValue) { MsgDup("B"); return false; }
                    isBlacklist = true; i++;
                    break;
                case "-l":
                    if (isLoose.HasValue) { MsgDup("L"); return false; }
                    isLoose = true; i++;
                    break;
                case "-u":
                    if (isUnsafe) { MsgDup("U"); return false; }
                    isUnsafe = true; i++;
                    break;
                case "-v":
                    if (hasV) { MsgDup("V"); return false; }
                    hasV = true; i++;
                    // First value: n=nolimit, n<num>=negative, otherwise positive float
                    if (i < parts.Count && !parts[i].StartsWith("-")
                                        && TryParseValueArg(parts[i], out var p1))
                    {
                        vMin = p1; i++;
                    }
                    // Second value
                    if (i < parts.Count && !parts[i].StartsWith("-")
                                        && TryParseValueArg(parts[i], out var p2))
                    {
                        vMax = p2; i++;
                    }
                    break;
                default: i++; break;
            }
        }

        if (!hasD)
        {
            Messages.Message("UR.BetterFilter.CmdNoD".Translate(),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }
        if (string.IsNullOrEmpty(propName))
        {
            Messages.Message("UR.BetterFilter.CmdValueNoProp".Translate(),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }
        if (!hasV)
        {
            Messages.Message("UR.BetterFilter.CmdValueNoV".Translate(),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }

        if (propName.StartsWith("def.", StringComparison.OrdinalIgnoreCase))
            propName = propName.Substring(4);

        var targetPath = ResolveNumericPath(propName);
        if (targetPath == null && propName.StartsWith("'") && propName.EndsWith("'"))
            targetPath = propName.Substring(1, propName.Length - 2);

        if (string.IsNullOrWhiteSpace(targetPath)
            || !RuntimeFilterPath.IsValidSyntax(targetPath))
        {
            if (!isUnsafe)
            {
                Messages.Message("UR.BetterFilter.CmdValueBadProp".Translate(propName),
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
        }

        var comp = new FilterComponent_Numeric
        {
            TargetPath = targetPath ?? propName,
            ErrorTargetPath = RuntimeFilterPath.IsValidSyntax(targetPath) ? null : propName,
            IsWhitelist = !(isBlacklist ?? false),
            IsLoose = isLoose ?? false,
            Min = vMin ?? 0f,
            Max = vMax ?? 0f,
            MinBuffer = vMin?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            MaxBuffer = vMax?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""
        };
        comp.TargetName = FilterTargetCatalog.Find(FilterTargetKind.Numeric, comp.TargetPath)?.Name;
        target.Add(comp);
        return true;
    }

    internal static bool ParseEnum(List<string> parts, List<FilterComponent> target)
    {
        string propName = null;
        bool? isBlacklist = null;
        bool? isLoose = null;
        var isUnsafe = false;
        var rawValues = new List<string>();
        var i = 1;
        bool hasD = false, hasV = false;

        while (i < parts.Count)
        {
            switch (parts[i])
            {
                case "-d":
                    if (hasD) { MsgDup("D"); return false; }
                    hasD = true; i++;
                    if (i < parts.Count && !parts[i].StartsWith("-"))
                    { propName = parts[i]; i++; }
                    // Exactly one argument required
                    if (i < parts.Count && !parts[i].StartsWith("-"))
                    {
                        Messages.Message("UR.BetterFilter.CmdValueMultiD".Translate(),
                            MessageTypeDefOf.RejectInput, false);
                        return false;
                    }
                    break;
                case "-b":
                    if (isBlacklist.HasValue) { MsgDup("B"); return false; }
                    isBlacklist = true; i++;
                    break;
                case "-l":
                    if (isLoose.HasValue) { MsgDup("L"); return false; }
                    isLoose = true; i++;
                    break;
                case "-u":
                    if (isUnsafe) { MsgDup("U"); return false; }
                    isUnsafe = true; i++;
                    break;
                case "-v":
                    if (hasV) { MsgDup("V"); return false; }
                    hasV = true; i++;
                    while (i < parts.Count && !parts[i].StartsWith("-"))
                    { rawValues.Add(parts[i]); i++; }
                    break;
                default: i++; break;
            }
        }

        if (!hasD)
        {
            Messages.Message("UR.BetterFilter.CmdNoD".Translate(),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }
        if (string.IsNullOrEmpty(propName))
        {
            Messages.Message("UR.BetterFilter.CmdValueNoProp".Translate(),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }

        var targetPath = ResolveEnumPath(propName);
        if (targetPath == null && propName.StartsWith("'") && propName.EndsWith("'"))
            targetPath = propName.Substring(1, propName.Length - 2);

        if (string.IsNullOrWhiteSpace(targetPath)
            || !RuntimeFilterPath.IsValidSyntax(targetPath))
        {
            if (!isUnsafe)
            {
                Messages.Message("UR.BetterFilter.CmdValueBadProp".Translate(propName),
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
        }

        var comp = new FilterComponent_Enum
        {
            TargetPath = targetPath ?? propName,
            ErrorTargetPath = RuntimeFilterPath.IsValidSyntax(targetPath) ? null : propName,
            IsWhitelist = !(isBlacklist ?? false),
            IsLoose = isLoose ?? false
        };

        var allValues = RuntimeFilterPath.GetDistinctTextValues(comp.TargetPath);
        var allSet = new HashSet<string>(allValues, StringComparer.Ordinal);
        foreach (var value in rawValues)
        {
            if (allSet.Contains(value)) comp.SelectedValues.Add(value);
            else comp.ErrorValues.Add(value);
        }

        if (comp.ErrorValues.Count > 0 && !isUnsafe)
        {
            Messages.Message("UR.BetterFilter.CmdBadEnumVals".Translate(
                string.Join(", ", comp.ErrorValues)), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        comp.TargetName = FilterTargetCatalog.Find(FilterTargetKind.Enum, comp.TargetPath)?.Name;
        target.Add(comp);
        return true;
    }

    internal static bool ParseItem(List<string> parts, List<FilterComponent> target)
    {
        var defNames = new List<string>();
        bool? isBlacklist = null;
        var isUnsafe = false;
        var i = 1;
        var hasD = false;

        while (i < parts.Count)
        {
            switch (parts[i])
            {
                case "-d":
                    if (hasD) { MsgDup("D"); return false; }
                    hasD = true; i++;
                    while (i < parts.Count && !parts[i].StartsWith("-"))
                    { defNames.Add(parts[i]); i++; }
                    break;
                case "-b":
                    if (isBlacklist.HasValue) { MsgDup("B"); return false; }
                    isBlacklist = true; i++;
                    break;
                case "-u":
                    if (isUnsafe) { MsgDup("U"); return false; }
                    isUnsafe = true; i++;
                    break;
                default: i++; break;
            }
        }

        if (!hasD) { MsgNoD(); return false; }
        if (defNames.Count == 0) { MsgNoDef(); return false; }

        var resolved = new List<ThingDef>();
        var errors = new List<string>();
        foreach (var name in defNames)
        {
            var def = DefDatabase<ThingDef>.GetNamed(name, errorOnFail: false);
            if (def != null)
                resolved.Add(def);
            else
                errors.Add(name);
        }

        if (errors.Count > 0 && !isUnsafe)
        {
            Messages.Message("UR.BetterFilter.CmdBadItemDefs".Translate(
                string.Join(", ", errors)), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        if (resolved.Count == 0 && errors.Count == 0)
            return false;

        var whitelist = !(isBlacklist ?? false);
        foreach (var def in resolved)
            target.Add(new FilterComponent_Item { SelectedDef = def, IsWhitelist = whitelist });
        foreach (var err in errors)
            target.Add(new FilterComponent_Item { ErrorDefName = err, IsWhitelist = whitelist });
        return true;
    }

    private static string ResolveNumericPath(string name)
    {
        if (string.Equals(name, "StackLimit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "stack", StringComparison.OrdinalIgnoreCase))
            return "Defs/ThingDef/stackLimit";
        if (string.Equals(name, "Mass", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "weight", StringComparison.OrdinalIgnoreCase))
            return "Defs/ThingDef/stats/Mass";
        if (string.Equals(name, "MarketValue", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "market", StringComparison.OrdinalIgnoreCase))
            return "Defs/ThingDef/stats/MarketValue";
        return null;
    }

    private static string ResolveEnumPath(string name)
    {
        if (string.Equals(name, "WeaponTags", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "weapon", StringComparison.OrdinalIgnoreCase))
            return "Defs/ThingDef/weaponTags/li";
        if (string.Equals(name, "TradeTags", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "trade", StringComparison.OrdinalIgnoreCase))
            return "Defs/ThingDef/tradeTags/li";
        if (string.Equals(name, "TechLevel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "tech", StringComparison.OrdinalIgnoreCase))
            return "Defs/ThingDef/techLevel";
        if (string.Equals(name, "ApparelTags", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "apparel", StringComparison.OrdinalIgnoreCase))
            return "Defs/ThingDef/apparel/tags/li";
        if (string.Equals(name, "ApparelLayer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "layer", StringComparison.OrdinalIgnoreCase))
            return "Defs/ThingDef/apparel/layers/li";
        if (string.Equals(name, "BodyCoverage", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "body", StringComparison.OrdinalIgnoreCase))
            return "Defs/ThingDef/apparel/bodyPartGroups/li";
        return null;
    }

    private static void MsgDup(string flag) =>
        Messages.Message("UR.BetterFilter.CmdDup" + flag.TrimStart('-'), MessageTypeDefOf.RejectInput, false);
    private static void MsgNoD() =>
        Messages.Message("UR.BetterFilter.CmdNoD", MessageTypeDefOf.RejectInput, false);
    private static void MsgNoDef() =>
        Messages.Message("UR.BetterFilter.CmdNoDefNames", MessageTypeDefOf.RejectInput, false);

    /// <summary>
    /// Parse a numeric value argument for -v.
    /// "n" alone = no limit (null);
    /// "n1" = -1 (n-prefix for negative, avoids clash with flag dash).
    /// </summary>
    private static bool TryParseValueArg(string s, out float? value)
    {
        if (s == "n")
        {
            value = null;
            return true;
        }
        if (s.StartsWith("n") && s.Length > 1 && float.TryParse(s.Substring(1), out var neg))
        {
            value = -neg;
            return true;
        }
        if (float.TryParse(s, out var pos))
        {
            value = pos;
            return true;
        }
        value = null;
        return false;
    }
}
