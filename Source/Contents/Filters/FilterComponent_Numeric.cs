using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BetterFilter.Contents.Utils;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Filters;

public class FilterComponent_Numeric : FilterComponent
{
    public string TargetPath = "Defs/ThingDef/stackLimit";
    public string TargetName;
    public string ErrorTargetPath;
    public float Min;
    public float Max;
    public string MinBuffer = "";
    public string MaxBuffer = "";
    public bool IsLoose = true;
    public bool IsWhitelist = true;
    public bool PendingDelete;

    private bool HasMinLimit => !string.IsNullOrWhiteSpace(MinBuffer);
    private bool HasMaxLimit => !string.IsNullOrWhiteSpace(MaxBuffer);

    private string TargetDisplayText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ErrorTargetPath))
                return "*" + ErrorTargetPath;
            var target = FilterTargetCatalog.Find(FilterTargetKind.Numeric, TargetPath, TargetName);
            return target == null ? TargetPath : FilterTargetCatalog.DisplayName(target);
        }
    }

    public override float BlockHeight => 52f;

    public override bool AllowsDef(ThingDef def)
    {
        if (!string.IsNullOrWhiteSpace(ErrorTargetPath)
            || !RuntimeFilterPath.IsValidSyntax(TargetPath))
            return true;

        if (!RuntimeFilterPath.TryGetNumericValues(def, TargetPath, out var values, out _)
            || values.Count == 0)
            return IsLoose;

        var matches = values.Any(IsInRange);
        return IsWhitelist ? matches : !matches;
    }

    private bool IsInRange(float value) =>
        (!HasMinLimit || value >= Min) && (!HasMaxLimit || value <= Max);

    public override string ToCommandLine()
    {
        var parts = new List<string>
        {
            "value", "-d", "'" + (ErrorTargetPath ?? TargetPath) + "'", "-v",
            FormatNumericValue(MinBuffer), FormatNumericValue(MaxBuffer)
        };
        if (!IsWhitelist) parts.Add("-b");
        if (IsLoose) parts.Add("-l");
        parts.Add("-u");
        return string.Join(" ", parts);
    }

    private static string FormatNumericValue(string buffer)
    {
        if (string.IsNullOrWhiteSpace(buffer)) return "n";
        if (float.TryParse(buffer, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            && v < 0f)
            return "n" + (-v).ToString(CultureInfo.InvariantCulture);
        return buffer;
    }

    public override void Draw(Rect rect, Dialog.Dialog_AdvancedFilters dialog, int index)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.18f, 0.18f, 0.2f));
        var inner = rect.ContractedBy(4f);

        var contentX = FilterDrawer.DrawBadge(inner, IsWhitelist, IsLoose,
            () => { IsWhitelist = !IsWhitelist; dialog.MarkDirty(); },
            "UR.BetterFilter.WhitelistNumericTooltip", "UR.BetterFilter.BlacklistNumericTooltip");
        FilterDrawer.DrawOptionTopRight(inner, IsLoose,
            () => { IsLoose = !IsLoose; dialog.MarkDirty(); });
        FilterDrawer.DrawDeleteTopRight(inner, ref PendingDelete);

        var inputW = 60f;
        var topRowY = inner.y + FilterDrawer.BadgeMargin;
        var maxLabelW = Text.CalcSize("UR.BetterFilter.MaxValue".Translate()).x;
        var maxLabelRect = new Rect(contentX, topRowY - 3f, maxLabelW, 20f);
        var maxInputRect = new Rect(maxLabelRect.xMax + 4f, topRowY - 5f, inputW, 22f);
        var prevMax = Max;
        var prevMaxBuf = MaxBuffer;
        Widgets.Label(maxLabelRect, "UR.BetterFilter.MaxValue".Translate());
        Widgets.TextFieldNumeric(maxInputRect, ref Max, ref MaxBuffer,
            float.MinValue, float.MaxValue);

        var bottomRowY = inner.yMax - FilterDrawer.BtnSize;
        var minLabelW = Text.CalcSize("UR.BetterFilter.MinValue".Translate()).x;
        var minLabelRect = new Rect(contentX, bottomRowY - 2f, minLabelW, 20f);
        var minInputRect = new Rect(minLabelRect.xMax + 4f, bottomRowY - 4f, inputW, 22f);
        var prevMin = Min;
        var prevMinBuf = MinBuffer;
        Widgets.Label(minLabelRect, "UR.BetterFilter.MinValue".Translate());
        Widgets.TextFieldNumeric(minInputRect, ref Min, ref MinBuffer,
            float.MinValue, float.MaxValue);

        if (HasMaxLimit && HasMinLimit && Max < Min) Max = Min;
        if (!Mathf.Approximately(Min, prevMin) || !Mathf.Approximately(Max, prevMax)
            || MinBuffer != prevMinBuf || MaxBuffer != prevMaxBuf)
            dialog.MarkDirty();

        var targetText = TargetDisplayText;
        var oldColor = GUI.color;
        if (string.IsNullOrWhiteSpace(ErrorTargetPath))
            GUI.color = Color.white;
        var targetW = Text.CalcSize(targetText).x;
        var targetX = inner.xMax - FilterDrawer.BtnSize - FilterDrawer.BtnGap - targetW;
        Widgets.Label(new Rect(targetX, bottomRowY - 1f, targetW, 20f), targetText);
        GUI.color = oldColor;

        FilterDrawer.DrawPlusBottomRight(inner, () =>
        {
            var items = FilterTargetCatalog.Get(FilterTargetKind.Numeric)
                .Select(target => (FilterTargetCatalog.DisplayName(target), (Action)(() =>
                {
                    TargetPath = target.XPath;
                    TargetName = target.Name;
                    ErrorTargetPath = null;
                    dialog.MarkDirty();
                }))).ToList();
            Find.WindowStack.Add(new Dialog.Dialog_SearchableSelect(
                "UR.BetterFilter.NumericFilter".Translate(), items));
        }, "UR.BetterFilter.SelectPropertyTooltip");
    }
}
