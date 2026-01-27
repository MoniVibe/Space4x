using System;
using PureDOTS.Runtime.Registry;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Resource
{
    public struct ResourceProcessorConfig : IComponentData
    {
        public FixedString32Bytes FacilityTag;
        public byte AutoRun;
    }

    public struct ResourceProcessorState : IComponentData
    {
        public FixedString64Bytes RecipeId;
        public FixedString64Bytes OutputResourceId;
        public ResourceRecipeKind Kind;
        public int OutputAmount;
        public float RemainingSeconds;
    }

    public struct ResourceProcessorQueue : IBufferElementData
    {
        public FixedString64Bytes RecipeId;
        public int Repeat;
    }

    public struct ProcessingStationRegistry : IComponentData
    {
        public int TotalStations;
        public int ActiveStations;
        public uint LastUpdateTick;
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
    }

    public struct ProcessingStationRegistryEntry : IBufferElementData, IComparable<ProcessingStationRegistryEntry>, IRegistryEntry
    {
        public Entity StationEntity;
        public FixedString64Bytes StationTypeId;
        public FixedList64Bytes<ushort> AcceptedResourceTypes;
        public byte QueueDepth;
        public byte ActiveJobs;
        public float AverageProcessSeconds;
        public byte SkillBias;
        public ResourceQualityTier TierUpgradeHint;
        public uint LastMutationTick;
        public int CellId;
        public uint SpatialVersion;

        public int CompareTo(ProcessingStationRegistryEntry other)
        {
            return StationEntity.Index.CompareTo(other.StationEntity.Index);
        }

        public Entity RegistryEntity => StationEntity;
    }
}

