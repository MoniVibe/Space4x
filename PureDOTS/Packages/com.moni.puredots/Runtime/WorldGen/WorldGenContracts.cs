using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen
{
    public struct ClimateMapBlob
    {
        public int2 SizeCells;
        public ushort CellSizeCm;
        public BlobArray<ushort> TemperatureQ;
        public BlobArray<ushort> HumidityQ;
        public BlobArray<byte> WaterMask;
        public BlobArray<ushort> WaterDistanceQ;
    }

    public struct BiomeTableBlob
    {
        public byte TempBins;
        public byte HumidBins;
        public ushort TempMaxQ;
        public ushort HumidMaxQ;
        public BlobArray<byte> BiomeIdLUT;
        public BlobArray<BiomeDef> Biomes;
    }

    public struct BiomeDef
    {
        public byte BiomeId;
        public TerrainParams Terrain;
        public CoreResourceParams Core;
        public VillageParams Village;
    }

    public struct TerrainParams
    {
        public ushort ElevationMinQ;
        public ushort ElevationMaxQ;
        public ushort RidgeStrengthQ;
        public ushort WarpStrengthQ;
    }

    public struct CoreResourceParams
    {
        public ushort FoodDensityQ;
        public ushort WoodDensityQ;
        public ushort StoneDensityQ;
        public ushort ClusterRadiusCm;
        public ushort ClusterJitterCm;
    }

    public struct VillageParams
    {
        public ushort MinSpacingCm;
        public ushort WaterProximityMinCm;
        public ushort WaterProximityMaxCm;
        public ushort SlopeMaxQ;
    }

    public struct WorldGenInput
    {
        public uint WorldSeed;
        public BlobAssetReference<ClimateMapBlob> Climate;
        public BlobAssetReference<BiomeTableBlob> Biomes;
        public WorldGenOptions Options;
    }

    public struct WorldGenOptions
    {
        public byte EnableBiomeDerive;
        public byte EnableResources;
        public byte EnableSettlements;
        public byte EnableFloraFauna;
        public byte EnableElevationGen;
    }

    public struct ResourceNodeSpawn
    {
        public byte ResourceType;
        public int2 Cell;
        public ushort LocalOffsetQx;
        public ushort LocalOffsetQy;
        public ushort RichnessQ;
        public ushort ClusterId;
    }

    public struct VillageSpawn
    {
        public int2 Cell;
        public ushort LocalOffsetQx;
        public ushort LocalOffsetQy;
        public byte BiomeId;
        public uint VillageSeed;
    }

    public struct StarterCacheSpawn
    {
        public int VillageIndex;
        public byte CacheType;
        public ushort FoodQ;
        public ushort WoodQ;
        public ushort StoneQ;
    }

    public struct WorldGenMetrics
    {
        public uint InputHash32;
        public ulong OutputHash64Lo;
        public ulong OutputHash64Hi;
        public int FoodCount;
        public int WoodCount;
        public int StoneCount;
        public int VillageCount;
        public ushort VillageNNMinCm;
        public ushort VillageNNMedianCm;
        public ushort VillageNNMaxCm;
        public ushort BootstrapCoverageQ;
        public ushort TempMinQ;
        public ushort TempMaxQ;
        public ushort HumidMinQ;
        public ushort HumidMaxQ;
        public ushort ElevMinQ;
        public ushort ElevMaxQ;
    }
}
