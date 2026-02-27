using Unity.Entities;

namespace Space4X.Runtime
{
    public enum Space4XSimulationLifecycle : byte
    {
        Boot = 0,
        Ready = 1,
        Running = 2,
        Paused = 3,
        ShuttingDown = 4
    }

    public struct Space4XSimulationStateSpineRootTag : IComponentData
    {
    }

    public struct Space4XSimulationStateSpineMeta : IComponentData
    {
        public const uint CurrentSchemaVersion = 1u;
        public const uint CurrentRegistryVersion = 1u;

        public uint SchemaVersion;
        public uint RegistryVersion;
    }

    public struct Space4XSimulationStateSpineConfig : IComponentData
    {
        public byte Enabled;
        public byte RecordEventHistory;
        public ushort Reserved0;
        public uint MaxRetainedEvents;
        public uint MaxIntakePerTick;
        public uint MaxRetainedDeterminismProbes;
        public uint MaxRetainedTransitions;

        public static Space4XSimulationStateSpineConfig Default => new Space4XSimulationStateSpineConfig
        {
            Enabled = 1,
            RecordEventHistory = 1,
            Reserved0 = 0,
            MaxRetainedEvents = 4096u,
            MaxIntakePerTick = 256u,
            MaxRetainedDeterminismProbes = 2048u,
            MaxRetainedTransitions = 2048u
        };
    }

    public struct Space4XSimulationStateSpineState : IComponentData
    {
        public Space4XSimulationLifecycle Lifecycle;
        public byte DeterministicLane;
        public byte Mode;
        public ushort Reserved0;
        public uint LastTick;
        public uint ScenarioSeed;
        public uint ScenarioHash32;
        public uint LastEventSerial;
        public uint LastTransitionSerial;
    }

    public struct Space4XSimulationStateSpineOverflow : IComponentData
    {
        public uint DroppedThisTick;
        public uint DroppedTotal;
        public uint LastOverflowTick;
    }

    public enum Space4XSimulationStateTransitionKind : byte
    {
        Lifecycle = 1,
        Scenario = 2,
        Mode = 3
    }

    public enum Space4XSimulationStateTransitionReason : byte
    {
        Unknown = 0,
        BootAdvance = 1,
        PauseState = 2,
        ScenarioObserved = 3,
        ModeObserved = 4
    }

    public struct Space4XSimulationStateSpineTransitionGuard : IComponentData
    {
        public uint InvalidLifecycleTransitions;
        public uint InvalidScenarioTransitions;
        public uint InvalidModeTransitions;
        public uint LastInvalidTick;
        public uint BlockedScenarioSeed;
        public uint BlockedScenarioHash32;
        public uint BlockedMode;
    }

    public struct Space4XSimulationStateSpineDeterminism : IComponentData
    {
        public const uint CurrentVersion = 1u;

        public uint Version;
        public uint RunningDigest;
        public uint LastTickDigest;
        public uint LastTick;
        public uint TicksDigested;
        public uint EventsDigested;
    }

    [InternalBufferCapacity(64)]
    public struct Space4XSimulationStateSpineDeterminismProbe : IBufferElementData
    {
        public uint Tick;
        public uint TickDigest;
        public uint RunningDigest;
        public uint ConsumedEvents;
        public uint DroppedEvents;
        public uint ScenarioSeed;
        public uint ScenarioHash32;
    }

    [InternalBufferCapacity(64)]
    public struct Space4XSimulationStateSpineTransition : IBufferElementData
    {
        public uint Serial;
        public uint Tick;
        public Space4XSimulationStateTransitionKind Kind;
        public byte Valid;
        public Space4XSimulationStateTransitionReason Reason;
        public byte Reserved0;
        public uint FromValue;
        public uint ToValue;
        public uint Context0;
        public uint Context1;
    }

    [InternalBufferCapacity(64)]
    public struct Space4XSimulationStateSpineEvent : IBufferElementData
    {
        public uint Serial;
        public uint Tick;
        public uint DomainId;
        public uint FamilyId;
        public uint EventTypeId;
        public Entity Actor;
        public Entity Subject;
        public float Intensity;
        public float Quality;
        public uint ContextFlags;
    }

    [InternalBufferCapacity(32)]
    public struct Space4XSimulationStateSpineEventInbox : IBufferElementData
    {
        public uint DomainId;
        public uint FamilyId;
        public uint EventTypeId;
        public Entity Actor;
        public Entity Subject;
        public float Intensity;
        public float Quality;
        public uint ContextFlags;
    }
}
