using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Filters;

public enum NumericPropertyType
{
    StackLimit,
    Mass,
    Custom // set customPropPath
}

public class FilterComponent_Numeric : FilterComponent
{
    public NumericPropertyType propertyType = NumericPropertyType.StackLimit;
    /// <summary>Reflection path for Custom property type, e.g. "recipeMaker?.displayPriority".</summary>
    public string customPropPath;
    /// <summary>Invalid property name from CLI -u mode. When set, filter always allows.</summary>
    public string errorPropName;
    public float min;
    public float max;
    public string minBuffer = "";
    public string maxBuffer = "";
    public bool isLoose = true;
    public bool isWhitelist = true;
    public bool pendingDelete;

    public override float BlockHeight => 52f;

    private string PropertyLabel
    {
        get
        {
            if (errorPropName != null)
                return "*" + errorPropName;
            if (propertyType == NumericPropertyType.Custom && !string.IsNullOrEmpty(customPropPath))
                return customPropPath;
            return propertyType == NumericPropertyType.StackLimit
                ? "UR.BetterFilter.StackLimit".Translate()
                : "UR.BetterFilter.MassKg".Translate();
        }
    }

    /// <summary>Reflection-based member-path resolver. Returns int? from a dotted path on ThingDef.</summary>
    internal static int? ResolveIntPath(ThingDef def, string path)
    {
        if (def == null || string.IsNullOrEmpty(path)) return null;
        object current = def;
        string[] segments = path.Split('.');
        for (int i = 0; i < segments.Length; i++)
        {
            string seg = segments[i];
            bool optional = seg.EndsWith("?");
            string memberName = optional ? seg.Substring(0, seg.Length - 1) : seg;
            var type = current.GetType();
            // Try property first, then field (RimWorld often uses public fields)
            var member = (MemberInfo)type.GetProperty(memberName,
                BindingFlags.Public | BindingFlags.Instance)
                ?? type.GetField(memberName,
                BindingFlags.Public | BindingFlags.Instance);
            if (member == null) return null;
            current = member is PropertyInfo pi ? pi.GetValue(current) : ((FieldInfo)member).GetValue(current);
            if (current == null)
            {
                if (optional && i < segments.Length - 1)
                    return null;
                return null;
            }
        }
        if (current is int i32) return i32;
        if (current is long i64) return (int)i64;
        if (current is float f) return (int)f;
        if (current is double d) return (int)d;
        return null;
    }

    // Empty buffer = no limit; non-empty = limit applies.
    private bool HasMinLimit => !string.IsNullOrWhiteSpace(minBuffer);
    private bool HasMaxLimit => !string.IsNullOrWhiteSpace(maxBuffer);

    public override bool AllowsDef(ThingDef def)
    {
        if (errorPropName != null)
            return true;

        // Resolve custom path once, then compute value and existence
        int? customVal = null;
        if (propertyType == NumericPropertyType.Custom && !string.IsNullOrEmpty(customPropPath))
            customVal = ResolveIntPath(def, customPropPath);

        bool hasProp;
        float? val;
        if (propertyType == NumericPropertyType.Custom)
        {
            hasProp = customVal.HasValue;
            val = hasProp ? (float?)customVal.Value : null;
        }
        else if (propertyType == NumericPropertyType.StackLimit)
        {
            hasProp = def.stackLimit > 0;
            val = def.stackLimit;
        }
        else
        {
            hasProp = def.statBases?.Any(s => s.stat == StatDefOf.Mass) == true;
            val = def.BaseMass;
        }

        if (!hasProp) return isLoose;
        if (val == null) return isLoose;

        float value = val.Value;
        if (HasMinLimit && value < min) return !isWhitelist;
        if (HasMaxLimit && value > max) return !isWhitelist;

        bool inRange = (!HasMinLimit || value >= min) && (!HasMaxLimit || value <= max);
        return isWhitelist ? inRange : !inRange;
    }

