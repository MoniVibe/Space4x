using Unity.Entities;

namespace PureDOTS.Runtime.Miracles
{
    /// <summary>
    /// Presentation-facing charge display data for HUD/UI systems.
    /// Updated by MiracleChargeDisplaySystem from MiracleChargeState.
    /// </summary>
    public struct MiracleChargeDisplayData : IComponentData
    {
        /// <summary>Charge percentage (0-100).</summary>
        public float ChargePercent;

        /// <summary>Current tier index (0 = uncharged, 1-N = tiers).</summary>
        public byte CurrentTier;

        /// <summary>Total hold time in seconds.</summary>
        public float HoldTimeSeconds;

        /// <summary>Whether currently charging (0/1).</summary>
        public byte IsCharging;
    }
}

