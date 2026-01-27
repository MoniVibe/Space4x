using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen.Domain
{
    public interface IWorldDomainProvider
    {
        int2 CellsPerChunk { get; }
        float CellSize { get; }

        int3 ChunkCoordFromWorld(float3 worldPos);
        float3 ToWorld(int3 chunkCoord, int2 gridPoint);
        float Latitude01(float3 worldPos);
        FixedList32Bytes<int3> NeighborChunks(int3 chunkCoord);
    }
}

