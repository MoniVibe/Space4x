using PureDOTS.Runtime.Traversal;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Environment
{
    public enum TerrainClearanceBand : byte
    {
        None = 0,
        Tiny = 1,
        Small = 2,
        Medium = 3,
        Large = 4,
        Ship = 5
    }

    public struct TerrainClearanceBandConfig
    {
        public float TinyMaxRadius;
        public float SmallMaxRadius;
        public float MediumMaxRadius;
        public float LargeMaxRadius;
        public float ShipMaxRadius;

        public float TinyMaxHeight;
        public float SmallMaxHeight;
        public float MediumMaxHeight;
        public float LargeMaxHeight;
        public float ShipMaxHeight;
    }

    public struct TerrainWorldConfig : IComponentData
    {
        public float SurfaceCellSize;
        public int2 SurfaceCellsPerTile;
        public float2 SurfaceWorldOriginXZ;
        public float SurfaceHeightScale;
        public float VoxelSize;
        public int3 VoxelsPerChunk;
        public float3 VolumeWorldOrigin;
        public TerrainClearanceBandConfig Clearance;

        public static TerrainWorldConfig Default => new()
        {
            SurfaceCellSize = 1f,
            SurfaceCellsPerTile = new int2(32, 32),
            SurfaceWorldOriginXZ = float2.zero,
            SurfaceHeightScale = 1f,
            VoxelSize = 1f,
            VoxelsPerChunk = new int3(16, 16, 16),
            VolumeWorldOrigin = float3.zero,
            Clearance = new TerrainClearanceBandConfig
            {
                TinyMaxRadius = 0.25f,
                SmallMaxRadius = 0.5f,
                MediumMaxRadius = 0.9f,
                LargeMaxRadius = 1.5f,
                ShipMaxRadius = 6f,
                TinyMaxHeight = 0.6f,
                SmallMaxHeight = 1.2f,
                MediumMaxHeight = 2.2f,
                LargeMaxHeight = 3.5f,
                ShipMaxHeight = 12f
            }
        };
    }

    public static class TerrainClearanceUtility
    {
        public static TerrainClearanceBand ResolveBand(in TerrainWorldConfig config, in BodyDimensions dimensions, TraversalStance stance)
        {
            var height = TraversalUtility.ResolveHeight(dimensions, stance);
            var radius = math.max(0f, dimensions.Radius);
            return ResolveBand(config, radius, height);
        }

        public static TerrainClearanceBand ResolveBand(in TerrainWorldConfig config, float radius, float height)
        {
            var clearance = config.Clearance;
            if (radius <= clearance.TinyMaxRadius && height <= clearance.TinyMaxHeight)
            {
                return TerrainClearanceBand.Tiny;
            }

            if (radius <= clearance.SmallMaxRadius && height <= clearance.SmallMaxHeight)
            {
                return TerrainClearanceBand.Small;
            }

            if (radius <= clearance.MediumMaxRadius && height <= clearance.MediumMaxHeight)
            {
                return TerrainClearanceBand.Medium;
            }

            if (radius <= clearance.LargeMaxRadius && height <= clearance.LargeMaxHeight)
            {
                return TerrainClearanceBand.Large;
            }

            if (radius <= clearance.ShipMaxRadius && height <= clearance.ShipMaxHeight)
            {
                return TerrainClearanceBand.Ship;
            }

            return TerrainClearanceBand.None;
        }
    }
}
