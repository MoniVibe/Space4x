using PureDOTS.Runtime.Logistics.Components;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Blobs
{
    /// <summary>
    /// Resource definition blob asset.
    /// Extends ItemSpec with logistics-specific properties.
    /// </summary>
    public struct ResourceDefBlob
    {
        public FixedString64Bytes ResourceId;
        public StorageClass StorageClass;
        public float MassPerUnit;
        public float VolumePerUnit;
        public float BaseValuePerUnit; // for risk / escort calculations
        public float HazardLevel; // 0..1 (explosive, toxic, cursed, etc.)
        public float PerishRate; // 0 = never; else units lost per tick w/o proper storage
    }

    /// <summary>
    /// Container definition blob asset.
    /// Defines container types and their properties.
    /// </summary>
    public struct ContainerDefBlob
    {
        public int ContainerDefId;
        public StorageClass AllowedClass; // or bitmask if multi-class needed
        public float MassCapacity;
        public float VolumeCapacity;
        public float SafetyFactor; // reduces hazard, explosion risk (0..1)
        public float TempControl; // how good is refrigeration (0..1)
        public float ShockResist; // for fragile/explosive cargo (0..1)
        public byte Tier; // tech tier
    }

    /// <summary>
    /// Catalog blob containing all resource definitions.
    /// </summary>
    public struct ResourceDefCatalogBlob
    {
        public BlobArray<ResourceDefBlob> Resources;
    }

    /// <summary>
    /// Catalog blob containing all container definitions.
    /// </summary>
    public struct ContainerDefCatalogBlob
    {
        public BlobArray<ContainerDefBlob> Containers;
    }
}

