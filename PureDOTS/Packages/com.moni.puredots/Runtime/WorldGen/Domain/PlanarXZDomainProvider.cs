using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen.Domain
{
    public struct PlanarXZDomainProvider : IWorldDomainProvider
    {
        public int2 CellsPerChunk { get; set; }
        public float CellSize { get; set; }

        public float2 WorldOriginXZ;

        /// <summary>
        /// Maps world Z into a 0..1 latitude value: latitude01 = saturate(0.5 + (z - originZ) * invRange).
        /// Set invRange to 0 for a constant 0.5 latitude.
        /// </summary>
        public float LatitudeOriginZ;
        public float LatitudeInvRange;

        public int3 ChunkCoordFromWorld(float3 worldPos)
        {
            var chunkSize = new float2(CellsPerChunk.x * CellSize, CellsPerChunk.y * CellSize);
            var local = worldPos.xz - WorldOriginXZ;
            var coord = (int2)math.floor(local / chunkSize);
            return new int3(coord.x, 0, coord.y);
        }

        /// <summary>
        /// Returns the world position of a grid point in the chunk lattice (no 0.5 cell offset).
        /// Grid point coordinates typically range 0..CellsPerChunk.
        /// </summary>
        public float3 ToWorld(int3 chunkCoord, int2 gridPoint)
        {
            var chunkSize = new float2(CellsPerChunk.x * CellSize, CellsPerChunk.y * CellSize);
            var chunkOrigin = WorldOriginXZ + new float2(chunkCoord.x * chunkSize.x, chunkCoord.z * chunkSize.y);
            var xz = chunkOrigin + (new float2(gridPoint.x, gridPoint.y) * CellSize);
            return new float3(xz.x, 0f, xz.y);
        }

        public float Latitude01(float3 worldPos)
        {
            if (LatitudeInvRange == 0f)
            {
                return 0.5f;
            }

            return math.saturate(0.5f + (worldPos.z - LatitudeOriginZ) * LatitudeInvRange);
        }

        public FixedList32Bytes<int3> NeighborChunks(int3 chunkCoord)
        {
            return new FixedList32Bytes<int3>
            {
                new int3(chunkCoord.x - 1, chunkCoord.y, chunkCoord.z),
                new int3(chunkCoord.x + 1, chunkCoord.y, chunkCoord.z),
                new int3(chunkCoord.x, chunkCoord.y, chunkCoord.z - 1),
                new int3(chunkCoord.x, chunkCoord.y, chunkCoord.z + 1)
            };
        }
    }
}

