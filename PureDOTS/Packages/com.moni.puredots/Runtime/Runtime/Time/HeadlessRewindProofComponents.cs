using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    [System.Flags]
    public enum HeadlessRewindProofStage : byte
    {
        None = 0,
        Playback = 1 << 0,
        CatchUp = 1 << 1,
        RecordReturn = 1 << 2
    }

    public enum HeadlessRewindProofPhase : byte
    {
        Idle = 0,
        Requested = 1,
        Playback = 2,
        CatchUp = 3,
        Record = 4,
        Completed = 5
    }

    public struct HeadlessRewindProofConfig : IComponentData
    {
        public byte Enabled;
        public uint TriggerTick;
        public uint TicksBack;
        public uint TimeoutTicks;
        public byte RequireGuardViolationsClear;
    }

    public struct HeadlessRewindProofState : IComponentData
    {
        public HeadlessRewindProofPhase Phase;
        public byte Result;
        public byte SawPlayback;
        public byte SawCatchUp;
        public byte SawRecord;
        public uint StartTick;
        public uint TargetTick;
        public uint PlaybackEnterTick;
        public uint CatchUpEnterTick;
        public uint RecordReturnTick;
        public uint DeadlineTick;
        public int GuardViolationCount;
    }

    public struct HeadlessRewindProofSubject : IBufferElementData
    {
        public FixedString64Bytes ProofId;
        public byte RequiredMask;
        public byte ObservedMask;
        public byte Result;
        public float Observed;
        public FixedString32Bytes Expected;
    }
}
