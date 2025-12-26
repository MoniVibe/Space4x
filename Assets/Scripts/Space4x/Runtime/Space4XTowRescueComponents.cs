using Unity.Entities;

namespace Space4X.Runtime
{
    public struct Space4XTowRescueRequest : IComponentData
    {
        public Entity Target;
        public uint IssuedTick;
        public uint LastUpdatedTick;
        public uint ExpireTick;
        public byte Priority;
    }
}
