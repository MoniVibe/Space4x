using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.WorldGen
{
    public struct WorldGenBiomeDefinitionBlob
    {
        public FixedString64Bytes Id;
        public float Weight;
        public float TemperatureMin;
        public float TemperatureMax;
        public float MoistureMin;
        public float MoistureMax;
    }

    public struct WorldGenResourceDefinitionBlob
    {
        public FixedString64Bytes Id;
        public float Scarcity;
        public FixedString64Bytes BiomeHint;
    }

    public struct WorldGenRuinSetDefinitionBlob
    {
        public FixedString64Bytes Id;
        public float Weight;
        public float TechLevelMin;
        public float TechLevelMax;
    }

    public struct WorldGenDefinitionsBlob
    {
        public uint SchemaVersion;
        public BlobArray<WorldGenBiomeDefinitionBlob> Biomes;
        public BlobArray<WorldGenResourceDefinitionBlob> Resources;
        public BlobArray<WorldGenRuinSetDefinitionBlob> RuinSets;
    }

    public struct WorldGenDefinitionsComponent : IComponentData
    {
        public Hash128 DefinitionsHash;
        public BlobAssetReference<WorldGenDefinitionsBlob> Definitions;
    }
}

