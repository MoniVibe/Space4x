using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen.Domain
{
    /// <summary>
    /// Cube-projected sphere domain suitable for planet-style chunking.
    /// ChunkCoord encoding: x = chunkU, y = faceIndex (0..5), z = chunkV.
    /// Face order: +X, -X, +Y, -Y, +Z, -Z.
    /// </summary>
    public struct SphereCubeQuadDomainProvider : IWorldDomainProvider
    {
        public int2 CellsPerChunk { get; set; }
        public float CellSize { get; set; }

        public float3 Center;
        public float Radius;
        public int2 ChunksPerFace;

        public int3 ChunkCoordFromWorld(float3 worldPos)
        {
            var dir = math.normalizesafe(worldPos - Center, new float3(0f, 0f, 1f));

            var face = ResolveFaceIndex(dir);
            var uv = DirToFaceUv(face, dir);

            var cellsPerChunk = math.max(CellsPerChunk, new int2(1, 1));
            var chunksPerFace = math.max(ChunksPerFace, new int2(1, 1));
            var faceCells = chunksPerFace * cellsPerChunk;

            var uv01 = math.saturate((uv + 1f) * 0.5f);

            var cellU = (int)math.floor(uv01.x * faceCells.x);
            var cellV = (int)math.floor(uv01.y * faceCells.y);
            cellU = cellU >= faceCells.x ? faceCells.x - 1 : cellU;
            cellV = cellV >= faceCells.y ? faceCells.y - 1 : cellV;

            var chunkU = math.clamp(cellU / cellsPerChunk.x, 0, chunksPerFace.x - 1);
            var chunkV = math.clamp(cellV / cellsPerChunk.y, 0, chunksPerFace.y - 1);

            return new int3(chunkU, face, chunkV);
        }

        public float3 ToWorld(int3 chunkCoord, int2 gridPoint)
        {
            var cellsPerChunk = math.max(CellsPerChunk, new int2(1, 1));
            var chunksPerFace = math.max(ChunksPerFace, new int2(1, 1));
            var faceCells = chunksPerFace * cellsPerChunk;

            var face = math.clamp(chunkCoord.y, 0, 5);
            var globalU = chunkCoord.x * cellsPerChunk.x + gridPoint.x;
            var globalV = chunkCoord.z * cellsPerChunk.y + gridPoint.y;

            var uv = new float2(
                -1f + (globalU / math.max(1f, faceCells.x)) * 2f,
                -1f + (globalV / math.max(1f, faceCells.y)) * 2f);

            var cube = FaceUvToDir(face, uv);
            var dir = math.normalizesafe(cube, new float3(0f, 0f, 1f));
            return Center + dir * math.max(0.001f, Radius);
        }

        public float Latitude01(float3 worldPos)
        {
            var dir = math.normalizesafe(worldPos - Center, new float3(0f, 1f, 0f));
            var lat = math.asin(math.clamp(dir.y, -1f, 1f));
            return math.saturate(0.5f + (lat / math.PI));
        }

        public FixedList32Bytes<int3> NeighborChunks(int3 chunkCoord)
        {
            var list = new FixedList32Bytes<int3>();
            list.Add(GetNeighbor(chunkCoord, -1, 0));
            list.Add(GetNeighbor(chunkCoord, 1, 0));
            list.Add(GetNeighbor(chunkCoord, 0, -1));
            list.Add(GetNeighbor(chunkCoord, 0, 1));
            return list;
        }

        private int3 GetNeighbor(int3 chunkCoord, int duChunks, int dvChunks)
        {
            var cellsPerChunk = math.max(CellsPerChunk, new int2(1, 1));
            var chunksPerFace = math.max(ChunksPerFace, new int2(1, 1));
            var faceCells = chunksPerFace * cellsPerChunk;

            var centerCellU = (chunkCoord.x + 0.5f) * cellsPerChunk.x + duChunks * cellsPerChunk.x;
            var centerCellV = (chunkCoord.z + 0.5f) * cellsPerChunk.y + dvChunks * cellsPerChunk.y;

            var uv = new float2(
                -1f + (centerCellU / math.max(1f, faceCells.x)) * 2f,
                -1f + (centerCellV / math.max(1f, faceCells.y)) * 2f);

            var face = math.clamp(chunkCoord.y, 0, 5);
            var cube = FaceUvToDir(face, uv);
            var dir = math.normalizesafe(cube, new float3(0f, 0f, 1f));
            var worldPos = Center + dir * math.max(0.001f, Radius);
            return ChunkCoordFromWorld(worldPos);
        }

        private static int ResolveFaceIndex(float3 dir)
        {
            var ax = math.abs(dir.x);
            var ay = math.abs(dir.y);
            var az = math.abs(dir.z);

            if (ax >= ay && ax >= az)
            {
                return dir.x >= 0f ? 0 : 1;
            }

            if (ay >= ax && ay >= az)
            {
                return dir.y >= 0f ? 2 : 3;
            }

            return dir.z >= 0f ? 4 : 5;
        }

        private static float2 DirToFaceUv(int face, float3 dir)
        {
            var ax = math.max(1e-6f, math.abs(dir.x));
            var ay = math.max(1e-6f, math.abs(dir.y));
            var az = math.max(1e-6f, math.abs(dir.z));

            return face switch
            {
                0 => new float2(-dir.z / ax, dir.y / ax), // +X
                1 => new float2(dir.z / ax, dir.y / ax),  // -X
                2 => new float2(dir.x / ay, -dir.z / ay), // +Y
                3 => new float2(dir.x / ay, dir.z / ay),  // -Y
                4 => new float2(dir.x / az, dir.y / az),  // +Z
                5 => new float2(-dir.x / az, dir.y / az), // -Z
                _ => new float2(dir.x / az, dir.y / az)
            };
        }

        private static float3 FaceUvToDir(int face, float2 uv)
        {
            return face switch
            {
                0 => new float3(1f, uv.y, -uv.x),  // +X
                1 => new float3(-1f, uv.y, uv.x),  // -X
                2 => new float3(uv.x, 1f, -uv.y),  // +Y
                3 => new float3(uv.x, -1f, uv.y),  // -Y
                4 => new float3(uv.x, uv.y, 1f),   // +Z
                5 => new float3(-uv.x, uv.y, -1f), // -Z
                _ => new float3(uv.x, uv.y, 1f)
            };
        }
    }
}

