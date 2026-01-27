using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Input
{
    /// <summary>
    /// Abstract order kinds that games interpret.
    /// </summary>
    public enum OrderKind : byte
    {
        Move = 0,
        Attack = 1,
        Harvest = 2,
        Defend = 3,
        Patrol = 4,
        UseAbility = 5,
        Interact = 6
    }

    /// <summary>
    /// Optional order flags for extended behavior.
    /// </summary>
    public enum OrderFlags : byte
    {
        None = 0,
        AttackMove = 1 << 0
    }

    /// <summary>
    /// Abstract order structure.
    /// </summary>
    public struct Order
    {
        public OrderKind Kind;
        public float3 TargetPosition;
        public Entity TargetEntity;  // Entity.Null if none
        public byte Flags;            // Queue, force, etc. (bit flags)
    }

    /// <summary>
    /// Order queue buffer element per unit/group.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct OrderQueueElement : IBufferElementData
    {
        public Order Order;
    }
}





















