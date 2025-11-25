using Space4X.Authoring;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Space4X.Editor.Generators
{
    public class ModuleGenerator : BasePrefabGenerator
    {
        public override bool Generate(PrefabMakerOptions options, PrefabMaker.GenerationResult result)
        {
            var catalog = LoadCatalog(options.CatalogPath);
            if (catalog == null || catalog.modules == null)
            {
                result.Errors.Add("Failed to load module catalog");
                return false;
            }

            EnsureDirectory($"{PrefabBasePath}/Modules");

            bool anyChanged = false;
            foreach (var moduleData in catalog.modules)
            {
                // Apply selected IDs filter if specified
                if (options.SelectedIds != null && options.SelectedIds.Count > 0 && !options.SelectedIds.Contains(moduleData.id))
                {
                    continue;
                }
                
                // Apply selected category filter if specified
                if (options.SelectedCategory.HasValue && options.SelectedCategory.Value != PrefabTemplateCategory.Modules)
                {
                    continue;
                }
                
                if (string.IsNullOrWhiteSpace(moduleData.id))
                {
                    result.Warnings.Add("Skipping module with empty ID");
                    continue;
                }

                var prefabPath = $"{PrefabBasePath}/Modules/{moduleData.id}.prefab";
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

                var moduleObj = LoadOrCreatePrefab(prefabPath, moduleData.id, out bool isNew);

                // Add ModuleIdAuthoring
                var moduleId = moduleObj.GetComponent<ModuleIdAuthoring>();
                if (moduleId == null) moduleId = moduleObj.AddComponent<ModuleIdAuthoring>();
                moduleId.moduleId = moduleData.id;

                // Add MountRequirementAuthoring
                var mountReq = moduleObj.GetComponent<MountRequirementAuthoring>();
                if (mountReq == null) mountReq = moduleObj.AddComponent<MountRequirementAuthoring>();
                mountReq.mountType = moduleData.requiredMount;
                mountReq.mountSize = moduleData.requiredSize;

                // Add module function if specified
                if (moduleData.function != ModuleFunction.None)
                {
                    var moduleFunction = moduleObj.GetComponent<ModuleFunctionAuthoring>();
                    if (moduleFunction == null) moduleFunction = moduleObj.AddComponent<ModuleFunctionAuthoring>();
                    moduleFunction.function = moduleData.function;
                    moduleFunction.capacity = moduleData.functionCapacity;
                    moduleFunction.description = moduleData.functionDescription;
                }

                // Add module quality attributes
                var moduleQuality = moduleObj.GetComponent<ModuleQualityAuthoring>();
                if (moduleQuality == null) moduleQuality = moduleObj.AddComponent<ModuleQualityAuthoring>();
                moduleQuality.quality = moduleData.quality;

                var moduleRarity = moduleObj.GetComponent<ModuleRarityAuthoring>();
                if (moduleRarity == null) moduleRarity = moduleObj.AddComponent<ModuleRarityAuthoring>();
                moduleRarity.rarity = moduleData.rarity;

                var moduleTier = moduleObj.GetComponent<ModuleTierAuthoring>();
                if (moduleTier == null) moduleTier = moduleObj.AddComponent<ModuleTierAuthoring>();
                moduleTier.tier = moduleData.tier;

                if (!string.IsNullOrWhiteSpace(moduleData.manufacturerId))
                {
                    var moduleManufacturer = moduleObj.GetComponent<ModuleManufacturerAuthoring>();
                    if (moduleManufacturer == null) moduleManufacturer = moduleObj.AddComponent<ModuleManufacturerAuthoring>();
                    moduleManufacturer.manufacturerId = moduleData.manufacturerId;
                }

                // Add facility archetype/tier if specified
                if (moduleData.facilityArchetype != FacilityArchetype.None)
                {
                    var facilityArchetype = moduleObj.GetComponent<FacilityArchetypeAuthoring>();
                    if (facilityArchetype == null) facilityArchetype = moduleObj.AddComponent<FacilityArchetypeAuthoring>();
                    facilityArchetype.facilityArchetype = moduleData.facilityArchetype;

                    var facilityTier = moduleObj.GetComponent<FacilityTierAuthoring>();
                    if (facilityTier == null) facilityTier = moduleObj.AddComponent<FacilityTierAuthoring>();
                    facilityTier.facilityTier = moduleData.facilityTier;
                }

                // Add placeholder visual
                if (options.PlaceholdersOnly)
                {
                    PlaceholderPrefabUtility.AddPlaceholderVisual(moduleObj, PrefabType.Module);
                }

                SavePrefab(moduleObj, prefabPath, isNew, result);
                anyChanged = true;
            }

            return anyChanged;
        }

        public override void Validate(PrefabMaker.ValidationReport report)
        {
            var catalog = LoadCatalog("Assets/Data/Catalogs");
            if (catalog?.modules == null) return;

            foreach (var moduleData in catalog.modules)
            {
                var prefabPath = $"{PrefabBasePath}/Modules/{moduleData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab == null)
                {
                    report.Issues.Add(new PrefabMaker.ValidationIssue
                    {
                        Severity = PrefabMaker.ValidationSeverity.Warning,
                        Message = $"Module '{moduleData.id}' has no prefab",
                        PrefabPath = prefabPath
                    });
                    continue;
                }

                var mountReq = prefab.GetComponent<MountRequirementAuthoring>();
                if (mountReq == null || mountReq.mountType != moduleData.requiredMount || mountReq.mountSize != moduleData.requiredSize)
                {
                    report.Issues.Add(new PrefabMaker.ValidationIssue
                    {
                        Severity = PrefabMaker.ValidationSeverity.Error,
                        Message = $"Module '{moduleData.id}' mount mismatch",
                        PrefabPath = prefabPath
                    });
                }

                // Validate facility tier compatibility if facility archetype is specified
                if (moduleData.facilityArchetype != FacilityArchetype.None)
                {
                    ValidateFacilityTierCompatibility(moduleData, prefab, prefabPath, report);
                }
            }
        }

        private void ValidateFacilityTierCompatibility(ModuleCatalogAuthoring.ModuleSpecData moduleData, GameObject prefab, string prefabPath, PrefabMaker.ValidationReport report)
        {
            var facilityTier = prefab.GetComponent<FacilityTierAuthoring>();
            if (facilityTier == null)
            {
                report.Issues.Add(new PrefabMaker.ValidationIssue
                {
                    Severity = PrefabMaker.ValidationSeverity.Warning,
                    Message = $"Module '{moduleData.id}' has facility archetype but missing FacilityTierAuthoring",
                    PrefabPath = prefabPath
                });
                return;
            }

            // Validate tier compatibility based on archetype
            // Titanic tier is only for Titan Forge, Stellar Manipulator, Supercarrier Hangar on stations/titans
            if (facilityTier.facilityTier == FacilityTier.Titanic)
            {
                bool isTitanFacility = moduleData.facilityArchetype == FacilityArchetype.TitanForge ||
                                      moduleData.facilityArchetype == FacilityArchetype.StellarManipulator ||
                                      moduleData.facilityArchetype == FacilityArchetype.SupercarrierHangar;
                
                if (!isTitanFacility)
                {
                    report.Issues.Add(new PrefabMaker.ValidationIssue
                    {
                        Severity = PrefabMaker.ValidationSeverity.Warning,
                        Message = $"Module '{moduleData.id}' has Titanic tier but archetype '{moduleData.facilityArchetype}' is not Titan-exclusive",
                        PrefabPath = prefabPath
                    });
                }
            }
        }

        private ModuleCatalogAuthoring LoadCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/ModuleCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<ModuleCatalogAuthoring>();
        }
    }
}

