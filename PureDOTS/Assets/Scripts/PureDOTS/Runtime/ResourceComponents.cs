using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    public struct ResourceTypeId : IComponentData
    {
        public FixedString64Bytes Value;
    }

    public struct ResourceSourceConfig : IComponentData
    {
        public float GatherRatePerWorker;
        public int MaxSimultaneousWorkers;
        public float RespawnSeconds;
        public byte Flags;

        public const byte FlagInfinite = 1 << 0;
        public const byte FlagRespawns = 1 << 1;
        public const byte FlagHandUprootAllowed = 1 << 2;
    }

    public struct ResourceSourceState : IComponentData
    {
        public float UnitsRemaining;
    }

    public struct StorehouseConfig : IComponentData
    {
        public float ShredRate;
        public int MaxShredQueueSize;
        public float InputRate;
        public float OutputRate;
    }

    public struct StorehouseInventory : IComponentData
    {
        public float TotalStored;
        public float TotalCapacity;
        public int ItemTypeCount;
        public byte IsShredding;
        public uint LastUpdateTick;
    }

    public struct StorehouseInventoryItem : IBufferElementData
    {
        public FixedString64Bytes ResourceTypeId;
        public float Amount;
        public float Reserved;
    }

    public struct ResourceChunkConfig : IComponentData
    {
        public float MassPerUnit;
        public float MinScale;
        public float MaxScale;
        public float DefaultUnits;
    }

    public struct ConstructionSiteProgress : IComponentData
    {
        public float RequiredProgress;
        public float CurrentProgress;
    }

    public struct ConstructionCostElement : IBufferElementData
    {
        public FixedString64Bytes ResourceTypeId;
        public float UnitsRequired;
    }

    public struct StorehouseCapacityElement : IBufferElementData
    {
        public FixedString64Bytes ResourceTypeId;
        public float MaxCapacity;
    }

    public struct ConstructionDeliveredElement : IBufferElementData
    {
        public FixedString64Bytes ResourceTypeId;
        public float UnitsDelivered;
    }

    public struct ConstructionSiteId : IComponentData
    {
        public int Value;
    }

    public struct ConstructionSiteFlags : IComponentData
    {
        public const byte Completed = 1 << 0;
        public byte Value;
    }

    public struct ConstructionCompletionPrefab : IComponentData
    {
        public Entity Prefab;
        public bool DestroySiteEntity;
    }

    public struct ConstructionCommandTag : IComponentData
    {
    }

    public struct ConstructionDepositCommand : IBufferElementData
    {
        public int SiteId;
        public FixedString64Bytes ResourceTypeId;
        public float Amount;
    }

    public struct ConstructionProgressCommand : IBufferElementData
    {
        public int SiteId;
        public float Delta;
    }
}
