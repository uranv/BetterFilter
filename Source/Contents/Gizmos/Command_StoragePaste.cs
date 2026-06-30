using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BetterFilter.Contents.Gizmos;

/// <summary>
/// Paste storage-settings command with right-click AND / OR sub-menu.
/// Left-click: vanilla overwrite paste.
/// Right-click: FloatMenu with Overwrite / AND / OR (via RightClickFloatMenuOptions).
/// </summary>
public class Command_StoragePaste : Command_Action
{
    private readonly StorageSettings _target;

    private static StorageSettings Clipboard =>
        AccessTools.StaticFieldRefAccess<StorageSettings>(
            typeof(StorageSettingsClipboard), "clipboard");

    public Command_StoragePaste(StorageSettings target)
    {
        _target = target;
        icon = ContentFinder<Texture2D>.Get("UI/Commands/PasteSettings");
        defaultLabel = "CommandPasteZoneSettingsLabel".Translate();
        defaultDesc = "CommandPasteZoneSettingsDesc".Translate()
                      + "\n" + "UR.BetterFilter.RightClickPaste".Translate();
        hotKey = KeyBindingDefOf.Misc5;
        action = DoVanillaPaste;
        if (!StorageSettingsClipboard.HasCopiedSettings)
            Disable();
    }

    public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
    {
        get
        {
            yield return new FloatMenuOption(
                "UR.BetterFilter.PasteOverwrite".Translate(), DoVanillaPaste);
            yield return new FloatMenuOption(
                "UR.BetterFilter.PasteAnd".Translate(), DoPasteAnd);
            yield return new FloatMenuOption(
                "UR.BetterFilter.PasteOr".Translate(), DoPasteOr);
        }
    }

    private void DoVanillaPaste()
    {
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        StorageSettingsClipboard.PasteInto(_target);
    }

    private void DoPasteAnd()
    {
        var cb = Clipboard;
        if (cb == null) return;
        var f = _target.filter;
        var cf = cb.filter;

        // ThingDef allowances: AND
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
        {
            if (!def.PlayerAcquirable || def.virtualDefParent != null) continue;
            f.SetAllow(def, f.Allows(def) && cf.Allows(def));
        }

        // SpecialThingFilterDef: AND
        foreach (SpecialThingFilterDef sf in DefDatabase<SpecialThingFilterDef>.AllDefs)
            f.SetAllow(sf, f.Allows(sf) && cf.Allows(sf));

        // 滑块过滤器的合并覆盖容易引起用户的意料外修改，停用此部分
        // // HP range: AND (intersection)
        // var hp = f.AllowedHitPointsPercents;
        // var cbHp = cf.AllowedHitPointsPercents;
        // float hpMin = Mathf.Max(hp.min, cbHp.min);
        // float hpMax = Mathf.Min(hp.max, cbHp.max);
        // if (hpMin > hpMax) hpMax = hpMin;
        // f.AllowedHitPointsPercents = new FloatRange(hpMin, hpMax);
        //
        // // Quality: AND (intersection)
        // var q = f.AllowedQualityLevels;
        // var cbQ = cf.AllowedQualityLevels;
        // int qMin = Mathf.Max((int)q.min, (int)cbQ.min);
        // int qMax = Mathf.Min((int)q.max, (int)cbQ.max);
        // if (qMin > qMax) qMax = qMin;
        // f.AllowedQualityLevels = new QualityRange((QualityCategory)qMin, (QualityCategory)qMax);

        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        Messages.Message("UR.BetterFilter.PasteAndMessage".Translate(),
            MessageTypeDefOf.NeutralEvent, false);
    }

    private void DoPasteOr()
    {
        var cb = Clipboard;
        if (cb == null) return;
        var f = _target.filter;
        var cf = cb.filter;

        // ThingDef allowances: OR
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
        {
            if (!def.PlayerAcquirable || def.virtualDefParent != null) continue;
            f.SetAllow(def, f.Allows(def) || cf.Allows(def));
        }

        // SpecialThingFilterDef: OR
        foreach (SpecialThingFilterDef sf in DefDatabase<SpecialThingFilterDef>.AllDefs)
            f.SetAllow(sf, f.Allows(sf) || cf.Allows(sf));

        // 滑块过滤器的合并覆盖容易引起用户的意料外修改，停用此部分
        // // HP range: OR (union)
        // var hp = f.AllowedHitPointsPercents;
        // var cbHp = cf.AllowedHitPointsPercents;
        // f.AllowedHitPointsPercents = new FloatRange(
        //     Mathf.Min(hp.min, cbHp.min), Mathf.Max(hp.max, cbHp.max));
        //
        // // Quality: OR (union)
        // var q = f.AllowedQualityLevels;
        // var cbQ = cf.AllowedQualityLevels;
        // f.AllowedQualityLevels = new QualityRange(
        //     (QualityCategory)Mathf.Min((int)q.min, (int)cbQ.min),
        //     (QualityCategory)Mathf.Max((int)q.max, (int)cbQ.max));

        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        Messages.Message("UR.BetterFilter.PasteOrMessage".Translate(),
            MessageTypeDefOf.NeutralEvent, false);
    }
}
