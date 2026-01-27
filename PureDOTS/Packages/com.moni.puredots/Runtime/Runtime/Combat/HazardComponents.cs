using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Hazard kind flags - identifies what type of danger a hazard represents.
    /// </summary>
    [System.Flags]
    public enum HazardKind : uint
    {
        AoE = 1 << 0,
        Chain = 1 << 1,
        Plague = 1 << 2,
        Homing = 1 << 3,
        Spray = 1 << 4,
        Erratic = 1 << 5
    }

    /// <summary>
    /// Hazard slice - represents a danger envelope over time.
    /// Produced by BuildHazardSlicesSystem from projectile states and specs.
    /// </summary>
    public struct HazardSlice : IBufferElementData
    {
        public float3 Center; // World position
        public float3 Vel; // Velocity for extrapolation
        public float Radius0; // Initial radius (meters)
        public float RadiusGrow; // Growth rate (m/s) - for expanding AoE or blast waves
        public uint StartTick; // Inclusive start tick
        public uint EndTick; // Inclusive end tick
        public HazardKind Kind; // Type of hazard

        // Behavior parameters (interpreted per Kind)
        public float ChainRadius; // Pack spacing risk for chain weapons
        public float ContagionProb; // Plague spread chance per contact
        public float HomingConeCos; // Homing approach cone (cosine of half-angle)
        public float SprayVariance; // Angular std dev for erratic/spray weapons
        public uint TeamMask; // Who is affected (collision mask)
        public uint Seed; // Deterministic RNG seed for stochastic operations
    }

    /// <summary>
    /// Blob asset root storing risk samples for the hazard grid.
    /// </summary>
    public struct HazardRiskBlob
    {
        public BlobArray<float> Risk;
    }

    /// <summary>
    /// Hazard grid - 3D risk accumulation grid.
    /// For 2D battles, set Size.z = 1.
    /// </summary>
    public struct HazardGrid : IComponentData
    {
        public int3 Size; // Grid dimensions in cells (z=1 for 2D)
        public float Cell; // Meters per cell (uniform)
        public float3 Origin; // World origin of grid
        public BlobAssetReference<HazardRiskBlob> Risk; // Flattened risk array [z][y][x]
    }

    /// <summary>
    /// Singleton component pointing to the hazard grid entity.
    /// </summary>
    public struct HazardGridSingleton : IComponentData
    {
        public Entity GridEntity;
    }
}

