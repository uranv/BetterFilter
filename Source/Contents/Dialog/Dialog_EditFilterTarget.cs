using System;
using System.Collections.Generic;
using BetterFilter.Contents.Utils;
using UnityEngine;
using Verse;

namespace BetterFilter.Contents.Dialog;

/// <summary>Editor for a user-defined numeric or enum runtime XPath target.</summary>
public class Dialog_EditFilterTarget : Window
{
    private readonly Action<FilterTargetDefinition> _onSave;
    private readonly Func<FilterTargetDefinition, bool> _isDuplicate;
    private readonly FilterTargetDefinition _working;
    private string _error;

    public Dialog_EditFilterTarget(FilterTargetDefinition existing,
        Action<FilterTargetDefinition> onSave,
        Func<FilterTargetDefinition, bool> isDuplicate)
    {
        _working = existing?.Clone() ?? new FilterTargetDefinition();
        _onSave = onSave;
        _isDuplicate = isDuplicate;
        forcePause = true;
        draggable = true;
        doCloseX = true;
        closeOnClickedOutside = false;
        closeOnAccept = false;
        absorbInputAroundWindow = true;
        layer = WindowLayer.Super;
    }

    public override Vector2 InitialSize => new(580f, 300f);

    public override void DoWindowContents(Rect inRect)
    {
        var inner = inRect.ContractedBy(12f);
        var rowH = 30f;
        var labelW = 100f;
        var inputX = inner.x + labelW + 8f;
        var inputW = inner.width - labelW - 8f;

        Widgets.Label(new Rect(inner.x, inner.y, labelW, rowH),
            "UR.BetterFilter.TargetType".Translate());
        if (Widgets.ButtonText(new Rect(inputX, inner.y, inputW, rowH),
                DisplayType(_working.Type)))
        {
            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
            {
                new("UR.BetterFilter.NumericFilter".Translate(),
                    () => _working.Type = "Numeric"),
                new("UR.BetterFilter.EnumFilter".Translate(),
                    () => _working.Type = "Enum")
            }));
        }

        var nameY = inner.y + rowH + 8f;
        Widgets.Label(new Rect(inner.x, nameY, labelW, rowH),
            "UR.BetterFilter.TargetName".Translate());
        _working.Name = Widgets.TextField(
            new Rect(inputX, nameY, inputW, rowH), _working.Name ?? "");

        var pathY = nameY + rowH + 8f;
        Widgets.Label(new Rect(inner.x, pathY, labelW, rowH),
            "UR.BetterFilter.TargetPath".Translate());
        _working.XPath = Widgets.TextField(
            new Rect(inputX, pathY, inputW, rowH), _working.XPath ?? "");

        if (!string.IsNullOrEmpty(_error))
        {
            var old = GUI.color;
            GUI.color = Color.red;
            Widgets.Label(new Rect(inner.x, pathY + rowH + 8f, inner.width, 42f), _error);
            GUI.color = old;
        }

        var buttonY = inner.yMax - 30f;
        var buttonW = (inner.width - 8f) / 2f;
        if (Widgets.ButtonText(new Rect(inner.x, buttonY, buttonW, 30f), "OK"))
        {
            if (TrySave()) Close();
        }
        if (Widgets.ButtonText(new Rect(inner.x + buttonW + 8f, buttonY, buttonW, 30f),
                "CancelButton".Translate()))
            Close();
    }

    private bool TrySave()
    {
        if (string.IsNullOrWhiteSpace(_working.Name))
        {
            _error = "UR.BetterFilter.TargetNameRequired".Translate();
            return false;
        }
        if (string.IsNullOrWhiteSpace(_working.XPath))
        {
            _error = "UR.BetterFilter.TargetPathRequired".Translate();
            return false;
        }
        if (!FilterTargetCatalog.TryParseKind(_working.Type, out var kind))
        {
            _error = "UR.BetterFilter.TargetTypeInvalid".Translate();
            return false;
        }
        if (!RuntimeFilterPath.TryValidate(_working.XPath, kind, out var validationError))
        {
            _error = validationError;
            return false;
        }
        if (_isDuplicate?.Invoke(_working) == true)
        {
            _error = "UR.BetterFilter.TargetDuplicate".Translate();
            return false;
        }

        _onSave(_working.Clone());
        return true;
    }

    private static string DisplayType(string type) =>
        FilterTargetCatalog.TryParseKind(type, out var kind)
            ? kind == FilterTargetKind.Numeric
                ? "UR.BetterFilter.NumericFilter".Translate()
                : "UR.BetterFilter.EnumFilter".Translate()
            : type;
}
