using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Reference to a villager worshipping at a worship site.
    /// </summary>
    public struct WorshipperRef : IBufferElementData
    {
        public Entity VillagerEntity;
        public float WorshipStartTime;
        public float ContributionRate; // Mana per second contributed by this worshipper
    }

    /// <summary>
    /// Configuration for worship site behavior.
    /// </summary>
    public struct WorshipSiteConfig : IComponentData
    {
        public int MaxWorshippers;
        public float WorshipBonusMultiplier;
        public byte CanStoreMana;
    }

    /// <summary>
    /// Reference to a villager residing in housing.
    /// </summary>
    public struct ResidentRef : IBufferElementData
    {
        public Entity VillagerEntity;
        public float MoveInTime;
        public float LastRestTime;
    }

    /// <summary>
    /// Configuration for housing behavior.
    /// </summary>
    public struct HousingConfig : IComponentData
    {
        public int MaxResidents;
        public float RestBonusMultiplier;
        public float ComfortLevel;
        public float TemperatureBonus;
        public float EnergyRestoreRate;
        public float MoraleRestoreRate;
    }

    /// <summary>
    /// Runtime state for housing occupancy.
    /// </summary>
    public struct HousingState : IComponentData
    {
        public int CurrentResidents;
        public float OccupancyRate; // 0-1 occupancy percentage
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Village identification component.
    /// </summary>
    public struct VillageId : IComponentData
    {
        public int Value;
        public int FactionId;
    }

    /// <summary>
    /// Configuration for village spawning behavior.
    /// </summary>
    public struct VillageSpawnConfig : IComponentData
    {
        public Entity VillagerPrefab;
        public int MaxPopulation;
        public float SpawnRadius;
    }

    /// <summary>
    /// Village-level statistics and metrics.
    /// </summary>
    public struct VillageStats : IComponentData
    {
        public float Alignment; // 0-100 alignment with player/divine
        public float Cohesion; // 0-100 internal unity
        public float Initiative; // 0-100 autonomous action level
        public int Population;
        public int ActiveWorkers;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Configuration for village residency system.
    /// </summary>
    public struct VillageResidencyConfig : IComponentData
    {
        public int ResidencyQuota;
        public float ResidencyRange;
    }

    /// <summary>
    /// Runtime state for village residency tracking.
    /// </summary>
    public struct VillageResidencyState : IComponentData
    {
        public int CurrentResidents;
        public int PendingResidents;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Entry tracking a villager's residency claim to this village.
    /// </summary>
    public struct VillageResidentEntry : IBufferElementData
    {
        public Entity VillagerEntity;
        public float ClaimTime;
        public byte Priority; // Higher priority residents get preference during quota limits
    }
}


