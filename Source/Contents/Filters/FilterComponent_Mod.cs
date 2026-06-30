using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Filters;

public class FilterComponent_Mod : FilterComponent
{
    public List<string> packageIds = new();
    /// <summary>PackageIds that failed to resolve (set by CLI parser in -u mode).</summary>
    public List<string> errorPackageIds = new();
    public bool isWhitelist = true;
    public bool pendingDelete;

    private HashSet<ThingDef> _cachedModDefs;
    private int _cachedPackageIdsHash;

    private string ValidText
    {
        get
        {
            if (packageIds.Count == 0 && errorPackageIds.Count == 0)
                return "UR.BetterFilter.NoModsSelected".Translate();
            if (packageIds.Count == 0)
                return "";
            var nameList = packageIds
                .Select(pid => LoadedModManager.RunningMods
                    .FirstOrDefault(m => m.PackageId == pid)?.Name ?? pid).ToList();
            return string.Join(", ", nameList);
        }
    }

    private string ErrorText =>
        errorPackageIds.Count == 0 ? "" :
        string.Join(", ", errorPackageIds.Select(n => "*" + n));

    private string FullDisplayText =>
        (ValidText + "  " + ErrorText).Trim();

    public override float BlockHeight =>
        Utils.FilterDrawer.MinBlockHeight(FullDisplayText, LayoutWidth);

    public HashSet<ThingDef> GetModDefs()
    {
        int hash = ContentHash(packageIds);
        if (_cachedModDefs != null && _cachedPackageIdsHash == hash)
            return _cachedModDefs;
        _cachedModDefs = new HashSet<ThingDef>();
        if (packageIds.Count == 0) { _cachedPackageIdsHash = hash; return _cachedModDefs; }
        var idSet = new HashSet<string>(packageIds);
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            string pid = def.modContentPack?.PackageId;
            if (pid != null && idSet.Contains(pid))
                _cachedModDefs.Add(def);
        }
        _cachedPackageIdsHash = hash;
        return _cachedModDefs;
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
        if (packageIds.Count == 0) return true;
        bool matches = GetModDefs().Contains(def);
        return isWhitelist ? matches : !matches;
    }

    public override string ToCommandLine()
    {
        var parts = new List<string> { "mod", "-d" };
        parts.AddRange(packageIds);
        parts.AddRange(errorPackageIds);
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
            "UR.BetterFilter.WhitelistModTooltip", "UR.BetterFilter.BlacklistModTooltip");

        Utils.FilterDrawer.DrawDeleteTopRight(inner, ref pendingDelete);

        float textMaxW = Utils.FilterDrawer.TextMaxWidth(inner, contentX);
        float topY = inner.y + Utils.FilterDrawer.BadgeMargin;

        if (errorPackageIds.Count > 0 && packageIds.Count > 0)
        {
            float validH = Text.CalcHeight(ValidText, textMaxW);
            Rect validRect = new Rect(contentX, topY, textMaxW, validH);
            Widgets.Label(validRect, ValidText);

            float afterValidX = contentX + Text.CalcSize(ValidText).x + 4f;
            float errorW = textMaxW - (afterValidX - contentX);
            if (errorW < 40f) { afterValidX = contentX; }
            Color oldColor = GUI.color;
            GUI.color = Color.gray;
            Rect errorRect = new Rect(afterValidX, topY, textMaxW - (afterValidX - contentX), validH);
            Widgets.Label(errorRect, ErrorText);
            GUI.color = oldColor;
        }
        else if (packageIds.Count == 0 && errorPackageIds.Count > 0)
        {
            Color oldColor = GUI.color;
            GUI.color = Color.gray;
            float h = Text.CalcHeight(ErrorText, textMaxW);
            Rect r = new Rect(contentX, topY, textMaxW, h);
            Widgets.Label(r, ErrorText);
            GUI.color = oldColor;
        }
        else
        {
            float h = Text.CalcHeight(ValidText, textMaxW);
            Rect r = new Rect(contentX, topY, textMaxW, h);
            Widgets.Label(r, ValidText);
        }

        Utils.FilterDrawer.DrawMinusBottomRight(inner, packageIds.Count > 0, () =>
        {
            packageIds.RemoveAt(packageIds.Count - 1);
            _cachedModDefs = null;
            dialog.MarkDirty();
        });
        Utils.FilterDrawer.DrawPlusBottomRight(inner, () =>
        {
            var existing = new HashSet<string>(packageIds);
            var opts = LoadedModManager.RunningMods
                .Where(m => !existing.Contains(m.PackageId))
                .OrderBy(m => m.Name)
                .Select(m => new FloatMenuOption(m.Name, () =>
                {
                    packageIds.Add(m.PackageId);
                    _cachedModDefs = null;
                    dialog.MarkDirty();
                })).ToList();
            Find.WindowStack.Add(new FloatMenu(opts));
        }, "UR.BetterFilter.AddModTooltip");
    }
}
