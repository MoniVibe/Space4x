using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PureDOTS.Runtime.WorldGen;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace PureDOTS.Authoring.WorldGen
{
    [CreateAssetMenu(fileName = "WorldGenDefinitions", menuName = "PureDOTS/WorldGen/WorldGen Definitions", order = 1)]
    public sealed class WorldGenDefinitionsCatalogAsset : ScriptableObject
    {
        [Serializable]
        public struct BiomeDefinition
        {
            public string id;
            public float weight;
            public float temperatureMin;
            public float temperatureMax;
            public float moistureMin;
            public float moistureMax;
        }

        [Serializable]
        public struct ResourceDefinition
        {
            public string id;
            public float scarcity;
            public string biomeHint;
        }

        [Serializable]
        public struct RuinSetDefinition
        {
            public string id;
            public float weight;
            public float techLevelMin;
            public float techLevelMax;
        }

        [Header("Biomes")]
        public BiomeDefinition[] biomes;

        [Header("Resources")]
        public ResourceDefinition[] resources;

        [Header("Ruins")]
        public RuinSetDefinition[] ruinSets;

        public static bool TryComputeMergedHash(
            WorldGenDefinitionsCatalogAsset baseCatalog,
            WorldGenDefinitionsCatalogAsset[] additionalCatalogs,
            out Hash128 definitionsHash,
            out string definitionsHashText,
            out string error)
        {
            definitionsHash = default;
            definitionsHashText = string.Empty;
            error = string.Empty;

            var catalogs = CollectCatalogs(baseCatalog, additionalCatalogs);
            BuildMergedData(catalogs, out var mergedBiomes, out var mergedResources, out var mergedRuins);

            definitionsHashText = ComputeStableHashText(mergedBiomes, mergedResources, mergedRuins);
            definitionsHash = new Hash128(definitionsHashText);
            if (!definitionsHash.IsValid)
            {
                error = "Computed definitionsHash is invalid.";
                return false;
            }

            return true;
        }

        public static bool TryBuildMergedBlobAsset(
            WorldGenDefinitionsCatalogAsset baseCatalog,
            WorldGenDefinitionsCatalogAsset[] additionalCatalogs,
            out BlobAssetReference<WorldGenDefinitionsBlob> blob,
            out Hash128 definitionsHash,
            out string definitionsHashText,
            out string error)
        {
            blob = default;
            definitionsHash = default;
            definitionsHashText = string.Empty;
            error = string.Empty;

            var catalogs = CollectCatalogs(baseCatalog, additionalCatalogs);
            BuildMergedData(catalogs, out var mergedBiomes, out var mergedResources, out var mergedRuins);

            definitionsHashText = ComputeStableHashText(mergedBiomes, mergedResources, mergedRuins);
            definitionsHash = new Hash128(definitionsHashText);
            if (!definitionsHash.IsValid)
            {
                error = "Computed definitionsHash is invalid.";
                return false;
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<WorldGenDefinitionsBlob>();
            root.SchemaVersion = WorldGenSchema.WorldGenDefinitionsSchemaVersion;

            var biomeArray = builder.Allocate(ref root.Biomes, mergedBiomes.Length);
            for (int i = 0; i < mergedBiomes.Length; i++)
            {
                biomeArray[i] = new WorldGenBiomeDefinitionBlob
                {
                    Id = new FixedString64Bytes(mergedBiomes[i].id),
                    Weight = mergedBiomes[i].weight,
                    TemperatureMin = mergedBiomes[i].temperatureMin,
                    TemperatureMax = mergedBiomes[i].temperatureMax,
                    MoistureMin = mergedBiomes[i].moistureMin,
                    MoistureMax = mergedBiomes[i].moistureMax
                };
            }

            var resourceArray = builder.Allocate(ref root.Resources, mergedResources.Length);
            for (int i = 0; i < mergedResources.Length; i++)
            {
                resourceArray[i] = new WorldGenResourceDefinitionBlob
                {
                    Id = new FixedString64Bytes(mergedResources[i].id),
                    Scarcity = mergedResources[i].scarcity,
                    BiomeHint = new FixedString64Bytes(mergedResources[i].biomeHint)
                };
            }

            var ruinArray = builder.Allocate(ref root.RuinSets, mergedRuins.Length);
            for (int i = 0; i < mergedRuins.Length; i++)
            {
                ruinArray[i] = new WorldGenRuinSetDefinitionBlob
                {
                    Id = new FixedString64Bytes(mergedRuins[i].id),
                    Weight = mergedRuins[i].weight,
                    TechLevelMin = mergedRuins[i].techLevelMin,
                    TechLevelMax = mergedRuins[i].techLevelMax
                };
            }

            blob = builder.CreateBlobAssetReference<WorldGenDefinitionsBlob>(Allocator.Persistent);
            builder.Dispose();
            return true;
        }

        private static List<WorldGenDefinitionsCatalogAsset> CollectCatalogs(
            WorldGenDefinitionsCatalogAsset baseCatalog,
            WorldGenDefinitionsCatalogAsset[] additionalCatalogs)
        {
            var catalogs = new List<WorldGenDefinitionsCatalogAsset>(4);
            if (baseCatalog != null)
            {
                catalogs.Add(baseCatalog);
            }

            if (additionalCatalogs != null)
            {
                for (int i = 0; i < additionalCatalogs.Length; i++)
                {
                    if (additionalCatalogs[i] != null)
                    {
                        catalogs.Add(additionalCatalogs[i]);
                    }
                }
            }

            return catalogs;
        }

        private static void BuildMergedData(
            List<WorldGenDefinitionsCatalogAsset> catalogs,
            out BiomeDefinition[] mergedBiomes,
            out ResourceDefinition[] mergedResources,
            out RuinSetDefinition[] mergedRuins)
        {
            mergedBiomes = MergeBiomes(catalogs);
            mergedResources = MergeResources(catalogs);
            mergedRuins = MergeRuinSets(catalogs);
        }

        private static BiomeDefinition[] MergeBiomes(List<WorldGenDefinitionsCatalogAsset> catalogs)
        {
            var merged = new Dictionary<string, BiomeDefinition>(StringComparer.Ordinal);
            for (int c = 0; c < catalogs.Count; c++)
            {
                var catalog = catalogs[c];
                var entries = catalog?.biomes;
                if (entries == null) continue;

                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if (string.IsNullOrWhiteSpace(entry.id)) continue;

                    entry.id = NormalizeId(entry.id);
                    merged[entry.id] = entry;
                }
            }

            var list = new List<BiomeDefinition>(merged.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return list.ToArray();
        }

        private static ResourceDefinition[] MergeResources(List<WorldGenDefinitionsCatalogAsset> catalogs)
        {
            var merged = new Dictionary<string, ResourceDefinition>(StringComparer.Ordinal);
            for (int c = 0; c < catalogs.Count; c++)
            {
                var catalog = catalogs[c];
                var entries = catalog?.resources;
                if (entries == null) continue;

                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if (string.IsNullOrWhiteSpace(entry.id)) continue;

                    entry.id = NormalizeId(entry.id);
                    entry.biomeHint = NormalizeId(entry.biomeHint);
                    merged[entry.id] = entry;
                }
            }

            var list = new List<ResourceDefinition>(merged.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return list.ToArray();
        }

        private static RuinSetDefinition[] MergeRuinSets(List<WorldGenDefinitionsCatalogAsset> catalogs)
        {
            var merged = new Dictionary<string, RuinSetDefinition>(StringComparer.Ordinal);
            for (int c = 0; c < catalogs.Count; c++)
            {
                var catalog = catalogs[c];
                var entries = catalog?.ruinSets;
                if (entries == null) continue;

                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if (string.IsNullOrWhiteSpace(entry.id)) continue;

                    entry.id = NormalizeId(entry.id);
                    merged[entry.id] = entry;
                }
            }

            var list = new List<RuinSetDefinition>(merged.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return list.ToArray();
        }

        private static string NormalizeId(string raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().ToLowerInvariant();
        }

        private static string ComputeStableHashText(
            BiomeDefinition[] mergedBiomes,
            ResourceDefinition[] mergedResources,
            RuinSetDefinition[] mergedRuins)
        {
            using var md5 = MD5.Create();
            var builder = new StringBuilder(256);
            builder.Append(WorldGenSchema.WorldGenDefinitionsSchemaVersion);

            AppendBiomes(builder, mergedBiomes);
            AppendResources(builder, mergedResources);
            AppendRuinSets(builder, mergedRuins);

            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            var hashBytes = md5.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        private static void AppendBiomes(StringBuilder builder, BiomeDefinition[] biomesSrc)
        {
            builder.Append("|biomes:");
            if (biomesSrc == null) return;
            for (int i = 0; i < biomesSrc.Length; i++)
            {
                var b = biomesSrc[i];
                builder.Append(b.id);
                builder.Append(',');
                builder.Append(b.weight.ToString("R", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(b.temperatureMin.ToString("R", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(b.temperatureMax.ToString("R", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(b.moistureMin.ToString("R", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(b.moistureMax.ToString("R", CultureInfo.InvariantCulture));
                builder.Append(';');
            }
        }

        private static void AppendResources(StringBuilder builder, ResourceDefinition[] resourcesSrc)
        {
            builder.Append("|resources:");
            if (resourcesSrc == null) return;
            for (int i = 0; i < resourcesSrc.Length; i++)
            {
                var r = resourcesSrc[i];
                builder.Append(r.id);
                builder.Append(',');
                builder.Append(r.scarcity.ToString("R", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(r.biomeHint);
                builder.Append(';');
            }
        }

        private static void AppendRuinSets(StringBuilder builder, RuinSetDefinition[] ruinsSrc)
        {
            builder.Append("|ruins:");
            if (ruinsSrc == null) return;
            for (int i = 0; i < ruinsSrc.Length; i++)
            {
                var r = ruinsSrc[i];
                builder.Append(r.id);
                builder.Append(',');
                builder.Append(r.weight.ToString("R", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(r.techLevelMin.ToString("R", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(r.techLevelMax.ToString("R", CultureInfo.InvariantCulture));
                builder.Append(';');
            }
        }
    }
}
