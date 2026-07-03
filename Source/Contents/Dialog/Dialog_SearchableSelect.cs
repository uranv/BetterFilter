using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Dialog;

/// <summary>
/// Small popup with a search field and a scrollable filtered list.
/// Items are rendered as clickable text (not buttons).  Layer is set above
/// the parent Dialog_AdvancedFilters so it is always usable.
/// </summary>
public class Dialog_SearchableSelect : Window
{
    private readonly string title;
    private readonly List<(string label, Action onSelect)> allItems;
    private List<(string label, Action onSelect)> filteredItems;
    private string searchText = "";
    private Vector2 scrollPos;

    public Dialog_SearchableSelect(string title, IEnumerable<(string label, Action onSelect)> items)
    {
        this.title = title;
        allItems = items.ToList();
        filteredItems = new List<(string label, Action onSelect)>(allItems);
        forcePause = true;
        draggable = true;
        doCloseX = true;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
        layer = WindowLayer.Dialog;
    }

    public override Vector2 InitialSize => new Vector2(360f, 420f);

    public override void DoWindowContents(Rect inRect)
    {
        Rect titleRect = inRect.TopPartPixels(28f);
        Text.Font = GameFont.Medium;
        Widgets.Label(titleRect, title);
        Text.Font = GameFont.Small;
        inRect.yMin += 30f;

        // ── Search field ──
        Rect searchRect = inRect.TopPartPixels(28f);
        string newSearch = Widgets.TextField(searchRect, searchText);
        if (newSearch != searchText)
        {
            searchText = newSearch;
            UpdateFilter();
        }
        inRect.yMin += 34f;

        // ── Results ──
        Rect listRect = inRect.ContractedBy(2f);
        if (filteredItems.Count == 0)
        {
            GUI.color = Color.gray;
            Widgets.Label(listRect, "UR.BetterFilter.NoSearchResults".Translate());
            GUI.color = Color.white;
            return;
        }

        float rowH = 24f;
        float totalH = filteredItems.Count * rowH;
        Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, totalH);
        Widgets.BeginScrollView(listRect, ref scrollPos, viewRect);
        float y = 0f;
        foreach (var item in filteredItems)
        {
            Rect rowRect = new Rect(0f, y, viewRect.width, rowH);
            if (Widgets.ButtonInvisible(rowRect))
            {
                item.onSelect();
                Close();
                break;
            }
            // Draw label on top of the invisible button
            Widgets.Label(rowRect, item.label);
            y += rowH;
        }
        Widgets.EndScrollView();
    }

    private void UpdateFilter()
    {
        if (string.IsNullOrEmpty(searchText))
        {
            filteredItems = new List<(string, Action)>(allItems);
        }
        else
        {
            string q = searchText.ToLowerInvariant();
            filteredItems = allItems
                .Where(it => it.label.ToLowerInvariant().Contains(q))
                .ToList();
        }
    }

    public override void OnAcceptKeyPressed()
    {
        if (filteredItems.Count > 0)
        {
            filteredItems[0].onSelect();
            Close();
        }
    }
}
