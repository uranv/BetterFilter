using BetterFilter.Contents.Filters;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Dialog;

/// <summary>
/// Small dialog for typing a filter-definition command line.
/// Syntax: filter -d DefName [DefName ...] [-b] [-u]
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
        float btnW = (inner.width - 6f) / 2f;
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
        return TryParseMulti(cmd, _parent.components);
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

        string type = parts[0].ToLowerInvariant();

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
        bool isUnsafe = false;
        int i = 1; // skip "filter"

        bool hasD = false;

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
            isWhitelist = !(isBlacklist ?? false)
        };
        comp.selectedDefs.AddRange(resolved);
        comp.errorDefNames.AddRange(errors);
        target.Add(comp);
        return true;
    }

    internal static bool ParseMod(List<string> parts, List<FilterComponent> target)
    {
        var packageIds = new List<string>();
        bool? isBlacklist = null;
        bool isUnsafe = false;
        int i = 1;
        bool hasD = false;

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

        var comp = new FilterComponent_Mod { isWhitelist = !(isBlacklist ?? false) };
        comp.packageIds.AddRange(valid);
        comp.errorPackageIds.AddRange(errors);
        target.Add(comp);
        return true;
    }

    internal static bool ParseCat(List<string> parts, List<FilterComponent> target)
    {
        var catNames = new List<string>();
        bool? isBlacklist = null;
        bool isUnsafe = false;
        int i = 1;
        bool hasD = false;

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

        var comp = new FilterComponent_Category { isWhitelist = !(isBlacklist ?? false) };
        comp.categoryDefNames.AddRange(valid);
        comp.errorCategoryNames.AddRange(errors);
        target.Add(comp);
        return true;
    }

    internal static bool ParseValue(List<string> parts, List<FilterComponent> target)
    {
        string propName = null;
        bool? isBlacklist = null;
        bool? isLoose = null;
        bool isUnsafe = false;
        float? vMin = null;
        float? vMax = null;
        int i = 1;
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
                                        && TryParseValueArg(parts[i], out float? p1))
                    {
                        vMin = p1; i++;
                    }
                    // Second value
                    if (i < parts.Count && !parts[i].StartsWith("-")
                                        && TryParseValueArg(parts[i], out float? p2))
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

        // Strip optional "def." prefix
        if (propName.StartsWith("def."))
            propName = propName.Substring(4);

        // Map property name: known aliases or single-quoted custom path
        NumericPropertyType propType = NumericPropertyType.StackLimit;
        string customPath = null;
        string errPropName = null;

        if (string.Equals(propName, "StackLimit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(propName, "stack", StringComparison.OrdinalIgnoreCase))
        {
            propType = NumericPropertyType.StackLimit;
        }
        else if (string.Equals(propName, "Mass", StringComparison.OrdinalIgnoreCase)
            || string.Equals(propName, "weight", StringComparison.OrdinalIgnoreCase))
        {
            propType = NumericPropertyType.Mass;
        }
        else if (propName.StartsWith("'") && propName.EndsWith("'"))
        {
            // Custom reflection path, e.g. 'recipeMaker?.displayPriority'
            customPath = propName.Substring(1, propName.Length - 2);
            // Validate it resolves to int/int? on at least one ThingDef
            bool valid = false;
            foreach (ThingDef td in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                var iv = FilterComponent_Numeric.ResolveIntPath(td, customPath);
                if (iv.HasValue) { valid = true; break; }
            }
            if (!valid)
            {
                if (!isUnsafe)
                {
                    Messages.Message("UR.BetterFilter.CmdValueBadProp".Translate(propName),
                        MessageTypeDefOf.RejectInput, false);
                    return false;
                }
                errPropName = propName;
            }
            else
            {
                propType = NumericPropertyType.Custom;
            }
        }
        else
        {
            if (!isUnsafe)
            {
                Messages.Message("UR.BetterFilter.CmdValueBadProp".Translate(propName),
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            errPropName = propName;
        }

        var comp = new FilterComponent_Numeric
        {
            propertyType = propType,
            customPropPath = customPath,
            errorPropName = errPropName,
            isWhitelist = !(isBlacklist ?? false),
            isLoose = isLoose ?? false,
            min = vMin ?? 0f,
            max = vMax ?? 0f,
            minBuffer = vMin?.ToString() ?? "",
            maxBuffer = vMax?.ToString() ?? ""
        };
        target.Add(comp);
        return true;
    }

    internal static bool ParseEnum(List<string> parts, List<FilterComponent> target)
    {
        string propName = null;
        bool? isBlacklist = null;
        bool? isLoose = null;
        bool isUnsafe = false;
        var rawValues = new List<string>();
        int i = 1;
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

        // Resolve property name
        EnumPropertyType? propType = ResolveEnumProperty(propName);

        var comp = new FilterComponent_Enum
        {
            isWhitelist = !(isBlacklist ?? false),
            isLoose = isLoose ?? false
        };

        if (propType == null)
        {
            if (!isUnsafe)
            {
                Messages.Message("UR.BetterFilter.CmdValueBadProp".Translate(propName),
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            // -u mode: property invalid → filter always allows, show error name
            comp.errorPropertyName = propName;
            // Invalid property → all values are meaningless, treat as errors
            if (rawValues.Count > 0)
                comp.errorValues.AddRange(rawValues);
        }
        else
        {
            comp.propertyType = propType;
            var allValues = FilterComponent_Enum.GetAllValues(propType.Value);
            var allSet = new HashSet<string>(allValues);
            var badVals = new List<string>();
            foreach (var v in rawValues)
            {
                if (allSet.Contains(v))
                    comp.selectedValues.Add(v);
                else
                    badVals.Add(v);
            }
            if (badVals.Count > 0 && !isUnsafe)
            {
                Messages.Message("UR.BetterFilter.CmdBadEnumVals".Translate(
                    string.Join(", ", badVals)), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            if (badVals.Count > 0)
                comp.errorValues.AddRange(badVals);
        }

        target.Add(comp);
        return true;
    }

    internal static bool ParseItem(List<string> parts, List<FilterComponent> target)
    {
        var defNames = new List<string>();
        bool? isBlacklist = null;
        bool isUnsafe = false;
        int i = 1;
        bool hasD = false;

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

        bool whitelist = !(isBlacklist ?? false);
        foreach (var def in resolved)
            target.Add(new FilterComponent_Item { selectedDef = def, isWhitelist = whitelist });
        foreach (var err in errors)
            target.Add(new FilterComponent_Item { errorDefName = err, isWhitelist = whitelist });
        return true;
    }

    internal static EnumPropertyType? ResolveEnumProperty(string name)
    {
        if (string.Equals(name, "WeaponTags", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "weapon", StringComparison.OrdinalIgnoreCase))
            return EnumPropertyType.WeaponTags;
        if (string.Equals(name, "TradeTags", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "trade", StringComparison.OrdinalIgnoreCase))
            return EnumPropertyType.TradeTags;
        if (string.Equals(name, "TechLevel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "tech", StringComparison.OrdinalIgnoreCase))
            return EnumPropertyType.TechLevel;
        if (string.Equals(name, "ApparelTags", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "apparel", StringComparison.OrdinalIgnoreCase))
            return EnumPropertyType.ApparelTags;
        if (string.Equals(name, "ApparelLayer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "layer", StringComparison.OrdinalIgnoreCase))
            return EnumPropertyType.ApparelLayer;
        if (string.Equals(name, "BodyCoverage", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "body", StringComparison.OrdinalIgnoreCase))
            return EnumPropertyType.BodyCoverage;
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
        if (s.StartsWith("n") && s.Length > 1 && float.TryParse(s.Substring(1), out float neg))
        {
            value = -neg;
            return true;
        }
        if (float.TryParse(s, out float pos))
        {
            value = pos;
            return true;
        }
        value = null;
        return false;
    }
}
