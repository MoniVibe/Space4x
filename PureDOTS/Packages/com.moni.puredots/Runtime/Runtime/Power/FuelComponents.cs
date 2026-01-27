using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Power
{
    public enum FuelDemandMode : byte
    {
        Fixed = 0,
        PowerRequested = 1,
        PowerAllocated = 2,
        GeneratorOutput = 3
    }

    public struct FuelConsumer : IComponentData
    {
        public FuelDemandMode DemandMode;
        public float FuelPerMW; // units per MW-second
        public float FuelPerSecond; // units per second
        public FixedString64Bytes DefaultFuelId;
        public byte AllowPartial;
    }

    [InternalBufferCapacity(2)]
    public struct FuelBlendElement : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public float FuelPerMW; // units per MW-second
        public byte Required;
    }

    public struct FuelConsumerState : IComponentData
    {
        public float FuelRatio;
        public float RequestedUnits;
        public float ConsumedUnits;
        public byte Starved;
        public uint LastTick;
    }

    public struct FuelStorageRef : IComponentData
    {
        public Entity Storage;
    }
}
