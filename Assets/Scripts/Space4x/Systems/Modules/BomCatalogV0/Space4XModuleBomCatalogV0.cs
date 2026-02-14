using System;
using System.IO;
using UnityEngine;

namespace Space4X.Systems.Modules.Bom
{
    [Serializable]
    public sealed class Space4XModuleBomCatalogV0
    {
        public int schemaVersion = 1;
        public string catalogId = string.Empty;
        public Space4XAffixPools affixes = new();
        public Space4XPartFamilyDefinition[] partFamilies = Array.Empty<Space4XPartFamilyDefinition>();
        public Space4XManufacturerDefinition[] manufacturers = Array.Empty<Space4XManufacturerDefinition>();
        public Space4XMarkDefinition[] marks = Array.Empty<Space4XMarkDefinition>();
        public Space4XPartDefinition[] parts = Array.Empty<Space4XPartDefinition>();
        public Space4XModuleFamilyDefinition[] moduleFamilies = Array.Empty<Space4XModuleFamilyDefinition>();
    }

    [Serializable]
    public sealed class Space4XPartFamilyDefinition
    {
        public string id = string.Empty;
        public string description = string.Empty;
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

        public static bool TryLoadDefault(out Space4XModuleBomCatalogV0 catalog, out string resolvedPath, out string error)
        {
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

        public static bool TryLoadFromPath(string path, out Space4XModuleBomCatalogV0 catalog, out string error)
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

                if (loaded.parts.Length == 0 || loaded.moduleFamilies.Length == 0)
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

        private static void NormalizeArrays(Space4XModuleBomCatalogV0 catalog)
        {
            catalog.catalogId ??= string.Empty;
            catalog.affixes ??= new Space4XAffixPools();
            catalog.affixes.lowPrefixes ??= Array.Empty<string>();
            catalog.affixes.midPrefixes ??= Array.Empty<string>();
            catalog.affixes.highPrefixes ??= Array.Empty<string>();
            catalog.affixes.suffixes ??= Array.Empty<string>();
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
