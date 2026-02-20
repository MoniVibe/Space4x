using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Source channel for cargo entries displayed in the inventory/cargo panel.
    /// </summary>
    public enum Space4XCargoSource : byte
    {
        VesselHold = 0,
        CarrierStorage = 1
    }

    /// <summary>
    /// Aggregated projection state used by UI bindings for inventory/cargo/equipment.
    /// </summary>
    public struct Space4XInventoryProjection : IComponentData
    {
        public float CargoUsed;
        public float CargoCapacity;
        public float CargoUtilization;
        public int CargoEntryCount;
        public int EquipmentEntryCount;
        public uint Revision;
        public byte Dirty;
    }

    /// <summary>
    /// Projected cargo line item for UI rendering.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct Space4XCargoProjectionEntry : IBufferElementData
    {
        public Space4XCargoSource Source;
        public ResourceType ResourceType;
        public FixedString64Bytes Label;
        public float Amount;
        public float Capacity;
    }

    /// <summary>
    /// Projected equipment line item for UI rendering.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct Space4XEquipmentProjectionEntry : IBufferElementData
    {
        public int SlotIndex;
        public ModuleSlotSize SlotSize;
        public ModuleSlotState SlotState;
        public Entity ModuleEntity;
        public FixedString64Bytes ModuleTypeId;
        public byte SegmentIndex;
        public byte SegmentSocketIndex;
        public MountType MountType;
    }

    /// <summary>
    /// Tone for projected ship fitting UI status.
    /// </summary>
    public enum Space4XShipFitUiTone : byte
    {
        None = 0,
        Positive = 1,
        Neutral = 2,
        Warning = 3,
        Error = 4
    }

    /// <summary>
    /// Latest projected ship fitting status for HUD/tooltips.
    /// </summary>
    public struct Space4XShipFitStatusProjection : IComponentData
    {
        public uint Revision;
        public uint LastConsumedResultRevision;
        public uint LastConsumedEventRevision;
        public Space4XShipFitRequestType RequestType;
        public Space4XShipFitTargetKind TargetKind;
        public int TargetIndex;
        public Space4XShipFitResultCode Code;
        public Space4XShipFitUiTone Tone;
        public FixedString128Bytes Message;
        public byte Dirty;
    }

    /// <summary>
    /// Recent projected ship fitting UI notifications.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct Space4XShipFitStatusFeedEntry : IBufferElementData
    {
        public uint Revision;
        public Space4XShipFitRequestType RequestType;
        public Space4XShipFitTargetKind TargetKind;
        public int TargetIndex;
        public Space4XShipFitResultCode Code;
        public Space4XShipFitUiTone Tone;
        public FixedString128Bytes Message;
    }
}
