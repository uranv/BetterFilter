using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Filters;

public enum NumericPropertyType
{
    StackLimit,
    Mass,
    MarketValue,
    Custom // set customPropPath
}

public class FilterComponent_Numeric : FilterComponent
{
    public NumericPropertyType PropertyType = NumericPropertyType.StackLimit;
    /// <summary>Reflection path for Custom property type, e.g. "recipeMaker?.displayPriority".</summary>
    public string CustomPropPath;
    /// <summary>Invalid property name from CLI -u mode. When set, filter always allows.</summary>
    public string ErrorPropName;
    public float Min;
    public float Max;
    public string MinBuffer = "";
    public string MaxBuffer = "";
    public bool IsLoose = true;
    public bool IsWhitelist = true;
    public bool PendingDelete;

    public override float BlockHeight => 52f;

    private string PropertyLabel
    {
        get
        {
            if (ErrorPropName != null)
                return "*" + ErrorPropName;
            if (PropertyType == NumericPropertyType.Custom && !string.IsNullOrEmpty(CustomPropPath))
                return CustomPropPath;
            return PropertyType switch
            {
                NumericPropertyType.StackLimit => "UR.BetterFilter.StackLimit".Translate(),
                NumericPropertyType.MarketValue => "UR.BetterFilter.MarketValue".Translate(),
                _ => "UR.BetterFilter.MassKg".Translate()
            };
        }
    }

