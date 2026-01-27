using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy
{
    [System.Flags]
    public enum BatchInventoryFlags : byte
    {
        None = 0,
        DisableSpoilage = 1 << 0
    }

    public struct BatchInventory : IComponentData
    {
        public float MaxCapacity;
        public float TotalUnits;
        public float SpoiledUnits;
        public int BatchCount;
        public uint LastUpdateTick;
        public BatchInventoryFlags Flags;
    }

    public struct InventoryBatch : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public float Units;
        public float UnitValue;
        public uint CreatedTick;
        public uint ExpiryTick;
        public float SpoilagePerTick;
    }

    public struct BatchConsumptionRequest : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public float RequestedUnits;
    }

    public struct BatchSpoilageSettings : IComponentData
    {
        public float SpoilagePerTick;
        public float MinRemainderBeforeRemove;

        public static BatchSpoilageSettings CreateDefault()
        {
            return new BatchSpoilageSettings
            {
                SpoilagePerTick = 0.05f,
                MinRemainderBeforeRemove = 0.01f
            };
        }
    }
}
