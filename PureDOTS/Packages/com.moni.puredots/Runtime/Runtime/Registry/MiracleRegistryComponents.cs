using System;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Registry
{
    /// <summary>
    /// Registry summary for miracles.
    /// </summary>
    public struct MiracleRegistry : IComponentData
    {
        public int TotalMiracles;
        public int ActiveMiracles;
        public int SustainedMiracles;
        public int CoolingMiracles;
        public float TotalEnergyCost;
        public float TotalCooldownSeconds;
        public uint LastUpdateTick;
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
    }

    /// <summary>
    /// Flags for miracle registry entries indicating state and behavior.
    /// </summary>
    [System.Flags]
    public enum MiracleRegistryFlags : byte
    {
        None = 0,
        Active = 1 << 0,
        Sustained = 1 << 1,
        CoolingDown = 1 << 2
    }

    /// <summary>
    /// Reason why a miracle was cancelled.
    /// Used for UX telemetry and design tuning.
    /// </summary>
    public enum MiracleCancelReason : byte
    {
        None = 0,
        UserCancelled = 1,
        TargetInvalid = 2,
        Interrupted = 3,
        InsufficientResources = 4,
        Timeout = 5,
        CasterDied = 6
    }

    /// <summary>
    /// Registry entry for a miracle instance, tracking its state, position, and telemetry data.
    /// </summary>
    public struct MiracleRegistryEntry :
        IBufferElementData,
        IComparable<MiracleRegistryEntry>,
        IRegistryEntry,
        IRegistryFlaggedEntry
    {
        public Entity MiracleEntity;
        public Entity CasterEntity;
        public MiracleType Type;
        public MiracleCastingMode CastingMode;
        public MiracleLifecycleState Lifecycle;
        public MiracleRegistryFlags Flags;
        public float3 TargetPosition;
        public int TargetCellId;
        public uint SpatialVersion;
        public float ChargePercent;
        public float CurrentRadius;
        public float CurrentIntensity;
        public float CooldownSecondsRemaining;
        public float EnergyCostThisCast;
        public uint LastCastTick;

        // UX Telemetry fields
        /// <summary>
        /// Tick when the user first provided input for this miracle.
        /// </summary>
        public uint LastInputTick;

        /// <summary>
        /// Tick when casting actually began (transitioned to Active/Charging).
        /// </summary>
        public uint CastStartTick;

        /// <summary>
        /// Tick when the miracle was cancelled (0 if not cancelled).
        /// </summary>
        public uint CancelTick;

        /// <summary>
        /// Reason for cancellation if the miracle was cancelled.
        /// </summary>
        public MiracleCancelReason CancelReason;

        public int CompareTo(MiracleRegistryEntry other)
        {
            return MiracleEntity.Index.CompareTo(other.MiracleEntity.Index);
        }

        public Entity RegistryEntity => MiracleEntity;

        public byte RegistryFlags => (byte)Flags;
    }
}

