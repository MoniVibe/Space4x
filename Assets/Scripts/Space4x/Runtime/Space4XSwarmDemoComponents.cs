using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    public enum Space4XSwarmDemoPhase : byte
    {
        Screen = 0,
        Tug = 1,
        Attack = 2,
        Return = 3
    }

    public struct Space4XSwarmDemoState : IComponentData
    {
        public Space4XSwarmDemoPhase Phase;
        public uint NextPhaseTick;
        public Entity AttackTarget;
        public float3 TugDirection;
    }
}
