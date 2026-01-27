using PureDOTS.Config;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Entities.Hybrid;
#endif

namespace PureDOTS.Authoring
{
    /// <summary>
    /// MonoBehaviour that references a vegetation species catalog asset for baking.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VegetationSpeciesCatalogAuthoring : MonoBehaviour
    {
        [Tooltip("Reference to the ScriptableObject catalog that defines species data.")]
        public VegetationSpeciesCatalog catalog;
        class Baker : Unity.Entities.Baker<VegetationSpeciesCatalogAuthoring>
        {
            public override void Bake(VegetationSpeciesCatalogAuthoring authoring)
            {
                if (authoring.catalog == null)
                {
                    Debug.LogWarning("[VegetationSpeciesCatalogBaker] Missing catalog reference.");
                    return;
                }

                var catalog = authoring.catalog;
                if (catalog.species == null || catalog.species.Count == 0)
                {
                    Debug.LogWarning($"[VegetationSpeciesCatalogBaker] No species defined in {catalog.name}.");
                    return;
                }

                // Build blob data
                var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<VegetationSpeciesCatalogBlob>();
                var speciesArrayBuilder = builder.Allocate(ref catalogBlob.Species, catalog.species.Count);

                for (int i = 0; i < catalog.species.Count; i++)
                {
                    var speciesDef = catalog.species[i];
                    ref var speciesBlob = ref speciesArrayBuilder[i];

                    builder.AllocateString(ref speciesBlob.SpeciesId, speciesDef.speciesId ?? string.Empty);

                    speciesBlob.SeedlingDuration = speciesDef.seedlingDuration;
                    speciesBlob.GrowingDuration = speciesDef.growingDuration;
                    speciesBlob.MatureDuration = speciesDef.matureDuration;
                    speciesBlob.FloweringDuration = speciesDef.floweringDuration;
                    speciesBlob.FruitingDuration = speciesDef.fruitingDuration;
                    speciesBlob.DyingDuration = speciesDef.dyingDuration;
                    speciesBlob.RespawnDelay = speciesDef.respawnDelay;

                    speciesBlob.BaseGrowthRate = speciesDef.baseGrowthRate;

                    var stageMultiplierBuilder = builder.Allocate(ref speciesBlob.StageMultipliers, 6);
                    for (int j = 0; j < stageMultiplierBuilder.Length; j++)
                    {
                        stageMultiplierBuilder[j] = (speciesDef.stageMultipliers != null && j < speciesDef.stageMultipliers.Length)
                            ? speciesDef.stageMultipliers[j]
                            : 1f;
                    }

                    var seasonalMultiplierBuilder = builder.Allocate(ref speciesBlob.SeasonalMultipliers, 4);
                    for (int j = 0; j < seasonalMultiplierBuilder.Length; j++)
                    {
                        seasonalMultiplierBuilder[j] = (speciesDef.seasonalMultipliers != null && j < speciesDef.seasonalMultipliers.Length)
                            ? speciesDef.seasonalMultipliers[j]
                            : 1f;
                    }

                    speciesBlob.MaxHealth = speciesDef.maxHealth;
                    speciesBlob.BaselineRegen = speciesDef.baselineRegen;
                    speciesBlob.DamagePerDeficit = speciesDef.damagePerDeficit;
                    speciesBlob.DroughtToleranceSeconds = speciesDef.droughtToleranceSeconds;
                    speciesBlob.FrostToleranceSeconds = speciesDef.frostToleranceSeconds;

                    speciesBlob.MaxYieldPerCycle = speciesDef.maxYieldPerCycle;
                    speciesBlob.HarvestCooldown = speciesDef.harvestCooldown;
                    builder.AllocateString(ref speciesBlob.ResourceTypeId, speciesDef.resourceTypeId ?? string.Empty);
                    speciesBlob.PartialHarvestPenalty = speciesDef.partialHarvestPenalty;

                    speciesBlob.DesiredMinWater = speciesDef.desiredMinWater;
                    speciesBlob.DesiredMaxWater = speciesDef.desiredMaxWater;
                    speciesBlob.DesiredMinLight = speciesDef.desiredMinLight;
                    speciesBlob.DesiredMaxLight = speciesDef.desiredMaxLight;
                    speciesBlob.DesiredMinSoilQuality = speciesDef.desiredMinSoilQuality;
                    speciesBlob.DesiredMaxSoilQuality = speciesDef.desiredMaxSoilQuality;
                    speciesBlob.PollutionTolerance = speciesDef.pollutionTolerance;
                    speciesBlob.WindTolerance = speciesDef.windTolerance;

                    speciesBlob.ReproductionCooldown = speciesDef.reproductionCooldown;
                    speciesBlob.SeedsPerEvent = speciesDef.seedsPerEvent;
                    speciesBlob.SpreadRadius = speciesDef.spreadRadius;
                    speciesBlob.OffspringCapPerParent = speciesDef.offspringCapPerParent;
                    speciesBlob.MaturityRequirement = speciesDef.maturityRequirement;
                    speciesBlob.GridCellPadding = speciesDef.gridCellPadding;

                    speciesBlob.GrowthSeed = speciesDef.growthSeed;
                    speciesBlob.ReproductionSeed = speciesDef.reproductionSeed;
                    speciesBlob.LootSeed = speciesDef.lootSeed;
                }

                var blobAsset = builder.CreateBlobAssetReference<VegetationSpeciesCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                // Register blob asset for automatic disposal / deduplication
                AddBlobAsset(ref blobAsset, out _);

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new VegetationSpeciesLookup
                {
                    CatalogBlob = blobAsset
                });

                Debug.Log($"[VegetationSpeciesCatalogBaker] Created catalog from {catalog.name} with {catalog.species.Count} species.");
            }
        }
    }
}
