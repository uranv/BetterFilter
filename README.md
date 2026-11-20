# [UR] Better Filter for Storage, Food and Clothes Restrictions

**A better storage filter for RimWorld — faster category navigation, more flexible paste options, finer-grained item filtering.**

## Main Features

### Filter Tree Navigation

* 【Invert】button: a one-click toggle between "Clear All" and "Allow All" that flips every item's allow/disallow state. Does not affect filter workers themselves.

* 【Expand/Collapse One Level】buttons: two arrow buttons next to the search bar expand or collapse the category tree one level at a time, simplifying manual browsing.


### Paste Storage Settings Enhancement

* Left-click paste retains vanilla behavior (overwrite).

* Right-click the paste icon for two new overlay modes:
  - **Add Allowed Items**: currently disallowed + clipboard allowed → allowed
  - **Add Disallowed Items**: currently allowed + clipboard disallowed → disallowed


### Outfit / Food Policy Enhancement

* New 【Copy】【Paste】buttons with independent clipboards — outfits and food each have their own.

* New 【Advanced Filter】button opens a multi-condition filter builder with save/load support.

* Paste also supports Overwrite, Add Allowed, and Add Disallowed modes.


## New Filters

This mod adds 10 item filters in 5 complementary pairs:

* Perishable / Non-Perishable — whether refrigeration is needed

* Deteriorating / Non-Deteriorating — whether it degrades outdoors (a deterioration rate of 0 is treated as non-deteriorating)

* Has Quality / No Quality — whether it has quality grades

* Has Hit Points / No Hit Points — whether it has durability

* Non-Stackable / Stackable — stack limit is 1 or ≥ 2

All new filters are based on an item's inherent properties; results are predictable and do not change during gameplay.

## Filter Category & Apply

* Better Filter and vanilla filters are grouped into a pinned filter category for convenient access. Filters and sliders added by other mods stay where those mods put them; a filter of another mod moves into the category only when you mark it on the Filter Settings page in mod settings.

* The 【Apply】button in this category pushes filter-worker states down to individual items (disallowed filter → disallow matching items), then resets the filter workers to allowed. Only static filters take part; choose the participating filters on the Filter Settings page in mod settings.

<img src="./Preview/e24b519c79b59e64218ae83f9e6e8a76.png" alt="e24b519c79b59e64218ae83f9e6e8a76" style="zoom: 50%;" />

## Advanced Filter

The mod adds an 【Advanced Filter】entry point in both the storage filter and policy management screens, supporting complex filtering rules:

* Supports filter-worker matching, mod matching, category matching, numeric matching, enum matching, and manual per-item overrides

* Multiple filters are combined with **intersection** (logical AND): an item must pass every filter to be allowed

* Within a single filter, conditions are combined with **union** (logical OR): an item matching any one condition is allowed

* Each filter can switch between whitelist (allow matching) / blacklist (disallow matching) mode — click the badge on the left to toggle

* Numeric and enum filters support Loose (allow items without the property) / Strict (disallow items without the property) strategy — click the option icon at the top-right to toggle

<img src="./Preview/984fa83ba36dcf55c4d1a71e6547e23a.png" alt="984fa83ba36dcf55c4d1a71e6547e23a" style="zoom: 33%;" />

### About Each Filter Type

* **Worker Filter:** [+] to add matching filters (e.g. "Has Hit Points", "Adult Apparel", etc.), [-] to remove the most recently added filter. Multiple filters can be added. The list contains every filter with the Static type on the Filter Settings page in mod settings.

* **Mod Filter:** matches items by their source mod. [+] to add a mod name. Items from any selected mod are allowed.

* **Category Filter:** matches items by their item category (e.g. Food, Manufactured, Raw Resources, etc.). Items in any selected category are allowed.

* **Numeric Filter:** matches items by a numeric XPath target. Enter a min/max range to select items whose target value falls within that range. Loose strategy additionally allows items without the target.

* **Enum Filter:** matches items by an enumerable XPath target. Enter the target path and select values discovered from the loaded DefDatabase.

* **Manual Override:** add specific item definitions to manually determine the final match result. Override values take priority over all filter results. When an override is inactive (not changing the item's allow status), the item name appears in gray.


**Filter sets can be saved, loaded, and appended as XML files. When importing, names or targets that have disappeared due to mod changes are marked in gray and preserved for later recovery.**

Numeric and enum filters select a target from the built-in target list. Each target can be enabled or disabled on the Match Property Settings page in mod settings, and additional targets can be added there by specifying the type, display name, and a validated RimWorld-oriented XPath subset. The path is resolved against RimWorld's loaded DefDatabase rather than by serializing Defs to XML.

Examples of custom targets:

* `Defs/ThingDef/stackLimit` — stack limit
* `Defs/ThingDef/stats/MarketValue` — computed market value
* `Defs/ThingDef/weaponTags/li` — weapon tags
* `Defs/ThingDef/apparel/layers/li` — apparel layers
* `Defs/ThingDef/verbs/li[verbClass='Verb_Shoot']/range` — shooting range of shooting verbs

Saved sets are stored under `Config/BetterFilter/SavedFilters/*.xml`. Existing legacy `.txt` sets can be converted from the mod settings page; the original files are retained.



## Mod Settings

Three pages with the same layout:

* **Introduction:** feature overview, the option to turn off the new-filter letter, and the legacy `.txt` conversion button.
* **Filter Settings:** lists every loaded filter by source (vanilla and DLC first, then this mod, then other mods as `Mod name (packageId)`). Each filter has three options:
  * **Type** — Static filters read only fixed properties of the item kind; Dynamic filters read the state of each single item. Static filters are the options of the Worker Filter and are hidden from the Advanced Filter item list. The type of vanilla and Better Filter filters is fixed. The type of other mods' filters is detected automatically and can be changed.
  * **Move** — show the filter in the pinned filter category. On by default for vanilla and Better Filter filters, off for other mods. Filters that the game never shows are grayed out.
  * **Apply** — include the filter in the 【Apply】button. Static filters only; off by default for other mods.
* **Match Property Settings:** lists the numeric and enum targets of the Advanced Filter. Left-click a row to enable or disable it. Built-in targets cannot be deleted. Right-click a disabled custom target to delete it.

When a save is loaded and another mod has added new filters, a letter lists them. Turn the letter off on the Introduction page.

## Compatibility & Performance

* Modifies only UI and filter logic; adds no game content.
* Can be added or removed at any time without affecting saves.
* Compatible with the vast majority of mods.
* All code runs only when the relevant storage filter UI is open; zero performance impact during other gameplay.


## Feedback

Questions or suggestions are welcome — feel free to leave a comment.
