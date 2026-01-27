using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Environment
{
    /// <summary>
    /// Optional flat terrain plane fallback used when no height grid is available.
    /// </summary>
    public struct TerrainHeightPlane : IComponentData
    {
        public float Height;
        public float3 WorldMin;
        public float3 WorldMax;
        public byte Enabled;
    }

    public static class TerrainHeightSampler
    {
        public static bool TrySampleHeight(in MoistureGrid grid, float3 worldPosition, out float height)
        {
            if (!grid.IsCreated)
            {
                height = 0f;
                return false;
            }

            ref var heights = ref grid.Blob.Value.TerrainHeight;
            height = EnvironmentGridMath.SampleBilinear(grid.Metadata, ref heights, worldPosition, 0f);
            return true;
        }

        public static bool TrySampleHeight(in TerrainHeightPlane plane, float3 worldPosition, out float height)
        {
            if (plane.Enabled == 0)
            {
                height = 0f;
                return false;
            }

            if (worldPosition.x < plane.WorldMin.x || worldPosition.x > plane.WorldMax.x ||
                worldPosition.z < plane.WorldMin.z || worldPosition.z > plane.WorldMax.z)
            {
                height = 0f;
                return false;
            }

            height = plane.Height;
            return true;
        }

        public static bool TrySampleHeight(in MoistureGrid grid, in TerrainHeightPlane plane, float3 worldPosition, out float height)
        {
            if (TrySampleHeight(grid, worldPosition, out height))
            {
                return true;
            }

            return TrySampleHeight(plane, worldPosition, out height);
        }

        public static float SampleHeight(in MoistureGrid grid, in TerrainHeightPlane plane, float3 worldPosition)
        {
            return TrySampleHeight(grid, plane, worldPosition, out var height) ? height : 0f;
        }
    }
}
