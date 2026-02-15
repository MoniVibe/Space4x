using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Space4X.Systems.Modules.Bom
{
    [Serializable]
    public sealed class Space4XModuleBomCatalogV0
    {
        public int schemaVersion = 1;
        public string catalogId = string.Empty;
        public Space4XAffixPools affixPools = new();
        public Space4XPartFamilyDefinition[] partFamilies = Array.Empty<Space4XPartFamilyDefinition>();
        public Space4XManufacturerDefinition[] manufacturers = Array.Empty<Space4XManufacturerDefinition>();
        public Space4XMarkDefinition[] marks = Array.Empty<Space4XMarkDefinition>();
        public Space4XPartDefinition[] parts = Array.Empty<Space4XPartDefinition>();
        public Space4XModuleFamilyDefinition[] moduleFamilies = Array.Empty<Space4XModuleFamilyDefinition>();
    }

    [Serializable]
    public sealed class Space4XAffixPools
    {
        public string[] lowPrefixes = Array.Empty<string>();
        public string[] midPrefixes = Array.Empty<string>();
        public string[] highPrefixes = Array.Empty<string>();
        public string[] suffixes = Array.Empty<string>();
    }

    [Serializable]
    public sealed class Space4XPartFamilyDefinition
    {
        public string id = string.Empty;
        public string description = string.Empty;
    }

    [Serializable]
    public sealed class Space4XManufacturerDefinition
    {
        public string id = string.Empty;
        public string flavor = string.Empty;
        public string[] allowedFamilies = Array.Empty<string>();
        public Space4XStatValue[] statMultipliers = Array.Empty<Space4XStatValue>();
    }

    [Serializable]
    public sealed class Space4XMarkDefinition
    {
        public int mark = 1;
        public string label = string.Empty;
        public float qualityBandMin = 0f;
        public float qualityBandMax = 1f;
    }

    [Serializable]
    public sealed class Space4XPartDefinition
    {
        public string id = string.Empty;
        public string family = string.Empty;
        public string manufacturer = string.Empty;
        public int markMin = 1;
        public int markMax = 1;
        public string[] tags = Array.Empty<string>();
        public Space4XStatValue[] baseStats = Array.Empty<Space4XStatValue>();
        public Space4XQualityTierRule[] qualityTierRules = Array.Empty<Space4XQualityTierRule>();
    }

    [Serializable]
    public sealed class Space4XQualityTierRule
    {
        public string tier = string.Empty;
        public float min = 0f;
        public float max = 1f;
        public float scalar = 1f;
    }

    [Serializable]
    public sealed class Space4XStatValue
    {
        public string key = string.Empty;
        public float value = 0f;
    }

    [Serializable]
    public sealed class Space4XModuleFamilyDefinition
    {
        public string id = string.Empty;
        public string model = string.Empty;
        public string[] requiredSlots = Array.Empty<string>();
        public string[] allowedManufacturers = Array.Empty<string>();
        public Space4XMarkStatBand[] baseStatsByMark = Array.Empty<Space4XMarkStatBand>();
        public Space4XModuleBomLine[] bomTemplate = Array.Empty<Space4XModuleBomLine>();
    }

    [Serializable]
    public sealed class Space4XMarkStatBand
    {
        public int mark = 1;
        public Space4XStatValue[] stats = Array.Empty<Space4XStatValue>();
    }

    [Serializable]
    public sealed class Space4XModuleBomLine
    {
        public string slotId = string.Empty;
        public string requiredFamily = string.Empty;
        public int quantity = 1;
    }

    public static class Space4XModuleBomCatalogV0Loader
    {
        public const string DefaultCatalogRelativePath = "Assets/Data/Catalogs/space4x_module_bom_catalog_v0.json";
        public const string CatalogPackRelativeDirectory = "Assets/Modules/Catalogs";

        public static bool TryLoadDefault(out Space4XModuleBomCatalogV0 catalog, out string resolvedPath, out string error)
        {
            var directoryPath = ResolveCatalogPackDirectory();
            if (Directory.Exists(directoryPath))
            {
                var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                if (files.Length > 0)
                {
                    resolvedPath = directoryPath;
                    return TryLoadFromDirectory(directoryPath, out catalog, out error);
                }
            }

            resolvedPath = ResolveDefaultCatalogPath();
            return TryLoadFromPath(resolvedPath, out catalog, out error);
        }

        public static string ResolveDefaultCatalogPath()
        {
            var inProject = Path.Combine(Application.dataPath, "Data", "Catalogs", "space4x_module_bom_catalog_v0.json");
            if (File.Exists(inProject))
            {
                return inProject;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Assets", "Data", "Catalogs", "space4x_module_bom_catalog_v0.json");
        }

        public static string ResolveCatalogPackDirectory()
        {
            var inProject = Path.Combine(Application.dataPath, "Modules", "Catalogs");
            if (Directory.Exists(inProject))
            {
                return inProject;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Assets", "Modules", "Catalogs");
        }

        public static bool TryLoadFromDirectory(string directoryPath, out Space4XModuleBomCatalogV0 catalog, out string error)
        {
            catalog = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                error = "catalog directory missing";
                return false;
            }

            if (!Directory.Exists(directoryPath))
            {
                error = $"catalog directory missing: {directoryPath}";
                return false;
            }

            var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            if (files.Length == 0)
            {
                error = $"catalog directory has no json files: {directoryPath}";
                return false;
            }

            var merged = new Space4XModuleBomCatalogV0();
            var sawAny = false;
            for (var i = 0; i < files.Length; i++)
            {
                if (!TryLoadFromPathInternal(files[i], requireContent: false, out var partial, out var loadError))
                {
                    error = $"catalog load failed for {files[i]}: {loadError}";
                    return false;
                }

                if (partial == null)
                {
                    continue;
                }

                MergeCatalog(merged, partial);
                sawAny = true;
            }

            NormalizeArrays(merged);
            if (!sawAny || merged.parts.Length == 0 || merged.moduleFamilies.Length == 0)
            {
                error = $"merged catalog from {directoryPath} has no parts or moduleFamilies";
                return false;
            }

            catalog = merged;
            return true;
        }

        public static bool TryLoadFromPath(string path, out Space4XModuleBomCatalogV0 catalog, out string error)
        {
            return TryLoadFromPathInternal(path, requireContent: true, out catalog, out error);
        }

        private static bool TryLoadFromPathInternal(string path, bool requireContent, out Space4XModuleBomCatalogV0 catalog, out string error)
        {
            catalog = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "catalog path missing";
                return false;
            }

            if (!File.Exists(path))
            {
                error = $"catalog missing: {path}";
                return false;
            }

            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonUtility.FromJson<Space4XModuleBomCatalogV0>(json);
                if (loaded == null)
                {
                    error = "catalog parse returned null";
                    return false;
                }

                NormalizeArrays(loaded);
                if (loaded.schemaVersion != 1)
                {
                    error = $"unsupported schema version: {loaded.schemaVersion}";
                    return false;
                }

                if (requireContent && (loaded.parts.Length == 0 || loaded.moduleFamilies.Length == 0))
                {
                    error = "catalog must define parts and moduleFamilies";
                    return false;
                }

                catalog = loaded;
                return true;
            }
            catch (Exception ex)
            {
                error = $"catalog load failed: {ex.Message}";
                return false;
            }
        }

        private static void MergeCatalog(Space4XModuleBomCatalogV0 target, Space4XModuleBomCatalogV0 source)
        {
            if (target == null || source == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(target.catalogId) && !string.IsNullOrWhiteSpace(source.catalogId))
            {
                target.catalogId = source.catalogId;
            }

            target.schemaVersion = source.schemaVersion;
            target.partFamilies = MergeById(target.partFamilies, source.partFamilies, x => x?.id);
            target.manufacturers = MergeById(target.manufacturers, source.manufacturers, x => x?.id);
            target.marks = MergeById(target.marks, source.marks, x => x?.mark.ToString());
            target.parts = MergeById(target.parts, source.parts, x => x?.id);
            target.moduleFamilies = MergeById(target.moduleFamilies, source.moduleFamilies, x => x?.id);

            target.affixPools.lowPrefixes = MergeStrings(target.affixPools.lowPrefixes, source.affixPools?.lowPrefixes);
            target.affixPools.midPrefixes = MergeStrings(target.affixPools.midPrefixes, source.affixPools?.midPrefixes);
            target.affixPools.highPrefixes = MergeStrings(target.affixPools.highPrefixes, source.affixPools?.highPrefixes);
            target.affixPools.suffixes = MergeStrings(target.affixPools.suffixes, source.affixPools?.suffixes);
        }

        private static T[] MergeById<T>(T[] a, T[] b, Func<T, string> idSelector)
        {
            var merged = new List<T>((a?.Length ?? 0) + (b?.Length ?? 0));
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (a != null)
            {
                for (var i = 0; i < a.Length; i++)
                {
                    var item = a[i];
                    if (item == null)
                    {
                        continue;
                    }

                    var id = idSelector(item);
                    if (string.IsNullOrWhiteSpace(id) || ids.Add(id))
                    {
                        merged.Add(item);
                    }
                }
            }

            if (b != null)
            {
                for (var i = 0; i < b.Length; i++)
                {
                    var item = b[i];
                    if (item == null)
                    {
                        continue;
                    }

                    var id = idSelector(item);
                    if (string.IsNullOrWhiteSpace(id) || ids.Add(id))
                    {
                        merged.Add(item);
                    }
                }
            }

            return merged.ToArray();
        }

        private static string[] MergeStrings(string[] a, string[] b)
        {
            var merged = new List<string>((a?.Length ?? 0) + (b?.Length ?? 0));
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddStrings(a, merged, values);
            AddStrings(b, merged, values);

            return merged.ToArray();
        }

        private static void AddStrings(string[] source, List<string> target, HashSet<string> seen)
        {
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var value = source[i];
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                {
                    continue;
                }

                target.Add(value);
            }
        }

        private static void NormalizeArrays(Space4XModuleBomCatalogV0 catalog)
        {
            catalog.catalogId ??= string.Empty;
            catalog.affixPools ??= new Space4XAffixPools();
            catalog.affixPools.lowPrefixes ??= Array.Empty<string>();
            catalog.affixPools.midPrefixes ??= Array.Empty<string>();
            catalog.affixPools.highPrefixes ??= Array.Empty<string>();
            catalog.affixPools.suffixes ??= Array.Empty<string>();
            catalog.partFamilies ??= Array.Empty<Space4XPartFamilyDefinition>();
            catalog.manufacturers ??= Array.Empty<Space4XManufacturerDefinition>();
            catalog.marks ??= Array.Empty<Space4XMarkDefinition>();
            catalog.parts ??= Array.Empty<Space4XPartDefinition>();
            catalog.moduleFamilies ??= Array.Empty<Space4XModuleFamilyDefinition>();

            for (var i = 0; i < catalog.manufacturers.Length; i++)
            {
                var item = catalog.manufacturers[i];
                if (item == null)
                {
                    continue;
                }

                item.id ??= string.Empty;
                item.flavor ??= string.Empty;
                item.allowedFamilies ??= Array.Empty<string>();
                item.statMultipliers ??= Array.Empty<Space4XStatValue>();
            }

            for (var i = 0; i < catalog.parts.Length; i++)
            {
                var item = catalog.parts[i];
                if (item == null)
                {
                    continue;
                }

                item.id ??= string.Empty;
                item.family ??= string.Empty;
                item.manufacturer ??= string.Empty;
                item.tags ??= Array.Empty<string>();
                item.baseStats ??= Array.Empty<Space4XStatValue>();
                item.qualityTierRules ??= Array.Empty<Space4XQualityTierRule>();
            }

            for (var i = 0; i < catalog.moduleFamilies.Length; i++)
            {
                var item = catalog.moduleFamilies[i];
                if (item == null)
                {
                    continue;
                }

                item.id ??= string.Empty;
                item.model ??= string.Empty;
                item.requiredSlots ??= Array.Empty<string>();
                item.allowedManufacturers ??= Array.Empty<string>();
                item.baseStatsByMark ??= Array.Empty<Space4XMarkStatBand>();
                item.bomTemplate ??= Array.Empty<Space4XModuleBomLine>();
            }
        }
    }
}
