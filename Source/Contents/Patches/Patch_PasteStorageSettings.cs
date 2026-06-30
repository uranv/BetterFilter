using System.Collections.Generic;
using System.Linq;
using BetterFilter.Contents.Gizmos;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterFilter.Contents.Patches;

/// <summary>
/// Replaces the vanilla paste gizmo (second in CopyPasteGizmosFor result)
/// with Command_StoragePaste which adds right-click AND/OR sub-menu.
/// </summary>
[HarmonyPatch(typeof(StorageSettingsClipboard), nameof(StorageSettingsClipboard.CopyPasteGizmosFor))]
public static class Patch_PasteStorageSettings
{
    static void Postfix(ref IEnumerable<Gizmo> __result, StorageSettings s)
    {
        var list = __result.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is Command_Action cmd
                && cmd.defaultLabel == "CommandPasteZoneSettingsLabel".Translate())
            {
                list[i] = new Command_StoragePaste(s);
                break;
            }
        }
        __result = list;
    }
}
