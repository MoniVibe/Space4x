using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Hand
{
    /// <summary>
    /// Command types emitted by state machine and consumed by verb systems.
    /// </summary>
    public enum HandCommandType : byte
    {
        None,
        Pick,
        Hold,
        Throw,
        SlingshotThrow,  // Charge-scaled throw
        QueueThrow,
        Siphon,
        Dump,
        CastMiracle
    }

    /// <summary>
    /// Command buffer element emitted by state machine and consumed by verb systems.
    /// Commands are tick-stamped to prevent double-execution during replay.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct HandCommand : IBufferElementData
    {
        public uint Tick;  // Tick when command was issued (for determinism/replay)
        public HandCommandType Type;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public float3 Direction;  // For throw/slingshot
        public float Speed;  // For throw/slingshot
        public float ChargeLevel;  // 0..1 for slingshot
        public ushort ResourceTypeIndex;  // For siphon/dump
        public float Amount;  // For siphon/dump
    }
}

