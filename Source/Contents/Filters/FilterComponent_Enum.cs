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
    public EnumPropertyType? propertyType;
    public List<string> selectedValues = new();
    /// <summary>Invalid property name from CLI -u mode. When set, filter always allows.</summary>
    public string errorPropertyName;
    /// <summary>Invalid enum values from CLI -u mode, displayed in gray.</summary>
    public List<string> errorValues = new();
    public bool isWhitelist = true;
    public bool isLoose = false;
    public bool pendingDelete;

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

    private static readonly Dictionary<EnumPropertyType, List<string>> _allValuesCache = new();
    private static int _allValuesCacheVersion = -1;

    internal static List<string> GetAllValues(EnumPropertyType p)
    {
        int currentVersion = DefDatabase<ThingDef>.AllDefsListForReading.Count;
        if (_allValuesCacheVersion != currentVersion)
        {
            _allValuesCache.Clear();
            _allValuesCacheVersion = currentVersion;
        }
        if (_allValuesCache.TryGetValue(p, out var cached))
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
        _allValuesCache[p] = result;
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
            if (propertyType != null)
                return PropertyLabel(propertyType.Value) + ":";
            if (errorPropertyName != null)
                return "*" + errorPropertyName + ":";
            return "UR.BetterFilter.SelectEnumProperty".Translate();
        }
    }

    private string ValidValuesText
    {
        get
        {
            if (propertyType == null || selectedValues.Count == 0) return "";
            var pt = propertyType.Value;
            return string.Join(", ", selectedValues.Select(v => ValueLabel(pt, v)));
        }
    }

    private string ErrorValuesText =>
        errorValues.Count == 0 ? "" : string.Join(", ", errorValues.Select(v => "*" + v));

    private string FullDisplayText
    {
        get
        {
            var parts = new List<string>();
            string prop = PropDisplayText;
            if (!string.IsNullOrEmpty(prop)) parts.Add(prop);
            string vals = ValidValuesText;
            if (!string.IsNullOrEmpty(vals)) parts.Add(vals);
            string errs = ErrorValuesText;
            if (!string.IsNullOrEmpty(errs)) parts.Add(errs);
            return string.Join(" ", parts);
        }
    }

    public override float BlockHeight =>
        Utils.FilterDrawer.MinBlockHeight(FullDisplayText, LayoutWidth);

    public override bool AllowsDef(ThingDef def)
    {
        if (propertyType == null)
            return true;

        var pt = propertyType.Value;
        if (!HasProperty(def, pt))
            return isLoose;

        if (selectedValues.Count == 0)
            return true;

        var defVals = GetDefValues(def, pt);
        bool matchesAny = defVals.Any(v => selectedValues.Contains(v));
        return isWhitelist ? matchesAny : !matchesAny;
    }

    public override string ToCommandLine()
    {
        var parts = new List<string> { "enum", "-d" };

        // Property name
        if (errorPropertyName != null)
            parts.Add(errorPropertyName);
        else if (propertyType != null)
            parts.Add(propertyType.Value.ToString());
        else
            return ""; // No property selected, can't serialize — skip

        // Values
        if (selectedValues.Count > 0 || errorValues.Count > 0)
        {
            parts.Add("-v");
            parts.AddRange(selectedValues);
            parts.AddRange(errorValues);
        }

        if (!isWhitelist) parts.Add("-b");
        if (isLoose) parts.Add("-l");
        parts.Add("-u");
        return string.Join(" ", parts);
    }

    public override void Draw(Rect rect, Dialog.Dialog_AdvancedFilters dialog, int index)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.18f, 0.18f, 0.2f));
        Rect inner = rect.ContractedBy(4f);

        float contentX = Utils.FilterDrawer.DrawBadge(inner, isWhitelist, isLoose,
            () => { isWhitelist = !isWhitelist; dialog.MarkDirty(); },
            "UR.BetterFilter.WhitelistNumericTooltip", "UR.BetterFilter.BlacklistNumericTooltip");

        Utils.FilterDrawer.DrawOptionTopRight(inner, isLoose,
            () => { isLoose = !isLoose; dialog.MarkDirty(); });
        Utils.FilterDrawer.DrawDeleteTopRight(inner, ref pendingDelete);

        // Draw text with gray error rendering
        float textMaxW = Utils.FilterDrawer.TextMaxWidth(inner, contentX);
        float topY = inner.y + Utils.FilterDrawer.BadgeMargin;
        Color oldColor = GUI.color;

        // Build segments: normal text vs gray error text
        bool hasErrorProp = errorPropertyName != null && propertyType == null;
        bool hasErrVals = errorValues.Count > 0;
        bool hasValVals = propertyType != null && selectedValues.Count > 0;

        if (hasErrorProp)
        {
            // Property name is invalid → draw all in gray
            GUI.color = Color.gray;
            string full = PropDisplayText;
            if (hasValVals) full += " " + ValidValuesText;
            if (hasErrVals) full += " " + ErrorValuesText;
            float h = Text.CalcHeight(full, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, h), full);
            GUI.color = oldColor;
        }
        else if (hasValVals && hasErrVals)
        {
            // Both valid and error values
            string prop = PropDisplayText;
            float propW = Text.CalcSize(prop).x;
            Widgets.Label(new Rect(contentX, topY, textMaxW, 20f), prop);
            float afterX = contentX + propW + 4f;

            string valText = ValidValuesText;
            float valW = Text.CalcSize(valText).x;
            float lineH = Text.CalcHeight(valText, textMaxW - (afterX - contentX));
            Widgets.Label(new Rect(afterX, topY, textMaxW - (afterX - contentX), lineH), valText);
            afterX += valW + 4f;

            GUI.color = Color.gray;
            string errText = ErrorValuesText;
            float errW = textMaxW - (afterX - contentX);
            if (errW < 40f) { afterX = contentX; topY += lineH; errW = textMaxW; }
            Widgets.Label(new Rect(afterX, topY, errW, lineH), errText);
            GUI.color = oldColor;
        }
        else if (propertyType == null && errorPropertyName == null)
        {
            // No property selected yet
            float h = Text.CalcHeight(FullDisplayText, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, h), FullDisplayText);
        }
        else if (hasErrVals)
        {
            // Only error values (no valid values)
            string prop = PropDisplayText;
            float lineH = Text.CalcHeight(prop, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, lineH), prop);
            float afterX = contentX + Text.CalcSize(prop).x + 4f;
            GUI.color = Color.gray;
            float errW = textMaxW - (afterX - contentX);
            if (errW < 40f) { afterX = contentX; errW = textMaxW; }
            Widgets.Label(new Rect(afterX, topY, errW, lineH), ErrorValuesText);
            GUI.color = oldColor;
        }
        else
        {
            // Normal: valid property, valid values (or no values)
            float h = Text.CalcHeight(FullDisplayText, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, h), FullDisplayText);
        }

        Utils.FilterDrawer.DrawMinusBottomRight(inner, selectedValues.Count > 0,
            () => { selectedValues.RemoveAt(selectedValues.Count - 1); dialog.MarkDirty(); });
        Utils.FilterDrawer.DrawPlusBottomRight(inner, () =>
        {
            if (propertyType == null && errorPropertyName == null)
            {
                var opts = Enum.GetValues(typeof(EnumPropertyType))
                    .Cast<EnumPropertyType>()
                    .Select(e => new FloatMenuOption(PropertyLabel(e),
                        () => { propertyType = e; dialog.MarkDirty(); }))
                    .ToList();
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            else if (propertyType != null)
            {
                var pt = propertyType.Value;
                var existing = new HashSet<string>(selectedValues);
                var opts = GetAllValues(pt)
                    .Where(v => !existing.Contains(v))
                    .Select(v => new FloatMenuOption(ValueLabel(pt, v),
                        () => { selectedValues.Add(v); dialog.MarkDirty(); }))
                    .ToList();
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }, "UR.BetterFilter.SelectEnumValueTooltip");
    }
}
