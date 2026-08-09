using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Filters;

public class FilterComponent_Worker : FilterComponent
{
    public List<SpecialThingFilterDef> SelectedDefs = new();
    /// <summary>DefNames that failed to resolve (set by CLI parser in -u mode).</summary>
    public List<string> ErrorDefNames = new();
    public bool IsWhitelist = true;
    public bool PendingDelete;

    private string ValidText
    {
        get
        {
            return SelectedDefs.Count switch
            {
                0 when ErrorDefNames.Count == 0 => "UR.BetterFilter.SelectFilter".Translate(),
                0 => "",
                _ => string.Join(", ", SelectedDefs.Select(d => (string)d.LabelCap))
            };
        }
    }

    private string ErrorText =>
        ErrorDefNames.Count == 0 ? "" :
        string.Join(", ", ErrorDefNames.Select(n => "*" + n));

    private string FullDisplayText =>
        (ValidText + "  " + ErrorText).Trim();

    public override float BlockHeight =>
        Utils.FilterDrawer.MinBlockHeight(FullDisplayText, LayoutWidth);

    public override bool AllowsDef(ThingDef def)
    {
        if (SelectedDefs.Count == 0)
            return true;
        var matches = SelectedDefs.Any(d => d.Worker.CanEverMatch(def));
        return IsWhitelist ? matches : !matches;
    }

    public override string ToCommandLine()
    {
        var parts = new List<string> { "worker", "-d" };
        parts.AddRange(SelectedDefs.Select(d => d.defName));
        parts.AddRange(ErrorDefNames);
        if (!IsWhitelist) parts.Add("-b");
        parts.Add("-u");
        return string.Join(" ", parts);
    }

    public override void Draw(Rect rect, Dialog.Dialog_AdvancedFilters dialog, int index)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.18f, 0.18f, 0.2f));
        var inner = rect.ContractedBy(4f);

        var contentX = Utils.FilterDrawer.DrawBadge(inner, IsWhitelist, false,
            () => { IsWhitelist = !IsWhitelist; dialog.MarkDirty(); },
            "UR.BetterFilter.WhitelistWorkerTooltip", "UR.BetterFilter.BlacklistWorkerTooltip");

        Utils.FilterDrawer.DrawDeleteTopRight(inner, ref PendingDelete);

        // Draw valid defs in normal color
        var textMaxW = Utils.FilterDrawer.TextMaxWidth(inner, contentX);
        var topY = inner.y + Utils.FilterDrawer.BadgeMargin;

        if (ErrorDefNames.Count > 0 && SelectedDefs.Count > 0)
        {
            // Render valid names normally, then error names in gray on the same line(s)
            var validH = Text.CalcHeight(ValidText, textMaxW);
            var validRect = new Rect(contentX, topY, textMaxW, validH);
            Widgets.Label(validRect, ValidText);

            // Error text in gray, right after valid text
            var afterValidX = contentX + Text.CalcSize(ValidText).x + 4f;
            var errorW = textMaxW - (afterValidX - contentX);
            if (errorW < 40f) { afterValidX = contentX; }
            var oldColor = GUI.color;
            GUI.color = Color.gray;
            var errorRect = new Rect(afterValidX, topY, textMaxW - (afterValidX - contentX), validH);
            Widgets.Label(errorRect, ErrorText);
            GUI.color = oldColor;
        }
        else if (SelectedDefs.Count == 0 && ErrorDefNames.Count > 0)
        {
            // Only errors — render all in gray
            var oldColor = GUI.color;
            GUI.color = Color.gray;
            var h = Text.CalcHeight(ErrorText, textMaxW);
            var r = new Rect(contentX, topY, textMaxW, h);
            Widgets.Label(r, ErrorText);
            GUI.color = oldColor;
        }
        else
        {
            // Normal: only valid defs
            var h = Text.CalcHeight(ValidText, textMaxW);
            var r = new Rect(contentX, topY, textMaxW, h);
            Widgets.Label(r, ValidText);
        }

        Utils.FilterDrawer.DrawMinusBottomRight(inner, SelectedDefs.Count > 0,
            () => { SelectedDefs.RemoveAt(SelectedDefs.Count - 1); dialog.MarkDirty(); });
        Utils.FilterDrawer.DrawPlusBottomRight(inner, () =>
        {
            var allowed = BetterFilterMod.Settings?.AllowedFilters
                ?? Enumerable.Empty<SpecialThingFilterDef>();
            var existing = new HashSet<SpecialThingFilterDef>(SelectedDefs);
            var items = allowed
                .Where(d => !existing.Contains(d))
                .Select(d => ((string) d.LabelCap,
                    (Action)(() => { SelectedDefs.Add(d); dialog.MarkDirty(); })))
                .ToList();
            Find.WindowStack.Add(new Dialog.Dialog_SearchableSelect(
                "UR.BetterFilter.FilterWorker".Translate(), items));
        }, "UR.BetterFilter.AddItemTooltip");
    }
}
