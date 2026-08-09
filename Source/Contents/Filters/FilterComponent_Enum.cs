using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Filters;

public enum EnumPropertyType
{
    WeaponTags,
    TradeTags,
    TechLevel,
    ApparelTags,
    ApparelLayer,
    BodyCoverage
}

public class FilterComponent_Enum : FilterComponent
{
    public EnumPropertyType? PropertyType;
    public List<string> SelectedValues = new();
    /// <summary>Invalid property name from CLI -u mode. When set, filter always allows.</summary>
    public string ErrorPropertyName;
    /// <summary>Invalid enum values from CLI -u mode, displayed in gray.</summary>
    public List<string> ErrorValues = new();
    public bool IsWhitelist = true;
    public bool IsLoose;
    public bool PendingDelete;

    private static string PropertyLabel(EnumPropertyType p) => p switch
    {
        EnumPropertyType.WeaponTags   => "UR.BetterFilter.WeaponTags".Translate(),
        EnumPropertyType.TradeTags    => "UR.BetterFilter.TradeTags".Translate(),
        EnumPropertyType.TechLevel    => "UR.BetterFilter.TechLevel".Translate(),
        EnumPropertyType.ApparelTags  => "UR.BetterFilter.ApparelTags".Translate(),
        EnumPropertyType.ApparelLayer => "UR.BetterFilter.ApparelLayer".Translate(),
        EnumPropertyType.BodyCoverage => "UR.BetterFilter.BodyCoverage".Translate(),
        _ => p.ToString()
    };

    private static List<string> GetDefValues(ThingDef def, EnumPropertyType p) => p switch
    {
        EnumPropertyType.WeaponTags   => def.weaponTags ?? new List<string>(),
        EnumPropertyType.TradeTags    => def.tradeTags ?? new List<string>(),
        EnumPropertyType.TechLevel    => def.techLevel == TechLevel.Undefined
            ? new List<string>() : new List<string> { def.techLevel.ToString() },
        EnumPropertyType.ApparelTags  => def.apparel?.tags ?? new List<string>(),
        EnumPropertyType.ApparelLayer => def.apparel?.layers?.Select(l => l.defName).ToList() ?? new List<string>(),
        EnumPropertyType.BodyCoverage => def.apparel?.bodyPartGroups?.Select(b => b.defName).ToList() ?? new List<string>(),
        _ => new List<string>()
    };

    private static readonly Dictionary<EnumPropertyType, List<string>> AllValuesCache = new();
    private static int _allValuesCacheVersion = -1;

    internal static List<string> GetAllValues(EnumPropertyType p)
    {
        var currentVersion = DefDatabase<ThingDef>.AllDefsListForReading.Count;
        if (_allValuesCacheVersion != currentVersion)
        {
            AllValuesCache.Clear();
            _allValuesCacheVersion = currentVersion;
        }
        if (AllValuesCache.TryGetValue(p, out var cached))
            return cached;

        List<string> result;
        switch (p)
        {
            case EnumPropertyType.WeaponTags:
                result = DefDatabase<ThingDef>.AllDefsListForReading
                    .SelectMany(d => d.weaponTags ?? Enumerable.Empty<string>())
                    .Distinct().OrderBy(s => s).ToList();
                break;
            case EnumPropertyType.TradeTags:
                result = DefDatabase<ThingDef>.AllDefsListForReading
                    .SelectMany(d => d.tradeTags ?? Enumerable.Empty<string>())
                    .Distinct().OrderBy(s => s).ToList();
                break;
            case EnumPropertyType.TechLevel:
                result = Enum.GetNames(typeof(TechLevel))
                    .Where(n => n != "Undefined").OrderBy(s => s).ToList();
                break;
            case EnumPropertyType.ApparelTags:
                result = DefDatabase<ThingDef>.AllDefsListForReading
                    .SelectMany(d => d.apparel?.tags ?? Enumerable.Empty<string>())
                    .Distinct().OrderBy(s => s).ToList();
                break;
            case EnumPropertyType.ApparelLayer:
                result = DefDatabase<ThingDef>.AllDefsListForReading
                    .SelectMany(d => d.apparel?.layers ?? Enumerable.Empty<ApparelLayerDef>())
                    .Select(l => l.defName).Distinct().OrderBy(s => s).ToList();
                break;
            case EnumPropertyType.BodyCoverage:
                result = DefDatabase<ThingDef>.AllDefsListForReading
                    .SelectMany(d => d.apparel?.bodyPartGroups ?? Enumerable.Empty<BodyPartGroupDef>())
                    .Select(b => b.defName).Distinct().OrderBy(s => s).ToList();
                break;
            default:
                result = new List<string>();
                break;
        }
        AllValuesCache[p] = result;
        return result;
    }

