using UnityEngine;
using Verse;

namespace BetterFilter.Contents;

/// <summary>
/// Centralised texture references for the BetterFilter mod.
/// All icon textures are resolved once at static init and reused.
/// </summary>
[StaticConstructorOnStartup]
public static class TexBetterFilter
{
    // ── Tree expand / collapse ──
    public static readonly Texture2D ExpandIcon;
    public static readonly Texture2D CollapseIcon;

    // ── Whitelist / blacklist badges (strict) ──
    public static readonly Texture2D WhiteListBadge;
    public static readonly Texture2D BlackListBadge;

    // ── Loose-mode badges (numeric filter) ──
    public static readonly Texture2D LooseWhiteBadge;
    public static readonly Texture2D LooseBlackBadge;

    // ── Control buttons ──
    public static readonly Texture2D OptionIcon;   // [s] strict/loose toggle
    public static readonly Texture2D PlusIcon;     // [+] add item
    public static readonly Texture2D MinusIcon;    // [-] remove last item
    public static readonly Texture2D DeleteIcon;   // [X] delete module

    static TexBetterFilter()
    {
        ExpandIcon = ContentFinder<Texture2D>.Get("UI/arrow.up.and.line.horizontal.and.arrow.down");
        CollapseIcon = ContentFinder<Texture2D>.Get("UI/arrow.down.and.line.horizontal.and.arrow.up");
        WhiteListBadge = ContentFinder<Texture2D>.Get("UI/text.badge.checkmark");
        BlackListBadge = ContentFinder<Texture2D>.Get("UI/text.badge.xmark");
        LooseWhiteBadge = ContentFinder<Texture2D>.Get("UI/text.badge.plus");
        LooseBlackBadge = ContentFinder<Texture2D>.Get("UI/text.badge.minus");
        // Badge icons (large, top-left) — text.badge.* series
        // Button icons (small, right-aligned) — plain icon files
        OptionIcon = ContentFinder<Texture2D>.Get("UI/option");
        PlusIcon = ContentFinder<Texture2D>.Get("UI/plus");
        MinusIcon = ContentFinder<Texture2D>.Get("UI/minus");
        DeleteIcon = ContentFinder<Texture2D>.Get("UI/xmark");
    }
}
