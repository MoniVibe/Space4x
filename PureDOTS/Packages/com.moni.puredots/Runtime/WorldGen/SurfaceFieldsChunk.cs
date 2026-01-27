using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen
{
    public struct SurfaceFieldsCellBlob
    {
        public byte WaterQ;
        public byte ResourcePotentialQ;
        public ushort BiomeId;
    }

    public struct SurfaceFieldsSummaryBlob
    {
        public ushort HeightMinQ;
        public ushort HeightMaxQ;
        public ushort HeightMeanQ;

        public byte TempMinQ;
        public byte TempMaxQ;
        public byte TempMeanQ;

        public byte MoistureMinQ;
        public byte MoistureMaxQ;
        public byte MoistureMeanQ;

        public uint LandCellCount;
        public uint WaterCellCount;
    }

    public struct SurfaceFieldsChunkBlob
    {
        public uint SchemaVersion;
        public int3 ChunkCoord;
        public int2 CellsPerChunk;
        public float CellSize;

        /// <summary>
        /// Quantized vertex samples (size: (CellsPerChunk.x + 1) * (CellsPerChunk.y + 1)).
        /// Stored at shared grid points so neighbor chunks share seam samples exactly.
        /// </summary>
        public BlobArray<ushort> HeightQ;
        public BlobArray<byte> TempQ;
        public BlobArray<byte> MoistureQ;

        /// <summary>
        /// Per-cell quantized values (size: CellsPerChunk.x * CellsPerChunk.y).
        /// </summary>
        public BlobArray<SurfaceFieldsCellBlob> Cells;

        public SurfaceFieldsSummaryBlob Summary;

        /// <summary>
        /// 64-bit FNV-1a over all quantized field bytes (stable for diffing/saves).
        /// </summary>
        public ulong QuantizedHash;
    }

    public struct SurfaceFieldsChunkComponent : IComponentData
    {
        public int3 ChunkCoord;
        public ulong QuantizedHash;
        public BlobAssetReference<SurfaceFieldsChunkBlob> Chunk;
    }

    public struct SurfaceFieldsChunkCleanup : ICleanupComponentData
    {
        public BlobAssetReference<SurfaceFieldsChunkBlob> Chunk;
    }
}
