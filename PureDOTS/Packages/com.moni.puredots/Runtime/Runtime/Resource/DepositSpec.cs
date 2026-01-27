using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Resource
{
    /// <summary>
    /// Deposit specification data - defines deposit properties for mining.
    /// </summary>
    public struct DepositSpec
    {
        public FixedString32Bytes Id;
        public FixedString32Bytes ResourceId; // Resource type ID
        public float Richness; // Multiplier for base extraction rate
        public float DepletionPerWork; // Units depleted per work tick
        public float Hardness; // Resistance to mining (affects work required)
    }

    /// <summary>
    /// Node spawner specification - defines how harvest nodes are spawned.
    /// </summary>
    public struct NodeSpawnerSpec
    {
        public FixedString32Bytes Id;
        public float Density; // Nodes per unit area
        public float Variance; // Random variance in density
        public uint Seed; // Seed for deterministic placement
    }

    /// <summary>
    /// Blob catalog for deposit specifications.
    /// </summary>
    public struct DepositCatalogBlob
    {
        public BlobArray<DepositSpec> Deposits;
    }

    /// <summary>
    /// Blob catalog for node spawner specifications.
    /// </summary>
    public struct NodeSpawnerCatalogBlob
    {
        public BlobArray<NodeSpawnerSpec> Spawners;
    }

    /// <summary>
    /// Singleton component holding deposit catalog reference.
    /// </summary>
    public struct DepositCatalog : IComponentData
    {
        public BlobAssetReference<DepositCatalogBlob> Catalog;
    }

    /// <summary>
    /// Singleton component holding node spawner catalog reference.
    /// </summary>
    public struct NodeSpawnerCatalog : IComponentData
    {
        public BlobAssetReference<NodeSpawnerCatalogBlob> Catalog;
    }

    /// <summary>
    /// Component marking a deposit entity.
    /// </summary>
    public struct DepositEntity : IComponentData
    {
        public FixedString32Bytes DepositId; // Reference to DepositSpec
        public float CurrentRichness; // Current richness (depletes over time)
        public float InitialRichness; // Starting richness
    }

    /// <summary>
    /// Component marking a harvest node entity (attachment point on deposit).
    /// </summary>
    public struct HarvestNode : IComponentData
    {
        public Entity DepositEntity; // Parent deposit
        public float WorkProgress; // Current work progress (0-1)
        public float WorkRate; // Work units per second
    }
}

