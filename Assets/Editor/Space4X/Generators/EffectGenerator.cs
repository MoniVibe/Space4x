using Space4X.Authoring;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.Generators
{
    public class EffectGenerator : BasePrefabGenerator
    {
        public override bool Generate(PrefabMakerOptions options, PrefabMaker.GenerationResult result)
        {
            var catalog = LoadCatalog(options.CatalogPath);
            if (catalog == null || catalog.effects == null)
            {
                result.Errors.Add("Failed to load effect catalog");
                return false;
            }

            EnsureDirectory($"{PrefabBasePath}/FX");

            bool anyChanged = false;
            foreach (var effectData in catalog.effects)
            {
                // Apply selected IDs filter if specified
                if (options.SelectedIds != null && options.SelectedIds.Count > 0 && !options.SelectedIds.Contains(effectData.id))
                {
                    continue;
                }
                
                // Apply selected category filter if specified
                if (options.SelectedCategory.HasValue && options.SelectedCategory.Value != PrefabTemplateCategory.FX)
                {
                    continue;
                }
                
                if (string.IsNullOrWhiteSpace(effectData.id))
                {
                    result.Warnings.Add("Skipping effect with empty ID");
                    continue;
                }

                var prefabPath = $"{PrefabBasePath}/FX/{effectData.id}.prefab";
                var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (existingPrefab != null)
                {
                    result.SkippedCount++;
                    continue;
                }

                if (options.DryRun)
                {
                    result.CreatedCount++;
                    anyChanged = true;
                    continue;
                }

                var effectObj = LoadOrCreatePrefab(prefabPath, effectData.id, out bool isNew);

                // Add EffectIdAuthoring
                var effectId = effectObj.GetComponent<EffectIdAuthoring>();
                if (effectId == null) effectId = effectObj.AddComponent<EffectIdAuthoring>();
                effectId.effectId = effectData.id;

                // Add style tokens
                AddStyleTokens(effectObj, effectData.defaultPalette, effectData.defaultRoughness, effectData.defaultPattern);

                // Add placeholder visual
                if (options.PlaceholdersOnly)
                {
                    PlaceholderPrefabUtility.AddPlaceholderVisual(effectObj, PrefabType.Effect);
                }

                SavePrefab(effectObj, prefabPath, isNew, result);
                anyChanged = true;
            }

            return anyChanged;
        }

        public override void Validate(PrefabMaker.ValidationReport report)
        {
            var catalog = LoadCatalog("Assets/Data/Catalogs");
            if (catalog?.effects == null) return;

            foreach (var effectData in catalog.effects)
            {
                var prefabPath = $"{PrefabBasePath}/FX/{effectData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab == null)
                {
                    report.Issues.Add(new PrefabMaker.ValidationIssue
                    {
                        Severity = PrefabMaker.ValidationSeverity.Warning,
                        Message = $"Effect '{effectData.id}' has no prefab",
                        PrefabPath = prefabPath
                    });
                }
            }
        }

        private EffectCatalogAuthoring LoadCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/EffectCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<EffectCatalogAuthoring>();
        }

        private void AddStyleTokens(GameObject obj, byte palette, byte roughness, byte pattern)
        {
            if (palette == 0 && roughness == 128 && pattern == 0) return;

            var styleTokens = obj.GetComponent<StyleTokensAuthoring>();
            if (styleTokens == null) styleTokens = obj.AddComponent<StyleTokensAuthoring>();
            styleTokens.palette = palette;
            styleTokens.roughness = roughness;
            styleTokens.pattern = pattern;
        }
    }
}

