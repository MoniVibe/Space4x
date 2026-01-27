using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Armies
{
    public struct ArmySupplyRequest : IBufferElementData
    {
        public Entity Army;
        public float SupplyNeeded; // days worth
        public float3 Destination;
        public byte Priority;
    }

    public struct ArmySupplyDepot : IComponentData
    {
        public Entity Village;
    }
}
