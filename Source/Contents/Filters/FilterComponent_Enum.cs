using System;
using System.Collections.Generic;
using System.Linq;
using BetterFilter.Contents.Utils;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Filters;

public class FilterComponent_Enum : FilterComponent
{
    public string TargetPath = "Defs/ThingDef/weaponTags/li";
    public string TargetName;
    public string ErrorTargetPath;
    public List<string> SelectedValues = new();
    public List<string> ErrorValues = new();
    public bool IsWhitelist = true;
    public bool IsLoose;
    public bool PendingDelete;

    private string TargetDisplayText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ErrorTargetPath))
                return "*" + ErrorTargetPath;
            var target = FilterTargetCatalog.Find(FilterTargetKind.Enum, TargetPath, TargetName);
            return target == null ? TargetPath : FilterTargetCatalog.DisplayName(target);
        }
    }

    private string ValuesDisplayText => string.Join(", ", SelectedValues);
    private string ErrorValuesDisplayText =>
        string.Join(", ", ErrorValues.Select(v => "*" + v));

    private string FullDisplayText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(TargetDisplayText)) parts.Add(TargetDisplayText);
            if (!string.IsNullOrEmpty(ValuesDisplayText)) parts.Add(ValuesDisplayText);
            if (!string.IsNullOrEmpty(ErrorValuesDisplayText)) parts.Add(ErrorValuesDisplayText);
            return string.Join(" ", parts);
        }
    }

    public override float BlockHeight => 76f;

    public override bool AllowsDef(ThingDef def)
    {
        if (!string.IsNullOrWhiteSpace(ErrorTargetPath)
            || !RuntimeFilterPath.IsValidSyntax(TargetPath))
            return true;
        if (!RuntimeFilterPath.TryGetTextValues(def, TargetPath, out var values, out _)
            || values.Count == 0)
            return IsLoose;
        if (SelectedValues.Count == 0) return true;

        var matches = values.Any(v => SelectedValues.Contains(v, StringComparer.Ordinal));
        return IsWhitelist ? matches : !matches;
    }

    public override string ToCommandLine()
    {
        var parts = new List<string> { "enum", "-d", "'" + (ErrorTargetPath ?? TargetPath) + "'" };
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

        var contentX = FilterDrawer.DrawBadge(inner, IsWhitelist, IsLoose,
            () => { IsWhitelist = !IsWhitelist; dialog.MarkDirty(); },
            "UR.BetterFilter.WhitelistNumericTooltip", "UR.BetterFilter.BlacklistNumericTooltip");
        FilterDrawer.DrawOptionTopRight(inner, IsLoose,
            () => { IsLoose = !IsLoose; dialog.MarkDirty(); });
        FilterDrawer.DrawDeleteTopRight(inner, ref PendingDelete);

        var oldColor = GUI.color;
        if (!string.IsNullOrWhiteSpace(ErrorTargetPath)) GUI.color = Color.gray;
        var targetW = FilterDrawer.TextRightX(inner) - contentX;
        var targetRect = new Rect(contentX, inner.y + FilterDrawer.BadgeMargin,
            targetW, 22f);
        if (Widgets.ButtonText(targetRect, TargetDisplayText))
        {
            var items = FilterTargetCatalog.Get(FilterTargetKind.Enum)
                .Select(target => (FilterTargetCatalog.DisplayName(target), (Action)(() =>
                {
                    TargetPath = target.XPath;
                    TargetName = target.Name;
                    ErrorTargetPath = null;
                    ErrorValues.Clear();
                    SelectedValues.Clear();
                    dialog.MarkDirty();
                }))).ToList();
            Find.WindowStack.Add(new Dialog.Dialog_SearchableSelect(
                "UR.BetterFilter.EnumFilter".Translate(), items));
        }
        GUI.color = oldColor;

        var valuesY = targetRect.yMax + 3f;
        var valuesW = targetW;
        var valueText = ValuesDisplayText;
        if (!string.IsNullOrEmpty(ErrorValuesDisplayText))
            valueText = (valueText + " " + ErrorValuesDisplayText).Trim();
        if (string.IsNullOrEmpty(valueText))
            valueText = "UR.BetterFilter.SelectEnumValue".Translate();
        var valueH = Text.CalcHeight(valueText, valuesW);
        Widgets.Label(new Rect(contentX, valuesY, valuesW, Mathf.Max(20f, valueH)), valueText);

        FilterDrawer.DrawMinusBottomRight(inner, SelectedValues.Count > 0,
            () => { SelectedValues.RemoveAt(SelectedValues.Count - 1); dialog.MarkDirty(); });
        FilterDrawer.DrawPlusBottomRight(inner, () =>
        {
            var existing = new HashSet<string>(SelectedValues);
            var items = RuntimeFilterPath.GetDistinctTextValues(TargetPath)
                .Where(v => !existing.Contains(v))
                .Select(v => (v, (Action)(() =>
                {
                    SelectedValues.Add(v);
                    ErrorValues.Remove(v);
                    dialog.MarkDirty();
                }))).ToList();
            Find.WindowStack.Add(new Dialog.Dialog_SearchableSelect(
                "UR.BetterFilter.SelectEnumValue".Translate(), items));
        }, "UR.BetterFilter.SelectEnumValueTooltip");
    }
}
