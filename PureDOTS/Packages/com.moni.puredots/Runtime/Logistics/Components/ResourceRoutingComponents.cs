using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Components
{
    /// <summary>
    /// Route component.
    /// Represents a calculated path between two nodes.
    /// </summary>
    public struct Route : IComponentData
    {
        public int RouteId;
        public Entity SourceNode;
        public Entity DestinationNode;
        public RouteStatus Status;
        public float TotalDistance;
        public float TotalCost;
        public float EstimatedTransitTime;
        public uint CalculatedTick;
        public uint CacheVersion;
        public RouteCacheKey CacheKey;
    }

    public enum RouteStatus : byte
    {
        Calculating = 0,
        Valid = 1,
        Invalid = 2,
        Expired = 3
    }

    [InternalBufferCapacity(32)]
    public struct RouteEdge : IBufferElementData
    {
        public Entity SourceNode;
        public Entity DestinationNode;
        public float Distance;
        public float BaseCost;
        public float RiskCost;
        public float CongestionCost;
        public float TotalCost;
        public RouteEdgeState State;
    }

    public struct RouteEdgeState : IComponentData
    {
        public byte ControlFlags;  // Ownership, access restrictions
        public float HazardLevel;
        public float InterdictionLikelihood;
        public float CongestionMultiplier;
        public float SeasonalModifier;
        public uint LastUpdatedTick;
    }

    public struct RouteCacheKey : IComponentData
    {
        public Entity SourceNode;
        public Entity DestinationNode;
        public int BehaviorProfileId;
        public byte LegalityMask;
        public uint KnowledgeVersionId;
        public uint TopologyVersionId;
    }

    public enum RouteRerouteReason : byte
    {
        None = 0,
        HazardDetected = 1,
        NodeCompromised = 2,
        BorderClosed = 3,
        RouteBlocked = 4,
        IntelUpdate = 5,
        CostChanged = 6
    }

    /// <summary>
    /// Route profile for cost calculation.
    /// </summary>
    public struct RouteProfile : IComponentData
    {
        public float RiskTolerance;  // 0-1, higher = more risk accepted
        public float CostWeight;     // Weight for cost vs time
        public float TimeWeight;     // Weight for time vs cost
        public byte LegalityFlags;   // Required legality level
        public byte SecrecyFlags;    // Secrecy requirements
        public FixedList64Bytes<ServiceType> RequiredServices;
    }

    public enum RouteGraphMode : byte
    {
        Direct = 0,
        None = 1
    }

    public struct RouteGraphConfig : IComponentData
    {
        public RouteGraphMode Mode;
        public float DirectCost;
    }

    public struct RouteGraphResult
    {
        public byte HasRoute;
        public float Cost;
    }
}

