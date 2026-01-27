using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Tag component that marks an entity as participating in the rewind system.
    /// </summary>
    public struct RewindableTag : IComponentData { }

    /// <summary>
    /// Tag component that disables gameplay systems during rewind playback.
    /// </summary>
    public struct PlaybackGuardTag : IComponentData { }

    /// <summary>
    /// Defines which history tier this entity belongs to for snapshot frequency.
    /// </summary>
    public struct HistoryTier : IComponentData
    {
        public enum TierType : byte
        {
            Critical = 0,
            Default = 1,
            LowVisibility = 2
        }

        public TierType Tier;
        public float OverrideStrideSeconds;
    }

    /// <summary>
    /// Global history recording configuration. Seeded at startup for determinism.
    /// </summary>
    public struct HistorySettings : IComponentData
    {
        public float DefaultStrideSeconds;
        public float CriticalStrideSeconds;
        public float LowVisibilityStrideSeconds;
        public float DefaultHorizonSeconds;
        public float MidHorizonSeconds;
        public float ExtendedHorizonSeconds;
        public float CheckpointIntervalSeconds;
        public float EventLogRetentionSeconds;
        public float MemoryBudgetMegabytes;
        public float DefaultTicksPerSecond;
        public float MinTicksPerSecond;
        public float MaxTicksPerSecond;
        public float StrideScale;
        public bool EnableInputRecording;
        
        // Extended horizon configuration
        /// <summary>Global horizon in ticks (calculated from DefaultHorizonSeconds * DefaultTicksPerSecond).</summary>
        public uint GlobalHorizonTicks;
        /// <summary>Memory budget in bytes (calculated from MemoryBudgetMegabytes).</summary>
        public long MemoryBudgetBytes;
        /// <summary>Maximum memory per entity in bytes before pruning.</summary>
        public int MaxMemoryPerEntityBytes;
        /// <summary>Whether to enforce strict memory limits (prune aggressively).</summary>
        public bool EnforceStrictMemoryLimits;
        /// <summary>Snapshot interval in ticks for global world snapshots.</summary>
        public uint SnapshotIntervalTicks;
        /// <summary>Maximum number of global snapshots to retain.</summary>
        public int MaxGlobalSnapshots;
    }

    /// <summary>
    /// Per-archetype horizon configuration for fine-grained control.
    /// </summary>
    public struct ArchetypeHistoryHorizon : IBufferElementData
    {
        /// <summary>Archetype identifier (e.g., hash of component types).</summary>
        public FixedString32Bytes ArchetypeId;
        /// <summary>Custom horizon in ticks for this archetype.</summary>
        public uint HorizonTicks;
        /// <summary>Custom sampling frequency in ticks.</summary>
        public uint SamplingFrequencyTicks;
        /// <summary>Priority for memory allocation (higher = retained longer).</summary>
        public byte Priority;
    }

    /// <summary>
    /// Optional override for <see cref="HistorySettings"/> produced by authoring.
    /// </summary>
    public struct HistorySettingsConfig : IComponentData
    {
        public HistorySettings Value;
    }

    public static class HistorySettingsDefaults
    {
        public const float DefaultStrideSeconds = 5f;
        public const float CriticalStrideSeconds = 1f;
        public const float LowVisibilityStrideSeconds = 30f;
        public const float DefaultHorizonSeconds = 10f;
        public const float MidHorizonSeconds = 10f;
        public const float ExtendedHorizonSeconds = 10f;
        public const float CheckpointIntervalSeconds = 10f;
        public const float EventLogRetentionSeconds = 10f;
        public const float MemoryBudgetMegabytes = 2048f;
        public const float DefaultTicksPerSecond = 90f;
        public const float MinTicksPerSecond = 60f;
        public const float MaxTicksPerSecond = 120f;
        public const uint DefaultGlobalHorizonTicks = 900; // 10 seconds * 90 TPS
        public const int DefaultMaxMemoryPerEntityBytes = 1024 * 1024; // 1 MB per entity max
        public const uint DefaultSnapshotIntervalTicks = 30;
        public const int DefaultMaxGlobalSnapshots = 100;

        public static HistorySettings CreateDefault() => new HistorySettings
        {
            DefaultStrideSeconds = DefaultStrideSeconds,
            CriticalStrideSeconds = CriticalStrideSeconds,
            LowVisibilityStrideSeconds = LowVisibilityStrideSeconds,
            DefaultHorizonSeconds = DefaultHorizonSeconds,
            MidHorizonSeconds = MidHorizonSeconds,
            ExtendedHorizonSeconds = ExtendedHorizonSeconds,
            CheckpointIntervalSeconds = CheckpointIntervalSeconds,
            EventLogRetentionSeconds = EventLogRetentionSeconds,
            MemoryBudgetMegabytes = MemoryBudgetMegabytes,
            DefaultTicksPerSecond = DefaultTicksPerSecond,
            MinTicksPerSecond = MinTicksPerSecond,
            MaxTicksPerSecond = MaxTicksPerSecond,
            StrideScale = 1f,
            EnableInputRecording = true,
            GlobalHorizonTicks = DefaultGlobalHorizonTicks,
            MemoryBudgetBytes = (long)(MemoryBudgetMegabytes * 1024 * 1024),
            MaxMemoryPerEntityBytes = DefaultMaxMemoryPerEntityBytes,
            EnforceStrictMemoryLimits = false,
            SnapshotIntervalTicks = DefaultSnapshotIntervalTicks,
            MaxGlobalSnapshots = DefaultMaxGlobalSnapshots
        };

        /// <summary>
        /// Calculates horizon ticks from seconds and TPS.
        /// </summary>
        public static uint CalculateHorizonTicks(float horizonSeconds, float ticksPerSecond)
        {
            return (uint)(horizonSeconds * ticksPerSecond);
        }

        /// <summary>
        /// Calculates memory budget bytes from megabytes.
        /// </summary>
        public static long CalculateMemoryBudgetBytes(float megabytes)
        {
            return (long)(megabytes * 1024 * 1024);
        }
    }

    /// <summary>
    /// Generic history sample for storing component snapshots.
    /// </summary>
    public struct HistorySample<T> : IBufferElementData where T : unmanaged
    {
        public uint Tick;
        public T Value;
    }

    public struct PositionHistorySample : IBufferElementData
    {
        public uint Tick;
        public float3 Position;
        public quaternion Rotation;
    }

    /// <summary>
    /// Health history sample for rewind playback.
    /// </summary>
    public struct HealthHistorySample : IBufferElementData
    {
        public uint Tick;
        public float Health;
        public float MaxHealth;
    }

    public struct ResourceHistorySample : IBufferElementData
    {
        public uint Tick;
        public float UnitsRemaining;
        public byte Flags;
    }

    public struct VillagerHistorySample : IBufferElementData
    {
        public uint Tick;
        public float3 Position;
        public quaternion Rotation;
        public float Health;
        public float Hunger;
        public float Energy;
        public float Morale;
        public byte CurrentJobId;
        public byte StateFlags;
        public byte DisciplineId;
        public byte AvailabilityFlags;
        public float Mood;
        public float Wellbeing;
    }

    public struct VegetationHistorySample : IBufferElementData
    {
        public uint Tick;
        public float GrowthProgress;
        public float Scale;
        public byte LifecycleStage;
    }

    public struct ConstructionHistorySample : IBufferElementData
    {
        public uint Tick;
        public int SiteId;
        public float BuildProgress;
        public float RequiredProgress;
        public byte IsComplete;
        public byte WorkerCount;
        public uint LastUpdateTick;
    }

    public struct StorehouseHistorySample : IBufferElementData
    {
        public uint Tick;
        public int ShredQueueCount;
        public byte IsShredding;
        public float TotalCapacity;
        public uint LastDepositTick;
    }

    public struct PileHistorySample : IBufferElementData
    {
        public uint Tick;
        public int PileId;
        public float3 Position;
        public byte ResourceType;
        public float Amount;
        public byte SizeIndex;
        public byte IsMerging;
    }

    public struct HandHistorySample : IBufferElementData
    {
        public uint Tick;
        public float3 CursorWorldPosition;
        public Entity HeldObjectId;
        public byte HeldObjectType;
        public float SlingshotCharge;
        public float3 AimDirection;
        public byte HandState;
        public uint GrabStartTick;
    }

    public struct InteractionHistorySample : IBufferElementData
    {
        public uint Tick;
        public HandState HandState;
        public DivineHandCommandType Command;
        public ushort ResourceTypeIndex;
        public int HeldAmount;
        public byte Flags;
    }

    public struct GridHistorySample : IBufferElementData
    {
        public uint Tick;
        public uint TerrainVersion;
        public int CellSampleCount;
        public float AverageValue;
        public float MinValue;
        public float MaxValue;
    }

    public struct CombatHistorySample : IBufferElementData
    {
        public uint Tick;
        public Entity GroupId;
        public int FactionId;
        public int MemberCount;
        public byte Formation;
        public Entity CombatTarget;
        public byte EngagementState;
        public float MoraleLevel;
        public uint LastDamageTick;
    }

    public struct LastRecordedTick : IComponentData
    {
        public uint Tick;
    }

    public struct CheckpointMarker : IComponentData
    {
        public uint Tick;
        public float RealTimeStamp;
        public int EntityCount;
        public long MemoryUsageBytes;
    }

    public struct ReplayableEvent : IBufferElementData
    {
        public enum EventType : byte
        {
            Damage = 0,
            Impulse = 1,
            Spawn = 2,
            Destroy = 3,
            StateChange = 4,
            Custom = 255
        }

        public uint Tick;
        public EventType Type;
        public Entity SourceEntity;
        public Entity TargetEntity;
        public float3 Position;
        public float FloatParam;
    }
}
