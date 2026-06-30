using System.Collections.Generic;
using System.Linq;
using BetterFilter.Contents.Utils;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterFilter.Contents.Patches;

/// <summary>
/// UI-only visual relocation: collects all childSpecialFilters from the
/// display-root node AND its ancestors (walking up to Root), filters out
/// those with no matching visible items, and moves the rest into the
/// UR_BetterFilter category.
///
/// Patches ListCategoryChildren (the public entry point) so ancestor
/// childSpecialFilters are cleared BEFORE ParentsSpecialThingFilterDefs
/// is iterated, preventing duplicate rendering.
///
/// Temporary list swap — prefix assigns modified copies, postfix restores.
/// </summary>
[HarmonyPatch(typeof(Listing_TreeThingFilter), "ListCategoryChildren")]
public static class Patch_FilterCategoryOrder
{
    private const string FilterCatDefName = "UR_BetterFilter";

    /// <summary>Mutex pairs: shown only when BOTH defNames are already in toMove.
    /// Uses string comparison against the already-filtered toMove list — no
    /// DefDatabase lookup or Worker re-evaluation needed.</summary>
    private static readonly string[][] MutexPairDefNames =
    {
        new[] { "UR_AllowHitPoints",     "UR_AllowNoHitPoints"     },
        new[] { "UR_AllowQuality",       "UR_AllowNoQuality"       },
        new[] { "UR_AllowPerishable",    "UR_AllowNotPerishable"   },
        new[] { "UR_AllowDeteriorating", "UR_AllowNotDeteriorating" },
        new[] { "UR_AllowStackable",     "UR_AllowNonStackable"    },
    };

    /// <summary>
    /// Compute which mutex-pair defNames should be hidden for a given display
    /// root and parent filter.  A pair is hidden when only one side passes
    /// the vanilla visibility check (CanEverMatch against visible descendants).
    /// Reusable by other patches (e.g. policy dialogs).
    /// </summary>
    internal static HashSet<string> GetMutexHiddenDefNames(
        ThingCategoryDef displayRoot, ThingFilter parentFilter)
    {
        var visibleDefs = displayRoot.DescendantThingDefs
            .Where(d => parentFilter == null || parentFilter.Allows(d))
            .ToList();
        var hidden = new HashSet<string>();
        for (int i = 0; i < MutexPairDefNames.Length; i++)
        {
            var pair = MutexPairDefNames[i];
            var defA = DefDatabase<SpecialThingFilterDef>.GetNamed(pair[0], errorOnFail: false);
            var defB = DefDatabase<SpecialThingFilterDef>.GetNamed(pair[1], errorOnFail: false);
            if (defA == null || defB == null) continue;
            bool anyA = visibleDefs.Any(d => defA.Worker.CanEverMatch(d));
            bool anyB = visibleDefs.Any(d => defB.Worker.CanEverMatch(d));
            if (anyA != anyB)
            {
                hidden.Add(pair[0]);
                hidden.Add(pair[1]);
            }
        }
        return hidden;
    }

    internal static readonly AccessTools.FieldRef<Listing_TreeThingFilter, ThingFilter>
        GetParentFilter = AccessTools.FieldRefAccess<Listing_TreeThingFilter, ThingFilter>("parentFilter");

    /// <summary>Exposed so Patch_InjectFilterSliders can read the current display-root
    /// for conditional slider visibility.</summary>
    internal static ThingCategoryDef CurrentDisplayRoot;

    private static ThingCategoryDef _savedDisplayRootParent;
    private static List<ThingCategoryDef> _savedCatChildren;
    private static List<SpecialThingFilterDef> _savedNodeFilters;
    private static List<SpecialThingFilterDef> _savedFilterCatFilters;
    private static bool _filterCatWasInserted;
    private static bool _swapped;

