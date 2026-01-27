using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Trade
{
    /// <summary>
    /// Transport mode enum.
    /// </summary>
    public enum TransportMode : byte
    {
        Caravan = 0,
        Stagecoach = 1,
        Ship = 2,
        Airship = 3
    }

    /// <summary>
    /// Terrain type enum.
    /// </summary>
    public enum TerrainType : byte
    {
        Road = 0,
        Trail = 1,
        Rough = 2,
        Mountain = 3,
        Water = 4,
        Mixed = 5
    }

    /// <summary>
    /// Trade route template component.
    /// Static route data: nodes, distance, terrain, risk.
    /// </summary>
    public struct TradeRouteTemplate : IComponentData
    {
        public FixedString64Bytes RouteId;
        public Entity NodeA;
        public Entity NodeB;
        public float Distance;
        public TerrainType TerrainType;
        public float TerrainDifficulty;
    }

    /// <summary>
    /// Trade line component.
    /// Active route instance with schedule, owner, cargo priority.
    /// </summary>
    public struct TradeLine : IComponentData
    {
        public FixedString64Bytes RouteId;
        public Entity Owner;
        public float Frequency; // Trips per month
        public uint LastTripTick;
        public uint NextTripTick;
    }

    /// <summary>
    /// Transport entity component.
    /// Caravan/ship with inventory, capacity, speed, owner wallet, route assignment.
    /// </summary>
    public struct TransportEntity : IComponentData
    {
        public TransportMode Mode;
        public Entity OwnerWallet;
        public FixedString64Bytes AssignedRouteId;
        public float Speed;
        public float Capacity;
        public Entity CurrentNode;
        public Entity DestinationNode;
    }

    /// <summary>
    /// Transport progress component.
    /// Current node, leg progress, cargo manifest.
    /// </summary>
    public struct TransportProgress : IComponentData
    {
        public Entity CurrentNode;
        public float LegProgress; // 0-1, progress along current leg
        public float DistanceTraveled;
        public uint DepartureTick;
        public uint EstimatedArrivalTick;
    }
}

