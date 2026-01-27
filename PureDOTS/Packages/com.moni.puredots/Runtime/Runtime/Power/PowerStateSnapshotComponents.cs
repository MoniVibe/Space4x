using Unity.Entities;

namespace PureDOTS.Runtime.Power
{
    /// <summary>
    /// Power state snapshot for hot path reads.
    /// Entities reading "Am I powered or not?"
    /// Production using current power/supply factor.
    /// </summary>
    public struct PowerStateSnapshot : IComponentData
    {
        /// <summary>
        /// Whether entity is powered (1) or not (0).
        /// </summary>
        public byte IsPowered;

        /// <summary>
        /// Power coverage factor (0..1, 0 = no power, 1 = full power).
        /// </summary>
        public float PowerFactor;

        /// <summary>
        /// Whether entity has supply (1) or not (0).
        /// </summary>
        public byte HasSupply;

        /// <summary>
        /// Supply factor (0..1, 0 = no supply, 1 = full supply).
        /// </summary>
        public float SupplyFactor;

        /// <summary>
        /// Tick when this snapshot was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }
}

