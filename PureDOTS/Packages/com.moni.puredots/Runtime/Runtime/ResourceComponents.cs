using System;
using PureDOTS.Runtime.Knowledge;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Resource;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    public struct ResourceTypeId : IComponentData
    {
        public FixedString64Bytes Value;
    }

    /// <summary>
    /// Classification for resource nodes (natural deposits, artificial sources, etc.).
    /// Defaults to <see cref="Default"/> when unspecified.
    /// </summary>
    public enum ResourceSourceType : byte
    {
        Default = 0,
        Natural = 1,
        Constructed = 2,
        Infinite = 3
    }

    public struct ResourceSourceConfig : IComponentData
    {
        public float GatherRatePerWorker;
        public int MaxSimultaneousWorkers;
        public float RespawnSeconds;
        public FixedString64Bytes LessonId;
        public byte Flags;

        public const byte FlagInfinite = 1 << 0;
        public const byte FlagRespawns = 1 << 1;
        public const byte FlagHandUprootAllowed = 1 << 2;
    }

    public struct ResourceSourceState : IComponentData
    {
        public ResourceSourceType SourceType;
        public float UnitsRemaining;
        public ResourceQualityTier QualityTier;
        public ushort BaseQuality;
        public ushort QualityVariance;
    }

    /// <summary>
    /// Lightweight summary for resource nodes used by perception/awareness systems.
    /// </summary>
    public struct ResourceNodeSummary : IComponentData
    {
        public ushort ResourceTypeIndex;
        public float UnitsRemaining;
        public byte IsDepleted;
    }

    public struct StorehouseConfig : IComponentData
    {
        public float ShredRate;
        public int MaxShredQueueSize;
        public float InputRate;
        public float OutputRate;
        public FixedString64Bytes Label;
    }

    public struct StorehouseInventory : IComponentData
    {
        public float TotalStored;
        public float TotalCapacity;
        public int ItemTypeCount;
        public byte IsShredding;
        public uint LastUpdateTick;
    }

    public struct StorehouseInventoryItem : IBufferElementData
    {
        public FixedString64Bytes ResourceTypeId;
        public float Amount;
        public float Reserved;
        public byte TierId;
        public ushort AverageQuality;
    }

    public struct ResourceChunkConfig : IComponentData
    {
        public float MassPerUnit;
        public float MinScale;
        public float MaxScale;
        public float DefaultUnits;
    }

    [System.Flags]
    public enum ResourceChunkFlags : byte
    {
        None = 0,
        Carried = 1 << 0,
        Thrown = 1 << 1,
        PendingDestroy = 1 << 2
    }

    public struct ResourceChunkState : IComponentData
    {
        public ushort ResourceTypeIndex;
        public float Units;
        public Entity SourceEntity;
        public Entity Carrier;
        public ResourceChunkFlags Flags;
        public float3 Velocity;
        public float Age;
        public ResourceQualityTier QualityTier;
        public ushort AverageQuality;
    }

    [System.Flags]
    public enum ResourceChunkSpawnFlags : byte
    {
        None = 0,
        AttachToRequester = 1 << 0,
        InheritVelocity = 1 << 1
    }

    public struct ResourceChunkSpawnCommand : IBufferElementData
    {
        public ushort ResourceTypeIndex;
        public float Units;
        public Entity Requester;
        public float3 SpawnPosition;
        public float3 LocalOffset;
        public float3 InitialVelocity;
        public ResourceChunkSpawnFlags Flags;
    }

    public struct ConstructionSiteProgress : IComponentData
    {
        public float RequiredProgress;
        public float CurrentProgress;
    }

    public struct ConstructionCostElement : IBufferElementData
    {
        public FixedString64Bytes ResourceTypeId;
        public float UnitsRequired;
    }

    public struct StorehouseCapacityElement : IBufferElementData
    {
        public FixedString64Bytes ResourceTypeId;
        public float MaxCapacity;
    }

    public struct ConstructionDeliveredElement : IBufferElementData
    {
        public FixedString64Bytes ResourceTypeId;
        public float UnitsDelivered;
    }

    public struct ConstructionSiteId : IComponentData
    {
        public int Value;
    }

    public struct ConstructionSiteFlags : IComponentData
    {
        public const byte Completed = 1 << 0;
        public const byte PartiallyUsable = 1 << 1;
        public byte Value;
    }

    public struct ConstructionCompletionPrefab : IComponentData
    {
        public Entity Prefab;
        public bool DestroySiteEntity;
    }

    public struct ConstructionCommandTag : IComponentData
    {
    }

    public struct ConstructionDepositCommand : IBufferElementData
    {
        public int SiteId;
        public FixedString64Bytes ResourceTypeId;
        public float Amount;
    }

    public struct ConstructionProgressCommand : IBufferElementData
    {
        public int SiteId;
        public float Delta;
    }

    public struct ConstructionIncidentCommand : IBufferElementData
    {
        public Entity Target;
        public Entity Source;
        public FixedString64Bytes CategoryId;
        public float Severity;
        public IncidentLearningKind Kind;
    }

    // Resource Registry Components
    public struct ResourceTypeIndex : IComponentData
    {
        public BlobAssetReference<ResourceTypeIndexBlob> Catalog;
    }

    public struct ResourceRegistry : IComponentData
    {
        public int TotalResources;
        public int TotalActiveResources;
        public uint LastUpdateTick;
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
    }

    public struct ResourceRegistryEntry : IBufferElementData, IComparable<ResourceRegistryEntry>, IRegistryEntry, IRegistryFlaggedEntry
    {
        public ushort ResourceTypeIndex;
        public Entity SourceEntity;
        public float3 Position;
        public float UnitsRemaining;
        public byte ActiveTickets;
        public byte ClaimFlags;
        public uint LastMutationTick;
        public int CellId;
        public uint SpatialVersion;
        public ushort FamilyIndex;
        public ResourceTier Tier;
        public ResourceQualityTier QualityTier;
        public ushort AverageQuality;
        public uint KnowledgeMask;

        public int CompareTo(ResourceRegistryEntry other)
        {
            return SourceEntity.Index.CompareTo(other.SourceEntity.Index);
        }

        public Entity RegistryEntity => SourceEntity;

        public byte RegistryFlags => ClaimFlags;
    }

    public struct StorehouseRegistry : IComponentData
    {
        public int TotalStorehouses;
        public float TotalCapacity;
        public float TotalStored;
        public uint LastUpdateTick;
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
    }

    public struct StorehouseRegistryCapacitySummary
    {
        public ushort ResourceTypeIndex;
        public float Capacity;
        public float Stored;
        public float Reserved;
        public byte TierId;
        public ushort AverageQuality;
    }

    public struct StorehouseRegistryEntry : IBufferElementData, IComparable<StorehouseRegistryEntry>, IRegistryEntry
    {
        public Entity StorehouseEntity;
        public float3 Position;
        public float TotalCapacity;
        public float TotalStored;
        public FixedList64Bytes<StorehouseRegistryCapacitySummary> TypeSummaries;
        public uint LastMutationTick;
        public int CellId;
        public uint SpatialVersion;
        public ResourceQualityTier DominantTier;
        public ushort AverageQuality;

        public int CompareTo(StorehouseRegistryEntry other)
        {
            return StorehouseEntity.Index.CompareTo(other.StorehouseEntity.Index);
        }

        public Entity RegistryEntity => StorehouseEntity;
    }

    public struct ConstructionRegistry : IComponentData
    {
        public int ActiveSiteCount;
        public int CompletedSiteCount;
        public uint LastUpdateTick;
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
    }

    public struct ConstructionRegistryEntry : IBufferElementData, IComparable<ConstructionRegistryEntry>, IRegistryEntry, IRegistryFlaggedEntry
    {
        public Entity SiteEntity;
        public int SiteId;
        public float3 Position;
        public float RequiredProgress;
        public float CurrentProgress;
        public float NormalizedProgress;
        public byte Flags;
        public int CellId;
        public uint SpatialVersion;

        public int CompareTo(ConstructionRegistryEntry other)
        {
            var siteComparison = SiteId.CompareTo(other.SiteId);
            if (siteComparison != 0)
            {
                return siteComparison;
            }

            return SiteEntity.Index.CompareTo(other.SiteEntity.Index);
        }

        public Entity RegistryEntity => SiteEntity;

        public byte RegistryFlags => Flags;
    }

    public struct SpawnerRegistry : IComponentData
    {
        public int TotalSpawners;
        public int ActiveSpawnerCount;
        public uint LastUpdateTick;
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
    }

    public struct SpawnerRegistryEntry : IBufferElementData, IComparable<SpawnerRegistryEntry>, IRegistryEntry, IRegistryFlaggedEntry
    {
        public Entity SpawnerEntity;
        public FixedString64Bytes SpawnerTypeId;
        public Entity OwnerFaction;
        public int ActiveSpawnCount;
        public int Capacity;
        public float CooldownSeconds;
        public float RemainingCooldown;
        public byte Flags;
        public float3 Position;
        public int CellId;
        public uint SpatialVersion;

        public int CompareTo(SpawnerRegistryEntry other)
        {
            return SpawnerEntity.Index.CompareTo(other.SpawnerEntity.Index);
        }

        public Entity RegistryEntity => SpawnerEntity;

        public byte RegistryFlags => Flags;
    }

    public struct SpawnerId : IComponentData
    {
        public int Value;
    }

    public struct SpawnerConfig : IComponentData
    {
        public FixedString64Bytes SpawnTypeId;
        public Entity OwnerFaction;
        public int Capacity;
        public float CooldownSeconds;
    }

    public struct SpawnerState : IComponentData
    {
        public int ActiveSpawnCount;
        public float RemainingCooldown;
        public byte Flags;
    }

    public static class SpawnerStatusFlags
    {
        public const byte Active = 1 << 0;
        public const byte Disabled = 1 << 1;
        public const byte Pending = 1 << 2;
    }

    public struct SpawnerTelemetry : IComponentData
    {
        public int TotalSpawners;
        public int ReadySpawners;
        public int CoolingSpawners;
        public int DisabledSpawners;
        public int SpawnAttempts;
        public int Spawned;
        public int SpawnFailures;
        public uint LastUpdateTick;
        public uint CatalogVersion;
    }

    public struct ResourceJobReservation : IComponentData
    {
        public byte ActiveTickets;
        public byte PendingTickets;
        public float ReservedUnits;
        public uint LastMutationTick;
        public byte ClaimFlags;
    }

    public struct ResourceActiveTicket : IBufferElementData
    {
        public Entity Villager;
        public uint TicketId;
        public float ReservedUnits;
    }

    public struct StorehouseJobReservation : IComponentData
    {
        public float ReservedCapacity;
        public uint LastMutationTick;
    }

    public struct StorehouseReservationItem : IBufferElementData
    {
        public ushort ResourceTypeIndex;
        public float Reserved;
    }

    public static class ResourceRegistryClaimFlags
    {
        public const byte PlayerClaim = 1 << 0;
        public const byte VillagerReserved = 1 << 1;
    }

    /// <summary>
    /// Generic inventory element for storing resources.
    /// Used by facilities, storage buildings, carriers, etc.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ResourceStack : IBufferElementData
    {
        public int ResourceTypeId;
        public float Amount;
    }

    /// <summary>
    /// Static class with hardcoded resource type IDs for initial scenarios.
    /// Can be replaced with data-driven catalog later.
    /// </summary>
    public static class ResourceIds
    {
        public const int Wood = 0;
        public const int Stone = 1;
        public const int Ore = 2;
        public const int Planks = 3;
        public const int Ingots = 4;
        public const int Food = 5;
        public const int RefinedOre = 6;
        public const int Fuel = 7;
        public const int Supplies = 8;
    }
}
