using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BetterFilter.Contents.Utils;

/// <summary>
/// Independent clipboard for outfit / food policy filter copy-paste.
/// Separate from vanilla StorageSettingsClipboard and from storage-zone clipboard.
/// </summary>
public static class PolicyFilterClipboard
{
    private static ThingFilter _apparelClipboard;
    private static ThingFilter _foodClipboard;
    private static bool _apparelCopied;
    private static bool _foodCopied;

    public static bool HasClipboard(bool isApparel) =>
        isApparel ? _apparelCopied : _foodCopied;

    public static ThingFilter GetClipboard(bool isApparel) =>
        isApparel ? _apparelClipboard : _foodClipboard;

    public static void Copy(bool isApparel, ThingFilter source)
    {
        var cb = new ThingFilter();
        cb.CopyAllowancesFrom(source);
        if (isApparel) { _apparelClipboard = cb; _apparelCopied = true; }
        else { _foodClipboard = cb; _foodCopied = true; }
    }

    public static bool IsOutfitOrFoodDialog()
    {
        return Find.WindowStack.Windows
            .Any(w => w is Dialog_ManageApparelPolicies
                   || w is Dialog_ManageFoodPolicies);
    }

    public static bool IsApparelDialog()
    {
        return Find.WindowStack.Windows
            .Any(w => w is Dialog_ManageApparelPolicies);
    }

    /// <summary>Compute context-relevant defs for the open policy dialog.</summary>
    public static List<ThingDef> GetContextDefs()
    {
        if (IsApparelDialog())
        {
            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.PlayerAcquirable && d.virtualDefParent == null
                    && d.apparel != null)
                .ToList();
        }
        return DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d => d.PlayerAcquirable && d.virtualDefParent == null
                && d.GetStatValueAbstract(StatDefOf.Nutrition) > 0f)
            .ToList();
    }

    /// <summary>AND / OR overlay paste logic (shared with Command_StoragePaste).</summary>
    public static void PasteInto(ThingFilter target, ThingFilter clipboard, bool isAnd)
    {
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
        {
            if (!def.PlayerAcquirable || def.virtualDefParent != null) continue;
            target.SetAllow(def, isAnd
                ? target.Allows(def) && clipboard.Allows(def)
                : target.Allows(def) || clipboard.Allows(def));
        }

        foreach (SpecialThingFilterDef sf in DefDatabase<SpecialThingFilterDef>.AllDefs)
            target.SetAllow(sf, isAnd
                ? target.Allows(sf) && clipboard.Allows(sf)
                : target.Allows(sf) || clipboard.Allows(sf));

        // 滑块过滤器的合并覆盖容易引起用户的意料外修改，停用此部分
        // var hp = target.AllowedHitPointsPercents;
        // var cbHp = clipboard.AllowedHitPointsPercents;
        // if (isAnd)
        // {
        //     float hpMin = Mathf.Max(hp.min, cbHp.min);
        //     float hpMax = Mathf.Min(hp.max, cbHp.max);
        //     if (hpMin > hpMax) hpMax = hpMin;
        //     target.AllowedHitPointsPercents = new FloatRange(hpMin, hpMax);
        // }
        // else
        // {
        //     target.AllowedHitPointsPercents = new FloatRange(
        //         Mathf.Min(hp.min, cbHp.min), Mathf.Max(hp.max, cbHp.max));
        // }

        // 滑块过滤器的合并覆盖容易引起用户的意料外修改，停用此部分
        // var q = target.AllowedQualityLevels;
        // var cbQ = clipboard.AllowedQualityLevels;
        // if (isAnd)
        // {
        //     int qMin = Mathf.Max((int)q.min, (int)cbQ.min);
        //     int qMax = Mathf.Min((int)q.max, (int)cbQ.max);
        //     if (qMin > qMax) qMax = qMin;
        //     target.AllowedQualityLevels = new QualityRange((QualityCategory)qMin, (QualityCategory)qMax);
        // }
        // else
        // {
        //     target.AllowedQualityLevels = new QualityRange(
        //         (QualityCategory)Mathf.Min((int)q.min, (int)cbQ.min),
        //         (QualityCategory)Mathf.Max((int)q.max, (int)cbQ.max));
        // }
    }

    public static void ShowPasteMenu(ThingFilter filter, bool isApparel)
    {
        var cb = GetClipboard(isApparel);
        if (cb == null) return;

        var opts = new List<FloatMenuOption>
        {
            new FloatMenuOption("UR.BetterFilter.PasteOverwrite".Translate(), () =>
            {
                filter.CopyAllowancesFrom(cb);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }),
            new FloatMenuOption("UR.BetterFilter.PasteAnd".Translate(), () =>
            {
                PasteInto(filter, cb, isAnd: true);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                Messages.Message("UR.BetterFilter.PasteAndMessage".Translate(),
                    MessageTypeDefOf.NeutralEvent, false);
            }),
            new FloatMenuOption("UR.BetterFilter.PasteOr".Translate(), () =>
            {
                PasteInto(filter, cb, isAnd: false);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                Messages.Message("UR.BetterFilter.PasteOrMessage".Translate(),
                    MessageTypeDefOf.NeutralEvent, false);
            })
        };
        Find.WindowStack.Add(new FloatMenu(opts));
    }
}
