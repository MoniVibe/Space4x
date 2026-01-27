using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Environment
{
    public struct TerrainVoxelSample
    {
        public byte SolidMask;
        public byte MaterialId;
        public byte DepositId;
        public byte OreGrade;
        public byte Damage;
    }

    public enum TerrainVoxelFaceDirection : byte
    {
        PosX = 0,
        NegX = 1,
        PosY = 2,
        NegY = 3,
        PosZ = 4,
        NegZ = 5
    }

    public struct TerrainVoxelFace
    {
        public int3 VoxelCoord;
        public TerrainVoxelFaceDirection Direction;
        public byte MaterialId;
        public byte DepositId;
        public byte OreGrade;
    }

    public struct TerrainVoxelAccessor
    {
        [ReadOnly] public NativeParallelHashMap<TerrainChunkKey, Entity> ChunkLookup;
        [ReadOnly] public ComponentLookup<TerrainChunk> Chunks;
        [ReadOnly] public BufferLookup<TerrainVoxelRuntime> RuntimeVoxels;
        public TerrainWorldConfig WorldConfig;

        public bool TryGetChunk(Entity volumeEntity, int3 chunkCoord, out TerrainChunk chunk, out int3 dims)
        {
            chunk = default;
            dims = default;

            var key = new TerrainChunkKey { VolumeEntity = volumeEntity, ChunkCoord = chunkCoord };
            if (!ChunkLookup.TryGetValue(key, out var entity))
            {
                return false;
            }

            if (!Chunks.HasComponent(entity))
            {
                return false;
            }

            chunk = Chunks[entity];
            dims = ResolveChunkDims(chunk);
            return dims.x > 0 && dims.y > 0 && dims.z > 0;
        }

        public bool TrySampleVoxel(Entity volumeEntity, int3 chunkCoord, int3 voxelCoord, out TerrainVoxelSample sample)
        {
            sample = default;

            var key = new TerrainChunkKey { VolumeEntity = volumeEntity, ChunkCoord = chunkCoord };
            if (!ChunkLookup.TryGetValue(key, out var entity))
            {
                return false;
            }

            if (!Chunks.HasComponent(entity))
            {
                return false;
            }

            var chunk = Chunks[entity];
            var dims = ResolveChunkDims(chunk);
            if (!TerrainVoxelMath.IsInside(voxelCoord, dims))
            {
                return false;
            }

            var index = TerrainVoxelMath.ToIndex(voxelCoord, dims);
            if (RuntimeVoxels.HasBuffer(entity))
            {
                var runtime = RuntimeVoxels[entity];
                if (index >= 0 && index < runtime.Length)
                {
                    var voxel = runtime[index];
                    sample = new TerrainVoxelSample
                    {
                        SolidMask = voxel.SolidMask,
                        MaterialId = voxel.MaterialId,
                        DepositId = voxel.DepositId,
                        OreGrade = voxel.OreGrade,
                        Damage = voxel.Damage
                    };
                    return true;
                }
            }

            if (!chunk.BaseBlob.IsCreated)
            {
                return false;
            }

            ref var blob = ref chunk.BaseBlob.Value;
            if (index < 0 || index >= blob.SolidMask.Length)
            {
                return false;
            }

            sample = new TerrainVoxelSample
            {
                SolidMask = blob.SolidMask[index],
                MaterialId = blob.MaterialId.Length > index ? blob.MaterialId[index] : (byte)0,
                DepositId = blob.DepositId.Length > index ? blob.DepositId[index] : (byte)0,
                OreGrade = blob.OreGrade.Length > index ? blob.OreGrade[index] : (byte)0,
                Damage = 0
            };

            return true;
        }

        public bool TrySampleNeighbor(Entity volumeEntity, int3 chunkCoord, int3 voxelCoord, int3 neighborOffset, out TerrainVoxelSample sample)
        {
            var neighborChunk = chunkCoord;
            var neighborVoxel = voxelCoord + neighborOffset;
            var dims = WorldConfig.VoxelsPerChunk;
            TerrainVoxelMath.Normalize(ref neighborChunk, ref neighborVoxel, dims);
            return TrySampleVoxel(volumeEntity, neighborChunk, neighborVoxel, out sample);
        }

        public bool IsSolid(Entity volumeEntity, int3 chunkCoord, int3 voxelCoord)
        {
            return TrySampleVoxel(volumeEntity, chunkCoord, voxelCoord, out var sample) && sample.SolidMask != 0;
        }

        private int3 ResolveChunkDims(in TerrainChunk chunk)
        {
            var dims = chunk.VoxelsPerChunk;
            if (dims.x <= 0 || dims.y <= 0 || dims.z <= 0)
            {
                dims = WorldConfig.VoxelsPerChunk;
            }

            return dims;
        }
    }

    public static class TerrainVoxelMath
    {
        public static readonly int3[] NeighborOffsets =
        {
            new int3(1, 0, 0),
            new int3(-1, 0, 0),
            new int3(0, 1, 0),
            new int3(0, -1, 0),
            new int3(0, 0, 1),
            new int3(0, 0, -1)
        };

        public static readonly TerrainVoxelFaceDirection[] NeighborDirections =
        {
            TerrainVoxelFaceDirection.PosX,
            TerrainVoxelFaceDirection.NegX,
            TerrainVoxelFaceDirection.PosY,
            TerrainVoxelFaceDirection.NegY,
            TerrainVoxelFaceDirection.PosZ,
            TerrainVoxelFaceDirection.NegZ
        };

        public static float3 GetChunkWorldOrigin(in TerrainWorldConfig config, int3 chunkCoord)
        {
            var chunkSize = new float3(config.VoxelsPerChunk.x, config.VoxelsPerChunk.y, config.VoxelsPerChunk.z) * config.VoxelSize;
            return config.VolumeWorldOrigin + new float3(chunkCoord.x * chunkSize.x, chunkCoord.y * chunkSize.y, chunkCoord.z * chunkSize.z);
        }

        public static float3 GetVoxelCenterWorld(in TerrainWorldConfig config, int3 chunkCoord, int3 voxelCoord)
        {
            var chunkOrigin = GetChunkWorldOrigin(config, chunkCoord);
            var voxelSize = config.VoxelSize;
            return chunkOrigin + new float3((voxelCoord.x + 0.5f) * voxelSize, (voxelCoord.y + 0.5f) * voxelSize, (voxelCoord.z + 0.5f) * voxelSize);
        }

        public static void AppendExposedFaces(
            in TerrainVoxelAccessor accessor,
            Entity volumeEntity,
            int3 chunkCoord,
            ref NativeList<TerrainVoxelFace> faces)
        {
            if (!accessor.TryGetChunk(volumeEntity, chunkCoord, out _, out var dims))
            {
                return;
            }

            for (int z = 0; z < dims.z; z++)
            {
                for (int y = 0; y < dims.y; y++)
                {
                    for (int x = 0; x < dims.x; x++)
                    {
                        var voxelCoord = new int3(x, y, z);
                        if (!accessor.TrySampleVoxel(volumeEntity, chunkCoord, voxelCoord, out var sample) || sample.SolidMask == 0)
                        {
                            continue;
                        }

                        for (int i = 0; i < NeighborOffsets.Length; i++)
                        {
                            if (accessor.TrySampleNeighbor(volumeEntity, chunkCoord, voxelCoord, NeighborOffsets[i], out var neighbor) &&
                                neighbor.SolidMask != 0)
                            {
                                continue;
                            }

                            faces.Add(new TerrainVoxelFace
                            {
                                VoxelCoord = voxelCoord,
                                Direction = NeighborDirections[i],
                                MaterialId = sample.MaterialId,
                                DepositId = sample.DepositId,
                                OreGrade = sample.OreGrade
                            });
                        }
                    }
                }
            }
        }

        public static bool IsInside(int3 coord, int3 dims)
        {
            if (dims.x <= 0 || dims.y <= 0 || dims.z <= 0)
            {
                return false;
            }

            return coord.x >= 0 && coord.x < dims.x &&
                   coord.y >= 0 && coord.y < dims.y &&
                   coord.z >= 0 && coord.z < dims.z;
        }

        public static int ToIndex(int3 coord, int3 dims)
        {
            if (dims.x <= 0 || dims.y <= 0 || dims.z <= 0)
            {
                return -1;
            }

            return coord.x + coord.y * dims.x + coord.z * dims.x * dims.y;
        }

        public static void Normalize(ref int3 chunkCoord, ref int3 voxelCoord, int3 dims)
        {
            if (dims.x <= 0 || dims.y <= 0 || dims.z <= 0)
            {
                return;
            }

            if (voxelCoord.x < 0)
            {
                var offset = (math.abs(voxelCoord.x) + dims.x - 1) / dims.x;
                chunkCoord.x -= offset;
                voxelCoord.x += offset * dims.x;
            }
            else if (voxelCoord.x >= dims.x)
            {
                var offset = voxelCoord.x / dims.x;
                chunkCoord.x += offset;
                voxelCoord.x -= offset * dims.x;
            }

            if (voxelCoord.y < 0)
            {
                var offset = (math.abs(voxelCoord.y) + dims.y - 1) / dims.y;
                chunkCoord.y -= offset;
                voxelCoord.y += offset * dims.y;
            }
            else if (voxelCoord.y >= dims.y)
            {
                var offset = voxelCoord.y / dims.y;
                chunkCoord.y += offset;
                voxelCoord.y -= offset * dims.y;
            }

            if (voxelCoord.z < 0)
            {
                var offset = (math.abs(voxelCoord.z) + dims.z - 1) / dims.z;
                chunkCoord.z -= offset;
                voxelCoord.z += offset * dims.z;
            }
            else if (voxelCoord.z >= dims.z)
            {
                var offset = voxelCoord.z / dims.z;
                chunkCoord.z += offset;
                voxelCoord.z -= offset * dims.z;
            }
        }

        public static void SplitCoord(int3 voxelIndex, int3 dims, out int3 chunkCoord, out int3 voxelCoord)
        {
            chunkCoord = new int3(
                FloorDiv(voxelIndex.x, dims.x),
                FloorDiv(voxelIndex.y, dims.y),
                FloorDiv(voxelIndex.z, dims.z));
            voxelCoord = voxelIndex - chunkCoord * dims;
        }

        private static int FloorDiv(int value, int divisor)
        {
            if (divisor == 0)
            {
                return 0;
            }

            var div = value / divisor;
            var mod = value % divisor;
            if (mod != 0 && ((mod < 0) != (divisor < 0)))
            {
                div -= 1;
            }

            return div;
        }
    }
}
