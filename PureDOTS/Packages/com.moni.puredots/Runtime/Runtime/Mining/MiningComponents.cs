using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Mining
{
    /// <summary>
    /// Tag component marking entities that can mine (villagers, miners, vessels, etc.).
    /// </summary>
    public struct MinerTag : IComponentData { }

    /// <summary>
    /// Component on mineable sources (asteroids, rocks, ore veins, trees, etc.).
    /// Wraps and clarifies ResourceSourceState for mining operations.
    /// </summary>
    public struct MineableSource : IComponentData
    {
        /// <summary>
        /// Current amount of resources remaining in the source.
        /// </summary>
        public float CurrentAmount;

        /// <summary>
        /// Maximum capacity of the source.
        /// </summary>
        public float MaxAmount;

        /// <summary>
        /// Extraction rate per tick or per second (depending on system implementation).
        /// </summary>
        public float ExtractionRate;
    }

    /// <summary>
    /// Active mining session tracking when a miner is engaged with a source.
    /// </summary>
    public struct MiningSession : IComponentData
    {
        /// <summary>
        /// The MineableSource entity being mined.
        /// </summary>
        public Entity Source;

        /// <summary>
        /// Where to deliver resources (carrier/storehouse), may be Entity.Null if not yet assigned.
        /// </summary>
        public Entity Carrier;

        /// <summary>
        /// Accumulated resources not yet "banked" or delivered.
        /// </summary>
        public float Accumulated;

        /// <summary>
        /// How much this miner can carry per trip.
        /// </summary>
        public float Capacity;

        /// <summary>
        /// Tick when the session started (for diagnostics and timeout handling).
        /// </summary>
        public uint StartTick;
    }

    /// <summary>
    /// Marker component indicating an entity is currently carrying resources in flight.
    /// </summary>
    public struct CarryingResource : IComponentData
    {
        /// <summary>
        /// Resource type identifier (Entity reference, hash, or enum index).
        /// </summary>
        public Entity ResourceType;

        /// <summary>
        /// Amount of resources being carried.
        /// </summary>
        public float Amount;
    }

    /// <summary>
    /// Component on carriers/storehouses indicating what resources they can accept.
    /// </summary>
    public struct ResourceSink : IComponentData
    {
        /// <summary>
        /// Maximum capacity of the sink.
        /// </summary>
        public float Capacity;

        /// <summary>
        /// Current amount stored in the sink.
        /// </summary>
        public float CurrentAmount;
    }

    /// <summary>
    /// Tag component marking mining-specific jobs in villager/AI job systems.
    /// </summary>
    public struct MiningJobTag : IComponentData { }

    /// <summary>
    /// Explicit state tracking for mining operations.
    /// Used for diagnostics and state machine clarity.
    /// </summary>
    public enum MiningState : byte
    {
        /// <summary>
        /// No active mining session. Miner is idle or searching for a source.
        /// </summary>
        Idle = 0,

        /// <summary>
        /// Moving towards the mineable source.
        /// </summary>
        GoingToSource = 1,

        /// <summary>
        /// Actively mining/extracting resources from the source.
        /// </summary>
        Mining = 2,

        /// <summary>
        /// Returning to carrier/storehouse to deliver resources.
        /// </summary>
        ReturningToCarrier = 3,

        /// <summary>
        /// Delivering resources to the carrier/storehouse.
        /// </summary>
        Delivering = 4
    }

    /// <summary>
    /// Component storing the current mining state for diagnostics and state machine logic.
    /// </summary>
    public struct MiningStateComponent : IComponentData
    {
        /// <summary>
        /// Current state of the mining operation.
        /// </summary>
        public MiningState State;

        /// <summary>
        /// Tick when the state last changed (for diagnostics).
        /// </summary>
        public uint LastStateChangeTick;
    }
}

