    /// <summary>Reflection-based member-path resolver. Returns int? from a dotted path on ThingDef.</summary>
    internal static int? ResolveIntPath(ThingDef def, string path)
    {
        if (def == null || string.IsNullOrEmpty(path)) return null;
        object current = def;
        var segments = path.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            var optional = seg.EndsWith("?");
            var memberName = optional ? seg.Substring(0, seg.Length - 1) : seg;
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
                {
                }

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
    private bool HasMinLimit => !string.IsNullOrWhiteSpace(MinBuffer);
    private bool HasMaxLimit => !string.IsNullOrWhiteSpace(MaxBuffer);

    public override bool AllowsDef(ThingDef def)
    {
        if (ErrorPropName != null)
            return true;

        // Resolve custom path once, then compute value and existence
        int? customVal = null;
        if (PropertyType == NumericPropertyType.Custom && !string.IsNullOrEmpty(CustomPropPath))
            customVal = ResolveIntPath(def, CustomPropPath);

        bool hasProp;
        float? val;
        if (PropertyType == NumericPropertyType.Custom)
        {
            hasProp = customVal.HasValue;
            val = hasProp ? customVal.Value : null;
        }
        else if (PropertyType == NumericPropertyType.StackLimit)
        {
            hasProp = def.stackLimit > 0;
            val = def.stackLimit;
        }
        else if (PropertyType == NumericPropertyType.MarketValue)
        {
            hasProp = def.statBases?.Any(s => s.stat == StatDefOf.MarketValue) == true;
            val = hasProp ? def.GetStatValueAbstract(StatDefOf.MarketValue) : null;
        }
        else// if (PropertyType == NumericPropertyType.Mass)
        {
            hasProp = def.statBases?.Any(s => s.stat == StatDefOf.Mass) == true;
            val = def.BaseMass;
        }
        // else
        // {
        //     hasProp = false;
        //     val = null;
        // }

        if (!hasProp) return IsLoose;
        if (val == null) return IsLoose;

        var value = val.Value;
        if (HasMinLimit && value < Min) return !IsWhitelist;
        if (HasMaxLimit && value > Max) return !IsWhitelist;

        var inRange = (!HasMinLimit || value >= Min) && (!HasMaxLimit || value <= Max);
        return IsWhitelist ? inRange : !inRange;
    }

    public override string ToCommandLine()
    {
        var parts = new List<string> { "value", "-d" };

        // Property name
        if (ErrorPropName != null)
            parts.Add(ErrorPropName);
        else if (PropertyType == NumericPropertyType.Custom && !string.IsNullOrEmpty(CustomPropPath))
            parts.Add("'" + CustomPropPath + "'");
        else if (PropertyType == NumericPropertyType.Mass)
            parts.Add("Mass");
        else if (PropertyType == NumericPropertyType.MarketValue)
            parts.Add("MarketValue");
        else
            parts.Add("StackLimit");

        // Values
        parts.Add("-v");
        parts.Add(FormatNumericValue(MinBuffer));
        parts.Add(FormatNumericValue(MaxBuffer));

        if (!IsWhitelist) parts.Add("-b");
        if (IsLoose) parts.Add("-l");
        parts.Add("-u");
        return string.Join(" ", parts);
    }

    private static string FormatNumericValue(string buffer)
    {
        if (string.IsNullOrWhiteSpace(buffer)) return "n";
        if (float.TryParse(buffer, out var v) && v < 0f)
            return "n" + (-v).ToString(CultureInfo.InvariantCulture);
        return buffer;
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

        var inputW = 60f;
        var topRowY = inner.y + Utils.FilterDrawer.BadgeMargin;

        var oldColor = GUI.color;
        if (ErrorPropName != null)
            GUI.color = Color.gray;

        var maxLabelW = Text.CalcSize("UR.BetterFilter.MaxValue".Translate()).x;
        var maxLabelRect = new Rect(contentX, topRowY - 3f, maxLabelW, 20f);
        var maxInputRect = new Rect(maxLabelRect.xMax + 4f, topRowY - 5f, inputW, 22f);
        var prevMax = Max;
        var prevMaxBuf = MaxBuffer;
        Widgets.Label(maxLabelRect, "UR.BetterFilter.MaxValue".Translate());
        Widgets.TextFieldNumeric(maxInputRect, ref Max, ref MaxBuffer,
            float.MinValue, float.MaxValue);

        var bottomRowY = inner.yMax - Utils.FilterDrawer.BtnSize;
        var minLabelW = Text.CalcSize("UR.BetterFilter.MinValue".Translate()).x;
        var minLabelRect = new Rect(contentX, bottomRowY - 2f, minLabelW, 20f);
        var minInputRect = new Rect(minLabelRect.xMax + 4f, bottomRowY - 4f, inputW, 22f);
        var prevMin = Min;
        var prevMinBuf = MinBuffer;
        Widgets.Label(minLabelRect, "UR.BetterFilter.MinValue".Translate());
        Widgets.TextFieldNumeric(minInputRect, ref Min, ref MinBuffer,
            float.MinValue, float.MaxValue);

        if (HasMaxLimit && HasMinLimit && Max < Min)
            Max = Min;

        if (!Mathf.Approximately(Min, prevMin) || !Mathf.Approximately(Max, prevMax)
                                               || MinBuffer != prevMinBuf || MaxBuffer != prevMaxBuf)
            dialog.MarkDirty();

        var propTextW = Text.CalcSize(PropertyLabel).x;
        var gap = Utils.FilterDrawer.BtnGap;
        var propTextX = inner.xMax - Utils.FilterDrawer.BtnSize - gap - propTextW;
        var propTextRect = new Rect(propTextX, bottomRowY - 1f, propTextW, 20f);
        Widgets.Label(propTextRect, PropertyLabel);

        GUI.color = oldColor;

        Utils.FilterDrawer.DrawPlusBottomRight(inner, () =>
        {
            var opts = new List<FloatMenuOption>
            {
                new FloatMenuOption("UR.BetterFilter.StackLimit".Translate(),
                    () => { PropertyType = NumericPropertyType.StackLimit; CustomPropPath = null; dialog.MarkDirty(); }),
                new FloatMenuOption("UR.BetterFilter.MassKg".Translate(),
                    () => { PropertyType = NumericPropertyType.Mass; CustomPropPath = null; dialog.MarkDirty(); }),
                new FloatMenuOption("UR.BetterFilter.MarketValue".Translate(),
                    () => { PropertyType = NumericPropertyType.MarketValue; CustomPropPath = null; dialog.MarkDirty(); })
            };
            Find.WindowStack.Add(new FloatMenu(opts));
        }, "UR.BetterFilter.SelectPropertyTooltip");
    }
}
