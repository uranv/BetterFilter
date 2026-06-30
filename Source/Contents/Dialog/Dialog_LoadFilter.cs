using System.Collections.Generic;
using System.IO;
using System.Linq;
using BetterFilter.Contents.Filters;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Dialog;

/// <summary>
/// Dialog for loading a saved filter set.
/// Each row: Rename, Overwrite (replaces current filters), Append (adds to end), Delete.
/// </summary>
public class Dialog_LoadFilter : Window
{
    private readonly Dialog_AdvancedFilters _parent;

    private string _renamingFile;
    private string _renameBuffer = "";
    private Vector2 _listScroll = Vector2.zero;

    private static string SaveDir =>
        Path.Combine(GenFilePaths.ConfigFolderPath, "BetterFilter", "SavedFilters");

    private static List<string> SavedFiles
    {
        get
        {
            try
            {
                Directory.CreateDirectory(SaveDir);
                return Directory.GetFiles(SaveDir, "*.txt")
                    .Select(Path.GetFileNameWithoutExtension)
                    .OrderBy(n => n)
                    .ToList();
            }
            catch { return new List<string>(); }
        }
    }

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
        float scrollAreaH = Mathf.Min(inner.height - 12f, Mathf.Max(120f, files.Count * 32f + 8f));
        float listH = Mathf.Max(scrollAreaH, files.Count * 32f);
        Rect listView = new Rect(0f, 0f, inner.width - 16f, listH);
        Rect scrollRect = new Rect(inner.x, inner.y, inner.width, scrollAreaH);
        Widgets.BeginScrollView(scrollRect, ref _listScroll, listView);

        float ry = 0f;
        foreach (string name in files)
        {
            float btnAreaW = 260f; // rename(70) + overwrite(80) + append(60) + delete(24) + gaps(18+6)
            Rect rowRect = new Rect(0f, ry, listView.width, 28f);

            if (_renamingFile == name)
            {
                Rect renameRect = new Rect(rowRect.x, rowRect.y, rowRect.width - btnAreaW, 24f);
                _renameBuffer = Widgets.TextField(renameRect, _renameBuffer);

                Rect okRect = new Rect(renameRect.xMax + 6f, rowRect.y, 50f, 24f);
                if (Widgets.ButtonText(okRect, "OK"))
                {
                    string newName = _renameBuffer.Trim();
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

                Rect cancelRect = new Rect(okRect.xMax + 6f, rowRect.y, 80f, 24f);
                if (Widgets.ButtonText(cancelRect, "CancelButton".Translate()))
                    _renamingFile = null;
            }
            else
            {
                // Normal row: label + rename + overwrite + append + delete
                float btnX = rowRect.xMax - 24f;
                Rect delBtn = new Rect(btnX, rowRect.y + 2f, 20f, 20f);
                if (Widgets.ButtonImage(delBtn, TexButton.Delete))
                {
                    DoDelete(name);
                    break;
                }
                btnX -= 66f;

                Rect appendBtn = new Rect(btnX, rowRect.y, 60f, 24f);
                if (Widgets.ButtonText(appendBtn, "UR.BetterFilter.Append".Translate()))
                {
                    DoLoad(name, append: true);
                    Close();
                }
                btnX -= 86f;

                Rect overwriteBtn = new Rect(btnX, rowRect.y, 80f, 24f);
                if (Widgets.ButtonText(overwriteBtn, "UR.BetterFilter.Overwrite".Translate()))
                {
                    DoLoad(name, append: false);
                    Close();
                }
                btnX -= 76f;

                Rect renameBtn = new Rect(btnX, rowRect.y, 70f, 24f);
                if (Widgets.ButtonText(renameBtn, "UR.BetterFilter.Rename".Translate()))
                {
                    _renamingFile = name;
                    _renameBuffer = name;
                }

                Rect labelRect = new Rect(rowRect.x, rowRect.y, btnX - rowRect.x - 6f, 24f);
                Widgets.Label(labelRect, name);
            }

            ry += 32f;
        }

        if (files.Count == 0)
        {
            Rect hintRect = new Rect(0f, ry, listView.width, 24f);
            Widgets.Label(hintRect, "UR.BetterFilter.NoSavedFilters".Translate());
        }

        Widgets.EndScrollView();
    }

    private void DoLoad(string filename, bool append)
    {
        try
        {
            string path = Path.Combine(SaveDir, filename + ".txt");
            if (!File.Exists(path)) return;

            string content = File.ReadAllText(path).Trim();
            if (string.IsNullOrEmpty(content)) return;

            if (!append)
                _parent.components.Clear();

            // Reuse command parser: comma-separated lines
            var lines = content.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0);
            string combined = string.Join(",", lines);
            Dialog_CommandInput.TryParseMulti(combined, _parent.components);
            _parent.MarkDirty();
        }
        catch (System.Exception ex)
        {
            Log.Error("[BetterFilter] Failed to load filter set: " + ex.Message);
        }
    }

    private void DoDelete(string filename)
    {
        try
        {
            string path = Path.Combine(SaveDir, filename + ".txt");
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (System.Exception ex)
        {
            Log.Error("[BetterFilter] Failed to delete filter set: " + ex.Message);
        }
    }

    private void DoRename(string oldName, string newName)
    {
        try
        {
            string oldPath = Path.Combine(SaveDir, oldName + ".txt");
            string newPath = Path.Combine(SaveDir, newName + ".txt");
            if (File.Exists(oldPath) && !File.Exists(newPath))
            {
                File.Move(oldPath, newPath);
                _renamingFile = null;
            }
        }
        catch (System.Exception ex)
        {
            Log.Error("[BetterFilter] Failed to rename filter set: " + ex.Message);
        }
    }
}
