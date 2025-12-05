using PureDOTS.Environment;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Climate
{
    /// <summary>
    /// Terraforming project that gradually adjusts planetary climate toward a target.
    /// </summary>
    public struct TerraformingProject : IComponentData
    {
        public Entity Planet;  // Planet entity being terraformed
        public float Progress; // 0..1, completion progress
        public ClimateVector TargetGlobalClimate;
        public float TerraformingRate; // How fast progress increases (0..1 per second)
    }

    /// <summary>
    /// Sector-level climate profile for a colony region.
    /// </summary>
    public struct SectorClimateProfile : IComponentData
    {
        public Entity Colony;  // Colony entity this sector belongs to
        public ClimateVector TargetClimate;
        public float InfluenceRadius;
    }

    /// <summary>
    /// BioDeck module component for ships/stations.
    /// </summary>
    public struct BioDeckModule : IComponentData
    {
        public Entity ShipOrStation;  // Parent entity
        public int2 GridResolution;   // e.g., 16x16
        public float CellSize;        // Size of each biodeck cell
        public float3 LocalOrigin;    // Origin of biodeck grid in local space
    }

    /// <summary>
    /// Per-cell climate data for a biodeck module.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct BioDeckCell : IBufferElementData
    {
        public ClimateVector Climate;
        public BiomeType Biome;
    }
}

