using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    public enum Space4XShipFitItemKind : byte
    {
        None = 0,
        Module = 1,
        Segment = 2
    }

    public enum Space4XShipFitTargetKind : byte
    {
        None = 0,
        ModuleInventory = 1,
        ModuleSocket = 2,
        SegmentInventory = 3,
        SegmentSocket = 4
    }

    public enum Space4XShipFitRequestType : byte
    {
        LeftClick = 0,
        CancelHeld = 1
    }

    public enum Space4XShipFitResultCode : byte
    {
        None = 0,
        Success = 1,
        NoHeldItem = 2,
        EmptySource = 3,
        InvalidTarget = 4,
        ItemTypeMismatch = 5,
        SlotSizeMismatch = 6,
        MountTypeMismatch = 7,
        SegmentAssemblyInvalid = 8,
        ModuleSpecMissing = 9
    }

    /// <summary>
    /// UI cursor state for Diablo-style click-to-pickup / click-to-equip interactions.
    /// </summary>
    public struct Space4XShipFitCursorState : IComponentData
    {
        public Space4XShipFitItemKind HeldKind;
        public Entity HeldModule;
        public FixedString64Bytes HeldSegmentId;
        public Space4XShipFitTargetKind OriginKind;
        public int OriginIndex;

        public static Space4XShipFitCursorState Empty => new Space4XShipFitCursorState
        {
            HeldKind = Space4XShipFitItemKind.None,
            HeldModule = Entity.Null,
            HeldSegmentId = default,
            OriginKind = Space4XShipFitTargetKind.None,
            OriginIndex = -1
        };
    }

    [InternalBufferCapacity(1)]
    public struct Space4XModuleInventoryEntry : IBufferElementData
    {
        public Entity Module;
    }

    [InternalBufferCapacity(1)]
    public struct Space4XSegmentInventoryEntry : IBufferElementData
    {
        public FixedString64Bytes SegmentId;
    }

    /// <summary>
    /// Layout metadata used by UI to draw module sockets grouped by hull segment.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct Space4XCarrierModuleSocketLayout : IBufferElementData
    {
        public int SlotIndex;
        public byte SegmentIndex;
        public byte SegmentSocketIndex;
        public MountType MountType;
    }

    /// <summary>
    /// Buffered interaction requests from UI input. Processed in-order each update.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct Space4XShipFitRequest : IBufferElementData
    {
        public Space4XShipFitRequestType RequestType;
        public Space4XShipFitTargetKind TargetKind;
        public int TargetIndex;
    }

    /// <summary>
    /// Latest fit interaction result for immediate UI status/tooltips.
    /// </summary>
    public struct Space4XShipFitLastResult : IComponentData
    {
        public uint Revision;
        public Space4XShipFitRequestType RequestType;
        public Space4XShipFitTargetKind TargetKind;
        public int TargetIndex;
        public Space4XShipFitResultCode Code;
    }

    /// <summary>
    /// Recent fit interaction result events (append-only, bounded in-system).
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct Space4XShipFitResultEvent : IBufferElementData
    {
        public uint Revision;
        public Space4XShipFitRequestType RequestType;
        public Space4XShipFitTargetKind TargetKind;
        public int TargetIndex;
        public Space4XShipFitResultCode Code;
    }
}
