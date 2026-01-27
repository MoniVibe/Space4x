using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen
{
    public static class WorldGenQuantization
    {
        public static int2 WorldPosCm(in int2 cell, ushort qx, ushort qy, ushort cellSizeCm)
        {
            uint cellSize = cellSizeCm;
            uint ox = ((uint)qx * cellSize + 32767u) / 65535u;
            uint oy = ((uint)qy * cellSize + 32767u) / 65535u;
            long baseX = (long)cell.x * cellSize;
            long baseY = (long)cell.y * cellSize;
            return new int2((int)(baseX + ox), (int)(baseY + oy));
        }

        public static ushort DistanceQFromMeters(float meters)
        {
            float cm = math.round(meters * 100f);
            return (ushort)math.clamp(cm, 0f, 65535f);
        }

        public static byte TempBin(ushort tempQ, byte tempBins, ushort tempMaxQ)
        {
            uint bin = (uint)(tempQ * tempBins) / (uint)(tempMaxQ + 1);
            return (byte)math.min(tempBins - 1, (int)bin);
        }

        public static byte HumidBin(ushort humidQ, byte humidBins, ushort humidMaxQ)
        {
            uint bin = (uint)(humidQ * humidBins) / (uint)(humidMaxQ + 1);
            return (byte)math.min(humidBins - 1, (int)bin);
        }
    }

    public static class WorldGenDeterminism
    {
        public static uint HashToNonZero(uint value)
        {
            return value == 0u ? 1u : value;
        }

        public static uint ComputeStreamSeed(uint worldSeed, uint streamId)
        {
            return HashToNonZero(math.hash(new uint2(worldSeed, streamId)));
        }

        public static uint ComputeCellSeed(uint streamSeed, int2 cell, uint salt)
        {
            var seed = math.hash(new uint4(streamSeed, unchecked((uint)cell.x), unchecked((uint)cell.y), salt));
            return HashToNonZero(seed);
        }

        public static Random CreateRandom(uint seed)
        {
            return new Random(HashToNonZero(seed));
        }
    }
}
