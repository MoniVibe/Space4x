using PureDOTS.Config;
using PureDOTS.Runtime.Components;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component for vegetation entities that will be converted to ECS runtime components.
    /// Supports both catalog-driven and manual species configuration.
    /// </summary>
    public class VegetationAuthoring : MonoBehaviour
    {
        [Header("Species Configuration")]
        public bool useCatalog = true;
        public VegetationSpeciesCatalog catalog;
        [Tooltip("Index into the catalog when using data-driven species.")]
        public byte catalogSpeciesIndex = 0;

        [Header("Manual Override (if useCatalog = false)")]
        public int vegetationId;
        public VegetationSpeciesType speciesType = VegetationSpeciesType.Tree;

        [Header("Lifecycle")]
        public VegetationLifecycle.LifecycleStage initialStage = VegetationLifecycle.LifecycleStage.Seedling;
        public float growthRate = 0.5f;

        [Header("Health")]
        public float maxHealth = 100f;
        public float initialHealth = 50f;
        public float initialWaterLevel = 50f;
        public float initialLightLevel = 75f;
        public float initialSoilQuality = 60f;

        [Header("Production")]
        public string resourceTypeId = "Wood";
        public float productionRate = 1f;
        public float maxProductionCapacity = 10f;
        public float harvestCooldown = 60f;

        [Header("Consumption")]
        public float waterConsumptionRate = 0.1f;
        public float nutrientConsumptionRate = 0.05f;
        public float energyProductionRate = 0.2f;

        [Header("Reproduction")]
        public float reproductionCooldown = 300f;
        public float spreadRange = 5f;
        public float spreadChance = 0.1f;
        public int maxOffspringRadius = 2;

        [Header("Seasonal")]
        public float frostResistance = 0.5f;
        public float droughtResistance = 0.5f;

        public enum VegetationSpeciesType : byte
        {
            Tree = 0,
            Shrub = 1,
            Grass = 2,
            Crop = 3,
            Flower = 4,
            Fungus = 5
        }
    }

    /// <summary>
    /// Baker that converts VegetationAuthoring settings into DOTS components.
    /// </summary>
    public sealed class VegetationBaker : Baker<VegetationAuthoring>
    {
        public override void Bake(VegetationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Identification and species index
            if (authoring.useCatalog && authoring.catalog != null && authoring.catalog.species.Count > authoring.catalogSpeciesIndex)
            {
                AddComponent(entity, new VegetationId
                {
                    Value = authoring.vegetationId,
                    SpeciesType = authoring.catalogSpeciesIndex
                });

                AddComponent(entity, new VegetationSpeciesIndex
                {
                    Value = authoring.catalogSpeciesIndex
                });
            }
            else
            {
                // Manual mode: warn only if catalog mode was intended but catalog is missing
                if (authoring.useCatalog && (authoring.catalog == null || authoring.catalog.species.Count == 0))
                {
                    Debug.LogWarning($"[VegetationBaker] {authoring.name} is configured to use catalog but no catalog entry was supplied. Falling back to slot 0.");
                }

                AddComponent(entity, new VegetationId
                {
                    Value = authoring.vegetationId,
                    SpeciesType = (byte)authoring.speciesType
                });

                AddComponent(entity, new VegetationSpeciesIndex
                {
                    Value = 0
                });
            }

            // Random state for deterministic behaviour
            AddComponent(entity, new VegetationRandomState
            {
                GrowthRandomIndex = 0,
                ReproductionRandomIndex = 0,
                LootRandomIndex = 0
            });

            // Lifecycle baseline (progress & timers reset)
            AddComponent(entity, new VegetationLifecycle
            {
                CurrentStage = authoring.initialStage,
                GrowthProgress = 0f,
                StageTimer = 0f,
                TotalAge = 0f,
                GrowthRate = authoring.growthRate
            });

            // Health
            AddComponent(entity, new VegetationHealth
            {
                Health = authoring.initialHealth,
                MaxHealth = authoring.maxHealth,
                WaterLevel = authoring.initialWaterLevel,
                LightLevel = authoring.initialLightLevel,
                SoilQuality = authoring.initialSoilQuality,
                Temperature = 20f
            });

            // Production & consumption
            AddComponent(entity, new VegetationProduction
            {
                ResourceTypeId = authoring.resourceTypeId,
                ProductionRate = authoring.productionRate,
                MaxProductionCapacity = authoring.maxProductionCapacity,
                CurrentProduction = 0f,
                LastHarvestTime = 0f,
                HarvestCooldown = authoring.harvestCooldown
            });

            AddComponent(entity, new VegetationConsumption
            {
                WaterConsumptionRate = authoring.waterConsumptionRate,
                NutrientConsumptionRate = authoring.nutrientConsumptionRate,
                EnergyProductionRate = authoring.energyProductionRate
            });

            // Reproduction
            AddComponent(entity, new VegetationReproduction
            {
                ReproductionTimer = 0f,
                ReproductionCooldown = authoring.reproductionCooldown,
                SpreadRange = authoring.spreadRange,
                SpreadChance = authoring.spreadChance,
                MaxOffspringRadius = authoring.maxOffspringRadius,
                ActiveOffspring = 0,
                SpawnSequence = 0
            });

            // Seasonal sensitivity
            AddComponent(entity, new VegetationSeasonal
            {
                CurrentSeason = VegetationSeasonal.SeasonType.Spring,
                SeasonMultiplier = 1f,
                FrostResistance = authoring.frostResistance,
                DroughtResistance = authoring.droughtResistance
            });

            // Buffers
            AddBuffer<VegetationSeedDrop>(entity);
            AddBuffer<VegetationHistoryEvent>(entity);

            // Enableable tags seeded disabled
            AddComponent(entity, new VegetationMatureTag());
            AddComponent(entity, new VegetationReadyToHarvestTag());
            AddComponent(entity, new VegetationDyingTag());
            AddComponent(entity, new VegetationStressedTag());

            AddComponent(entity, new VegetationParent
            {
                Value = Entity.Null
            });

            AddComponent<RewindableTag>(entity);

            bool isMatureStage = authoring.initialStage == VegetationLifecycle.LifecycleStage.Mature ||
                                 authoring.initialStage == VegetationLifecycle.LifecycleStage.Flowering ||
                                 authoring.initialStage == VegetationLifecycle.LifecycleStage.Fruiting;

            SetComponentEnabled<VegetationMatureTag>(entity, isMatureStage);
            SetComponentEnabled<VegetationReadyToHarvestTag>(entity, authoring.initialStage == VegetationLifecycle.LifecycleStage.Fruiting);
            SetComponentEnabled<VegetationDyingTag>(entity, authoring.initialStage == VegetationLifecycle.LifecycleStage.Dying);
            SetComponentEnabled<VegetationStressedTag>(entity, false);

            // Additional tags for dying/dead states
            if (authoring.initialStage == VegetationLifecycle.LifecycleStage.Dying ||
                authoring.initialStage == VegetationLifecycle.LifecycleStage.Dead)
            {
                AddComponent(entity, new VegetationDecayableTag());
            }

            if (authoring.initialStage == VegetationLifecycle.LifecycleStage.Dead)
            {
                AddComponent(entity, new VegetationDeadTag());
            }
        }
    }
}
