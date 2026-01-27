using System;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    public enum FactionType : byte
    {
        Undefined = 0,
        PlayerControlled = 1,
        AiControlled = 2,
        Neutral = 3,
        Custom0 = 240
    }

    [System.Flags]
    public enum DiplomaticStatusFlags : byte
    {
        None = 0,
        Allied = 1 << 0,
        AtWar = 1 << 1,
        TradePact = 1 << 2,
        Ceasefire = 1 << 3,
        NonAggression = 1 << 4,
        Custom0 = 1 << 7
    }

    public enum ClimateHazardType : byte
    {
        Undefined = 0,
        Storm = 1,
        Drought = 2,
        HeatWave = 3,
        Blizzard = 4,
        Radiation = 5,
        Custom0 = 240
    }

    [System.Flags]
    public enum EnvironmentChannelMask : byte
    {
        None = 0,
        Moisture = 1 << 0,
        Temperature = 1 << 1,
        Wind = 1 << 2,
        Sunlight = 1 << 3,
        Radiation = 1 << 4,
        Debris = 1 << 5,
        Custom0 = 1 << 7
    }

    public enum AreaEffectType : byte
    {
        Undefined = 0,
        Buff = 1,
        Debuff = 2,
        SlowField = 3,
        TimeDilation = 4,
        Shield = 5,
        Custom0 = 240
    }

    [System.Flags]
    public enum AreaEffectTargetMask : byte
    {
        None = 0,
        Villagers = 1 << 0,
        Resources = 1 << 1,
        Creatures = 1 << 2,
        Structures = 1 << 3,
        Vehicles = 1 << 4,
        Custom0 = 1 << 7
    }

    public enum CultureType : byte
    {
        Undefined = 0,
        Tribal = 1,
        Religious = 2,
        Political = 3,
        Technological = 4,
        Custom0 = 240
    }

    [System.Flags]
    public enum CultureAlignmentFlags : byte
    {
        None = 0,
        Shifting = 1 << 0,
        Stable = 1 << 1,
        Volatile = 1 << 2,
        Ascending = 1 << 3,
        Declining = 1 << 4,
        Custom0 = 1 << 7
    }

    /// <summary>
    /// Registry tracking factions/empires, their territories, resources, and diplomatic state.
    /// </summary>
    public struct FactionRegistry : IComponentData
    {
        public int FactionCount;
        public int TotalTerritoryCells;
        public float TotalResources;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Entry in the faction registry following hot/cold split pattern.
    /// </summary>
    public struct FactionRegistryEntry : IBufferElementData, IComparable<FactionRegistryEntry>, IRegistryEntry
    {
        // Hot fields (frequently updated)
        public Entity FactionEntity;
        public float3 TerritoryCenter; // Weighted center of controlled cells
        public int CellId; // Primary territory cell
        public uint SpatialVersion;
        public uint LastMutationTick;

        // Cold fields (rarely updated)
        public ushort FactionId;
        public FixedString64Bytes FactionName;
        public FactionType FactionType; // Player, AI, Neutral, etc.
        public float ResourceStockpile;
        public int PopulationCount;
        public int TerritoryCellCount;
        public DiplomaticStatusFlags DiplomaticStatus; // Flags: AtWar, Allied, Neutral, etc.
        public FixedString128Bytes Description;

        // IRegistryEntry implementation
        public Entity RegistryEntity => FactionEntity;

        public int CompareTo(FactionRegistryEntry other)
        {
            var entityCompare = FactionEntity.Index.CompareTo(other.FactionEntity.Index);
            if (entityCompare != 0) return entityCompare;
            return FactionEntity.Version.CompareTo(other.FactionEntity.Version);
        }
    }

    /// <summary>
    /// Component on faction entities identifying them for registry.
    /// </summary>
    public struct FactionId : IComponentData
    {
        public ushort Value;
        public FixedString64Bytes Name;
        public FactionType Type; // Player, AI, Neutral, etc.
    }

    /// <summary>
    /// State component on faction entities tracking current metrics.
    /// </summary>
    public struct FactionState : IComponentData
    {
        public float ResourceStockpile;
        public int PopulationCount;
        public int TerritoryCellCount;
        public DiplomaticStatusFlags DiplomaticStatus; // Flags: AtWar, Allied, Neutral, etc.
        public float3 TerritoryCenter;
    }

    /// <summary>
    /// Registry for global climate / hazard events affecting regions.
    /// </summary>
    public struct ClimateHazardRegistry : IComponentData
    {
        public int ActiveHazardCount;
        public float GlobalHazardIntensity; // 0-1 aggregate
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Entry in the climate hazard registry following hot/cold split pattern.
    /// </summary>
    public struct ClimateHazardRegistryEntry : IBufferElementData, IComparable<ClimateHazardRegistryEntry>, IRegistryEntry
    {
        // Hot fields
        public Entity HazardEntity;
        public float3 Position; // Center of hazard
        public int CellId;
        public uint SpatialVersion;
        public uint LastMutationTick;
        public float CurrentIntensity; // 0-1
        public uint ExpirationTick;

        // Cold fields
        public ClimateHazardType HazardType; // Storm, Drought, HeatWave, Blizzard, etc.
        public float Radius;
        public float MaxIntensity;
        public uint StartTick;
        public uint DurationTicks;
        public FixedString64Bytes HazardName;
        public EnvironmentChannelMask AffectedEnvironmentChannels; // Bitmask: Moisture, Temperature, Wind, etc.

        // IRegistryEntry implementation
        public Entity RegistryEntity => HazardEntity;

        public int CompareTo(ClimateHazardRegistryEntry other)
        {
            var entityCompare = HazardEntity.Index.CompareTo(other.HazardEntity.Index);
            if (entityCompare != 0) return entityCompare;
            return HazardEntity.Version.CompareTo(other.HazardEntity.Version);
        }
    }

    /// <summary>
    /// Component on hazard entities identifying them for registry.
    /// </summary>
    public struct ClimateHazardState : IComponentData
    {
        public ClimateHazardType HazardType;
        public float CurrentIntensity;
        public float Radius;
        public float MaxIntensity;
        public uint StartTick;
        public uint DurationTicks;
        public FixedString64Bytes HazardName;
        public EnvironmentChannelMask AffectedEnvironmentChannels;
    }

    /// <summary>
    /// Registry for area-based effects (buffs, debuffs, slow fields, time manipulation zones).
    /// </summary>
    public struct AreaEffectRegistry : IComponentData
    {
        public int ActiveEffectCount;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Entry in the area effect registry following hot/cold split pattern.
    /// </summary>
    public struct AreaEffectRegistryEntry : IBufferElementData, IComparable<AreaEffectRegistryEntry>, IRegistryEntry
    {
        // Hot fields
        public Entity EffectEntity;
        public float3 Position;
        public int CellId;
        public uint SpatialVersion;
        public uint LastMutationTick;
        public float CurrentStrength; // Modifier strength (0-1 or multiplier)
        public uint ExpirationTick;

        // Cold fields
        public AreaEffectType EffectType; // Buff, Debuff, SlowField, TimeDilation, etc.
        public float Radius;
        public float MaxStrength;
        public Entity OwnerEntity; // Entity that created the effect
        public ushort EffectId; // Reference to effect catalog
        public AreaEffectTargetMask AffectedArchetypes; // Bitmask: Villagers, Resources, Creatures, etc.
        public FixedString64Bytes EffectName;

        // IRegistryEntry implementation
        public Entity RegistryEntity => EffectEntity;

        public int CompareTo(AreaEffectRegistryEntry other)
        {
            var entityCompare = EffectEntity.Index.CompareTo(other.EffectEntity.Index);
            if (entityCompare != 0) return entityCompare;
            return EffectEntity.Version.CompareTo(other.EffectEntity.Version);
        }
    }

    /// <summary>
    /// Component on area effect entities identifying them for registry.
    /// </summary>
    public struct AreaEffectState : IComponentData
    {
        public AreaEffectType EffectType;
        public float CurrentStrength;
        public float Radius;
        public float MaxStrength;
        public Entity OwnerEntity;
        public ushort EffectId;
        public AreaEffectTargetMask AffectedArchetypes;
        public FixedString64Bytes EffectName;
        public uint ExpirationTick;
    }

    /// <summary>
    /// Registry for culture/alignment/outlook state (villager loyalty, faction affinity, ideological shifts).
    /// </summary>
    public struct CultureAlignmentRegistry : IComponentData
    {
        public int CultureCount;
        public float GlobalAlignmentScore; // -1 to 1 aggregate
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Entry in the culture alignment registry following hot/cold split pattern.
    /// </summary>
    public struct CultureAlignmentRegistryEntry : IBufferElementData, IComparable<CultureAlignmentRegistryEntry>, IRegistryEntry
    {
        // Hot fields
        public Entity CultureEntity; // May be per-village or global
        public float3 RegionCenter;
        public int CellId;
        public uint SpatialVersion;
        public uint LastMutationTick;
        public float CurrentAlignment; // -1 (hostile) to 1 (loyal)
        public float AlignmentVelocity; // Rate of change per tick

        // Cold fields
        public ushort CultureId;
        public FixedString64Bytes CultureName;
        public CultureType CultureType; // Tribal, Religious, Political, etc.
        public int MemberCount; // Villagers/entities belonging to this culture
        public float BaseAlignment;
        public CultureAlignmentFlags AlignmentFlags; // Flags: Shifting, Stable, Volatile, etc.
        public FixedString128Bytes Description;

        // IRegistryEntry implementation
        public Entity RegistryEntity => CultureEntity;

        public int CompareTo(CultureAlignmentRegistryEntry other)
        {
            var entityCompare = CultureEntity.Index.CompareTo(other.CultureEntity.Index);
            if (entityCompare != 0) return entityCompare;
            return CultureEntity.Version.CompareTo(other.CultureEntity.Version);
        }
    }

    /// <summary>
    /// Component on culture entities identifying them for registry.
    /// </summary>
    public struct CultureState : IComponentData
    {
        public ushort CultureId;
        public FixedString64Bytes CultureName;
        public CultureType CultureType;
        public int MemberCount;
        public float CurrentAlignment;
        public float AlignmentVelocity;
        public float BaseAlignment;
        public CultureAlignmentFlags AlignmentFlags;
        public FixedString128Bytes Description;
    }
}
