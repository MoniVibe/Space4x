using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Navigation
{
    /// <summary>
    /// Region node - represents a region/cluster in the world (village district, valley, sector).
    /// Used for hierarchical pathfinding to avoid full-grid A* for long-range paths.
    /// </summary>
    public struct RegionNode : IComponentData
    {
        /// <summary>
        /// Region node ID (unique identifier).
        /// </summary>
        public int RegionId;

        /// <summary>
        /// Center position of the region.
        /// </summary>
        public float3 Center;

        /// <summary>
        /// Bounds min of the region.
        /// </summary>
        public float3 BoundsMin;

        /// <summary>
        /// Bounds max of the region.
        /// </summary>
        public float3 BoundsMax;

        /// <summary>
        /// Biome type (for cost calculations).
        /// </summary>
        public byte BiomeType;

        /// <summary>
        /// Base cost to traverse this region.
        /// </summary>
        public float BaseCost;
    }

    /// <summary>
    /// Region edge - connectivity between regions.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct RegionEdge : IBufferElementData
    {
        /// <summary>
        /// Source region ID.
        /// </summary>
        public int FromRegionId;

        /// <summary>
        /// Destination region ID.
        /// </summary>
        public int ToRegionId;

        /// <summary>
        /// Cost to traverse this edge (distance + modifiers).
        /// </summary>
        public float Cost;

        /// <summary>
        /// Whether edge is bidirectional.
        /// </summary>
        public byte IsBidirectional;
    }

    /// <summary>
    /// Transit node - represents a transport hub (port, hyperway node, warp relay, gate).
    /// </summary>
    public struct TransitNode : IComponentData
    {
        /// <summary>
        /// Transit node ID (unique identifier).
        /// </summary>
        public int TransitId;

        /// <summary>
        /// Node position.
        /// </summary>
        public float3 Position;

        /// <summary>
        /// Type of transit node (port, hyperway, warp relay, gate, etc.).
        /// </summary>
        public TransitNodeType Type;

        /// <summary>
        /// Associated transport entity (ferry, warp relay entity, etc.) or Entity.Null.
        /// </summary>
        public Entity TransportEntity;

        /// <summary>
        /// Base cost to use this transit node.
        /// </summary>
        public float BaseCost;

        /// <summary>
        /// Whether node requires payment.
        /// </summary>
        public byte RequiresPayment;
    }

    /// <summary>
    /// Transit node type.
    /// </summary>
    public enum TransitNodeType : byte
    {
        Port = 0,
        HyperwayNode = 1,
        WarpRelay = 2,
        Gate = 3,
        FerryLanding = 4,
        RoadJunction = 5
    }

    /// <summary>
    /// Transit edge - transport route (ferry route, warp link, hyperway, road).
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct TransitEdge : IBufferElementData
    {
        /// <summary>
        /// Source transit node ID.
        /// </summary>
        public int FromTransitId;

        /// <summary>
        /// Destination transit node ID.
        /// </summary>
        public int ToTransitId;

        /// <summary>
        /// Cost to traverse this edge (time + fuel + risk).
        /// </summary>
        public float Cost;

        /// <summary>
        /// Estimated travel time.
        /// </summary>
        public float EstimatedTime;

        /// <summary>
        /// Estimated fuel/logistics cost.
        /// </summary>
        public float EstimatedFuel;

        /// <summary>
        /// Estimated risk level (0-1).
        /// </summary>
        public float EstimatedRisk;

        /// <summary>
        /// Transport entity (ferry, warp relay, etc.) or Entity.Null.
        /// </summary>
        public Entity TransportEntity;

        /// <summary>
        /// Whether edge is bidirectional.
        /// </summary>
        public byte IsBidirectional;

        /// <summary>
        /// Whether edge requires payment.
        /// </summary>
        public byte RequiresPayment;
    }

    /// <summary>
    /// Navigation graph hierarchy singleton - maintains references to LocalGrid, RegionGraph, TransitGraph.
    /// </summary>
    public struct NavGraphHierarchy : IComponentData
    {
        /// <summary>
        /// Entity containing the local grid graph (NavGraph with NavNode/NavEdge).
        /// </summary>
        public Entity LocalGridEntity;

        /// <summary>
        /// Entity containing the region graph (RegionNode/RegionEdge).
        /// </summary>
        public Entity RegionGraphEntity;

        /// <summary>
        /// Entity containing the transit graph (TransitNode/TransitEdge).
        /// </summary>
        public Entity TransitGraphEntity;

        /// <summary>
        /// Version number (incremented when graph structure changes).
        /// </summary>
        public uint Version;

        /// <summary>
        /// Number of region nodes.
        /// </summary>
        public int RegionNodeCount;

        /// <summary>
        /// Number of transit nodes.
        /// </summary>
        public int TransitNodeCount;
    }

    /// <summary>
    /// Dirty region ID - marks a region that needs graph rebuild.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct DirtyRegion : IBufferElementData
    {
        /// <summary>
        /// Region ID that is dirty.
        /// </summary>
        public int RegionId;

        /// <summary>
        /// Reason for dirty flag (road destroyed, siege, etc.).
        /// </summary>
        public byte Reason;
    }

    /// <summary>
    /// Dirty edge ID - marks an edge that needs graph rebuild.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct DirtyEdge : IBufferElementData
    {
        /// <summary>
        /// Edge ID or index that is dirty.
        /// </summary>
        public int EdgeId;

        /// <summary>
        /// Whether this is a region edge (true) or transit edge (false).
        /// </summary>
        public byte IsRegionEdge;

        /// <summary>
        /// Reason for dirty flag.
        /// </summary>
        public byte Reason;
    }
}

