// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Movement
{
    public struct MovementSpec : IComponentData
    {
        public float MaxSpeed;
        public float Acceleration;
    }

    public struct MovementIntent : IComponentData
    {
        public Entity Target;
        public byte Mode;
    }

    public struct MovementSolutionState : IComponentData
    {
        public byte Status;
        public uint LastUpdatedTick;
    }
}
