using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BetterFilter.Contents.Utils;

/// <summary>
/// Expand / collapse the storage filter category tree one level at a time.
/// </summary>
public static class TreeExpandCollapse
{
    private const string FilterCatDefName = "UR_BetterFilter";

    private static readonly HashSet<string> ExcludeCatDefNames = new() { FilterCatDefName };

    public static void ToggleOneLevel(ThingCategoryDef start, int mask, bool expand)
    {
        if (expand)
            ExpandOneLevel(start, mask);
        else
            CollapseOneLevel(start, mask);
    }

    /// <summary>
    /// BFS from start: at the shallowest open nodes that have closed children,
    /// open those children exactly one level deep.  The start node is always
    /// treated as open (like Root) so expansion works from any display-root.
    /// </summary>
    private static void ExpandOneLevel(ThingCategoryDef start, int mask)
    {
        var toOpen = new List<ThingCategoryDef>();
        var queue = new Queue<ThingCategoryDef>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cat = queue.Dequeue();
            if (ExcludeCatDefNames.Contains(cat.defName)) continue;
            bool isOpen = cat == start || cat.parent == null || cat.treeNode.IsOpen(mask);
            if (!isOpen) continue;

            bool foundClosedChild = false;
            foreach (var child in cat.childCategories)
            {
                if (ExcludeCatDefNames.Contains(child.defName)) continue;
                if (!child.treeNode.IsOpen(mask))
                {
                    toOpen.Add(child);
                    foundClosedChild = true;
                }
            }

            if (!foundClosedChild)
            {
                foreach (var child in cat.childCategories)
                {
                    if (ExcludeCatDefNames.Contains(child.defName)) continue;
                    if (child.treeNode.IsOpen(mask))
                        queue.Enqueue(child);
                }
            }
        }

        foreach (var cat in toOpen)
            cat.treeNode.SetOpen(mask, true);
    }

    /// <summary>
    /// Post-order DFS from start: close the deepest open nodes that have no
    /// open children (leaf-open nodes). One level retreats at a time.
    /// The start node is always treated as open.
    /// </summary>
    private static void CollapseOneLevel(ThingCategoryDef start, int mask)
    {
        var toClose = new List<ThingCategoryDef>();
        CollectLeafOpen(start, start, mask, toClose);
        foreach (var cat in toClose)
            cat.treeNode.SetOpen(mask, false);
    }

    private static bool CollectLeafOpen(ThingCategoryDef start, ThingCategoryDef cat,
        int mask, List<ThingCategoryDef> toClose)
    {
        if (ExcludeCatDefNames.Contains(cat.defName)) return false;
        bool isOpen = cat == start || cat.parent == null || cat.treeNode.IsOpen(mask);
        if (!isOpen) return false;

        bool anyChildOpen = false;
        foreach (var child in cat.childCategories)
        {
            if (ExcludeCatDefNames.Contains(child.defName)) continue;
            if (CollectLeafOpen(start, child, mask, toClose))
                anyChildOpen = true;
        }

        if (!anyChildOpen)
            toClose.Add(cat);

        return true;
    }
}
