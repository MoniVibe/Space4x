using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Logistics
{
    /// <summary>
    /// Caravan/convoy states.
    /// </summary>
    public enum CaravanState : byte
    {
        Idle = 0,
        Loading = 1,
        Traveling = 2,
        Unloading = 3,
        Returning = 4,
        UnderAttack = 5,
        Destroyed = 6
    }

    /// <summary>
    /// Ambush outcome types.
    /// </summary>
    public enum AmbushOutcome : byte
    {
        Pending = 0,
        CaravanEscaped = 1,
        CaravanDefended = 2,
        CargoStolen = 3,
        CaravanDestroyed = 4
    }

    /// <summary>
    /// Trade route between locations.
    /// </summary>
    public struct TradeRoute : IComponentData
    {
        public Entity SourceLocation;        // Village, colony
        public Entity DestinationLocation;
        public float Distance;               // In world units
        public float TravelTime;             // Ticks to traverse
        public float TransportCostPerUnit;   // Currency per unit
        public byte RouteQuality;            // 0-3 (Uncharted→Major)
        public uint TotalTrips;              // Trips completed
        public float SecurityRating;         // 0-1, higher = safer
    }

    /// <summary>
    /// Caravan/convoy entity.
    /// </summary>
    public struct Caravan : IComponentData
    {
        public Entity HomeBase;
        public Entity CurrentRoute;          // Active trade route
        public float3 CurrentPosition;
        public float Progress;               // 0-1 along route
        public CaravanState State;
        public float CargoCapacity;          // Max weight
        public float CurrentCargoWeight;
        public byte GuardCount;              // Escorts
        public float Speed;                  // Modifies travel time
    }

    /// <summary>
    /// Cargo manifest entry.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct CargoManifest : IBufferElementData
    {
        public FixedString32Bytes ResourceType;
        public float Quantity;
        public float PurchasePrice;          // For profit calculation
        public Entity DestinationStorage;    // Where to deliver
    }

    /// <summary>
    /// Route infrastructure.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct RouteInfrastructure : IBufferElementData
    {
        public Entity RouteEntity;
        public FixedString32Bytes InfraType; // "road", "bridge", "inn", "guard_post"
        public float3 Position;
        public float Condition;              // 0-1, degrades over time
        public float EffectRadius;
        public float BenefitModifier;        // Cost reduction, safety bonus
    }

    /// <summary>
    /// Ambush/interdiction event.
    /// </summary>
    public struct AmbushEvent : IComponentData
    {
        public Entity TargetCaravan;
        public Entity AttackerEntity;        // Bandit camp, pirate fleet
        public float AmbushStrength;
        public float DefenseStrength;
        public byte IsResolved;
        public AmbushOutcome Outcome;
    }

    /// <summary>
    /// Logistics configuration.
    /// </summary>
    public struct LogisticsConfig : IComponentData
    {
        public uint RouteQualityThreshold1;  // Trips for "Developing"
        public uint RouteQualityThreshold2;  // Trips for "Established"
        public uint RouteQualityThreshold3;  // Trips for "Major"
        public float BaseAmbushChance;
        public float GuardEffectiveness;     // Per guard reduction in ambush
        public float InfrastructureDecayRate;
    }

    /// <summary>
    /// Caravan trip record.
    /// </summary>
    public struct TripRecord : IComponentData
    {
        public Entity RouteEntity;
        public uint StartTick;
        public uint EndTick;
        public float CargoValue;
        public float Profit;
        public byte WasAttacked;
        public byte WasSuccessful;
    }
}

