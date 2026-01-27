using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Navigation
{
    public struct SurfaceNavTile : IComponentData
    {
        public int2 TileCoord;
        public uint Version;
        public BlobAssetReference<SurfaceNavTileBlob> Data;
    }

    public struct SurfaceNavTileBlob
    {
        public BlobArray<byte> WalkableMask;
        public BlobArray<byte> MoveCost;
        public BlobArray<float> Height;
        public BlobArray<float> Slope;
    }

    public struct UndergroundNavChunk : IComponentData
    {
        public int3 ChunkCoord;
        public uint Version;
        public BlobAssetReference<UndergroundNavChunkBlob> Data;
    }

    public struct UndergroundNavChunkBlob
    {
        public BlobArray<byte> PassableMask;
        public BlobArray<byte> ClearanceHeight;
        public BlobArray<ushort> RegionId;
    }
}
