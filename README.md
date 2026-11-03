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

* New filters and selected vanilla static-property filters are grouped into a pinned filter category for convenient access. Filters and sliders added by other mods stay where those mods put them.

* The 【Apply】button in this category pushes filter-worker states down to individual items (disallowed filter → disallow matching items), then resets the filter workers to allowed. The list of participating filters can be configured in mod settings.

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

* **Worker Filter:** [+] to add matching filters (e.g. "Has Hit Points", "Adult Apparel", etc.), [-] to remove the most recently added filter. Multiple filters can be added. The available filter list is set in mod settings and is the same as the applicable filter list.

* **Mod Filter:** matches items by their source mod. [+] to add a mod name. Items from any selected mod are allowed.

* **Category Filter:** matches items by their item category (e.g. Food, Manufactured, Raw Resources, etc.). Items in any selected category are allowed.

* **Numeric Filter:** matches items by a numeric XPath target. Enter a min/max range to select items whose target value falls within that range. Loose strategy additionally allows items without the target.

* **Enum Filter:** matches items by an enumerable XPath target. Enter the target path and select values discovered from the loaded DefDatabase.

* **Manual Override:** add specific item definitions to manually determine the final match result. Override values take priority over all filter results. When an override is inactive (not changing the item's allow status), the item name appears in gray.


**Filter sets can be saved, loaded, and appended as XML files. When importing, names or targets that have disappeared due to mod changes are marked in gray and preserved for later recovery.**

Numeric and enum filters select a target from the built-in target list. Additional targets can be added from the mod settings page by specifying the type, display name, and a validated RimWorld-oriented XPath subset. The path is resolved against RimWorld's loaded DefDatabase rather than by serializing Defs to XML.

Examples of custom targets:

* `Defs/ThingDef/stackLimit` — stack limit
* `Defs/ThingDef/stats/MarketValue` — computed market value
* `Defs/ThingDef/weaponTags/li` — weapon tags
* `Defs/ThingDef/apparel/layers/li` — apparel layers
* `Defs/ThingDef/verbs/li[verbClass='Verb_Shoot']/range` — shooting range of shooting verbs

Saved sets are stored under `Config/BetterFilter/SavedFilters/*.xml`. Existing legacy `.txt` sets can be converted from the mod settings page; the original files are retained.



## Mod Settings

Includes an Introduction page and an Applicable Filter configuration page, with a reset-to-defaults option.

## Compatibility & Performance

* Modifies only UI and filter logic; adds no game content.
* Can be added or removed at any time without affecting saves.
* Compatible with the vast majority of mods.
* All code runs only when the relevant storage filter UI is open; zero performance impact during other gameplay.


## Feedback

Questions or suggestions are welcome — feel free to leave a comment.
