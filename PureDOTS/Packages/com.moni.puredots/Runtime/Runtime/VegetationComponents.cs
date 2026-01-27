using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Core vegetation identification and species type.
    /// </summary>
    public struct VegetationId : IComponentData
    {
        public int Value;
        public byte SpeciesType; // Tree, Shrub, Grass, Crop, etc.
    }

    /// <summary>
    /// Vegetation lifecycle state (seedling, growing, mature, dying, dead).
    /// </summary>
    public struct VegetationLifecycle : IComponentData
    {
        public enum LifecycleStage : byte
        {
            Seedling = 0,
            Growing = 1,
            Mature = 2,
            Flowering = 3,
            Fruiting = 4,
            Dying = 5,
            Dead = 6
        }

        public LifecycleStage CurrentStage;
        public float GrowthProgress; // 0-1, normalized within current stage
        public float StageTimer; // Time spent in current stage
        public float TotalAge; // Total age in seconds
        public float GrowthRate; // Units per second
    }

    /// <summary>
    /// Health and environmental needs for vegetation.
    /// </summary>
    public struct VegetationHealth : IComponentData
    {
        public float Health; // 0-100
        public float MaxHealth;
        public float WaterLevel; // 0-100
        public float LightLevel; // 0-100
        public float SoilQuality; // 0-100
        public float Temperature; // Comfort level
    }

    /// <summary>
    /// Resource production configuration for vegetation.
    /// </summary>
    public struct VegetationProduction : IComponentData
    {
        public FixedString64Bytes ResourceTypeId; // e.g., "Wood", "Fruit", "Berries"
        public float ProductionRate; // Units per second when fruiting
        public float MaxProductionCapacity; // Max resources this plant can yield
        public float CurrentProduction; // Current accumulated resources
        public float LastHarvestTime; // Timestamp of last harvest
        public float HarvestCooldown; // Minimum time between harvests
    }

    /// <summary>
    /// Resource consumption requirements (water, nutrients, etc.).
    /// </summary>
    public struct VegetationConsumption : IComponentData
    {
        public float WaterConsumptionRate; // Water used per second
        public float NutrientConsumptionRate; // Nutrients used per second
        public float EnergyProductionRate; // Energy produced (for photosynthesis)
    }

    /// <summary>
    /// Reproduction and spreading behavior.
    /// </summary>
    public struct VegetationReproduction : IComponentData
    {
        public float ReproductionTimer; // Time until next reproduction attempt
        public float ReproductionCooldown; // Minimum time between reproductions
        public float SpreadRange; // Maximum distance for spreading
        public float SpreadChance; // Probability of spreading on timer
        public int MaxOffspringRadius; // Grid radius for offspring placement
        public ushort ActiveOffspring; // Number of currently alive offspring spawned by this entity
        public uint SpawnSequence; // Monotonic sequence used to derive deterministic offspring ids
    }

    /// <summary>
    /// Seasonal effects and climate sensitivity.
    /// </summary>
    public struct VegetationSeasonal : IComponentData
    {
        public enum SeasonType : byte
        {
            Spring = 0,
            Summer = 1,
            Autumn = 2,
            Winter = 3
        }

        public SeasonType CurrentSeason;
        public float SeasonMultiplier; // Growth/production multiplier for current season
        public float FrostResistance; // Resistance to cold damage (0-1)
        public float DroughtResistance; // Resistance to water shortage (0-1)
    }

    /// <summary>
    /// Tags for vegetation states.
    /// </summary>
    public struct VegetationMatureTag : IComponentData, IEnableableComponent { }
    public struct VegetationReadyToHarvestTag : IComponentData, IEnableableComponent { }
    public struct VegetationDeadTag : IComponentData { }
    public struct VegetationDyingTag : IComponentData, IEnableableComponent { }
    public struct VegetationDecayableTag : IComponentData { }

    /// <summary>
    /// Buffer for tracking seeds dropped by this vegetation.
    /// </summary>
    public struct VegetationSeedDrop : IBufferElementData
    {
        public float3 DropPosition;
        public uint DropTick;
        public float GerminationChance;
    }

    /// <summary>
    /// History events for vegetation (growth milestones, harvests, deaths).
    /// </summary>
    public struct VegetationHistoryEvent : IBufferElementData
    {
        public enum EventType : byte
        {
            Planted = 0,
            StageTransition = 1,
            Harvested = 2,
            Died = 3,
            Reproduced = 4,
            Damage = 5
        }

        public EventType Type;
        public uint EventTick;
        public float Value; // Contextual value (damage amount, harvest yield, etc.)
    }

    /// <summary>
    /// Configuration for vegetation spawning and placement.
    /// </summary>
    public struct VegetationSpawnConfig : IComponentData
    {
        public Entity VegetationPrefab;
        public float3 SpawnAreaCenter;
        public float SpawnAreaRadius;
        public int InitialCount;
        public int MaxCount;
        public float SpawnDensity; // Vegetation per unit area
    }

    /// <summary>
    /// Singleton marker for queued vegetation spawn commands.
    /// </summary>
    public struct VegetationSpawnCommandQueue : IComponentData { }

    /// <summary>
    /// Command describing a vegetation spawn request.
    /// </summary>
    public struct VegetationSpawnCommand : IBufferElementData
    {
        public ushort SpeciesIndex;
        public float3 Position;
        public Entity Parent;
        public uint ParentId;
        public uint IssuedTick;
        public uint SequenceId;
        public FixedString64Bytes ResourceTypeId;
    }

    /// <summary>
    /// Links a vegetation entity to the parent that spawned it.
    /// </summary>
    public struct VegetationParent : IComponentData
    {
        public Entity Value;
    }

    /// <summary>
    /// Grid-based vegetation management singleton.
    /// </summary>
    public struct VegetationGridData : IComponentData
    {
        public int GridCellSize;
        public int TotalVegetationCount;
        public int MatureVegetationCount;
        public int DeadVegetationCount;
        public float AverageGrowthRate;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Singleton marker for vegetation harvest command queue.
    /// </summary>
    public struct VegetationHarvestCommandQueue : IComponentData { }

    /// <summary>
    /// Harvest command issued by villagers or gameplay systems.
    /// </summary>
    public struct VegetationHarvestCommand : IBufferElementData
    {
        public Entity Villager;
        public Entity Vegetation;
        public ushort SpeciesIndex;
        public float RequestedAmount;
        public uint IssuedTick;
        public uint CommandId;
    }

    public enum VegetationHarvestResult : byte
    {
        Success = 0,
        InvalidEntities = 1,
        NotReady = 2,
        Cooldown = 3,
        Empty = 4,
        NoInventory = 5
    }

    /// <summary>
    /// Receipt written after a harvest command is processed.
    /// </summary>
    public struct VegetationHarvestReceipt : IBufferElementData
    {
        public VegetationHarvestResult Result;
        public Entity Villager;
        public Entity Vegetation;
        public FixedString64Bytes ResourceTypeId;
        public float HarvestedAmount;
        public uint IssuedTick;
        public uint ProcessedTick;
    }
}
