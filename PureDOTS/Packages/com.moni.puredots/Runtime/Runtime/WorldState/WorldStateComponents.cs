using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldState
{
    /// <summary>
    /// World state types.
    /// </summary>
    public enum WorldStateType : byte
    {
        Normal = 0,
        DemonHellscape = 1,
        UndeadWasteland = 2,
        FrozenEternity = 3,
        VerdantOvergrowth = 4,
        CelestialDominion = 5,
        VoidCorruption = 6
    }

    /// <summary>
    /// Current world state.
    /// </summary>
    public struct WorldState : IComponentData
    {
        public WorldStateType CurrentState;
        public WorldStateType PreviousState;
        public uint StateChangedTick;
        public float TransitionProgress;     // 0-1 during transitions
        public Entity DominantFaction;       // Who caused this state
    }

    /// <summary>
    /// Apocalypse trigger conditions.
    /// </summary>
    public struct ApocalypseTrigger : IComponentData
    {
        public Entity FactionEntity;         // World boss faction
        public float TerritoryControl;       // 0-1 percent of world
        public uint CivilizationsDestroyed;
        public uint UnchallengeTicks;        // How long unchallenged
        public byte TriggerReady;            // Met victory conditions
    }

    /// <summary>
    /// Apocalypse trigger configuration.
    /// </summary>
    public struct ApocalypseTriggerConfig : IComponentData
    {
        public float TerritoryThreshold;     // 0.8 = 80% control
        public uint UnchallengeDuration;     // Ticks to wait
        public byte RequireAllCivilizationsDestroyed;
    }

    /// <summary>
    /// Biome transformation in progress.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct BiomeTransformation : IBufferElementData
    {
        public Entity BiomeEntity;
        public byte OriginalBiomeType;
        public byte TargetBiomeType;
        public float TransformProgress;      // 0-1
        public uint StartTick;
    }

    /// <summary>
    /// Transformation wave spreading from epicenter.
    /// </summary>
    public struct TransformationWave : IComponentData
    {
        public float3 EpicenterPosition;
        public float CurrentRadius;
        public float ExpansionRate;          // Units per tick
        public float MaxRadius;
        public WorldStateType TransformTo;
    }

    /// <summary>
    /// Counter-apocalypse spawn conditions.
    /// </summary>
    public struct CounterApocalypseSpawn : IComponentData
    {
        public WorldStateType ApocalypseType;
        public FixedString32Bytes CounterFaction; // "Angels" vs demons
        public float SpawnProbability;       // Per check interval
        public uint MinTicksBeforeSpawn;     // Grace period
        public byte HasSpawned;
    }

    /// <summary>
    /// Spawn table override for world state.
    /// </summary>
    public struct SpawnTableOverride : IComponentData
    {
        public WorldStateType ForState;
        public FixedString64Bytes SpawnTableId; // Different creatures/resources
        public byte OverrideActive;
    }

    /// <summary>
    /// World transformation configuration.
    /// </summary>
    public struct WorldTransformConfig : IComponentData
    {
        public uint BiomeTransformDuration;  // Ticks per biome
        public float WaveExpansionRate;
        public uint CounterSpawnCheckInterval;
        public byte AllowRebirthCycles;      // Can world return to normal?
    }

    /// <summary>
    /// World state event.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct WorldStateEvent : IBufferElementData
    {
        public WorldStateType FromState;
        public WorldStateType ToState;
        public uint EventTick;
        public Entity CausingEntity;
        public FixedString64Bytes EventDescription;
    }
}

