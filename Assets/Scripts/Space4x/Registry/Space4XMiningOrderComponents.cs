using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

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
        Undocking = 1,
        ApproachTarget = 2,
        Latching = 3,
        Mining = 4,
        Detaching = 5,
        ReturnApproach = 6,
        Docking = 7
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
        public float PhaseTimer;
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

    /// <summary>
    /// Tracks the current mining target a carrier is scanning or moving toward.
    /// </summary>
    public struct CarrierMiningTarget : IComponentData
    {
        public Entity TargetEntity;
        public float3 TargetPosition;
        public uint AssignedTick;
        public uint NextScanTick;
    }
}
