using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Environment
{
    /// <summary>
    /// Terrain chunk definition (authoritative base data).
    /// </summary>
    public struct TerrainChunk : IComponentData
    {
        public int3 ChunkCoord;
        public int3 VoxelsPerChunk;
        public Entity VolumeEntity;
        public BlobAssetReference<TerrainChunkBlob> BaseBlob;
    }

    public struct TerrainVolume : IComponentData
    {
        public float3 LocalOrigin;
    }

    public struct TerrainChunkBlob
    {
        public BlobArray<byte> SolidMask;
        public BlobArray<byte> MaterialId;
        public BlobArray<byte> Hardness;
        public BlobArray<byte> DepositId;
        public BlobArray<byte> OreGrade;
    }

    [InternalBufferCapacity(0)]
    public struct TerrainVoxelRuntime : IBufferElementData
    {
        public byte SolidMask;
        public byte MaterialId;
        public byte DepositId;
        public byte OreGrade;
        public byte Damage;
    }

    public struct TerrainChunkDirty : IComponentData
    {
        public uint EditVersion;
        public uint LastEditTick;
    }

    [System.Flags]
    public enum TerrainModificationFlags : byte
    {
        None = 0,
        AffectsSurface = 1 << 0,
        AffectsVolume = 1 << 1,
        AffectsMaterial = 1 << 2
    }

    public enum TerrainModificationKind : byte
    {
        Dig = 0,
        Fill = 1,
        Carve = 2,
        PaintMaterial = 3
    }

    public enum TerrainModificationShape : byte
    {
        Brush = 0,
        Tunnel = 1,
        Ramp = 2
    }

    public enum TerrainModificationToolKind : byte
    {
        Drill = 0,
        Laser = 1,
        Microwave = 2
    }

    public enum TerrainModificationSpace : byte
    {
        World = 0,
        VolumeLocal = 1
    }

    [InternalBufferCapacity(32)]
    public struct TerrainModificationRequest : IBufferElementData
    {
        public TerrainModificationKind Kind;
        public TerrainModificationShape Shape;
        public TerrainModificationToolKind ToolKind;
        public float3 Start;
        public float3 End;
        public float Radius;
        public float Depth;
        public byte MaterialId;
        public byte DamageDelta;
        public byte DamageThreshold;
        public float YieldMultiplier;
        public float HeatDelta;
        public float InstabilityDelta;
        public TerrainModificationFlags Flags;
        public uint RequestedTick;
        public Entity Actor;
        public Entity VolumeEntity;
        public TerrainModificationSpace Space;
    }

    public struct TerrainModificationQueue : IComponentData { }

    public struct TerrainModificationBudget : IComponentData
    {
        public int MaxEditsPerTick;
        public int MaxDirtyRegionsPerTick;

        public static TerrainModificationBudget Default => new()
        {
            MaxEditsPerTick = 8,
            MaxDirtyRegionsPerTick = 16
        };
    }

    [InternalBufferCapacity(16)]
    public struct TerrainDirtyRegion : IBufferElementData
    {
        public float3 WorldMin;
        public float3 WorldMax;
        public uint Version;
        public byte Flags;
    }

    [InternalBufferCapacity(16)]
    public struct TerrainModificationEvent : IBufferElementData
    {
        public float3 WorldPosition;
        public float3 WorldDirection;
        public float Radius;
        public int ClearedVoxels;
        public TerrainModificationToolKind ToolKind;
        public TerrainModificationShape Shape;
        public Entity VolumeEntity;
        public uint Tick;
    }

    [InternalBufferCapacity(0)]
    public struct TerrainSurfaceTileVersion : IBufferElementData
    {
        public int2 TileCoord;
        public uint Version;
    }

    [InternalBufferCapacity(0)]
    public struct TerrainUndergroundChunkVersion : IBufferElementData
    {
        public Entity VolumeEntity;
        public int3 ChunkCoord;
        public uint Version;
    }

    public struct TerrainChunkKey : IEquatable<TerrainChunkKey>
    {
        public Entity VolumeEntity;
        public int3 ChunkCoord;

        public bool Equals(TerrainChunkKey other)
        {
            return VolumeEntity.Equals(other.VolumeEntity) && ChunkCoord.Equals(other.ChunkCoord);
        }

        public override int GetHashCode()
        {
            var hash = math.hash(new int4(VolumeEntity.Index, VolumeEntity.Version, ChunkCoord.x, ChunkCoord.y));
            hash = math.hash(new int2((int)hash, ChunkCoord.z));
            return (int)hash;
        }
    }
}
