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
        public const float DefaultHorizonSeconds = 60f;
        public const float MidHorizonSeconds = 300f;
        public const float ExtendedHorizonSeconds = 600f;
        public const float CheckpointIntervalSeconds = 20f;
        public const float EventLogRetentionSeconds = 30f;
        public const float MemoryBudgetMegabytes = 2048f;
        public const float DefaultTicksPerSecond = 90f;
        public const float MinTicksPerSecond = 60f;
        public const float MaxTicksPerSecond = 120f;

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
            StrideScale = 1f
        };
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
