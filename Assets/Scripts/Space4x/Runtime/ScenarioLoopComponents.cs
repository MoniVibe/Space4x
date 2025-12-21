using Unity.Entities;

namespace Space4X.Runtime
{
    public struct ScenarioSide : IComponentData
    {
        public byte Side;
    }

    public struct EscortAssignment : IComponentData
    {
        public Entity Target;
        public uint AssignedTick;
        public uint ReleaseTick;
        public byte Released;
    }
}