    static void Prefix(Listing_TreeThingFilter __instance, TreeNode_ThingCategory node)
    {
        // ListCategoryChildren is only called once externally, not recursively.
        // No depth counter needed.

        _swapped = false;
        CurrentDisplayRoot = null;
        if (node.catDef.defName == FilterCatDefName) return;
        if (Dialog.Dialog_AdvancedFilters.IsPreview) return;

        // Don't relocate filters in outfit/food policy dialogs
        if (PolicyFilterClipboard.IsOutfitOrFoodDialog()) return;

        var catDef = node.catDef;
        var filterCat = catDef.childCategories
            .FirstOrDefault(c => c.defName == FilterCatDefName)
            ?? DefDatabase<ThingCategoryDef>.GetNamed(FilterCatDefName);

        if (filterCat == null) return;

        // ── Collect filters (self + all ancestors up to Root) ──
        var allFilters = new List<SpecialThingFilterDef>();
        allFilters.AddRange(catDef.childSpecialFilters);
        allFilters.AddRange(catDef.ParentsSpecialThingFilterDefs);

        // ── Determine which defs are visible under the parentFilter ──
        var parentFilter = GetParentFilter(__instance);
        var visibleDefs = catDef.DescendantThingDefs
            .Where(d => parentFilter == null || parentFilter.Allows(d))
            .ToList();

        // ── Only move filters that match at least one visible item ──
        var toMove = allFilters
            .Where(sf => visibleDefs.Any(d => sf.Worker.CanEverMatch(d)))
            .Select(sf => sf.defName)
            .Distinct()
            .ToList();

        // ── Mutex pairs: hide both when only one side passes visibility ──
        var mutexHidden = GetMutexHiddenDefNames(catDef, parentFilter);
        if (mutexHidden.Count > 0)
            toMove.RemoveAll(n => mutexHidden.Contains(n));

        var toMoveSet = new HashSet<string>(toMove);

        // ── Save originals ──
        _savedDisplayRootParent = catDef.parent;
        _savedCatChildren = catDef.childCategories;
        _savedNodeFilters = catDef.childSpecialFilters;
        _savedFilterCatFilters = filterCat.childSpecialFilters;
        _filterCatWasInserted = !_savedCatChildren.Contains(filterCat);

        // ── Suppress ParentsSpecialThingFilterDefs: nullify the parent chain
        //     so ListCategoryChildren renders nothing at indent 0 for ancestors.
        catDef.parent = null;

        // ── Category list: UR_BetterFilter first ──
        var newCats = new List<ThingCategoryDef>(catDef.childCategories);
        if (_filterCatWasInserted)
            newCats.Insert(0, filterCat);
        else if (newCats.IndexOf(filterCat) > 0)
        {
            newCats.Remove(filterCat);
            newCats.Insert(0, filterCat);
        }

        // ── Move matching filters into UR_BetterFilter ──
        var newCatFilters = new List<SpecialThingFilterDef>(filterCat.childSpecialFilters);
        var toMoveDefs = toMove
            .Select(n => DefDatabase<SpecialThingFilterDef>.GetNamed(n))
            .Where(d => d != null)
            .ToList();
        for (int i = toMoveDefs.Count - 1; i >= 0; i--)
            newCatFilters.Insert(0, toMoveDefs[i]);

        // ── Remove moved filters from display root ──
        var newNodeFilters = new List<SpecialThingFilterDef>(catDef.childSpecialFilters);
        newNodeFilters.RemoveAll(f => toMoveSet.Contains(f.defName));

        // Assign
        catDef.childCategories = newCats;
        catDef.childSpecialFilters = newNodeFilters;
        filterCat.childSpecialFilters = newCatFilters;
        CurrentDisplayRoot = catDef;
        _swapped = true;
    }

    static void Postfix(TreeNode_ThingCategory node)
    {
        CurrentDisplayRoot = null;
        if (!_swapped) return;

        var catDef = node.catDef;

        catDef.parent = _savedDisplayRootParent;
        catDef.childCategories = _savedCatChildren;
        catDef.childSpecialFilters = _savedNodeFilters;

        var filterCat = _filterCatWasInserted
            ? DefDatabase<ThingCategoryDef>.GetNamed(FilterCatDefName)
            : catDef.childCategories.FirstOrDefault(c => c.defName == FilterCatDefName);

        if (filterCat != null)
            filterCat.childSpecialFilters = _savedFilterCatFilters;
    }
}
