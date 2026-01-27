using System;
using PureDOTS.Runtime.Registry;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Transport
{
    /// <summary>
    /// Flags describing request state for registry consumers.
    /// </summary>
    [System.Flags]
    public enum LogisticsRequestFlags : byte
    {
        None = 0,
        Urgent = 1 << 0,
        Blocking = 1 << 1,
        PlayerPinned = 1 << 2
    }

    public enum LogisticsRequestPriority : byte
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Authoritative data describing a logistics/transport request.
    /// </summary>
    public struct LogisticsRequest : IComponentData
    {
        public Entity SourceEntity;
        public Entity DestinationEntity;
        public float3 SourcePosition;
        public float3 DestinationPosition;
        public ushort ResourceTypeIndex;
        public float RequestedUnits;
        public float FulfilledUnits;
        public LogisticsRequestPriority Priority;
        public LogisticsRequestFlags Flags;
        public uint CreatedTick;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Supplemental progress information for a logistics request.
    /// </summary>
    public struct LogisticsRequestProgress : IComponentData
    {
        public float AssignedUnits;
        public int AssignedTransportCount;
        public uint LastAssignmentTick;
    }

    /// <summary>
    /// Registry aggregate summarising current logistics requests.
    /// </summary>
    public struct LogisticsRequestRegistry : IComponentData
    {
        public int TotalRequests;
        public int PendingRequests;
        public int InProgressRequests;
        public int CriticalRequests;
        public float TotalRequestedUnits;
        public float TotalAssignedUnits;
        public float TotalRemainingUnits;
        public uint LastUpdateTick;
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
    }

    /// <summary>
    /// Registry entry representing an individual logistics request.
    /// </summary>
    public struct LogisticsRequestRegistryEntry :
        IBufferElementData,
        IComparable<LogisticsRequestRegistryEntry>,
        IRegistryEntry,
        IRegistryFlaggedEntry
    {
        public Entity RequestEntity;
        public Entity SourceEntity;
        public Entity DestinationEntity;
        public float3 SourcePosition;
        public float3 DestinationPosition;
        public int SourceCellId;
        public int DestinationCellId;
        public uint SpatialVersion;
        public ushort ResourceTypeIndex;
        public float RequestedUnits;
        public float AssignedUnits;
        public float RemainingUnits;
        public LogisticsRequestPriority Priority;
        public LogisticsRequestFlags Flags;
        public uint CreatedTick;
        public uint LastUpdateTick;

        public int CompareTo(LogisticsRequestRegistryEntry other)
        {
            return RequestEntity.Index.CompareTo(other.RequestEntity.Index);
        }

        public Entity RegistryEntity => RequestEntity;

        public byte RegistryFlags => (byte)Flags;
    }
}
