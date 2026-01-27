using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// [OBSOLETE] Legacy hand state enum. Use PureDOTS.Runtime.Hand.HandStateType instead.
    /// Migration: Map from PureDOTS.Runtime.Hand.HandStateType when syncing HandInteractionState.
    /// </summary>
    [System.Obsolete("Use PureDOTS.Runtime.Hand.HandStateType instead. This enum is deprecated and will be removed in a future version.")]
    public enum HandState : byte
    {
        Idle = 0,
        Hovering = 1,
        Grabbing = 2,
        Holding = 3,
        Placing = 4,
        Casting = 5,
        Cooldown = 6
    }

    /// <summary>
    /// [OBSOLETE] Legacy divine hand command type enum. Use PureDOTS.Runtime.Hand.HandCommandType instead.
    /// Migration: Map from PureDOTS.Runtime.Hand.HandCommandType when syncing HandInteractionState.
    /// </summary>
    [System.Obsolete("Use PureDOTS.Runtime.Hand.HandCommandType instead. This enum is deprecated and will be removed in a future version.")]
    public enum DivineHandCommandType : byte
    {
        None = 0,
        Grab = 1,
        Drop = 2,
        Siphon = 3,
        Dump = 4,
        Miracle = 5,
        Cancel = 6,
        SiphonPile = 7,
        DumpToStorehouse = 8,
        DumpToConstruction = 9,
        GroundDrip = 10
    }

    /// <summary>
    /// Shared hand interaction state consumed by resource and miracle systems.
    /// Mirrors the authoritative values driven by <see cref="DivineHandSystem"/>.
    /// </summary>
    public struct HandInteractionState : IComponentData
    {
        public Entity HandEntity;
        public HandState CurrentState;
        public HandState PreviousState;
        public DivineHandCommandType ActiveCommand;
        public ushort ActiveResourceType;
        public int HeldAmount;
        public int HeldCapacity;
        public float CooldownSeconds;
        public uint LastUpdateTick;
        public byte Flags;

        public const byte FlagMiracleArmed = 1 << 0;
        public const byte FlagSiphoning = 1 << 1;
        public const byte FlagDumping = 1 << 2;
    }

    /// <summary>
    /// Aggregated siphon state so resource and miracle chains operate on identical data.
    /// </summary>
    public struct ResourceSiphonState : IComponentData
    {
        public Entity HandEntity;
        public Entity TargetEntity;
        public ushort ResourceTypeIndex;
        public float SiphonRate;
        public float DumpRate;
        public float AccumulatedUnits;
        public uint LastUpdateTick;
        public byte Flags;

        public const byte FlagSiphoning = 1 << 0;
        public const byte FlagDumpCommandPending = 1 << 1;
    }
}