    public override string ToCommandLine()
    {
        var parts = new List<string> { "value", "-d" };

        // Property name
        if (errorPropName != null)
            parts.Add(errorPropName);
        else if (propertyType == NumericPropertyType.Custom && !string.IsNullOrEmpty(customPropPath))
            parts.Add("'" + customPropPath + "'");
        else if (propertyType == NumericPropertyType.Mass)
            parts.Add("Mass");
        else
            parts.Add("StackLimit");

        // Values
        parts.Add("-v");
        parts.Add(FormatNumericValue(minBuffer));
        parts.Add(FormatNumericValue(maxBuffer));

        if (!isWhitelist) parts.Add("-b");
        if (isLoose) parts.Add("-l");
        parts.Add("-u");
        return string.Join(" ", parts);
    }

    private static string FormatNumericValue(string buffer)
    {
        if (string.IsNullOrWhiteSpace(buffer)) return "n";
        if (float.TryParse(buffer, out float v) && v < 0f)
            return "n" + (-v).ToString();
        return buffer;
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

        float inputW = 60f;
        float topRowY = inner.y + Utils.FilterDrawer.BadgeMargin;

        Color oldColor = GUI.color;
        if (errorPropName != null)
            GUI.color = Color.gray;

        float maxLabelW = Text.CalcSize("UR.BetterFilter.MaxValue".Translate()).x;
        Rect maxLabelRect = new Rect(contentX, topRowY - 3f, maxLabelW, 20f);
        Rect maxInputRect = new Rect(maxLabelRect.xMax + 4f, topRowY - 5f, inputW, 22f);
        float prevMax = max;
        string prevMaxBuf = maxBuffer;
        Widgets.Label(maxLabelRect, "UR.BetterFilter.MaxValue".Translate());
        Widgets.TextFieldNumeric(maxInputRect, ref max, ref maxBuffer,
            float.MinValue, float.MaxValue);

        float bottomRowY = inner.yMax - Utils.FilterDrawer.BtnSize;
        float minLabelW = Text.CalcSize("UR.BetterFilter.MinValue".Translate()).x;
        Rect minLabelRect = new Rect(contentX, bottomRowY - 2f, minLabelW, 20f);
        Rect minInputRect = new Rect(minLabelRect.xMax + 4f, bottomRowY - 4f, inputW, 22f);
        float prevMin = min;
        string prevMinBuf = minBuffer;
        Widgets.Label(minLabelRect, "UR.BetterFilter.MinValue".Translate());
        Widgets.TextFieldNumeric(minInputRect, ref min, ref minBuffer,
            float.MinValue, float.MaxValue);

        if (HasMaxLimit && HasMinLimit && max < min)
            max = min;

        if (min != prevMin || max != prevMax
            || minBuffer != prevMinBuf || maxBuffer != prevMaxBuf)
            dialog.MarkDirty();

        float propTextW = Text.CalcSize(PropertyLabel).x;
        float gap = Utils.FilterDrawer.BtnGap;
        float propTextX = inner.xMax - Utils.FilterDrawer.BtnSize - gap - propTextW;
        Rect propTextRect = new Rect(propTextX, bottomRowY - 1f, propTextW, 20f);
        Widgets.Label(propTextRect, PropertyLabel);

        GUI.color = oldColor;

        Utils.FilterDrawer.DrawPlusBottomRight(inner, () =>
        {
            var opts = new List<FloatMenuOption>
            {
                new FloatMenuOption("UR.BetterFilter.StackLimit".Translate(),
                    () => { propertyType = NumericPropertyType.StackLimit; customPropPath = null; dialog.MarkDirty(); }),
                new FloatMenuOption("UR.BetterFilter.MassKg".Translate(),
                    () => { propertyType = NumericPropertyType.Mass; customPropPath = null; dialog.MarkDirty(); })
            };
            Find.WindowStack.Add(new FloatMenu(opts));
        }, "UR.BetterFilter.SelectPropertyTooltip");
    }
}
