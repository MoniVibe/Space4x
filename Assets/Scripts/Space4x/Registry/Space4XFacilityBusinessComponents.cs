using Unity.Entities;
using Unity.Collections;

namespace Space4X.Registry
{
    public enum FacilityBusinessClass : byte
    {
        None = 0,
        Research = 1,
        Refinery = 2,
        Production = 3,
        ModuleFacility = 4,
        ShipFabrication = 5,
        Shipyard = 6,
        Construction = 7
    }

    public struct FacilityBusinessClassComponent : IComponentData
    {
        public FacilityBusinessClass Value;
    }

    public struct ConstructorShipTag : IComponentData
    {
    }

    public struct ConstructionRig : IComponentData
    {
        public float BuildRatePerSecond;
        public float RangeMeters;

        public static ConstructionRig Default => new ConstructionRig
        {
            BuildRatePerSecond = 0.8f,
            RangeMeters = 180f
        };
    }

    [InternalBufferCapacity(4)]
    public struct ShipyardAssemblyRequest : IBufferElementData
    {
        public FixedString64Bytes HullId;
        public byte Priority;
        public uint EnqueuedTick;
    }
}
