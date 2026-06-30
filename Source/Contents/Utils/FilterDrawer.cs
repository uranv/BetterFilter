using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BetterFilter.Contents.Utils;

/// <summary>
/// Unified filter-component drawing helpers.
/// Every filter block shares the same visual structure:
///   [badge 32×32] | text area | [s][X]  ← top row
///   [badge 32×32] | text area | [-][+]  ← bottom row
/// </summary>
public static class FilterDrawer
{
    public const float BadgeSize = 32f;
    public const float BadgeMargin = 6f;
    public const float BtnSize = 16f;
    public const float BtnGap = 6f;

    /// <summary>
    /// Draw the top-left whitelist/blacklist badge and return the content-start X.
    /// Badge is always a whitelist/blacklist toggle.
    /// </summary>
    public static float DrawBadge(Rect inner, bool isWhitelist, bool isLoose,
        Action onToggle, string whitelistTipKey, string blacklistTipKey)
    {
        Rect r = new Rect(inner.x + BadgeMargin, inner.y + BadgeMargin, BadgeSize, BadgeSize);

        Texture2D tex;
        if (isLoose)
            tex = isWhitelist ? TexBetterFilter.LooseWhiteBadge : TexBetterFilter.LooseBlackBadge;
        else
            tex = isWhitelist ? TexBetterFilter.WhiteListBadge : TexBetterFilter.BlackListBadge;

        if (Widgets.ButtonImage(r, tex))
        {
            onToggle();
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }
        string tip = isWhitelist ? whitelistTipKey.Translate() : blacklistTipKey.Translate();
        TooltipHandler.TipRegion(r, tip);

        return r.xMax + 10f;
    }

    /// <summary>
    /// Draw the [X] delete button at top-right. Returns its rect.
    /// </summary>
    public static void DrawDeleteTopRight(Rect inner, ref bool pendingDelete)
    {
        Rect r = new Rect(inner.xMax - BtnSize, inner.y, BtnSize, BtnSize);
        if (Widgets.ButtonImage(r, TexBetterFilter.DeleteIcon))
        {
            pendingDelete = true;
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }
        TooltipHandler.TipRegion(r, "UR.BetterFilter.DeleteModuleTooltip".Translate());
    }

    /// <summary>
    /// Draw the [s] Loose/Strict toggle at top-right (left of delete).  Only for Numeric filter.
    /// </summary>
    public static void DrawOptionTopRight(Rect inner, bool isLoose, Action onToggle)
    {
        Rect r = new Rect(inner.xMax - BtnSize * 2f - BtnGap, inner.y, BtnSize, BtnSize);
        if (Widgets.ButtonImage(r, TexBetterFilter.OptionIcon))
        {
            onToggle();
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }
        string tip = isLoose
            ? "UR.BetterFilter.LooseTooltip".Translate()
            : "UR.BetterFilter.StrictTooltip".Translate();
        TooltipHandler.TipRegion(r, tip);
    }

    /// <summary>
    /// Draw the [-] remove-last button at bottom-right (left of [+]).  Only when hasItems.
    /// </summary>
    public static void DrawMinusBottomRight(Rect inner, bool hasItems, Action onRemove)
    {
        Rect r = new Rect(inner.xMax - BtnSize * 2f - BtnGap, inner.yMax - BtnSize, BtnSize, BtnSize);
        if (hasItems)
        {
            if (Widgets.ButtonImage(r, TexBetterFilter.MinusIcon))
            {
                onRemove();
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(r, "UR.BetterFilter.RemoveLastAddedTooltip".Translate());
        }
    }

    /// <summary>
    /// Draw the [+] add-item button at bottom-right.
    /// </summary>
    public static void DrawPlusBottomRight(Rect inner, Action onAdd, string tooltipKey)
    {
        Rect r = new Rect(inner.xMax - BtnSize, inner.yMax - BtnSize, BtnSize, BtnSize);
        if (Widgets.ButtonImage(r, TexBetterFilter.PlusIcon))
            onAdd();
        TooltipHandler.TipRegion(r, tooltipKey.Translate());
    }

    /// <summary>
    /// Width reserved by right-aligned top-row buttons (always 2 buttons).
    /// </summary>
    public static float TopRightWidth() => BtnSize * 2f + BtnGap;

    /// <summary>
    /// X position of the right edge of the text area (before top-right buttons).
    /// </summary>
    public static float TextRightX(Rect inner) =>
        inner.xMax - BtnSize * 2f - BtnGap - 10f;

    /// <summary>
    /// Maximum width available for text content (between badge and right buttons).
    /// </summary>
    public static float TextMaxWidth(Rect inner, float contentX) =>
        TextRightX(inner) - contentX;

    /// <summary>
    /// Height needed to render the given text within maxWidth (accounts for wrapping).
    /// </summary>
    public static float TextHeight(string text, float maxWidth) =>
        Mathf.Max(20f, Text.CalcHeight(text, maxWidth));

    /// <summary>
    /// Draw word-wrapping text in the content area.
    /// The block height must already be large enough (see MinBlockHeight).
    /// </summary>
    public static void DrawText(Rect inner, float contentX, string text)
    {
        float maxW = TextMaxWidth(inner, contentX);
        float textH = Text.CalcHeight(text, maxW);
        float topY = inner.y + BadgeMargin;
        Rect textRect = new Rect(contentX, topY, maxW, textH);
        Widgets.Label(textRect, text);
    }

    /// <summary>
    /// Compute text max width from the outer block layout width.
    /// </summary>
    public static float TextMaxWidthFromBlock(float blockWidth) =>
        blockWidth - 8f - BadgeMargin - BadgeSize - 10f - BtnSize * 2f - BtnGap - 10f;

    /// <summary>
    /// Minimum outer block height for a component to fit the given text content.
    /// blockWidth = viewRect.width passed to the component.
    /// </summary>
    public static float MinBlockHeight(string text, float blockWidth)
    {
        float maxW = TextMaxWidthFromBlock(blockWidth);
        float textH = Text.CalcHeight(text, maxW);
        float minInnerH = BadgeMargin + textH + BtnSize;
        return Mathf.Max(52f, minInnerH + 8f); // +8 for ContractedBy(4f) × 2
    }
}
