using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Filters;

public class FilterComponent_Worker : FilterComponent
{
    public List<SpecialThingFilterDef> selectedDefs = new();
    /// <summary>DefNames that failed to resolve (set by CLI parser in -u mode).</summary>
    public List<string> errorDefNames = new();
    public bool isWhitelist = true;
    public bool pendingDelete;

    private string ValidText
    {
        get
        {
            if (selectedDefs.Count == 0 && errorDefNames.Count == 0)
                return "UR.BetterFilter.SelectFilter".Translate();
            if (selectedDefs.Count == 0)
                return "";
            return string.Join(", ", selectedDefs.Select(d => (string)d.LabelCap));
        }
    }

    private string ErrorText =>
        errorDefNames.Count == 0 ? "" :
        string.Join(", ", errorDefNames.Select(n => "*" + n));

    private string FullDisplayText =>
        (ValidText + "  " + ErrorText).Trim();

    public override float BlockHeight =>
        Utils.FilterDrawer.MinBlockHeight(FullDisplayText, LayoutWidth);

    public override bool AllowsDef(ThingDef def)
    {
        if (selectedDefs.Count == 0)
            return true;
        bool matches = selectedDefs.Any(d => d.Worker.CanEverMatch(def));
        return isWhitelist ? matches : !matches;
    }

    public override string ToCommandLine()
    {
        var parts = new List<string> { "worker", "-d" };
        parts.AddRange(selectedDefs.Select(d => d.defName));
        parts.AddRange(errorDefNames);
        if (!isWhitelist) parts.Add("-b");
        parts.Add("-u");
        return string.Join(" ", parts);
    }

    public override void Draw(Rect rect, Dialog.Dialog_AdvancedFilters dialog, int index)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.18f, 0.18f, 0.2f));
        Rect inner = rect.ContractedBy(4f);

        float contentX = Utils.FilterDrawer.DrawBadge(inner, isWhitelist, false,
            () => { isWhitelist = !isWhitelist; dialog.MarkDirty(); },
            "UR.BetterFilter.WhitelistWorkerTooltip", "UR.BetterFilter.BlacklistWorkerTooltip");

        Utils.FilterDrawer.DrawDeleteTopRight(inner, ref pendingDelete);

        // Draw valid defs in normal color
        float textMaxW = Utils.FilterDrawer.TextMaxWidth(inner, contentX);
        float topY = inner.y + Utils.FilterDrawer.BadgeMargin;

        if (errorDefNames.Count > 0 && selectedDefs.Count > 0)
        {
            // Render valid names normally, then error names in gray on the same line(s)
            float validH = Text.CalcHeight(ValidText, textMaxW);
            Rect validRect = new Rect(contentX, topY, textMaxW, validH);
            Widgets.Label(validRect, ValidText);

            // Error text in gray, right after valid text
            float afterValidX = contentX + Text.CalcSize(ValidText).x + 4f;
            float errorW = textMaxW - (afterValidX - contentX);
            if (errorW < 40f) { afterValidX = contentX; }
            Color oldColor = GUI.color;
            GUI.color = Color.gray;
            Rect errorRect = new Rect(afterValidX, topY, textMaxW - (afterValidX - contentX), validH);
            Widgets.Label(errorRect, ErrorText);
            GUI.color = oldColor;
        }
        else if (selectedDefs.Count == 0 && errorDefNames.Count > 0)
        {
            // Only errors — render all in gray
            Color oldColor = GUI.color;
            GUI.color = Color.gray;
            float h = Text.CalcHeight(ErrorText, textMaxW);
            Rect r = new Rect(contentX, topY, textMaxW, h);
            Widgets.Label(r, ErrorText);
            GUI.color = oldColor;
        }
        else
        {
            // Normal: only valid defs
            float h = Text.CalcHeight(ValidText, textMaxW);
            Rect r = new Rect(contentX, topY, textMaxW, h);
            Widgets.Label(r, ValidText);
        }

        Utils.FilterDrawer.DrawMinusBottomRight(inner, selectedDefs.Count > 0,
            () => { selectedDefs.RemoveAt(selectedDefs.Count - 1); dialog.MarkDirty(); });
        Utils.FilterDrawer.DrawPlusBottomRight(inner, () =>
        {
            var allowed = BetterFilterMod.Settings?.AllowedFilters
                ?? Enumerable.Empty<SpecialThingFilterDef>();
            var existing = new HashSet<SpecialThingFilterDef>(selectedDefs);
            var opts = allowed
                .Where(d => !existing.Contains(d))
                .Select(d => new FloatMenuOption(d.LabelCap,
                    () => { selectedDefs.Add(d); dialog.MarkDirty(); }))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(opts));
        }, "UR.BetterFilter.AddItemTooltip");
    }
}
