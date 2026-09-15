using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace BetterFilter.Contents.Utils;

/// <summary>
/// Resolves the supported RimWorld XPath subset against runtime ThingDef objects.
/// The path is a user-facing address, not an XML query: no ThingDef is serialized
/// to XML during matching.
/// </summary>
public static class RuntimeFilterPath
{
    private sealed class Step
    {
        public string Name;
        public string Predicate;
    }

    private sealed class StatsTarget
    {
        public readonly ThingDef Def;
        public StatsTarget(ThingDef def) => Def = def;
    }

    private static readonly Dictionary<string, Step[]> ParsedPathCache = new(
        StringComparer.Ordinal);

    public static bool IsValidSyntax(string path) => TryParse(path, out _, out _);

    public static bool TryValidate(string path, FilterTargetKind kind, out string error)
    {
        error = null;
        if (!TryParse(path, out _, out error)) return false;

        var hasValue = false;
        var hasRawValue = false;
        foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (!TryGetValues(def, path, out var rawValues, out _)
                || rawValues.Count == 0)
                continue;
            hasRawValue = true;

            if (kind == FilterTargetKind.Numeric)
            {
                if (rawValues.All(IsNumericValue))
                {
                    hasValue = true;
                    break;
                }
            }
            else if (rawValues.All(IsEnumValue))
            {
                hasValue = true;
                break;
            }
        }

