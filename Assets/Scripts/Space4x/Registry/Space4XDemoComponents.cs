using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Resource types available in the mining demo.
    /// </summary>
    public enum ResourceType : byte
    {
        Minerals = 0,
        RareMetals = 1,
        EnergyCrystals = 2,
        OrganicMatter = 3,
        Ore = 4
    }

    /// <summary>
    /// Storage buffer for resources on carriers. Capacity is 10000 per type as documented.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ResourceStorage : IBufferElementData
    {
        public ResourceType Type;
        public float Amount;
        public float Capacity;

        public static ResourceStorage Create(ResourceType type, float capacity = 10000f)
        {
            return new ResourceStorage
            {
                Type = type,
                Amount = 0f,
                Capacity = capacity
            };
        }

        public float GetRemainingCapacity()
        {
            return math.max(0f, Capacity - Amount);
        }

        public bool CanStore(float amount)
        {
            return (Amount + amount) <= Capacity;
        }

        public float AddAmount(float amount)
        {
            var spaceAvailable = GetRemainingCapacity();
            var amountToAdd = math.min(amount, spaceAvailable);
            Amount += amountToAdd;
            return amount - amountToAdd;
        }
    }

    /// <summary>
    /// String identifier for a resource type (used by mining systems).
    /// </summary>
    public struct ResourceTypeId : IComponentData
    {
        public FixedString64Bytes Value;
    }

    /// <summary>
    /// Runtime state for a resource source (e.g., asteroid node).
    /// Tracks remaining units and last harvest tick.
    /// </summary>
    public struct ResourceSourceState : IComponentData
    {
        public float UnitsRemaining;
        public uint LastHarvestTick;
    }

    /// <summary>
    /// Configuration for how a resource source can be gathered from.
    /// </summary>
    public struct ResourceSourceConfig : IComponentData
    {
        public float GatherRatePerWorker;
        public ushort MaxSimultaneousWorkers;
        public float RespawnSeconds;
        public byte Flags;
    }

    /// <summary>
    /// Queues a resource spawn to be instantiated near a miner once a chunk threshold is reached.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct SpawnResourceRequest : IBufferElementData
    {
        public ResourceType Type;
        public float Amount;
        public float3 Position;
        public Entity SourceEntity;
        public uint RequestedTick;
    }

    /// <summary>
    /// Represents a spawned pickup on the map for carriers to collect.
    /// </summary>
    public struct SpawnResource : IComponentData
    {
        public ResourceType Type;
        public float Amount;
        public Entity SourceEntity;
        public uint SpawnTick;
    }

    /// <summary>
    /// Identifies a carrier ship that patrols and receives resources from mining vessels.
    /// </summary>
    public struct Carrier : IComponentData
    {
        public FixedString64Bytes CarrierId;
        public Entity AffiliationEntity;
        public float Speed;
        public float3 PatrolCenter;
        public float PatrolRadius;
    }

    /// <summary>
    /// Stores the hull type identifier for a carrier, used for refit eligibility checks.
    /// </summary>
    public struct CarrierHullId : IComponentData
    {
        public FixedString64Bytes HullId;
    }

    /// <summary>
    /// States for mining job execution.
    /// </summary>
    public enum MiningJobState : byte
    {
        None = 0,
        MovingToAsteroid = 1,
        Mining = 2,
        ReturningToCarrier = 3,
        TransferringResources = 4
    }

    /// <summary>
    /// Tracks the current mining job state and progress for a mining vessel.
    /// </summary>
    public struct MiningJob : IComponentData
    {
        public MiningJobState State;
        public Entity TargetAsteroid;
        public float MiningProgress;
    }

    /// <summary>
    /// Identifies an asteroid containing resources that can be mined.
    /// </summary>
    public struct Asteroid : IComponentData
    {
        public FixedString64Bytes AsteroidId;
        public ResourceType ResourceType;
        public float ResourceAmount;
        public float MaxResourceAmount;
        public float MiningRate;
    }

    /// <summary>
    /// Identifies a mining vessel that extracts resources from asteroids and delivers them to carriers.
    /// </summary>
    public struct MiningVessel : IComponentData
    {
        public FixedString64Bytes VesselId;
        public Entity CarrierEntity;
        public float MiningEfficiency;
        public float Speed;
        public float CargoCapacity;
        public float CurrentCargo;
        public ResourceType CargoResourceType;
    }

    /// <summary>
    /// Aggregated mining telemetry cached for presentation bindings.
    /// </summary>
    public struct Space4XMiningTelemetry : IComponentData
    {
        public float OreInHold;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Relation component for affiliation entities. Stores the affiliation name for lookup.
    /// </summary>
    public struct AffiliationRelation : IComponentData
    {
        public FixedString64Bytes AffiliationName;
    }

    /// <summary>
    /// Patrol behavior component that manages waypoint movement and waiting.
    /// </summary>
    public struct PatrolBehavior : IComponentData
    {
        public float3 CurrentWaypoint;
        public float WaitTime;
        public float WaitTimer;
    }

    /// <summary>
    /// Movement command component that specifies a target position and arrival threshold.
    /// </summary>
    public struct MovementCommand : IComponentData
    {
        public float3 TargetPosition;
        public float ArrivalThreshold;
    }
}















