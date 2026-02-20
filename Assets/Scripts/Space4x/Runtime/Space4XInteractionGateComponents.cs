using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    public enum Space4XInteractionGateKind : byte
    {
        None = 0,
        Hail = 1,
        Docking = 2,
        RoomEvent = 3,
        Equipping = 4,
        Trade = 5,
        Production = 6
    }

    public enum Space4XInteractionUnavailableReason : byte
    {
        None = 0,
        StandingTooLow = 1,
        MissingTarget = 2,
        MissingFaction = 3,
        InvalidSelection = 4
    }

    /// <summary>
    /// Request to open a dialogue gate for a specific interaction context.
    /// Producers should append requests; resolve system consumes and clears.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct Space4XInteractionGateTrigger : IBufferElementData
    {
        public Space4XInteractionGateKind Kind;
        public Entity ContextEntity;
        public Entity Actor;
        public Entity Target;
        public ushort TargetFactionId;
        public uint RoomId;
        /// <summary>
        /// Bitmask of allowed slots (bit0=slot1 ... bit4=slot5) when UseOptionMask=1.
        /// </summary>
        public uint OptionMask;
        /// <summary>
        /// When 1, OptionMask controls which template slots are included.
        /// </summary>
        public byte UseOptionMask;
        public FixedString64Bytes GateId;
        public FixedString64Bytes YarnNodePrefix;
    }

    /// <summary>
    /// Current gate state. Singleton-like component maintained by the gate systems.
    /// </summary>
    public struct Space4XInteractionGateState : IComponentData
    {
        public byte IsOpen;
        public Space4XInteractionGateKind Kind;
        public Entity ContextEntity;
        public Entity Actor;
        public Entity Target;
        public ushort TargetFactionId;
        public half Standing01;
        public uint RoomId;
        public uint OpenTick;
        public uint LastResolvedTick;
        public FixedString64Bytes GateId;
    }

    /// <summary>
    /// Option list generated when a gate opens.
    /// </summary>
    [InternalBufferCapacity(5)]
    public struct Space4XInteractionOption : IBufferElementData
    {
        public byte Slot;
        public byte IsEnabled;
        public half RequiredStanding;
        public Space4XInteractionUnavailableReason UnavailableReason;
        public FixedString64Bytes Label;
        public FixedString64Bytes YarnNodeId;
    }

    /// <summary>
    /// Choice request, typically produced by input systems (1-5).
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct Space4XInteractionChoiceRequest : IBufferElementData
    {
        public byte Slot;
        public uint Tick;
    }

    /// <summary>
    /// Output event for downstream dialogue systems (Yarn or other runners).
    /// Accepted=1 means launch dialogue node, Accepted=0 means deny/feedback.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct Space4XYarnGateEvent : IBufferElementData
    {
        public byte Accepted;
        public Space4XInteractionGateKind Kind;
        public Entity ContextEntity;
        public Entity Actor;
        public Entity Target;
        public byte Slot;
        public uint Tick;
        public Space4XInteractionUnavailableReason Reason;
        public FixedString64Bytes GateId;
        public FixedString64Bytes YarnNodeId;
    }

    public struct Space4XInteractionGateConfig : IComponentData
    {
        public half HailTradeStanding;
        public half HailMissionStanding;
        public half HailCoerciveStanding;
        public half DockingStandardStanding;
        public half DockingPriorityStanding;
        public half DockingBribeStanding;
        public half RoomRiskStanding;
        public half RoomHighRiskStanding;
        public half EquipOverhaulStanding;
        public half TradeContractStanding;
        public half TradeGuildStanding;
        public half ProductionSpecialistStanding;
        public half ProductionRushStanding;
        public half ProductionPrototypeStanding;
        public byte EnableKeyboardDigitInput;
        public byte CloseGateOnAccept;

        public static Space4XInteractionGateConfig Default => new Space4XInteractionGateConfig
        {
            HailTradeStanding = (half)0.2f,
            HailMissionStanding = (half)0.35f,
            HailCoerciveStanding = (half)0.6f,
            DockingStandardStanding = (half)0.15f,
            DockingPriorityStanding = (half)0.4f,
            DockingBribeStanding = (half)0.55f,
            RoomRiskStanding = (half)0.35f,
            RoomHighRiskStanding = (half)0.6f,
            EquipOverhaulStanding = (half)0.25f,
            TradeContractStanding = (half)0.3f,
            TradeGuildStanding = (half)0.45f,
            ProductionSpecialistStanding = (half)0.2f,
            ProductionRushStanding = (half)0.35f,
            ProductionPrototypeStanding = (half)0.6f,
            EnableKeyboardDigitInput = 1,
            CloseGateOnAccept = 1
        };
    }
}
