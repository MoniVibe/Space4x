using PureDOTS.Runtime.WorldGen;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Environment
{
    /// <summary>
    /// Optional flat surface provider used when no height grid is present.
    /// </summary>
    public struct TerrainFlatSurface : IComponentData
    {
        public float Height;
        public byte Enabled;
    }

    /// <summary>
    /// Minimal solid-volume provider for validation (solid sphere).
    /// </summary>
    public struct TerrainSolidSphere : IComponentData
    {
        public float3 Center;
        public float Radius;
        public byte Enabled;
    }

    /// <summary>
    /// Bundles terrain providers for query use in Burst jobs.
    /// </summary>
    public struct TerrainQueryContext
    {
        public MoistureGrid MoistureGrid;
        public TerrainHeightPlane HeightPlane;
        public TerrainFlatSurface FlatSurface;
        public TerrainSolidSphere SolidSphere;
        public TerrainWorldConfig WorldConfig;
        public uint GlobalTerrainVersion;
        public SurfaceFieldsDomainConfig SurfaceFieldsDomain;
        public NativeArray<SurfaceFieldsChunkRef> SurfaceFieldsChunks;
        public NativeArray<TerrainSurfaceTileVersion> SurfaceTileVersions;
        public NativeArray<TerrainUndergroundChunkVersion> UndergroundChunkVersions;
        public TerrainVoxelAccessor VoxelAccessor;
        public Entity VolumeEntity;
        public float3 VolumeOrigin;
        public float4x4 VolumeWorldToLocal;
        public byte VolumeEnabled;
    }

    public enum TerrainHitKind : byte
    {
        None = 0,
        Surface = 1,
        Volume = 2
    }

    public struct TerrainHit
    {
        public float3 Position;
        public float3 Normal;
        public float Distance;
        public TerrainHitKind Kind;
    }

    public struct TerrainCapsule
    {
        public float3 PointA;
        public float3 PointB;
        public float Radius;
    }

    public struct SurfaceFieldsChunkRef : IBufferElementData
    {
        public int3 ChunkCoord;
        public BlobAssetReference<SurfaceFieldsChunkBlob> Chunk;
    }

    [System.Flags]
    public enum TerrainSurfaceCellFlags : byte
    {
        None = 0,
        Hole = 1 << 0,
        Water = 1 << 1,
        Diggable = 1 << 2,
        Portal = 1 << 3,
        Built = 1 << 4
    }

    public struct TerrainWalkEval
    {
        public byte IsWalkable;
        public float SurfaceHeight;
        public float SlopeAngle;
        public TerrainClearanceBand ClearanceBand;
        public TerrainSurfaceCellFlags SurfaceFlags;
    }

    /// <summary>
    /// Central terrain query facade (height + volume). Uses simple providers in Phase 0.
    /// </summary>
    public static class TerrainQueryFacade
    {
        public static bool TrySampleHeight(in TerrainQueryContext context, float3 worldPosition, out float height)
        {
            if (TrySampleSurfaceFieldsHeight(context, worldPosition, out height))
            {
                return true;
            }

            if (TerrainHeightSampler.TrySampleHeight(context.MoistureGrid, context.HeightPlane, worldPosition, out height))
            {
                return true;
            }

            if (context.FlatSurface.Enabled != 0)
            {
                height = context.FlatSurface.Height;
                return true;
            }

            height = 0f;
            return false;
        }

        public static float SampleHeight(in TerrainQueryContext context, float3 worldPosition)
        {
            return TrySampleHeight(context, worldPosition, out var height) ? height : 0f;
        }

        public static bool TrySampleNormal(in TerrainQueryContext context, float3 worldPosition, out float3 normal)
        {
            if (!TrySampleHeight(context, worldPosition, out var height))
            {
                normal = default;
                return false;
            }

            var step = math.max(0.01f, context.WorldConfig.SurfaceCellSize * 0.5f);
            var left = worldPosition + new float3(-step, 0f, 0f);
            var right = worldPosition + new float3(step, 0f, 0f);
            var down = worldPosition + new float3(0f, 0f, -step);
            var up = worldPosition + new float3(0f, 0f, step);

            if (!TrySampleHeight(context, left, out var heightLeft) ||
                !TrySampleHeight(context, right, out var heightRight) ||
                !TrySampleHeight(context, down, out var heightDown) ||
                !TrySampleHeight(context, up, out var heightUp))
            {
                normal = new float3(0f, 1f, 0f);
                return true;
            }

            var dx = (heightRight - heightLeft) / (2f * step);
            var dz = (heightUp - heightDown) / (2f * step);
            normal = math.normalizesafe(new float3(-dx, 1f, -dz), new float3(0f, 1f, 0f));
            return true;
        }

        public static bool TrySampleSlope(in TerrainQueryContext context, float3 worldPosition, out float slopeAngle)
        {
            if (!TrySampleNormal(context, worldPosition, out var normal))
            {
                slopeAngle = 0f;
                return false;
            }

            slopeAngle = math.acos(math.saturate(normal.y));
            return true;
        }

        public static TerrainSurfaceCellFlags GetSurfaceCellFlags(in TerrainQueryContext context, float3 worldPosition)
        {
            return TerrainSurfaceCellFlags.None;
        }

        public static bool IsSolid(in TerrainQueryContext context, float3 worldPosition)
        {
            if (context.VolumeEnabled != 0 &&
                context.VolumeEntity != Entity.Null &&
                context.VoxelAccessor.ChunkLookup.IsCreated)
            {
                var local = math.transform(context.VolumeWorldToLocal, worldPosition) - context.VolumeOrigin;
                var chunkSize = new float3(context.WorldConfig.VoxelsPerChunk) * context.WorldConfig.VoxelSize;
                if (context.WorldConfig.VoxelSize > 0f && math.all(chunkSize > 0f))
                {
                    var chunkCoord = (int3)math.floor(local / chunkSize);
                    var chunkOrigin = new float3(chunkCoord) * chunkSize;
                    var voxelCoord = (int3)math.floor((local - chunkOrigin) / context.WorldConfig.VoxelSize);

                    if (context.VoxelAccessor.IsSolid(context.VolumeEntity, chunkCoord, voxelCoord))
                    {
                        return true;
                    }
                }
            }

            if (context.SolidSphere.Enabled == 0)
            {
                return false;
            }

            var delta = worldPosition - context.SolidSphere.Center;
            return math.lengthsq(delta) <= context.SolidSphere.Radius * context.SolidSphere.Radius;
        }

        public static TerrainClearanceBand SampleClearanceBand(in TerrainQueryContext context, float3 worldPosition)
        {
            if (IsSolid(context, worldPosition))
            {
                return TerrainClearanceBand.None;
            }

            return TerrainClearanceBand.Ship;
        }

        public static TerrainWalkEval EvaluateWalkableSurface(in TerrainQueryContext context, float3 worldPosition)
        {
            var eval = new TerrainWalkEval
            {
                IsWalkable = 0,
                SurfaceHeight = 0f,
                SlopeAngle = 0f,
                ClearanceBand = TerrainClearanceBand.None,
                SurfaceFlags = TerrainSurfaceCellFlags.None
            };

            if (!TrySampleHeight(context, worldPosition, out var height))
            {
                return eval;
            }

            eval.IsWalkable = 1;
            eval.SurfaceHeight = height;
            eval.SurfaceFlags = GetSurfaceCellFlags(context, worldPosition);
            if (TrySampleSlope(context, worldPosition, out var slope))
            {
                eval.SlopeAngle = slope;
            }

            return eval;
        }

        public static TerrainWalkEval EvaluateWalkableVolume(in TerrainQueryContext context, float3 worldPosition, TerrainClearanceBand requiredBand)
        {
            var eval = new TerrainWalkEval
            {
                IsWalkable = 0,
                SurfaceHeight = 0f,
                SlopeAngle = 0f,
                ClearanceBand = TerrainClearanceBand.None,
                SurfaceFlags = TerrainSurfaceCellFlags.None
            };

            var maxBand = SampleClearanceBand(context, worldPosition);
            eval.ClearanceBand = maxBand;
            eval.IsWalkable = (byte)(maxBand >= requiredBand ? 1 : 0);
            return eval;
        }

        public static void EnumerateTraversalLinks(in TerrainQueryContext context, in AABB bounds, ref NativeList<Entity> links)
        {
        }

        public static uint GetSurfaceTileVersion(in TerrainQueryContext context, int2 tileCoord)
        {
            if (context.SurfaceTileVersions.IsCreated)
            {
                for (int i = 0; i < context.SurfaceTileVersions.Length; i++)
                {
                    if (context.SurfaceTileVersions[i].TileCoord.Equals(tileCoord))
                    {
                        return context.SurfaceTileVersions[i].Version;
                    }
                }
            }

            return context.GlobalTerrainVersion;
        }

        public static uint GetUndergroundChunkVersion(in TerrainQueryContext context, Entity volumeEntity, int3 chunkCoord)
        {
            if (context.UndergroundChunkVersions.IsCreated)
            {
                for (int i = 0; i < context.UndergroundChunkVersions.Length; i++)
                {
                    if (context.UndergroundChunkVersions[i].VolumeEntity == volumeEntity &&
                        context.UndergroundChunkVersions[i].ChunkCoord.Equals(chunkCoord))
                    {
                        return context.UndergroundChunkVersions[i].Version;
                    }
                }
            }

            return context.GlobalTerrainVersion;
        }

        public static bool RaycastSolid(in TerrainQueryContext context, float3 from, float3 to, out TerrainHit hit)
        {
            if (context.SolidSphere.Enabled == 0)
            {
                hit = default;
                return false;
            }

            return RaycastSphere(from, to, context.SolidSphere.Center, context.SolidSphere.Radius, TerrainHitKind.Volume, out hit);
        }

        public static bool CapsuleCastSolid(in TerrainQueryContext context, in TerrainCapsule capsule, float3 delta, out TerrainHit hit)
        {
            if (context.SolidSphere.Enabled == 0)
            {
                hit = default;
                return false;
            }

            // Placeholder: approximate capsule sweep with a single sphere cast from capsule midpoint.
            var midpoint = (capsule.PointA + capsule.PointB) * 0.5f;
            var expandedRadius = context.SolidSphere.Radius + capsule.Radius;
            return RaycastSphere(midpoint, midpoint + delta, context.SolidSphere.Center, expandedRadius, TerrainHitKind.Volume, out hit);
        }

        private static bool RaycastSphere(float3 from, float3 to, float3 center, float radius, TerrainHitKind kind, out TerrainHit hit)
        {
            var direction = to - from;
            var length = math.length(direction);
            if (length <= 1e-5f)
            {
                hit = default;
                return false;
            }

            var dirNorm = direction / length;
            var originToCenter = from - center;
            var b = math.dot(originToCenter, dirNorm);
            var c = math.dot(originToCenter, originToCenter) - radius * radius;
            var discriminant = b * b - c;

            if (discriminant < 0f)
            {
                hit = default;
                return false;
            }

            var t = -b - math.sqrt(discriminant);
            if (t < 0f || t > length)
            {
                hit = default;
                return false;
            }

            var position = from + dirNorm * t;
            var normal = math.normalizesafe(position - center, new float3(0f, 1f, 0f));

            hit = new TerrainHit
            {
                Position = position,
                Normal = normal,
                Distance = t,
                Kind = kind
            };

            return true;
        }

        private static bool TrySampleSurfaceFieldsHeight(in TerrainQueryContext context, float3 worldPosition, out float height)
        {
            if (!context.SurfaceFieldsChunks.IsCreated || context.SurfaceFieldsChunks.Length == 0)
            {
                height = 0f;
                return false;
            }

            var domain = context.SurfaceFieldsDomain;
            if (domain.CellSize <= 0f || domain.CellsPerChunk.x <= 0 || domain.CellsPerChunk.y <= 0)
            {
                height = 0f;
                return false;
            }

            var chunkSize = new float2(domain.CellsPerChunk.x * domain.CellSize, domain.CellsPerChunk.y * domain.CellSize);
            var local = worldPosition.xz - domain.WorldOriginXZ;
            var chunkCoord2 = (int2)math.floor(local / chunkSize);
            var chunkCoord = new int3(chunkCoord2.x, 0, chunkCoord2.y);
            var chunkIndex = FindSurfaceFieldsChunk(context.SurfaceFieldsChunks, chunkCoord);
            if (chunkIndex < 0)
            {
                height = 0f;
                return false;
            }

            var chunkBlob = context.SurfaceFieldsChunks[chunkIndex].Chunk;
            if (!chunkBlob.IsCreated)
            {
                height = 0f;
                return false;
            }

            ref var chunk = ref chunkBlob.Value;
            var chunkOrigin = domain.WorldOriginXZ + new float2(chunkCoord.x * chunkSize.x, chunkCoord.z * chunkSize.y);
            var localXZ = worldPosition.xz - chunkOrigin;

            var gx = localXZ.x / domain.CellSize;
            var gz = localXZ.y / domain.CellSize;
            var x0 = math.clamp((int)math.floor(gx), 0, domain.CellsPerChunk.x);
            var z0 = math.clamp((int)math.floor(gz), 0, domain.CellsPerChunk.y);
            var x1 = math.min(x0 + 1, domain.CellsPerChunk.x);
            var z1 = math.min(z0 + 1, domain.CellsPerChunk.y);
            var tx = math.saturate(gx - x0);
            var tz = math.saturate(gz - z0);

            var vertexStride = domain.CellsPerChunk.x + 1;
            var i00 = x0 + z0 * vertexStride;
            var i10 = x1 + z0 * vertexStride;
            var i01 = x0 + z1 * vertexStride;
            var i11 = x1 + z1 * vertexStride;

            var h00 = SurfaceFieldsQuantization.DequantizeU16(chunk.HeightQ[i00]);
            var h10 = SurfaceFieldsQuantization.DequantizeU16(chunk.HeightQ[i10]);
            var h01 = SurfaceFieldsQuantization.DequantizeU16(chunk.HeightQ[i01]);
            var h11 = SurfaceFieldsQuantization.DequantizeU16(chunk.HeightQ[i11]);

            var h0 = math.lerp(h00, h10, tx);
            var h1 = math.lerp(h01, h11, tx);
            var h = math.lerp(h0, h1, tz);

            height = h * math.max(0f, context.WorldConfig.SurfaceHeightScale);
            return true;
        }

        private static int FindSurfaceFieldsChunk(in NativeArray<SurfaceFieldsChunkRef> chunks, int3 chunkCoord)
        {
            for (int i = 0; i < chunks.Length; i++)
            {
                if (chunks[i].ChunkCoord.Equals(chunkCoord))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
