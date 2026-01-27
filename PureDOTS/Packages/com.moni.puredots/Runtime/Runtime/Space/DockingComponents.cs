using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Space
{
    /// <summary>
    /// Configuration for a carrier/capital ship docking bay.
    /// </summary>
    public struct DockingBayConfig : IComponentData
    {
        public int MaxDockedCraft;
        public int MaxHangarSlots;
        public float DockingThroughputPerMinute;
        public float UndockingThroughputPerMinute;
        public float DefaultPatrolRadiusKm;
        public float DefaultHarvestRadiusKm;
    }

    /// <summary>
    /// Runtime state tracked per docking bay.
    /// </summary>
    public struct DockingBayState : IComponentData
    {
        public int CurrentDocked;
        public int PendingLaunchCount;
        public uint LastLaunchTick;
        public uint LastDockTick;
    }

    /// <summary>
    /// Configuration for harvester/mining vessels.
    /// </summary>
    public struct HarvesterConfig : IComponentData
    {
        public float HarvestRadiusMeters;
        public float ReturnDistanceMeters;
        public float DefaultDropoffIntervalSeconds;
    }

    /// <summary>
    /// Reference to the parent carrier/hauler entity a vessel reports to.
    /// </summary>
    public struct ParentCarrierRef : IComponentData
    {
        public Entity Carrier;
    }

    /// <summary>
    /// Base configuration for strike craft/sortie-capable vessels.
    /// </summary>
    public struct StrikeCraftConfig : IComponentData
    {
        public FixedString64Bytes CraftId;
        public float DefaultPatrolRadiusKm;
        public float UndockWarmupSeconds;
    }
}
