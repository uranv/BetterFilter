using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BetterFilter.Contents.Filters;
using BetterFilter.Contents.Utils;
using RimWorld;
using Verse;

namespace BetterFilter.Contents.Dialog;

/// <summary>
/// XML persistence for advanced filter sets. Legacy TXT files are supported only
/// by the explicit migration operation in the mod settings.
/// </summary>
public static class FilterSetStorage
{
    public static string SaveDir =>
        Path.Combine(GenFilePaths.ConfigFolderPath, "BetterFilter", "SavedFilters");

    public sealed class MigrationResult
    {
        public int Converted;
        public int Skipped;
        public int Failed;
    }

    public static List<string> SavedFiles
    {
        get
        {
            try
            {
                Directory.CreateDirectory(SaveDir);
                return Directory.GetFiles(SaveDir, "*.xml")
                    .Select(Path.GetFileNameWithoutExtension)
                    .OrderBy(n => n)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
    }

    public static void Save(string filename, IEnumerable<FilterComponent> components)
    {
        Directory.CreateDirectory(SaveDir);
        var root = new XElement("BetterFilterSet",
            new XAttribute("version", "1"),
            new XAttribute("name", filename));
        foreach (var component in components)
        {
            var element = Serialize(component);
            if (element != null) root.Add(element);
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        doc.Save(Path.Combine(SaveDir, filename + ".xml"));
    }

    public static bool Load(string filename, List<FilterComponent> target, bool append,
        out string error)
    {
        error = null;
        try
        {
            var path = Path.Combine(SaveDir, filename + ".xml");
            if (!File.Exists(path)) return false;
            var doc = XDocument.Load(path, LoadOptions.SetLineInfo);
            if (doc.Root == null || doc.Root.Name != "BetterFilterSet")
            {
                error = "Missing BetterFilterSet root element.";
                return false;
            }

            var loaded = new List<FilterComponent>();
            foreach (var element in doc.Root.Elements("Filter"))
            {
                var component = Deserialize(element);
                if (component != null) loaded.Add(component);
            }

            if (!append) target.Clear();
            target.AddRange(loaded);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static MigrationResult ConvertLegacyTxtFiles()
    {
        var result = new MigrationResult();
        try { Directory.CreateDirectory(SaveDir); }
        catch
        {
            result.Failed++;
            return result;
        }

        foreach (var txtPath in Directory.GetFiles(SaveDir, "*.txt"))
        {
            var xmlPath = Path.Combine(SaveDir,
                Path.GetFileNameWithoutExtension(txtPath) + ".xml");
            if (File.Exists(xmlPath))
            {
                result.Skipped++;
                continue;
            }

            try
            {
                var content = File.ReadAllText(txtPath).Trim();
                var components = new List<FilterComponent>();
                if (!string.IsNullOrEmpty(content))
                {
                    var commands = content.Split('\n')
                        .Select(line => line.Trim())
                        .Where(line => line.Length > 0);
                    var combined = string.Join(",", commands);
                    if (!Dialog_CommandInput.TryParseMulti(combined, components))
                    {
                        result.Failed++;
                        continue;
                    }
                }

                var filename = Path.GetFileNameWithoutExtension(txtPath);
                Save(filename, components);
                result.Converted++;
            }
            catch (Exception ex)
            {
                Log.Error("[BetterFilter] Failed to convert legacy filter set "
                          + txtPath + ": " + ex.Message);
                result.Failed++;
            }
        }

        return result;
    }

    private static XElement Serialize(FilterComponent component)
    {
        var element = new XElement("Filter");
        switch (component)
        {
            case FilterComponent_Worker worker:
                SetCommon(element, "Worker", worker.IsWhitelist);
                AddNames(element, worker.SelectedDefs.Select(d => d.defName),
                    worker.ErrorDefNames);
                return element;
            case FilterComponent_Mod mod:
                SetCommon(element, "Mod", mod.IsWhitelist);
                AddNames(element, mod.PackageIds, mod.ErrorPackageIds);
                return element;
            case FilterComponent_Category category:
                SetCommon(element, "Category", category.IsWhitelist);
                AddNames(element, category.CategoryDefNames,
                    category.ErrorCategoryNames);
                return element;
            case FilterComponent_Numeric numeric:
                SetCommon(element, "Numeric", numeric.IsWhitelist);
                element.SetAttributeValue("missing", numeric.IsLoose ? "Allow" : "Strict");
                var numericTarget = new XElement("Target",
                    new XAttribute("xpath", numeric.ErrorTargetPath ?? numeric.TargetPath ?? ""));
                if (!string.IsNullOrEmpty(numeric.TargetName))
                    numericTarget.SetAttributeValue("name", numeric.TargetName);
                element.Add(numericTarget);
                element.Add(RangeElement("Min", numeric.MinBuffer));
                element.Add(RangeElement("Max", numeric.MaxBuffer));
                return element;
            case FilterComponent_Enum enumeration:
                SetCommon(element, "Enum", enumeration.IsWhitelist);
                element.SetAttributeValue("missing", enumeration.IsLoose ? "Allow" : "Strict");
                var enumTarget = new XElement("Target",
                    new XAttribute("xpath", enumeration.ErrorTargetPath
                        ?? enumeration.TargetPath ?? ""));
                if (!string.IsNullOrEmpty(enumeration.TargetName))
                    enumTarget.SetAttributeValue("name", enumeration.TargetName);
                element.Add(enumTarget);
                AddValues(element, enumeration.SelectedValues, enumeration.ErrorValues);
                return element;
            case FilterComponent_Item item:
                SetCommon(element, "Item", item.IsWhitelist);
                AddNames(element,
                    item.SelectedDef == null ? Enumerable.Empty<string>()
                        : new[] { item.SelectedDef.defName },
                    item.ErrorDefName == null ? Enumerable.Empty<string>()
                        : new[] { item.ErrorDefName });
                return element;
            default:
                return null;
        }
    }

    private static void SetCommon(XElement element, string type, bool whitelist)
    {
        element.SetAttributeValue("type", type);
        element.SetAttributeValue("mode", whitelist ? "Whitelist" : "Blacklist");
    }

    private static XElement RangeElement(string name, string buffer) =>
        new XElement(name, string.IsNullOrWhiteSpace(buffer) ? null : buffer);

    private static void AddNames(XElement parent, IEnumerable<string> valid,
        IEnumerable<string> errors)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in valid.Concat(errors))
        {
            if (string.IsNullOrEmpty(value) || !values.Add(value)) continue;
            parent.Add(new XElement("Value", value));
        }
    }

    private static void AddValues(XElement parent, IEnumerable<string> valid,
        IEnumerable<string> errors)
    {
        foreach (var value in valid)
            parent.Add(new XElement("Value", value));
        foreach (var value in errors)
            parent.Add(new XElement("Value", new XAttribute("invalid", "true"), value));
    }

    private static FilterComponent Deserialize(XElement element)
    {
        var type = (string)element.Attribute("type");
        var whitelist = !string.Equals((string)element.Attribute("mode"),
            "Blacklist", StringComparison.OrdinalIgnoreCase);

        switch (type)
        {
            case "Worker":
            {
                var component = new FilterComponent_Worker { IsWhitelist = whitelist };
                foreach (var value in element.Elements("Value"))
                {
                    var name = value.Value.Trim();
                    var def = DefDatabase<SpecialThingFilterDef>.GetNamed(name,
                        errorOnFail: false);
                    if (def != null) component.SelectedDefs.Add(def);
                    else component.ErrorDefNames.Add(name);
                }
                return component;
            }
            case "Mod":
            {
                var component = new FilterComponent_Mod { IsWhitelist = whitelist };
                var ids = new HashSet<string>(
                    LoadedModManager.RunningMods.Select(m => m.PackageId),
                    StringComparer.OrdinalIgnoreCase);
                foreach (var value in element.Elements("Value"))
                {
                    var packageId = value.Value.Trim();
                    var actual = LoadedModManager.RunningMods
                        .FirstOrDefault(m => string.Equals(m.PackageId, packageId,
                            StringComparison.OrdinalIgnoreCase))?.PackageId;
                    if (actual != null && ids.Contains(actual)) component.PackageIds.Add(actual);
                    else component.ErrorPackageIds.Add(packageId);
                }
                return component;
            }
            case "Category":
            {
                var component = new FilterComponent_Category { IsWhitelist = whitelist };
                foreach (var value in element.Elements("Value"))
                {
                    var name = value.Value.Trim();
                    if (DefDatabase<ThingCategoryDef>.GetNamed(name, errorOnFail: false) != null)
                        component.CategoryDefNames.Add(name);
                    else component.ErrorCategoryNames.Add(name);
                }
                return component;
            }
            case "Numeric":
            {
                var targetElement = element.Element("Target");
                var path = (string)targetElement?.Attribute("xpath") ?? "";
                var component = new FilterComponent_Numeric
                {
                    TargetPath = path,
                    TargetName = (string)targetElement?.Attribute("name"),
                    ErrorTargetPath = RuntimeFilterPath.IsValidSyntax(path) ? null : path,
                    IsWhitelist = whitelist,
                    IsLoose = !string.Equals((string)element.Attribute("missing"),
                        "Strict", StringComparison.OrdinalIgnoreCase)
                };
                ReadRange(element.Element("Min"), ref component.Min, ref component.MinBuffer);
                ReadRange(element.Element("Max"), ref component.Max, ref component.MaxBuffer);
                return component;
            }
            case "Enum":
            {
                var targetElement = element.Element("Target");
                var path = (string)targetElement?.Attribute("xpath") ?? "";
                var component = new FilterComponent_Enum
                {
                    TargetPath = path,
                    TargetName = (string)targetElement?.Attribute("name"),
                    ErrorTargetPath = RuntimeFilterPath.IsValidSyntax(path) ? null : path,
                    IsWhitelist = whitelist,
                    IsLoose = !string.Equals((string)element.Attribute("missing"),
                        "Strict", StringComparison.OrdinalIgnoreCase)
                };
                var known = new HashSet<string>(
                    RuntimeFilterPath.GetDistinctTextValues(path), StringComparer.Ordinal);
                foreach (var value in element.Elements("Value"))
                {
                    var text = value.Value;
                    if (value.Attribute("invalid") != null || !known.Contains(text))
                        component.ErrorValues.Add(text);
                    else component.SelectedValues.Add(text);
                }
                return component;
            }
            case "Item":
            {
                var first = element.Elements("Value").FirstOrDefault();
                if (first == null) return null;
                var name = first.Value.Trim();
                var def = DefDatabase<ThingDef>.GetNamed(name, errorOnFail: false);
                return def != null
                    ? new FilterComponent_Item { SelectedDef = def, IsWhitelist = whitelist }
                    : new FilterComponent_Item { ErrorDefName = name, IsWhitelist = whitelist };
            }
            default:
                Log.Warning("[BetterFilter] Unknown XML filter type: " + type);
                return null;
        }
    }

    private static void ReadRange(XElement element, ref float value, ref string buffer)
    {
        buffer = element?.Value?.Trim() ?? "";
        if (string.IsNullOrEmpty(buffer))
        {
            value = 0f;
            return;
        }
        if (!float.TryParse(buffer, NumberStyles.Float, CultureInfo.InvariantCulture,
                out value))
            value = 0f;
    }
}
