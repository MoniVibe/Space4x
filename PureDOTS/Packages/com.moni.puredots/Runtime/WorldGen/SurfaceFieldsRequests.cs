using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen
{
    public struct SurfaceFieldsChunkRequestQueue : IComponentData
    {
    }

    [InternalBufferCapacity(64)]
    public struct SurfaceFieldsChunkRequest : IBufferElementData
    {
        public int3 ChunkCoord;
    }

    public struct SurfaceFieldsDomainConfig : IComponentData
    {
        public int2 CellsPerChunk;
        public float CellSize;
        public float2 WorldOriginXZ;
        public float LatitudeOriginZ;
        public float LatitudeInvRange;

        public static SurfaceFieldsDomainConfig Default => new()
        {
            CellsPerChunk = new int2(64, 64),
            CellSize = 1f,
            WorldOriginXZ = float2.zero,
            LatitudeOriginZ = 0f,
            LatitudeInvRange = 0f
        };
    }

    /// <summary>
    /// Optional domain config that switches SurfaceFields generation from planar XZ to a cube-projected sphere.
    /// When present on the SurfaceFields queue singleton, chunk coords encode (u, face, v).
    /// </summary>
    public struct SurfaceFieldsSphereCubeQuadDomainConfig : IComponentData
    {
        public float3 Center;
        public float Radius;
        public int2 ChunksPerFace;

        public static SurfaceFieldsSphereCubeQuadDomainConfig Default => new()
        {
            Center = float3.zero,
            Radius = 1000f,
            ChunksPerFace = new int2(16, 16)
        };
    }

    public struct SurfaceFieldsStreamingConfig : IComponentData
    {
        public int LoadRadiusChunks;
        public int KeepRadiusChunks;
        public int MaxNewChunksPerTick;
        public byte EnableEviction;

        public static SurfaceFieldsStreamingConfig Default => new()
        {
            LoadRadiusChunks = 1,
            KeepRadiusChunks = 2,
            MaxNewChunksPerTick = 8,
            EnableEviction = 0
        };
    }
}