    private static string ValueLabel(EnumPropertyType p, string raw) => p switch
    {
        EnumPropertyType.ApparelLayer => DefDatabase<ApparelLayerDef>.GetNamed(raw)?.LabelCap ?? raw,
        EnumPropertyType.BodyCoverage => DefDatabase<BodyPartGroupDef>.GetNamed(raw)?.LabelCap ?? raw,
        _ => raw
    };

    private static bool HasProperty(ThingDef def, EnumPropertyType p) => p switch
    {
        EnumPropertyType.WeaponTags   => def.weaponTags?.Count > 0,
        EnumPropertyType.TradeTags    => def.tradeTags?.Count > 0,
        EnumPropertyType.TechLevel    => def.techLevel != TechLevel.Undefined,
        EnumPropertyType.ApparelTags  => def.apparel?.tags?.Count > 0,
        EnumPropertyType.ApparelLayer => def.apparel?.layers?.Count > 0,
        EnumPropertyType.BodyCoverage => def.apparel?.bodyPartGroups?.Count > 0,
        _ => false
    };

    private string PropDisplayText
    {
        get
        {
            if (PropertyType != null)
                return PropertyLabel(PropertyType.Value) + ":";
            if (ErrorPropertyName != null)
                return "*" + ErrorPropertyName + ":";
            return "UR.BetterFilter.SelectEnumProperty".Translate();
        }
    }

    private string ValidValuesText
    {
        get
        {
            if (PropertyType == null || SelectedValues.Count == 0) return "";
            var pt = PropertyType.Value;
            return string.Join(", ", SelectedValues.Select(v => ValueLabel(pt, v)));
        }
    }

    private string ErrorValuesText =>
        ErrorValues.Count == 0 ? "" : string.Join(", ", ErrorValues.Select(v => "*" + v));

    private string FullDisplayText
    {
        get
        {
            var parts = new List<string>();
            var prop = PropDisplayText;
            if (!string.IsNullOrEmpty(prop)) parts.Add(prop);
            var vals = ValidValuesText;
            if (!string.IsNullOrEmpty(vals)) parts.Add(vals);
            var errs = ErrorValuesText;
            if (!string.IsNullOrEmpty(errs)) parts.Add(errs);
            return string.Join(" ", parts);
        }
    }

    public override float BlockHeight =>
        Utils.FilterDrawer.MinBlockHeight(FullDisplayText, LayoutWidth);

    public override bool AllowsDef(ThingDef def)
    {
        if (PropertyType == null)
            return true;

        var pt = PropertyType.Value;
        if (!HasProperty(def, pt))
            return IsLoose;

        if (SelectedValues.Count == 0)
            return true;

        var defVals = GetDefValues(def, pt);
        var matchesAny = defVals.Any(v => SelectedValues.Contains(v));
        return IsWhitelist ? matchesAny : !matchesAny;
    }

    public override string ToCommandLine()
    {
        var parts = new List<string> { "enum", "-d" };

        // Property name
        if (ErrorPropertyName != null)
            parts.Add(ErrorPropertyName);
        else if (PropertyType != null)
            parts.Add(PropertyType.Value.ToString());
        else
            return ""; // No property selected, can't serialize — skip

        // Values
        if (SelectedValues.Count > 0 || ErrorValues.Count > 0)
        {
            parts.Add("-v");
            parts.AddRange(SelectedValues);
            parts.AddRange(ErrorValues);
        }

        if (!IsWhitelist) parts.Add("-b");
        if (IsLoose) parts.Add("-l");
        parts.Add("-u");
        return string.Join(" ", parts);
    }

