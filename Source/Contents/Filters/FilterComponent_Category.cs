using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Filters;

public class FilterComponent_Category : FilterComponent
{
    public List<string> categoryDefNames = new();
    /// <summary>DefNames that failed to resolve (set by CLI parser in -u mode).</summary>
    public List<string> errorCategoryNames = new();
    public bool isWhitelist = true;
    public bool pendingDelete;

    private HashSet<ThingDef> _cachedCategoryDefs;
    private int _cachedCategoryHash;

    private string ValidText
    {
        get
        {
            if (categoryDefNames.Count == 0 && errorCategoryNames.Count == 0)
                return "UR.BetterFilter.NoCategoriesSelected".Translate();
            if (categoryDefNames.Count == 0)
                return "";
            return string.Join(", ", categoryDefNames
                .Select(cn => DefDatabase<ThingCategoryDef>.GetNamed(cn)?.LabelCap ?? cn));
        }
    }

    private string ErrorText =>
        errorCategoryNames.Count == 0 ? "" :
        string.Join(", ", errorCategoryNames.Select(n => "*" + n));

    private string FullDisplayText =>
        (ValidText + "  " + ErrorText).Trim();

    public override float BlockHeight =>
        Utils.FilterDrawer.MinBlockHeight(FullDisplayText, LayoutWidth);

    private HashSet<ThingDef> GetCategoryDefs()
    {
        int hash = ContentHash(categoryDefNames);
        if (_cachedCategoryDefs != null && _cachedCategoryHash == hash)
            return _cachedCategoryDefs;
        _cachedCategoryDefs = new HashSet<ThingDef>();
        if (categoryDefNames.Count == 0) { _cachedCategoryHash = hash; return _cachedCategoryDefs; }
        foreach (string catName in categoryDefNames)
        {
            var catDef = DefDatabase<ThingCategoryDef>.GetNamed(catName);
            if (catDef != null)
            {
                foreach (ThingDef def in catDef.DescendantThingDefs)
                    _cachedCategoryDefs.Add(def);
            }
        }
        _cachedCategoryHash = hash;
        return _cachedCategoryDefs;
    }

    private static int ContentHash(List<string> items)
    {
        int h = 0;
        foreach (string s in items)
            h = unchecked(h * 31 + s.GetHashCode());
        return h;
    }

    public override bool AllowsDef(ThingDef def)
    {
        if (categoryDefNames.Count == 0) return true;
        bool matches = GetCategoryDefs().Contains(def);
        return isWhitelist ? matches : !matches;
    }

    public override string ToCommandLine()
    {
        var parts = new List<string> { "cat", "-d" };
        parts.AddRange(categoryDefNames);
        parts.AddRange(errorCategoryNames);
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
            "UR.BetterFilter.WhitelistCategoryTooltip", "UR.BetterFilter.BlacklistCategoryTooltip");

        Utils.FilterDrawer.DrawDeleteTopRight(inner, ref pendingDelete);

        float textMaxW = Utils.FilterDrawer.TextMaxWidth(inner, contentX);
        float topY = inner.y + Utils.FilterDrawer.BadgeMargin;

        if (errorCategoryNames.Count > 0 && categoryDefNames.Count > 0)
        {
            float validH = Text.CalcHeight(ValidText, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, validH), ValidText);
            float afterX = contentX + Text.CalcSize(ValidText).x + 4f;
            float errorW = textMaxW - (afterX - contentX);
            if (errorW < 40f) afterX = contentX;
            Color old = GUI.color;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(afterX, topY, textMaxW - (afterX - contentX), validH), ErrorText);
            GUI.color = old;
        }
        else if (categoryDefNames.Count == 0 && errorCategoryNames.Count > 0)
        {
            Color old = GUI.color;
            GUI.color = Color.gray;
            float h = Text.CalcHeight(ErrorText, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, h), ErrorText);
            GUI.color = old;
        }
        else
        {
            float h = Text.CalcHeight(ValidText, textMaxW);
            Widgets.Label(new Rect(contentX, topY, textMaxW, h), ValidText);
        }

        Utils.FilterDrawer.DrawMinusBottomRight(inner, categoryDefNames.Count > 0, () =>
        {
            categoryDefNames.RemoveAt(categoryDefNames.Count - 1);
            _cachedCategoryDefs = null;
            dialog.MarkDirty();
        });
        Utils.FilterDrawer.DrawPlusBottomRight(inner, () =>
        {
            var existing = new HashSet<string>(categoryDefNames);
            var opts = DefDatabase<ThingCategoryDef>.AllDefsListForReading
                .Where(c => c.DescendantThingDefs.Any() && c != ThingCategoryDefOf.Root && !existing.Contains(c.defName))
                .OrderBy(c => c.LabelCap.ToString())
                .Select(c => new FloatMenuOption((string)c.LabelCap, () =>
                {
                    categoryDefNames.Add(c.defName);
                    _cachedCategoryDefs = null;
                    dialog.MarkDirty();
                })).ToList();
            Find.WindowStack.Add(new FloatMenu(opts));
        }, "UR.BetterFilter.AddCategoryTooltip");
    }
}