        if (!hasValue)
            error = hasRawValue
                ? "Target value type does not match the selected filter type."
                : kind == FilterTargetKind.Numeric
                    ? "Target does not resolve to a numeric value on any ThingDef."
                    : "Target does not resolve to an enum or text value on any ThingDef.";
        return hasValue;
    }

    public static bool TryGetNumericValues(ThingDef def, string path,
        out List<float> values, out string error)
    {
        values = new List<float>();
        if (!TryGetValues(def, path, out var raw, out error)) return false;
        foreach (var value in raw)
        {
            if (TryConvertNumber(value, out var number))
                values.Add(number);
        }
        return true;
    }

    public static bool TryGetTextValues(ThingDef def, string path,
        out List<string> values, out string error)
    {
        values = new List<string>();
        if (!TryGetValues(def, path, out var raw, out error)) return false;
        foreach (var value in raw)
        {
            var text = ToText(value);
            if (text != null) values.Add(text);
        }
        return true;
    }

    public static List<string> GetDistinctTextValues(string path)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (!TryParse(path, out _, out _)) return result.OrderBy(s => s).ToList();

        foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (!TryGetTextValues(def, path, out var values, out _)) continue;
            foreach (var value in values) result.Add(value);
        }

        return result.OrderBy(s => s).ToList();
    }

    public static bool TryGetValues(ThingDef def, string path,
        out List<object> values, out string error)
    {
        values = new List<object>();
        if (def == null)
        {
            error = "ThingDef is null.";
            return false;
        }

        if (!TryParse(path, out var steps, out error)) return false;

        var current = new List<object> { def };
        // Step 0 is Defs, step 1 is ThingDef. Both identify the root universe.
        ApplyPredicate(current, steps[1].Predicate);
        for (var i = 2; i < steps.Length; i++)
        {
            current = Expand(current, steps[i].Name);
            ApplyPredicate(current, steps[i].Predicate);
            if (current.Count == 0) break;
        }

        foreach (var value in current)
            Flatten(value, values);
        return true;
    }

    private static List<object> Expand(IEnumerable<object> source, string name)
    {
        var result = new List<object>();
        foreach (var item in source)
        {
            if (item == null) continue;
            if (string.Equals(name, "li", StringComparison.OrdinalIgnoreCase))
            {
                if (item is IEnumerable enumerable && item is not string)
                {
                    foreach (var child in enumerable)
                        if (child != null) result.Add(child);
                }
                continue;
            }

            if (item is IEnumerable collection && item is not string)
            {
                foreach (var child in collection)
                    AddMembers(child, name, result);
            }
            else
            {
                AddMembers(item, name, result);
            }
        }
        return result;
    }

    private static void AddMembers(object item, string name, List<object> result)
    {
        if (item == null) return;

        if (item is ThingDef def && string.Equals(name, "stats", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(new StatsTarget(def));
            return;
        }

        if (item is StatsTarget stats)
        {
            var statDef = DefDatabase<StatDef>.GetNamed(name, errorOnFail: false);
            if (statDef != null)
            {
                try { result.Add(stats.Def.GetStatValueAbstract(statDef)); }
                catch { /* missing/inapplicable stat */ }
            }
            return;
        }

        var type = item.GetType();
        var property = type.GetProperty(name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property != null && property.GetIndexParameters().Length == 0)
        {
            try
            {
                var value = property.GetValue(item);
                if (value != null) result.Add(value);
            }
            catch { }
            return;
        }

        var field = type.GetField(name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (field != null)
        {
            try
            {
                var value = field.GetValue(item);
                if (value != null) result.Add(value);
            }
            catch { }
        }
    }

    private static void ApplyPredicate(List<object> values, string predicate)
    {
        if (string.IsNullOrWhiteSpace(predicate)) return;
        var p = predicate.Trim();
        if (string.Equals(p, "text()", StringComparison.OrdinalIgnoreCase))
        {
            values.RemoveAll(v => string.IsNullOrEmpty(ToText(v)));
            return;
        }

        var equalsAt = p.IndexOf('=');
        if (equalsAt < 0)
        {
            values.RemoveAll(v => !HasMember(v, p));
            return;
        }

        var memberName = p.Substring(0, equalsAt).Trim().TrimStart('@');
        var expected = p.Substring(equalsAt + 1).Trim();
        if ((expected.StartsWith("'") && expected.EndsWith("'"))
            || (expected.StartsWith("\"") && expected.EndsWith("\"")))
            expected = expected.Substring(1, expected.Length - 2);

        values.RemoveAll(value =>
            !GetComparableMembers(value, memberName)
                .Any(actual => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HasMember(object value, string name) =>
        GetComparableMembers(value, name).Count > 0;

    private static List<string> GetComparableMembers(object value, string name)
    {
        var result = new List<string>();
        if (value == null) return result;
        if (string.Equals(name, "text()", StringComparison.OrdinalIgnoreCase))
        {
            var text = ToText(value);
            if (text != null) result.Add(text);
            return result;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var child in enumerable)
                result.AddRange(GetComparableMembers(child, name));
            return result;
        }

        if (string.Equals(name, "defName", StringComparison.OrdinalIgnoreCase)
            && value is Def def)
        {
            result.Add(def.defName);
            return result;
        }

        var type = value.GetType();
        var property = type.GetProperty(name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        object member = null;
        if (property != null && property.GetIndexParameters().Length == 0)
        {
            try { member = property.GetValue(value); } catch { }
        }
        else
        {
            var field = type.GetField(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null)
            {
                try { member = field.GetValue(value); } catch { }
            }
        }

        if (member is IEnumerable memberEnumerable && member is not string)
        {
            foreach (var child in memberEnumerable)
            {
                var childText = ToText(child);
                if (childText != null) result.Add(childText);
            }
        }
        else
        {
            var text = ToText(member);
            if (text != null) result.Add(text);
        }
        return result;
    }

    private static void Flatten(object value, List<object> target)
    {
        if (value == null) return;
        if (value is StatsTarget) return;
        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var child in enumerable) Flatten(child, target);
            return;
        }
        target.Add(value);
    }

    private static bool TryConvertNumber(object value, out float number)
    {
        try
        {
            if (!IsNumericValue(value))
            {
                number = 0f;
                return false;
            }
            number = Convert.ToSingle(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            number = 0f;
            return false;
        }
    }

    private static bool IsNumericValue(object value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong
            or float or double or decimal;

    private static bool IsEnumValue(object value) =>
        value is string or Enum or Def or Type;

    private static string ToText(object value)
    {
        if (value == null) return null;
        if (value is Def def) return def.defName;
        if (value is Type type) return type.Name;
        if (value is Enum e) return e.ToString();
        if (value is string text) return text;
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static bool TryParse(string path, out Step[] steps, out string error)
    {
        if (path != null && ParsedPathCache.TryGetValue(path, out steps))
        {
            error = null;
            return true;
        }

        steps = null;
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "XPath is empty.";
            return false;
        }

        var parts = path.Trim().TrimStart('/').Split('/');
        if (parts.Length < 2
            || !string.Equals(parts[0], "Defs", StringComparison.OrdinalIgnoreCase)
            || !parts[1].StartsWith("ThingDef", StringComparison.OrdinalIgnoreCase))
        {
            error = "XPath must start with Defs/ThingDef.";
            return false;
        }

        var parsed = new List<Step>
        {
            new Step { Name = "Defs" },
        };
        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            var bracket = part.IndexOf('[');
            string name;
            string predicate = null;
            if (bracket >= 0)
            {
                if (!part.EndsWith("]") || bracket == 0)
                {
                    error = "Malformed XPath predicate.";
                    return false;
                }
                name = part.Substring(0, bracket);
                predicate = part.Substring(bracket + 1, part.Length - bracket - 2);
                if (predicate.Contains("[") || predicate.Contains("]"))
                {
                    error = "Nested XPath predicates are not supported.";
                    return false;
                }
            }
            else
            {
                name = part;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                error = "XPath contains an empty segment.";
                return false;
            }
            parsed.Add(new Step { Name = name, Predicate = predicate });
        }

        if (!string.Equals(parsed[1].Name, "ThingDef", StringComparison.OrdinalIgnoreCase))
        {
            error = "XPath must address ThingDef objects.";
            return false;
        }

        steps = parsed.ToArray();
        ParsedPathCache[path] = steps;
        return true;
    }
}
