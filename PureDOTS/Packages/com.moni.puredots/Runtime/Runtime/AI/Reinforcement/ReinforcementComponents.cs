using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI.Reinforcement
{
    /// <summary>
    /// Timing pattern for arrivals.
    /// </summary>
    public enum ArrivalPattern : byte
    {
        Simultaneous = 0,    // All arrive at once
        Staggered = 1,       // Sequential with delays
        Wave = 2,            // Groups arrive in waves
        Random = 3           // Random timing within window
    }

    /// <summary>
    /// How units position relative to rally point.
    /// </summary>
    public enum ArrivalFormation : byte
    {
        Scatter = 0,         // Random positions in radius
        Circle = 1,          // Arranged in circle
        Line = 2,            // Arranged in line facing target
        Wedge = 3,           // V-formation
        Flanking = 4         // Split to sides of target
    }

    /// <summary>
    /// Arrival timing configuration.
    /// </summary>
    public struct ArrivalTiming : IComponentData
    {
        public ArrivalPattern Pattern;
        public float BaseDelay;            // Base time before arrival
        public float DelayVariance;        // Random variance +/-
        public float WaveInterval;         // Time between waves
        public byte WaveCount;             // Number of waves
        public uint ScheduledTick;         // When arrival was scheduled
    }

    /// <summary>
    /// Positional precision for arrival.
    /// </summary>
    public struct ArrivalPrecision : IComponentData
    {
        public float BaseScatter;          // Base scatter radius
        public float PrecisionModifier;    // 0-1, higher = tighter grouping
        public float MaxScatter;           // Maximum scatter limit
        public float MinDistance;          // Minimum distance from rally point
        public float PreferredDistance;    // Ideal distance from rally point
        public uint Seed;                  // Random seed for reproducible scatter
    }

    /// <summary>
    /// Target location for arriving units.
    /// </summary>
    public struct RallyPoint : IComponentData
    {
        public float3 Position;
        public float3 FacingDirection;     // Direction to face on arrival
        public Entity TargetEntity;        // Optional entity to rally near
        public float Radius;               // Radius of rally area
        public byte IsActive;
        public uint CreatedTick;
    }

    /// <summary>
    /// Group of units arriving together.
    /// </summary>
    public struct ArrivalGroup : IComponentData
    {
        public Entity LeaderEntity;        // First to arrive / commander
        public ushort GroupSize;           // Total units in group
        public ushort ArrivedCount;        // Units that have arrived
        public ArrivalFormation Formation;
        public float FormationSpacing;     // Distance between units
        public uint ArrivalTick;           // When group arrives
        public byte IsComplete;            // All units have arrived
    }

    /// <summary>
    /// Per-unit arrival state.
    /// </summary>
    public struct ArrivalState : IComponentData
    {
        public Entity GroupEntity;         // Which arrival group this belongs to
        public float3 AssignedPosition;    // Where this unit should arrive
        public byte SlotIndex;             // Position in formation
        public float ArrivalDelay;         // Individual delay offset
        public byte HasArrived;
        public uint ArrivedTick;
    }

    /// <summary>
    /// Request to find optimal rally point.
    /// </summary>
    public struct RallyPointRequest : IComponentData
    {
        public float3 FriendlyCentroid;    // Center of friendly forces
        public float3 EnemyCentroid;       // Center of enemy forces
        public float3 ObjectivePosition;   // What we're trying to reach/defend
        public float PreferredDistance;    // Distance from objective
        public byte AvoidEnemies;          // Stay away from enemies
        public byte FlankObjective;        // Try to arrive on flank
    }

    /// <summary>
    /// Reinforcement request.
    /// </summary>
    public struct ReinforcementRequest : IComponentData
    {
        public Entity RequestingEntity;
        public float3 RequestPosition;
        public ushort UnitsRequested;
        public float Urgency;
        public uint RequestTick;
        public byte IsFulfilled;
    }
}

