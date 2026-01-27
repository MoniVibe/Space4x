using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Miracles
{
    /// <summary>
    /// Reason a targeting attempt failed. Guides UX messaging.
    /// </summary>
    public enum MiracleTargetValidityReason : byte
    {
        None = 0,
        OutOfRange = 1,
        InvalidTarget = 2,
        InsufficientResource = 3,
        OnCooldown = 4,
        NoTargetFound = 5,
        InvalidGround = 6,
        TargetNotFriendly = 7,
        TargetNotEnemy = 8
    }

    /// <summary>
    /// Computed targeting solution for the currently selected miracle.
    /// Written by MiracleTargetingSystem, consumed by preview + activation.
    /// </summary>
    public struct MiracleTargetSolution : IComponentData
    {
        public float3 TargetPoint;
        public Entity TargetEntity;
        public float Radius;
        public byte IsValid;
        public MiracleTargetValidityReason ValidityReason;
        public float3 PreviewArcStart;
        public float3 PreviewArcEnd;
        public MiracleId SelectedMiracleId;
    }

    /// <summary>
    /// Optional per-caster slot tracking for legacy pipelines.
    /// Games that still select miracles by slot can populate this and attach
    /// MiracleSlotDefinition buffers to drive catalog lookup.
    /// </summary>
    public struct MiracleCasterSelection : IComponentData
    {
        public byte SelectedSlot;
    }

    /// <summary>
    /// Presentation-facing data describing the current preview ring/reticle.
    /// Updated by MiraclePreviewSystem and consumed by game-specific renderers.
    /// </summary>
    public struct MiraclePreviewData : IComponentData
    {
        public float3 Position;
        public float Radius;
        public byte IsValid;
        public MiracleTargetValidityReason ValidityReason;
        public MiracleId SelectedMiracleId;
    }
}

