using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BetterFilter.Contents.Filters;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Dialog;

/// <summary>
/// Dialog for saving the current filter-component list as an XML filter set.
/// Shows existing saved filter sets (rename/overwrite per row) and a text input
/// for creating new saves.
/// </summary>
public class Dialog_SaveFilter : Window
{
    private readonly Dialog_AdvancedFilters _parent;
    private readonly List<FilterComponent> _components;

    private string _newFilename = "";
    private string _renamingFile;
    private string _renameBuffer = "";
    private Vector2 _listScroll = Vector2.zero;

    private static string SaveDir => FilterSetStorage.SaveDir;

    private static List<string> SavedFiles => FilterSetStorage.SavedFiles;

    public Dialog_SaveFilter(Dialog_AdvancedFilters parent, List<FilterComponent> components)
    {
        _parent = parent;
        _components = components;
        forcePause = true;
        draggable = true;
        doCloseX = true;
        closeOnClickedOutside = false;
        closeOnAccept = false;
        absorbInputAroundWindow = false;
        layer = WindowLayer.Super;
    }

    public override Vector2 InitialSize => new Vector2(500f, 400f);

    public override void DoWindowContents(Rect inRect)
    {
        var inner = inRect.ContractedBy(12f);

        // ── Bottom input row (centered, tight to bottom) ──
        var bottomMargin = 12f;
        var rowH = 28f;
        var inputW = 300f;
        var btnW = 60f;
        var totalW = inputW + 6f + btnW;
        var inputY = inRect.yMax - bottomMargin - rowH;
        var startX = inRect.center.x - totalW / 2f;

        var inputRect = new Rect(startX, inputY, inputW, rowH);
        _newFilename = Widgets.TextField(inputRect, _newFilename);

        var saveBtn = new Rect(inputRect.xMax + 6f, inputY, btnW, rowH);
        if (Widgets.ButtonText(saveBtn, "UR.BetterFilter.Save".Translate()))
        {
            var name = _newFilename.Trim();
            if (string.IsNullOrEmpty(name))
            {
                Messages.Message("UR.BetterFilter.SaveEmptyName".Translate(),
                    MessageTypeDefOf.RejectInput, false);
            }
            else if (SavedFiles.Contains(name))
            {
                Messages.Message("UR.BetterFilter.SaveDuplicateName".Translate(),
                    MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                DoSave(name);
                Close();
            }
        }

        // ── Existing files list (fills remaining space above input) ──
        var files = SavedFiles;
        var scrollAreaH = inputY - inRect.y - 6f;
        var listH = Mathf.Max(scrollAreaH, files.Count * 32f);
        var listView = new Rect(0f, 0f, inner.width - 16f, listH);
        var scrollRect = new Rect(inner.x, inner.y, inner.width, scrollAreaH);
        Widgets.BeginScrollView(scrollRect, ref _listScroll, listView);

        var ry = 0f;
        foreach (var name in files)
        {
            var btnAreaW = 190f;
            var rowRect = new Rect(0f, ry, listView.width, 28f);

            if (_renamingFile == name)
            {
                var renameRect = new Rect(rowRect.x, rowRect.y, rowRect.width - btnAreaW, 24f);
                _renameBuffer = Widgets.TextField(renameRect, _renameBuffer);

                var okRect = new Rect(renameRect.xMax + 6f, rowRect.y, 50f, 24f);
                if (Widgets.ButtonText(okRect, "OK"))
                {
                    var newName = _renameBuffer.Trim();
                    if (!string.IsNullOrEmpty(newName) && newName != name)
                    {
                        if (SavedFiles.Contains(newName))
                            Messages.Message("UR.BetterFilter.SaveDuplicateName".Translate(),
                                MessageTypeDefOf.RejectInput, false);
                        else
                            DoRename(name, newName);
                    }
                    else
                    {
                        _renamingFile = null;
                    }
                }

                var cancelRect = new Rect(okRect.xMax + 6f, rowRect.y, 80f, 24f);
                if (Widgets.ButtonText(cancelRect, "CancelButton".Translate()))
                    _renamingFile = null;
            }
            else
            {
                var btnX = rowRect.xMax - 24f;
                var delBtn = new Rect(btnX, rowRect.y + 2f, 20f, 20f);
                if (Widgets.ButtonImage(delBtn, TexButton.Delete))
                {
                    DoDelete(name);
                    break;
                }
                btnX -= 86f;

                var overwriteBtn = new Rect(btnX, rowRect.y, 80f, 24f);
                if (Widgets.ButtonText(overwriteBtn, "UR.BetterFilter.Overwrite".Translate()))
                {
                    DoSave(name);
                    Close();
                }
                btnX -= 76f;

                var renameBtn = new Rect(btnX, rowRect.y, 70f, 24f);
                if (Widgets.ButtonText(renameBtn, "UR.BetterFilter.Rename".Translate()))
                {
                    _renamingFile = name;
                    _renameBuffer = name;
                }

                var labelRect = new Rect(rowRect.x, rowRect.y, btnX - rowRect.x - 6f, 24f);
                Widgets.Label(labelRect, name);
            }

            ry += 32f;
        }

        if (files.Count == 0)
        {
            var hintRect = new Rect(0f, ry, listView.width, 24f);
            Widgets.Label(hintRect, "UR.BetterFilter.NoSavedFilters".Translate());
        }

        Widgets.EndScrollView();
    }

    private void DoSave(string filename)
    {
        try
        {
            FilterSetStorage.Save(filename, _components);
        }
        catch (Exception ex)
        {
            Log.Error("[BetterFilter] Failed to save filter set: " + ex.Message);
        }
    }

    private void DoDelete(string filename)
    {
        try
        {
            var path = Path.Combine(SaveDir, filename + ".xml");
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Error("[BetterFilter] Failed to delete filter set: " + ex.Message);
        }
    }

    private void DoRename(string oldName, string newName)
    {
        try
        {
            var oldPath = Path.Combine(SaveDir, oldName + ".xml");
            var newPath = Path.Combine(SaveDir, newName + ".xml");
            if (File.Exists(oldPath) && !File.Exists(newPath))
            {
                File.Move(oldPath, newPath);
                _renamingFile = null;
            }
        }
        catch (Exception ex)
        {
            Log.Error("[BetterFilter] Failed to rename filter set: " + ex.Message);
        }
    }
}