    public override void Draw(Rect rect, Dialog.Dialog_AdvancedFilters dialog, int index)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.18f, 0.18f, 0.2f));
        var inner = rect.ContractedBy(4f);

        var contentX = Utils.FilterDrawer.DrawBadge(inner, IsWhitelist, IsLoose,
            () => { IsWhitelist = !IsWhitelist; dialog.MarkDirty(); },
            "UR.BetterFilter.WhitelistNumericTooltip", "UR.BetterFilter.BlacklistNumericTooltip");

        Utils.FilterDrawer.DrawOptionTopRight(inner, IsLoose,
            () => { IsLoose = !IsLoose; dialog.MarkDirty(); });
        Utils.FilterDrawer.DrawDeleteTopRight(inner, ref PendingDelete);

        // Draw text with gray error rendering
        var textMaxW = Utils.FilterDrawer.TextMaxWidth(inner, contentX);
        var topY = inner.y + Utils.FilterDrawer.BadgeMargin;
        var oldColor = GUI.color;

        // Build segments: normal text vs gray error text
        var hasErrorProp = ErrorPropertyName != null && PropertyType == null;
        var hasErrVals = ErrorValues.Count > 0;
        var hasValVals = PropertyType != null && SelectedValues.Count > 0;

        if (hasErrorProp)
        {
            // Property name is invalid → draw all in gray
            GUI.color = Color.gray;
            var full = PropDisplayText;
            if (hasValVals) full += " " + ValidValuesText;
            if (hasErrVals) full += " " + ErrorValuesText;
            var h = Text.CalcHeight(full, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, h), full);
            GUI.color = oldColor;
        }
        else if (hasValVals && hasErrVals)
        {
            // Both valid and error values
            var prop = PropDisplayText;
            var propW = Text.CalcSize(prop).x;
            Widgets.Label(new Rect(contentX, topY, textMaxW, 20f), prop);
            var afterX = contentX + propW + 4f;

            var valText = ValidValuesText;
            var valW = Text.CalcSize(valText).x;
            var lineH = Text.CalcHeight(valText, textMaxW - (afterX - contentX));
            Widgets.Label(new Rect(afterX, topY, textMaxW - (afterX - contentX), lineH), valText);
            afterX += valW + 4f;

            GUI.color = Color.gray;
            var errText = ErrorValuesText;
            var errW = textMaxW - (afterX - contentX);
            if (errW < 40f) { afterX = contentX; topY += lineH; errW = textMaxW; }
            Widgets.Label(new Rect(afterX, topY, errW, lineH), errText);
            GUI.color = oldColor;
        }
        else if (PropertyType == null && ErrorPropertyName == null)
        {
            // No property selected yet
            var h = Text.CalcHeight(FullDisplayText, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, h), FullDisplayText);
        }
        else if (hasErrVals)
        {
            // Only error values (no valid values)
            var prop = PropDisplayText;
            var lineH = Text.CalcHeight(prop, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, lineH), prop);
            var afterX = contentX + Text.CalcSize(prop).x + 4f;
            GUI.color = Color.gray;
            var errW = textMaxW - (afterX - contentX);
            if (errW < 40f) { afterX = contentX; errW = textMaxW; }
            Widgets.Label(new Rect(afterX, topY, errW, lineH), ErrorValuesText);
            GUI.color = oldColor;
        }
        else
        {
            // Normal: valid property, valid values (or no values)
            var h = Text.CalcHeight(FullDisplayText, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, h), FullDisplayText);
        }

        Utils.FilterDrawer.DrawMinusBottomRight(inner, SelectedValues.Count > 0,
            () => { SelectedValues.RemoveAt(SelectedValues.Count - 1); dialog.MarkDirty(); });
        Utils.FilterDrawer.DrawPlusBottomRight(inner, () =>
        {
            if (PropertyType == null && ErrorPropertyName == null)
            {
                var items = Enum.GetValues(typeof(EnumPropertyType))
                    .Cast<EnumPropertyType>()
                    .Select(e => (PropertyLabel(e), (Action)(() =>
                        { PropertyType = e; dialog.MarkDirty(); })))
                    .ToList();
                Find.WindowStack.Add(new Dialog.Dialog_SearchableSelect(
                    "UR.BetterFilter.EnumFilter".Translate(), items));
            }
            else if (PropertyType != null)
            {
                var pt = PropertyType.Value;
                var existing = new HashSet<string>(SelectedValues);
                var items = GetAllValues(pt)
                    .Where(v => !existing.Contains(v))
                    .Select(v => (ValueLabel(pt, v), (Action)(() =>
                        { SelectedValues.Add(v); dialog.MarkDirty(); })))
                    .ToList();
                Find.WindowStack.Add(new Dialog.Dialog_SearchableSelect(
                    PropertyLabel(pt), items));
            }
        }, "UR.BetterFilter.SelectEnumValueTooltip");
    }
}
