using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    public enum MiningOrderSource : byte
    {
        None = 0,
        Scripted = 1,
        Input = 2
    }

    public enum MiningOrderStatus : byte
    {
        None = 0,
        Pending = 1,
        Active = 2,
        Completed = 3
    }

    /// <summary>
    /// Logical mining order assigned to a vessel. Uses registry-friendly resource identifiers.
    /// </summary>
    public struct MiningOrder : IComponentData
    {
        public FixedString64Bytes ResourceId;
        public MiningOrderSource Source;
        public MiningOrderStatus Status;
        public Entity PreferredTarget;
        public Entity TargetEntity;
        public uint IssuedTick;
    }

    public enum MiningPhase : byte
    {
        Idle = 0,
        Seeking = 1,
        MovingToTarget = 2,
        Mining = 3,
        AwaitingOutput = 4
    }

    /// <summary>
    /// Tracks the active mining state and cadence for a miner vessel.
    /// </summary>
    public struct MiningState : IComponentData
    {
        public MiningPhase Phase;
        public Entity ActiveTarget;
        public float MiningTimer;
        public float TickInterval;
    }

    /// <summary>
    /// Accumulates mined output and exposes a spawn trigger for downstream systems.
    /// </summary>
    public struct MiningYield : IComponentData
    {
        public FixedString64Bytes ResourceId;
        public float PendingAmount;
        public float SpawnThreshold;
        public byte SpawnReady;
    }
}
