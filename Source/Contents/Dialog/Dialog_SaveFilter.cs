using System.Collections.Generic;
using System.IO;
using System.Linq;
using BetterFilter.Contents.Filters;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Dialog;

/// <summary>
/// Dialog for saving the current filter-component list as a command-line file.
/// Shows existing saved filter sets (rename/overwrite per row) and a text input
/// for creating new saves.
/// </summary>
public class Dialog_SaveFilter : Window
{
    private readonly Dialog_AdvancedFilters _parent;
    private readonly List<FilterComponent> _components;

    private string _newFilename = "";
    private string _renamingFile = null;
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
        float bottomMargin = 12f;
        float rowH = 28f;
        float inputW = 300f;
        float btnW = 60f;
        float totalW = inputW + 6f + btnW;
        float inputY = inRect.yMax - bottomMargin - rowH;
        float startX = inRect.center.x - totalW / 2f;

        Rect inputRect = new Rect(startX, inputY, inputW, rowH);
        _newFilename = Widgets.TextField(inputRect, _newFilename);

        Rect saveBtn = new Rect(inputRect.xMax + 6f, inputY, btnW, rowH);
        if (Widgets.ButtonText(saveBtn, "UR.BetterFilter.Save".Translate()))
        {
            string name = _newFilename.Trim();
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
        float scrollAreaH = inputY - inRect.y - 6f;
        float listH = Mathf.Max(scrollAreaH, files.Count * 32f);
        Rect listView = new Rect(0f, 0f, inner.width - 16f, listH);
        Rect scrollRect = new Rect(inner.x, inner.y, inner.width, scrollAreaH);
        Widgets.BeginScrollView(scrollRect, ref _listScroll, listView);

        float ry = 0f;
        foreach (string name in files)
        {
            float btnAreaW = 190f;
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
                float btnX = rowRect.xMax - 24f;
                Rect delBtn = new Rect(btnX, rowRect.y + 2f, 20f, 20f);
                if (Widgets.ButtonImage(delBtn, TexButton.Delete))
                {
                    DoDelete(name);
                    break;
                }
                btnX -= 86f;

                Rect overwriteBtn = new Rect(btnX, rowRect.y, 80f, 24f);
                if (Widgets.ButtonText(overwriteBtn, "UR.BetterFilter.Overwrite".Translate()))
                {
                    DoSave(name);
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

    private void DoSave(string filename)
    {
        try
        {
            Directory.CreateDirectory(SaveDir);
            string path = Path.Combine(SaveDir, filename + ".txt");

            // Non-item components: one line per component
            var lines = _components
                .Where(c => c is not FilterComponent_Item)
                .Select(c => c.ToCommandLine())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            // Item components: group by isWhitelist for compact output
            var itemGroups = _components
                .OfType<FilterComponent_Item>()
                .GroupBy(it => it.isWhitelist);
            foreach (var group in itemGroups)
            {
                var parts = new List<string> { "item", "-d" };
                foreach (var it in group)
                {
                    if (it.selectedDef != null)
                        parts.Add(it.selectedDef.defName);
                    else if (it.errorDefName != null)
                        parts.Add(it.errorDefName);
                }
                if (parts.Count <= 2) continue; // no defs
                if (!group.Key) parts.Add("-b");
                parts.Add("-u");
                lines.Add(string.Join(" ", parts));
            }

            File.WriteAllLines(path, lines);
        }
        catch (System.Exception ex)
        {
            Log.Error("[BetterFilter] Failed to save filter set: " + ex.Message);
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
