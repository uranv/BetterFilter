using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Filters;

public abstract class FilterComponent
{
    /// <summary>Set by the panel before BlockHeight is read; used to compute text wrapping width.</summary>
    internal float LayoutWidth { get; set; }

    public abstract float BlockHeight { get; }
    public abstract void Draw(Rect rect, Dialog.Dialog_AdvancedFilters dialog, int index);

    /// <summary>
    /// Returns true if this filter component allows the given ThingDef.
    /// All components are combined with AND: an item must pass every active component.
    /// </summary>
    public abstract bool AllowsDef(ThingDef def);

    /// <summary>
    /// Serialize this filter to a command-line string for save/load.
    /// Always uses -u to include invalid/error entries.
    /// </summary>
    public abstract string ToCommandLine();
}
