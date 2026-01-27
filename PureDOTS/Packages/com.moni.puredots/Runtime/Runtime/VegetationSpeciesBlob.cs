using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Blob asset structure for vegetation species data.
    /// </summary>
    public struct VegetationSpeciesBlob
    {
        public BlobString SpeciesId;
        
        // Stage durations in seconds
        public float SeedlingDuration;
        public float GrowingDuration;
        public float MatureDuration;
        public float FloweringDuration;
        public float FruitingDuration;
        public float DyingDuration;
        public float RespawnDelay;
        
        // Growth settings
        public float BaseGrowthRate;
        public BlobArray<float> StageMultipliers;
        public BlobArray<float> SeasonalMultipliers;
        
        // Health settings
        public float MaxHealth;
        public float BaselineRegen;
        public float DamagePerDeficit;
        public float DroughtToleranceSeconds;
        public float FrostToleranceSeconds;
        
        // Harvest settings
        public float MaxYieldPerCycle;
        public float HarvestCooldown;
        public BlobString ResourceTypeId;
        public float PartialHarvestPenalty;
        
        // Environment thresholds
        public float DesiredMinWater;
        public float DesiredMaxWater;
        public float DesiredMinLight;
        public float DesiredMaxLight;
        public float DesiredMinSoilQuality;
        public float DesiredMaxSoilQuality;
        public float PollutionTolerance;
        public float WindTolerance;
        
        // Reproduction settings
        public float ReproductionCooldown;
        public int SeedsPerEvent;
        public float SpreadRadius;
        public int OffspringCapPerParent;
        public float MaturityRequirement;
        public int GridCellPadding;
        
        // Random seeds
        public uint GrowthSeed;
        public uint ReproductionSeed;
        public uint LootSeed;
    }

    /// <summary>
    /// Blob asset catalog for all vegetation species.
    /// </summary>
    public struct VegetationSpeciesCatalogBlob
    {
        public BlobArray<VegetationSpeciesBlob> Species;
    }

    /// <summary>
    /// Singleton component providing access to the species catalog blob.
    /// </summary>
    public struct VegetationSpeciesLookup : IComponentData
    {
        public BlobAssetReference<VegetationSpeciesCatalogBlob> CatalogBlob;
    }

    /// <summary>
    /// Index reference to species in the catalog blob.
    /// </summary>
    public struct VegetationSpeciesIndex : IComponentData
    {
        public ushort Value;
    }

    /// <summary>
    /// Random state for deterministic behavior in vegetation systems.
    /// </summary>
    public struct VegetationRandomState : IComponentData
    {
        public uint GrowthRandomIndex;
        public uint ReproductionRandomIndex;
        public uint LootRandomIndex;
    }
}




