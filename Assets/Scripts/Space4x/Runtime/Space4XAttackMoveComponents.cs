using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    public enum AttackMoveSource : byte
    {
        Unknown = 0,
        AttackTerrain = 1,
        CtrlConvert = 2,
        MoveWhileEngaged = 3
    }

    public struct AttackMoveIntent : IComponentData
    {
        public float3 Destination;
        public float DestinationRadius;
        public Entity EngageTarget;
        public byte AcquireTargetsAlongRoute;
        public byte KeepFiringWhileInRange;
        public uint StartTick;
        public AttackMoveSource Source;
    }

    public struct AttackMoveOrigin : IComponentData
    {
        public byte WasPatrolling;
    }

    public struct AttackMoveSourceHint : IComponentData
    {
        public AttackMoveSource Source;
        public uint IssuedTick;
    }

    public struct VesselAimDirective : IComponentData
    {
        public float3 AimDirection;
        public float AimWeight;
        public Entity AimTarget;
    }
}
