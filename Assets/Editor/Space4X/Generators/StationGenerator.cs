using Space4X.Authoring;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Space4X.Editor.Generators
{
    public class StationGenerator : BasePrefabGenerator
    {
        public override bool Generate(PrefabMakerOptions options, PrefabMaker.GenerationResult result)
        {
            var catalog = LoadCatalog(options.CatalogPath);
            if (catalog == null || catalog.stations == null)
            {
                result.Errors.Add("Failed to load station catalog");
                return false;
            }

            EnsureDirectory($"{PrefabBasePath}/Stations");

            bool anyChanged = false;
            foreach (var stationData in catalog.stations)
            {
                // Apply selected IDs filter if specified
                if (options.SelectedIds != null && options.SelectedIds.Count > 0 && !options.SelectedIds.Contains(stationData.id))
                {
                    continue;
                }
                
                // Apply selected category filter if specified
                if (options.SelectedCategory.HasValue && options.SelectedCategory.Value != PrefabTemplateCategory.Stations)
                {
                    continue;
                }
                
                if (string.IsNullOrWhiteSpace(stationData.id))
                {
                    result.Warnings.Add("Skipping station with empty ID");
                    continue;
                }

                var prefabPath = $"{PrefabBasePath}/Stations/{stationData.id}.prefab";
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

                var stationObj = LoadOrCreatePrefab(prefabPath, stationData.id, out bool isNew);

                // Add StationIdAuthoring
                var stationId = stationObj.GetComponent<StationIdAuthoring>();
                if (stationId == null) stationId = stationObj.AddComponent<StationIdAuthoring>();
                stationId.stationId = stationData.id;
                stationId.isRefitFacility = stationData.hasRefitFacility;
                stationId.facilityZoneRadius = stationData.facilityZoneRadius;

                // Add style tokens
                AddStyleTokens(stationObj, stationData.defaultPalette, stationData.defaultRoughness, stationData.defaultPattern);

                // Add placeholder visual
                if (options.PlaceholdersOnly)
                {
                    PlaceholderPrefabUtility.AddPlaceholderVisual(stationObj, PrefabType.Station);
                }

                SavePrefab(stationObj, prefabPath, isNew, result);
                anyChanged = true;
            }

            return anyChanged;
        }

        public override void Validate(PrefabMaker.ValidationReport report)
        {
            var catalog = LoadCatalog("Assets/Data/Catalogs");
            if (catalog?.stations == null) return;

            foreach (var stationData in catalog.stations)
            {
                var prefabPath = $"{PrefabBasePath}/Stations/{stationData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab == null)
                {
                    report.Issues.Add(new PrefabMaker.ValidationIssue
                    {
                        Severity = PrefabMaker.ValidationSeverity.Warning,
                        Message = $"Station '{stationData.id}' has no prefab",
                        PrefabPath = prefabPath
                    });
                    continue;
                }

                // Validate facility tags
                if (stationData.hasRefitFacility)
                {
                    var stationId = prefab.GetComponent<StationIdAuthoring>();
                    if (stationId == null || !stationId.isRefitFacility || stationId.facilityZoneRadius <= 0f)
                    {
                        report.Issues.Add(new PrefabMaker.ValidationIssue
                        {
                            Severity = PrefabMaker.ValidationSeverity.Error,
                            Message = $"Station '{stationData.id}' missing refit facility configuration",
                            PrefabPath = prefabPath
                        });
                    }
                }
            }
        }

        private StationCatalogAuthoring LoadCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/StationCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<StationCatalogAuthoring>();
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

