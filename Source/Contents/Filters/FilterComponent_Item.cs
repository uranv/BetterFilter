using BetterFilter.Contents.Dialog;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Filters;

/// <summary>
/// Single-def manual override: directly allows or disallows one ThingDef.
/// Rendered in the per-item override panel, not as a regular filter block.
/// Cmd: item -d DefName [-b] [-u]
/// Multiple -d defs → multiple FilterComponent_Item instances.
/// </summary>
public class FilterComponent_Item : FilterComponent
{
    public ThingDef selectedDef;
    /// <summary>DefName that failed to resolve (set by CLI parser in -u mode).</summary>
    public string errorDefName;
    public bool isWhitelist = true;
    public bool pendingDelete;

    public override float BlockHeight => 0f; // rendered in override panel

    public override bool AllowsDef(ThingDef def) => true; // pass-through, not AND-ed

    public override string ToCommandLine()
    {
        var parts = new System.Collections.Generic.List<string> { "item", "-d" };
        if (selectedDef != null)
            parts.Add(selectedDef.defName);
        else if (errorDefName != null)
            parts.Add(errorDefName);
        if (!isWhitelist) parts.Add("-b");
        parts.Add("-u");
        return string.Join(" ", parts);
    }

    public override void Draw(Rect rect, Dialog.Dialog_AdvancedFilters dialog, int index)
    {
        // Never called — rendered in DrawPerItemOverride.
    }
}
