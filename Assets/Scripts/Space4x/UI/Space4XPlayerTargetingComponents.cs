using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace Space4X.UI
{
    [System.Flags]
    public enum Space4XPlayerTargetPurpose : byte
    {
        None = 0,
        Combat = 1 << 0,
        Inspect = 1 << 1,
        Trading = 1 << 2
    }

    /// <summary>
    /// Player-facing target lock selected by click in manual flight modes.
    /// </summary>
    public struct Space4XPlayerTargetSelection : IComponentData
    {
        public Entity TargetEntity;
        public float3 TargetPoint;
        public byte HasSelection;
        public byte SelectedInMode2;

        public static Space4XPlayerTargetSelection None => new Space4XPlayerTargetSelection
        {
            TargetEntity = Entity.Null,
            TargetPoint = float3.zero,
            HasSelection = 0,
            SelectedInMode2 = 0
        };
    }

    /// <summary>
    /// MVP multi-target lock entries for future combat/inspect/trade intent routing.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct Space4XPlayerTargetLockEntry : IBufferElementData
    {
        public Entity TargetEntity;
        public float3 LastKnownPoint;
        public float Score;
        public byte PurposeMask;
        public byte IsPrimary;
    }

    /// <summary>
    /// Player-facing weapon input state for manual aiming/firing.
    /// </summary>
    public struct Space4XPlayerWeaponControl : IComponentData
    {
        public byte ManualAimMode;
        public byte TriggerHeld;

        public static Space4XPlayerWeaponControl Default => new Space4XPlayerWeaponControl
        {
            ManualAimMode = 0,
            TriggerHeld = 0
        };
    }

    /// <summary>
    /// Stable short identifier for player-facing targeting/readouts.
    /// </summary>
    public struct Space4XEntityCallsign : IComponentData
    {
        public FixedString64Bytes Value;
        public uint StableHash;
    }
}
