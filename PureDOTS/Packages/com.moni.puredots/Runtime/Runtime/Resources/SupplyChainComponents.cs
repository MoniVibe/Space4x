using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Resources
{
    /// <summary>
    /// Per-resource consumption rate.
    /// </summary>
    public struct ConsumptionRate
    {
        public ushort ResourceTypeId;
        public float BaseRate;             // Units consumed per tick
        public float CurrentRate;          // After modifiers
        public float Efficiency;           // 0-1, higher = less waste
    }

    /// <summary>
    /// Current supply status for an entity or group.
    /// </summary>
    public struct SupplyStatus : IComponentData
    {
        public float TotalSupply;          // Current stockpile
        public float MaxCapacity;          // Storage limit
        public float TotalConsumption;     // Sum of all consumption rates
        public float NetFlow;              // Income - consumption
        public float DaysRemaining;        // Estimated time until empty
        public byte IsInDeficit;           // Consuming more than receiving
        public byte IsEmergency;           // Below emergency threshold
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Buffer of consumption rates by resource type.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ConsumptionRateEntry : IBufferElementData
    {
        public ConsumptionRate Rate;
    }

    /// <summary>
    /// Supply source definition.
    /// </summary>
    public struct SupplySource : IComponentData
    {
        public Entity SourceEntity;        // Where supplies come from
        public ushort ResourceTypeId;
        public float ProductionRate;       // Units produced per tick
        public float CurrentStock;         // Available at source
        public float MaxStock;
        public float ReserveRatio;         // Minimum % to keep in reserve
        public byte IsAvailable;           // Can currently supply
    }

    /// <summary>
    /// Route between supply source and consumer.
    /// </summary>
    public struct SupplyRoute : IComponentData
    {
        public Entity SourceEntity;
        public Entity DestinationEntity;
        public ushort ResourceTypeId;
        public float Distance;             // Travel distance
        public float TransportCapacity;    // Units per trip
        public float TravelTime;           // Ticks per round trip
        public float RiskFactor;           // Chance of loss (0-1)
        public float Efficiency;           // Calculated efficiency score
        public byte IsActive;
    }

    /// <summary>
    /// Emergency supply situation.
    /// </summary>
    public struct EmergencySupplyState : IComponentData
    {
        public ushort ResourceTypeId;
        public float CriticalThreshold;    // % below which is emergency
        public float CurrentRatio;         // Current supply / consumption
        public byte IsEmergency;
        public byte IsForaging;            // Emergency gathering active
        public uint EmergencyStartTick;
    }

    /// <summary>
    /// Supply chain configuration.
    /// </summary>
    public struct SupplyChainConfig : IComponentData
    {
        public float EmergencyThreshold;   // Days remaining to trigger emergency
        public float WarningThreshold;     // Days remaining for warning
        public float ReserveRatio;         // Target reserve %
        public float MaxRouteDistance;     // Maximum viable supply route
        public float EfficiencyMinimum;    // Minimum route efficiency to use
    }

    /// <summary>
    /// Supply delivery in progress.
    /// </summary>
    public struct SupplyDelivery : IComponentData
    {
        public Entity SourceEntity;
        public Entity DestinationEntity;
        public ushort ResourceTypeId;
        public float Quantity;
        public float Progress;             // 0-1 along route
        public uint StartTick;
        public uint ArrivalTick;
    }

    /// <summary>
    /// Request for supplies.
    /// </summary>
    public struct SupplyRequest : IComponentData
    {
        public Entity RequestingEntity;
        public ushort ResourceTypeId;
        public float QuantityNeeded;
        public float Urgency;              // 0-1
        public uint RequestTick;
        public byte IsFulfilled;
    }
}

