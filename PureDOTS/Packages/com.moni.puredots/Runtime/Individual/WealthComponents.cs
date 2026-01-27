using Unity.Entities;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Individual wealth component for SimIndividuals.
    /// Only attached to important SimIndividuals (not mass Villagers) for scale.
    /// Bridges to economy Chunk 1 (VillagerWealth).
    /// </summary>
    public struct IndividualWealth : IComponentData
    {
        /// <summary>
        /// Liquid funds (cash, currency). Direct mapping to VillagerWealth.Balance.
        /// </summary>
        public double LiquidFunds;

        /// <summary>
        /// Influence points (social capital, favors, connections).
        /// </summary>
        public float Influence;

        /// <summary>
        /// Reputation score (fame/infamy, honor/glory).
        /// </summary>
        public float Reputation;

        /// <summary>
        /// Tick when wealth was last updated (for sync with Chunk 1).
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Types of assets an individual can own.
    /// </summary>
    public enum AssetKind : byte
    {
        Ship = 0,           // Personal ship (Space4X)
        Building = 1,       // Building/structure (Godgame)
        Land = 2,           // Land plot
        Business = 3,       // Business ownership share
        Equipment = 4,      // Valuable equipment
        Artifact = 5        // Unique artifact/relic
    }

    /// <summary>
    /// Asset holding entry linking an individual to an owned asset.
    /// Used for asset ownership propagation and inheritance.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct AssetHolding : IBufferElementData
    {
        /// <summary>
        /// Entity reference to the asset.
        /// </summary>
        public Entity Asset;

        /// <summary>
        /// Ownership share [0..1]. Fraction of asset owned by this individual.
        /// </summary>
        public float OwnershipShare;

        /// <summary>
        /// Type of asset (for filtering and queries).
        /// </summary>
        public AssetKind Kind;

        /// <summary>
        /// Tick when asset was acquired (for inheritance calculations).
        /// </summary>
        public uint AcquiredTick;
    }
}

