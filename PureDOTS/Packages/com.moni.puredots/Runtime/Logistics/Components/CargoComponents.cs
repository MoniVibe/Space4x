using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Components
{
    /// <summary>
    /// Storage classification for resources/cargo.
    /// Determines container compatibility and handling requirements.
    /// </summary>
    public enum StorageClass : byte
    {
        BulkSolid = 0,
        Liquid = 1,
        Gas = 2,
        Explosive = 3,
        Exotic = 4,
        Perishable = 5,
        Living = 6,
        Personnel = 7
    }

    /// <summary>
    /// Cargo item buffer element.
    /// Represents cargo loaded on a hauler.
    /// </summary>
    public struct CargoItem : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public float Amount;
        public int ContainerSlotIndex; // -1 if using implicit hold
    }

    /// <summary>
    /// Current cargo load state on a hauler.
    /// Aggregated from CargoItem buffer.
    /// </summary>
    public struct CargoLoadState : IComponentData
    {
        public float TotalMass;
        public float TotalVolume;
        public float TotalValue;
        public float HazardAggregate; // 0..1 aggregate hazard level
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Load effect summary for movement systems.
    /// Provides load ratio for speed/inertia calculations.
    /// </summary>
    public struct LoadEffect : IComponentData
    {
        public float LoadMass;
        public float LoadRatio; // LoadMass / HaulerCapacity.MaxMass
    }

    /// <summary>
    /// Cargo value assessment for AI decisions.
    /// Used by raiders and escorts to evaluate targets.
    /// </summary>
    public struct CargoValueState : IComponentData
    {
        public float TotalValue;
        public float RaidAttractiveness; // 0..1 scaled by enemy culture, desperation, etc.
        public float EscortPriority; // 0..1 used by friendly AI
    }
}

