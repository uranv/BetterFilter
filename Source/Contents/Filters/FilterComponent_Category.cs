using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Filters;

public class FilterComponent_Category : FilterComponent
{
    public List<string> CategoryDefNames = new();
    /// <summary>DefNames that failed to resolve (set by CLI parser in -u mode).</summary>
    public List<string> ErrorCategoryNames = new();
    public bool IsWhitelist = true;
    public bool PendingDelete;

    private HashSet<ThingDef> _cachedCategoryDefs;
    private int _cachedCategoryHash;

    private string ValidText
    {
        get
        {
            if (CategoryDefNames.Count == 0 && ErrorCategoryNames.Count == 0)
                return "UR.BetterFilter.NoCategoriesSelected".Translate();
            if (CategoryDefNames.Count == 0)
                return "";
            return string.Join(", ", CategoryDefNames
                .Select(cn => DefDatabase<ThingCategoryDef>.GetNamed(cn)?.LabelCap ?? cn));
        }
    }

    private string ErrorText =>
        ErrorCategoryNames.Count == 0 ? "" :
        string.Join(", ", ErrorCategoryNames.Select(n => "*" + n));

    private string FullDisplayText =>
        (ValidText + "  " + ErrorText).Trim();

    public override float BlockHeight =>
        Utils.FilterDrawer.MinBlockHeight(FullDisplayText, LayoutWidth);

    private HashSet<ThingDef> GetCategoryDefs()
    {
        var hash = ContentHash(CategoryDefNames);
        if (_cachedCategoryDefs != null && _cachedCategoryHash == hash)
            return _cachedCategoryDefs;
        _cachedCategoryDefs = new HashSet<ThingDef>();
        if (CategoryDefNames.Count == 0) { _cachedCategoryHash = hash; return _cachedCategoryDefs; }
        foreach (var catName in CategoryDefNames)
        {
            var catDef = DefDatabase<ThingCategoryDef>.GetNamed(catName);
            if (catDef != null)
            {
                foreach (var def in catDef.DescendantThingDefs)
                    _cachedCategoryDefs.Add(def);
            }
        }
        _cachedCategoryHash = hash;
        return _cachedCategoryDefs;
    }

    private static int ContentHash(List<string> items)
    {
        var h = 0;
        foreach (var s in items)
            h = unchecked(h * 31 + s.GetHashCode());
        return h;
    }

    public override bool AllowsDef(ThingDef def)
    {
        if (CategoryDefNames.Count == 0) return true;
        var matches = GetCategoryDefs().Contains(def);
        return IsWhitelist ? matches : !matches;
    }

    public override string ToCommandLine()
    {
        var parts = new List<string> { "cat", "-d" };
        parts.AddRange(CategoryDefNames);
        parts.AddRange(ErrorCategoryNames);
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
            "UR.BetterFilter.WhitelistCategoryTooltip", "UR.BetterFilter.BlacklistCategoryTooltip");

        Utils.FilterDrawer.DrawDeleteTopRight(inner, ref PendingDelete);

        var textMaxW = Utils.FilterDrawer.TextMaxWidth(inner, contentX);
        var topY = inner.y + Utils.FilterDrawer.BadgeMargin;

        if (ErrorCategoryNames.Count > 0 && CategoryDefNames.Count > 0)
        {
            var validH = Text.CalcHeight(ValidText, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, validH), ValidText);
            var afterX = contentX + Text.CalcSize(ValidText).x + 4f;
            var errorW = textMaxW - (afterX - contentX);
            if (errorW < 40f) afterX = contentX;
            var old = GUI.color;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(afterX, topY, textMaxW - (afterX - contentX), validH), ErrorText);
            GUI.color = old;
        }
        else if (CategoryDefNames.Count == 0 && ErrorCategoryNames.Count > 0)
        {
            var old = GUI.color;
            GUI.color = Color.gray;
            var h = Text.CalcHeight(ErrorText, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, h), ErrorText);
            GUI.color = old;
        }
        else
        {
            var h = Text.CalcHeight(ValidText, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, h), ValidText);
        }

        Utils.FilterDrawer.DrawMinusBottomRight(inner, CategoryDefNames.Count > 0, () =>
        {
            CategoryDefNames.RemoveAt(CategoryDefNames.Count - 1);
            _cachedCategoryDefs = null;
            dialog.MarkDirty();
        });
        Utils.FilterDrawer.DrawPlusBottomRight(inner, () =>
        {
            var existing = new HashSet<string>(CategoryDefNames);
            var items = DefDatabase<ThingCategoryDef>.AllDefsListForReading
                .Where(c => c.DescendantThingDefs.Any() && c != ThingCategoryDefOf.Root && !existing.Contains(c.defName))
                .OrderBy(c => c.LabelCap.ToString())
                .Select(c => ((string) c.LabelCap, (Action)(() =>
                {
                    CategoryDefNames.Add(c.defName);
                    _cachedCategoryDefs = null;
                    dialog.MarkDirty();
                }))).ToList();
            Find.WindowStack.Add(new Dialog.Dialog_SearchableSelect(
                "UR.BetterFilter.CategoryFilter".Translate(), items));
        }, "UR.BetterFilter.AddCategoryTooltip");
    }
}
