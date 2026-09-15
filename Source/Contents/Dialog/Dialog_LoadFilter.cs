using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Dialog;

/// <summary>
/// Dialog for loading a saved XML filter set.
/// Each row: Rename, Overwrite (replaces current filters), Append (adds to end), Delete.
/// </summary>
public class Dialog_LoadFilter : Window
{
    private readonly Dialog_AdvancedFilters _parent;

    private string _renamingFile;
    private string _renameBuffer = "";
    private Vector2 _listScroll = Vector2.zero;

    private static string SaveDir => FilterSetStorage.SaveDir;

    private static List<string> SavedFiles => FilterSetStorage.SavedFiles;

    public Dialog_LoadFilter(Dialog_AdvancedFilters parent)
    {
        _parent = parent;
        forcePause = true;
        draggable = true;
        doCloseX = true;
        closeOnClickedOutside = false;
        closeOnAccept = false;
        absorbInputAroundWindow = false;
        layer = WindowLayer.Super;
    }

    public override Vector2 InitialSize => new Vector2(540f, 380f);

    public override void DoWindowContents(Rect inRect)
    {
        var inner = inRect.ContractedBy(12f);

        var files = SavedFiles;
        var scrollAreaH = Mathf.Min(inner.height - 12f, Mathf.Max(120f, files.Count * 32f + 8f));
        var listH = Mathf.Max(scrollAreaH, files.Count * 32f);
        var listView = new Rect(0f, 0f, inner.width - 16f, listH);
        var scrollRect = new Rect(inner.x, inner.y, inner.width, scrollAreaH);
        Widgets.BeginScrollView(scrollRect, ref _listScroll, listView);

        var ry = 0f;
        foreach (var name in files)
        {
            var btnAreaW = 260f; // rename(70) + overwrite(80) + append(60) + delete(24) + gaps(18+6)
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
                // Normal row: label + rename + overwrite + append + delete
                var btnX = rowRect.xMax - 24f;
                var delBtn = new Rect(btnX, rowRect.y + 2f, 20f, 20f);
                if (Widgets.ButtonImage(delBtn, TexButton.Delete))
                {
                    DoDelete(name);
                    break;
                }
                btnX -= 66f;

                var appendBtn = new Rect(btnX, rowRect.y, 60f, 24f);
                if (Widgets.ButtonText(appendBtn, "UR.BetterFilter.Append".Translate()))
                {
                    DoLoad(name, append: true);
                    Close();
                }
                btnX -= 86f;

                var overwriteBtn = new Rect(btnX, rowRect.y, 80f, 24f);
                if (Widgets.ButtonText(overwriteBtn, "UR.BetterFilter.Overwrite".Translate()))
                {
                    DoLoad(name, append: false);
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

    private void DoLoad(string filename, bool append)
    {
        if (!FilterSetStorage.Load(filename, _parent.Components, append, out var error))
        {
            if (!string.IsNullOrEmpty(error))
                Log.Error("[BetterFilter] Failed to load filter set: " + error);
            return;
        }
        _parent.MarkDirty();
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
