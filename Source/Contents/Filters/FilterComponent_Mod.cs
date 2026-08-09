using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Filters;

public class FilterComponent_Mod : FilterComponent
{
    public List<string> PackageIds = new();
    /// <summary>PackageIds that failed to resolve (set by CLI parser in -u mode).</summary>
    public List<string> ErrorPackageIds = new();
    public bool IsWhitelist = true;
    public bool PendingDelete;

    private HashSet<ThingDef> _cachedModDefs;
    private int _cachedPackageIdsHash;

    private string ValidText
    {
        get
        {
            if (PackageIds.Count == 0 && ErrorPackageIds.Count == 0)
                return "UR.BetterFilter.NoModsSelected".Translate();
            if (PackageIds.Count == 0)
                return "";
            var nameList = PackageIds
                .Select(pid => LoadedModManager.RunningMods
                    .FirstOrDefault(m => m.PackageId == pid)?.Name ?? pid).ToList();
            return string.Join(", ", nameList);
        }
    }

    private string ErrorText =>
        ErrorPackageIds.Count == 0 ? "" :
        string.Join(", ", ErrorPackageIds.Select(n => "*" + n));

    private string FullDisplayText =>
        (ValidText + "  " + ErrorText).Trim();

    public override float BlockHeight =>
        Utils.FilterDrawer.MinBlockHeight(FullDisplayText, LayoutWidth);

    public HashSet<ThingDef> GetModDefs()
    {
        var hash = ContentHash(PackageIds);
        if (_cachedModDefs != null && _cachedPackageIdsHash == hash)
            return _cachedModDefs;
        _cachedModDefs = new HashSet<ThingDef>();
        if (PackageIds.Count == 0) { _cachedPackageIdsHash = hash; return _cachedModDefs; }
        var idSet = new HashSet<string>(PackageIds);
        foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            var pid = def.modContentPack?.PackageId;
            if (pid != null && idSet.Contains(pid))
                _cachedModDefs.Add(def);
        }
        _cachedPackageIdsHash = hash;
        return _cachedModDefs;
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
        if (PackageIds.Count == 0) return true;
        var matches = GetModDefs().Contains(def);
        return IsWhitelist ? matches : !matches;
    }

    public override string ToCommandLine()
    {
        var parts = new List<string> { "mod", "-d" };
        parts.AddRange(PackageIds);
        parts.AddRange(ErrorPackageIds);
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
            "UR.BetterFilter.WhitelistModTooltip", "UR.BetterFilter.BlacklistModTooltip");

        Utils.FilterDrawer.DrawDeleteTopRight(inner, ref PendingDelete);

        var textMaxW = Utils.FilterDrawer.TextMaxWidth(inner, contentX);
        var topY = inner.y + Utils.FilterDrawer.BadgeMargin;

        if (ErrorPackageIds.Count > 0 && PackageIds.Count > 0)
        {
            var validH = Text.CalcHeight(ValidText, textMaxW);
            var validRect = new Rect(contentX, topY, textMaxW, validH);
            Widgets.Label(validRect, ValidText);

            var afterValidX = contentX + Text.CalcSize(ValidText).x + 4f;
            var errorW = textMaxW - (afterValidX - contentX);
            if (errorW < 40f) { afterValidX = contentX; }
            var oldColor = GUI.color;
            GUI.color = Color.gray;
            var errorRect = new Rect(afterValidX, topY, textMaxW - (afterValidX - contentX), validH);
            Widgets.Label(errorRect, ErrorText);
            GUI.color = oldColor;
        }
        else if (PackageIds.Count == 0 && ErrorPackageIds.Count > 0)
        {
            var oldColor = GUI.color;
            GUI.color = Color.gray;
            var h = Text.CalcHeight(ErrorText, textMaxW);
            var r = new Rect(contentX, topY, textMaxW, h);
            Widgets.Label(r, ErrorText);
            GUI.color = oldColor;
        }
        else
        {
            var h = Text.CalcHeight(ValidText, textMaxW);
            var r = new Rect(contentX, topY, textMaxW, h);
            Widgets.Label(r, ValidText);
        }

        Utils.FilterDrawer.DrawMinusBottomRight(inner, PackageIds.Count > 0, () =>
        {
            PackageIds.RemoveAt(PackageIds.Count - 1);
            _cachedModDefs = null;
            dialog.MarkDirty();
        });
        Utils.FilterDrawer.DrawPlusBottomRight(inner, () =>
        {
            var existing = new HashSet<string>(PackageIds);
            var items = LoadedModManager.RunningMods
                .Where(m => !existing.Contains(m.PackageId))
                .OrderBy(m => m.Name)
                .Select(m => (m.Name, (Action)(() =>
                {
                    PackageIds.Add(m.PackageId);
                    _cachedModDefs = null;
                    dialog.MarkDirty();
                }))).ToList();
            Find.WindowStack.Add(new Dialog.Dialog_SearchableSelect(
                "UR.BetterFilter.ModFilter".Translate(), items));
        }, "UR.BetterFilter.AddModTooltip");
    }
}
