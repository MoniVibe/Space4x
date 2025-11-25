using System;
using System.Collections.Generic;
using System.Linq;
using Space4X.Authoring;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor
{
    /// <summary>
    /// Parametric variant generator - creates N variants from one spec deterministically.
    /// Supports palette, size, biome, tech tier variations.
    /// </summary>
    public static class ParametricVariantGenerator
    {
        [Serializable]
        public class VariantRule
        {
            public VariantDimension Dimension;
            public List<string> Values = new List<string>();
            public bool Required = true;
        }

        public enum VariantDimension
        {
            Palette,
            Size,
            Biome,
            TechTier
        }

        [Serializable]
        public class VariantSpec
        {
            public string BaseId;
            public Dictionary<VariantDimension, string> Dimensions = new Dictionary<VariantDimension, string>();
            public string VariantId; // Generated: BaseId_Palette_Size_Biome_Tier
        }

        public static List<VariantSpec> GenerateVariants(string baseId, List<VariantRule> rules)
        {
            var variants = new List<VariantSpec>();

            if (rules == null || rules.Count == 0)
            {
                // No variants, return base
                variants.Add(new VariantSpec { BaseId = baseId, VariantId = baseId });
                return variants;
            }

            // Generate all combinations
            var dimensions = rules.Select(r => r.Dimension).ToList();
            var valueLists = rules.Select(r => r.Values).ToList();

            var combinations = GenerateCombinations(valueLists);
            foreach (var combination in combinations)
            {
                var variant = new VariantSpec { BaseId = baseId };
                var variantParts = new List<string> { baseId };

                for (int i = 0; i < dimensions.Count; i++)
                {
                    var dimension = dimensions[i];
                    var value = combination[i];
                    variant.Dimensions[dimension] = value;
                    variantParts.Add(value);
                }

                variant.VariantId = string.Join("_", variantParts);
                variants.Add(variant);
            }

            return variants;
        }

        private static List<List<string>> GenerateCombinations(List<List<string>> valueLists)
        {
            if (valueLists.Count == 0)
            {
                return new List<List<string>> { new List<string>() };
            }

            var result = new List<List<string>>();
            var firstList = valueLists[0];
            var restCombinations = GenerateCombinations(valueLists.Skip(1).ToList());

            foreach (var value in firstList)
            {
                foreach (var rest in restCombinations)
                {
                    var combination = new List<string> { value };
                    combination.AddRange(rest);
                    result.Add(combination);
                }
            }

            return result;
        }

        public static void ApplyVariantToPrefab(GameObject prefab, VariantSpec variant)
        {
            // Apply palette variant
            if (variant.Dimensions.TryGetValue(VariantDimension.Palette, out var palette))
            {
                var styleTokens = prefab.GetComponent<StyleTokensAuthoring>();
                if (styleTokens == null)
                {
                    styleTokens = prefab.AddComponent<StyleTokensAuthoring>();
                }
                // Map palette string to byte value (simplified - could use lookup table)
                styleTokens.palette = (byte)Mathf.Abs(palette.GetHashCode() % 256);
            }

            // Apply size variant (affects scale)
            if (variant.Dimensions.TryGetValue(VariantDimension.Size, out var size))
            {
                var scale = GetSizeScale(size);
                prefab.transform.localScale = Vector3.one * scale;
            }

            // Apply biome variant (could affect materials/colors)
            if (variant.Dimensions.TryGetValue(VariantDimension.Biome, out var biome))
            {
                // Biome-specific styling could be applied here
                // For now, we'll store it in a custom component or style tokens
            }

            // Apply tech tier variant
            if (variant.Dimensions.TryGetValue(VariantDimension.TechTier, out var techTier))
            {
                // Tech tier could affect module quality, hull variant, etc.
                var tier = ParseTechTier(techTier);
                // Could add TechTierAuthoring component here
            }
        }

        private static float GetSizeScale(string size)
        {
            switch (size.ToLower())
            {
                case "small": return 0.75f;
                case "medium": return 1.0f;
                case "large": return 1.5f;
                case "massive": return 2.0f;
                default: return 1.0f;
            }
        }

        private static byte ParseTechTier(string techTier)
        {
            if (byte.TryParse(techTier, out var tier))
            {
                return tier;
            }
            // Try to parse from string like "Tier1", "Tier2", etc.
            if (techTier.StartsWith("Tier", StringComparison.OrdinalIgnoreCase))
            {
                var tierStr = techTier.Substring(4);
                if (byte.TryParse(tierStr, out var parsedTier))
                {
                    return parsedTier;
                }
            }
            return 1;
        }

        public static void GenerateVariantPrefabs(
            string basePrefabPath,
            List<VariantRule> rules,
            string outputDir,
            PrefabMaker.GenerationResult result)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);
            if (basePrefab == null)
            {
                result.Errors.Add($"Base prefab not found: {basePrefabPath}");
                return;
            }

            // Extract base ID from prefab name or component
            var baseId = basePrefab.name;
            var hullId = basePrefab.GetComponent<HullIdAuthoring>();
            if (hullId != null) baseId = hullId.hullId;

            var moduleId = basePrefab.GetComponent<ModuleIdAuthoring>();
            if (moduleId != null) baseId = moduleId.moduleId;

            // Generate variant specs
            var variants = GenerateVariants(baseId, rules);

            // Create variant prefabs
            foreach (var variant in variants)
            {
                var variantPrefabPath = $"{outputDir}/{variant.VariantId}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(variantPrefabPath) != null)
                {
                    result.SkippedCount++;
                    continue;
                }

                // Instantiate base prefab
                var variantObj = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
                if (variantObj == null)
                {
                    variantObj = UnityEngine.Object.Instantiate(basePrefab);
                }

                // Apply variant modifications
                ApplyVariantToPrefab(variantObj, variant);

                // Update ID components
                if (hullId != null)
                {
                    var variantHullId = variantObj.GetComponent<HullIdAuthoring>();
                    if (variantHullId != null)
                    {
                        variantHullId.hullId = variant.VariantId;
                    }
                }

                if (moduleId != null)
                {
                    var variantModuleId = variantObj.GetComponent<ModuleIdAuthoring>();
                    if (variantModuleId != null)
                    {
                        variantModuleId.moduleId = variant.VariantId;
                    }
                }

                // Save variant prefab
                PrefabUtility.SaveAsPrefabAsset(variantObj, variantPrefabPath);
                UnityEngine.Object.DestroyImmediate(variantObj);
                result.CreatedCount++;
            }
        }
    }
}

