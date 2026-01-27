using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Navigation
{
    /// <summary>
    /// Navigation domains - where movement occurs.
    /// </summary>
    public enum NavDomain : byte
    {
        Ground = 0,
        Air = 1,
        Water = 2,
        Underground = 3,
        Orbital = 4,      // Around bodies
        Interstellar = 5
    }

    /// <summary>
    /// Kind of navigation segment in a path.
    /// </summary>
    public enum NavSegmentKind : byte
    {
        MoveLocal = 0,      // Local grid move (walk, ride, fly)
        MoveRegion = 1,      // Region-to-region move
        UseTransport = 2     // Ferry, warp relay, portal, etc.
    }

    /// <summary>
    /// A single segment in a navigation path.
    /// </summary>
    public struct NavSegment : IBufferElementData
    {
        /// <summary>
        /// Kind of segment (local move, region move, or transport).
        /// </summary>
        public NavSegmentKind Kind;

        /// <summary>
        /// Domain for this segment.
        /// </summary>
        public NavDomain Domain;

        /// <summary>
        /// Transport entity (ferry, carrier, warp relay, etc.) or Entity.Null if not using transport.
        /// </summary>
        public Entity Transport;

        /// <summary>
        /// Source node ID (region/transit node index).
        /// </summary>
        public int FromNodeId;

        /// <summary>
        /// Destination node ID (region/transit node index).
        /// </summary>
        public int ToNodeId;

        /// <summary>
        /// Estimated travel time for this segment.
        /// </summary>
        public float EstimatedTime;

        /// <summary>
        /// Estimated fuel/logistics cost for this segment.
        /// </summary>
        public float EstimatedFuel;

        /// <summary>
        /// Estimated risk level (0-1) for this segment.
        /// </summary>
        public float EstimatedRisk;

        /// <summary>
        /// Start position for this segment.
        /// </summary>
        public float3 StartPosition;

        /// <summary>
        /// End position for this segment.
        /// </summary>
        public float3 EndPosition;
    }

    /// <summary>
    /// Navigation preference profile for an entity or group.
    /// Determines how pathfinding evaluates cost (time vs fuel vs risk).
    /// </summary>
    public struct NavPreference : IComponentData
    {
        /// <summary>
        /// How much the entity values speed (0-1).
        /// </summary>
        public float TimeWeight;

        /// <summary>
        /// How much the entity cares about fuel/logistics (0-1).
        /// </summary>
        public float FuelWeight;

        /// <summary>
        /// How risk-averse the entity is (0-1, higher = more risk-averse).
        /// </summary>
        public float RiskWeight;

        /// <summary>
        /// How much the entity protects high-value cargo (0-1).
        /// </summary>
        public float ValueProtection;

        /// <summary>
        /// Whether the entity can use paid routes (warpways, ports, etc.).
        /// </summary>
        public byte AllowsPaidRoutes;

        /// <summary>
        /// Creates a preference profile for a civilian caravan.
        /// High risk aversion, low time priority, cares about value protection.
        /// </summary>
        public static NavPreference CreateCivilianCaravan()
        {
            return new NavPreference
            {
                TimeWeight = 0.3f,
                FuelWeight = 0.4f,
                RiskWeight = 0.9f,
                ValueProtection = 0.8f,
                AllowsPaidRoutes = 1
            };
        }

        /// <summary>
        /// Creates a preference profile for a military courier.
        /// High time priority, moderate risk tolerance.
        /// </summary>
        public static NavPreference CreateMilitaryCourier()
        {
            return new NavPreference
            {
                TimeWeight = 0.9f,
                FuelWeight = 0.5f,
                RiskWeight = 0.4f,
                ValueProtection = 0.6f,
                AllowsPaidRoutes = 1
            };
        }

        /// <summary>
        /// Creates a preference profile for desperate raiders.
        /// Low risk aversion, high time priority, doesn't care about fuel.
        /// </summary>
        public static NavPreference CreateRaider()
        {
            return new NavPreference
            {
                TimeWeight = 0.95f,
                FuelWeight = 0.1f,
                RiskWeight = 0.2f,
                ValueProtection = 0.3f,
                AllowsPaidRoutes = 0
            };
        }

        /// <summary>
        /// Creates a default preference profile (balanced).
        /// </summary>
        public static NavPreference CreateDefault()
        {
            return new NavPreference
            {
                TimeWeight = 0.5f,
                FuelWeight = 0.5f,
                RiskWeight = 0.5f,
                ValueProtection = 0.5f,
                AllowsPaidRoutes = 1
            };
        }

        /// <summary>
        /// Calculates cost for a segment based on this preference profile.
        /// Cost = TimeWeight * TravelTime + FuelWeight * FuelCost + RiskWeight * Risk + ValueProtection * ValueRisk
        /// </summary>
        public float CalculateCost(in NavSegment segment, float valueRisk = 0f)
        {
            return TimeWeight * segment.EstimatedTime +
                   FuelWeight * segment.EstimatedFuel +
                   RiskWeight * segment.EstimatedRisk +
                   ValueProtection * valueRisk;
        }
    }
}






















