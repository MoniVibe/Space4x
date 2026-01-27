using PureDOTS.Runtime.Miracles;
using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Tracks per-caster miracle charge progress while the activation input is held.
    /// </summary>
    public struct MiracleChargeState : IComponentData
    {
        /// <summary>Normalized charge (0-1) derived from HoldToContinuous or tier mapping.</summary>
        public float Charge01;

        /// <summary>Total time, in seconds, that the activation has been held.</summary>
        public float HeldTime;

        /// <summary>Current tier index (0 = uncharged).</summary>
        public byte TierIndex;

        /// <summary>Whether the caster is currently charging (0/1).</summary>
        public byte IsCharging;
    }

    /// <summary>
    /// Tracks previous selected miracle ID to detect when player switches miracles.
    /// Used by MiracleChargeSystem to reset charge when miracle changes.
    /// </summary>
    public struct MiracleChargeTrackingState : IComponentData
    {
        /// <summary>Previously selected miracle ID.</summary>
        public MiracleId PreviousSelectedId;
    }
}

