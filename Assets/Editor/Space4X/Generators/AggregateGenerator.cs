using Space4X.Authoring;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.Generators
{
    public class AggregateGenerator : BasePrefabGenerator
    {
        public override bool Generate(PrefabMakerOptions options, PrefabMaker.GenerationResult result)
        {
            var catalog = LoadCatalog(options.CatalogPath);
            if (catalog == null || catalog.aggregates == null)
            {
                result.Errors.Add("Failed to load aggregate catalog");
                return false;
            }

            EnsureDirectory($"{PrefabBasePath}/Aggregates");

            bool anyChanged = false;
            foreach (var aggregateData in catalog.aggregates)
            {
                // Apply selected IDs filter if specified
                if (options.SelectedIds != null && options.SelectedIds.Count > 0 && !options.SelectedIds.Contains(aggregateData.id))
                {
                    continue;
                }
                
                // Apply selected category filter if specified
                if (options.SelectedCategory.HasValue && options.SelectedCategory.Value != PrefabTemplateCategory.Aggregates)
                {
                    continue;
                }
                
                if (string.IsNullOrWhiteSpace(aggregateData.id))
                {
                    result.Warnings.Add("Skipping aggregate with empty ID");
                    continue;
                }

                var prefabPath = $"{PrefabBasePath}/Aggregates/{aggregateData.id}.prefab";
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

                var aggregateObj = LoadOrCreatePrefab(prefabPath, aggregateData.id, out bool isNew);

                // Add AggregateIdAuthoring
                var aggregateId = aggregateObj.GetComponent<AggregateIdAuthoring>();
                if (aggregateId == null) aggregateId = aggregateObj.AddComponent<AggregateIdAuthoring>();
                aggregateId.aggregateId = aggregateData.id;
                
                // Add simple tokens or composed profiles based on mode
                if (aggregateData.useComposedProfiles)
                {
                    // Use composed aggregate with profile references
                    var composedAggregate = aggregateObj.GetComponent<ComposedAggregateAuthoring>();
                    if (composedAggregate == null) composedAggregate = aggregateObj.AddComponent<ComposedAggregateAuthoring>();
                    composedAggregate.templateId = aggregateData.templateId;
                    composedAggregate.outlookId = aggregateData.outlookId;
                    composedAggregate.alignmentId = aggregateData.alignmentId;
                    composedAggregate.personalityId = aggregateData.personalityId;
                    composedAggregate.themeId = aggregateData.themeId;
                }
                else
                {
                    // Use simple byte tokens
                    aggregateId.alignment = aggregateData.alignment;
                    aggregateId.outlook = aggregateData.outlook;
                    aggregateId.policy = aggregateData.policy;
                }

                // Add aggregate type
                var aggregateType = aggregateObj.GetComponent<AggregateTypeAuthoring>();
                if (aggregateType == null) aggregateType = aggregateObj.AddComponent<AggregateTypeAuthoring>();
                aggregateType.aggregateType = aggregateData.aggregateType;

                // Add reputation/prestige
                var reputation = aggregateObj.GetComponent<ReputationAuthoring>();
                if (reputation == null) reputation = aggregateObj.AddComponent<ReputationAuthoring>();
                reputation.reputation = aggregateData.initialReputation;
                reputation.prestige = aggregateData.initialPrestige;

                // Add aggregate alignment (for aggregated buffers)
                var aggregateAlignment = aggregateObj.GetComponent<AggregateAlignmentAuthoring>();
                if (aggregateAlignment == null)
                {
                    aggregateAlignment = aggregateObj.AddComponent<AggregateAlignmentAuthoring>();
                }

                // Add style tokens
                AddStyleTokens(aggregateObj, aggregateData.defaultPalette, aggregateData.defaultRoughness, aggregateData.defaultPattern);

                // Add placeholder visual (HUD token)
                if (options.PlaceholdersOnly)
                {
                    PlaceholderPrefabUtility.AddPlaceholderVisual(aggregateObj, PrefabType.Aggregate);
                }

                SavePrefab(aggregateObj, prefabPath, isNew, result);
                anyChanged = true;
            }

            return anyChanged;
        }

        public override void Validate(PrefabMaker.ValidationReport report)
        {
            var catalog = LoadCatalog("Assets/Data/Catalogs");
            if (catalog?.aggregates == null) return;

            foreach (var aggregateData in catalog.aggregates)
            {
                var prefabPath = $"{PrefabBasePath}/Aggregates/{aggregateData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab == null)
                {
                    report.Issues.Add(new PrefabMaker.ValidationIssue
                    {
                        Severity = PrefabMaker.ValidationSeverity.Warning,
                        Message = $"Aggregate '{aggregateData.id}' has no prefab",
                        PrefabPath = prefabPath
                    });
                }
            }
        }

        private AggregateCatalogAuthoring LoadCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/AggregateCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<AggregateCatalogAuthoring>();
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

