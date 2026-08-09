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
    private readonly string _title;
    private readonly List<(string label, Action onSelect)> _allItems;
    private List<(string label, Action onSelect)> _filteredItems;
    private string _searchText = "";
    private Vector2 _scrollPos;

    public Dialog_SearchableSelect(string title, IEnumerable<(string label, Action onSelect)> items)
    {
        _title = title;
        _allItems = items.ToList();
        _filteredItems = new List<(string label, Action onSelect)>(_allItems);
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
        var titleRect = inRect.TopPartPixels(28f);
        Text.Font = GameFont.Medium;
        Widgets.Label(titleRect, _title);
        Text.Font = GameFont.Small;
        inRect.yMin += 30f;

        // ── Search field ──
        var searchRect = inRect.TopPartPixels(28f);
        var newSearch = Widgets.TextField(searchRect, _searchText);
        if (newSearch != _searchText)
        {
            _searchText = newSearch;
            UpdateFilter();
        }
        inRect.yMin += 34f;

        // ── Results ──
        var listRect = inRect.ContractedBy(2f);
        if (_filteredItems.Count == 0)
        {
            GUI.color = Color.gray;
            Widgets.Label(listRect, "UR.BetterFilter.NoSearchResults".Translate());
            GUI.color = Color.white;
            return;
        }

        var rowH = 24f;
        var totalH = _filteredItems.Count * rowH;
        var viewRect = new Rect(0f, 0f, listRect.width - 16f, totalH);
        Widgets.BeginScrollView(listRect, ref _scrollPos, viewRect);
        var y = 0f;
        foreach (var item in _filteredItems)
        {
            var rowRect = new Rect(0f, y, viewRect.width, rowH);
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
        if (string.IsNullOrEmpty(_searchText))
        {
            _filteredItems = new List<(string, Action)>(_allItems);
        }
        else
        {
            var q = _searchText.ToLowerInvariant();
            _filteredItems = _allItems
                .Where(it => it.label.ToLowerInvariant().Contains(q))
                .ToList();
        }
    }

    public override void OnAcceptKeyPressed()
    {
        if (_filteredItems.Count > 0)
        {
            _filteredItems[0].onSelect();
            Close();
        }
    }
}
