using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.WorldGen
{
    public enum WorldGenStageKind : byte
    {
        Unknown = 0,
        Topology = 1,
        Elevation = 2,
        Climate = 3,
        Hydrology = 4,
        Biomes = 5,
        Resources = 6,
        Ruins = 7,
        SurfaceFields = 8
    }

    public enum WorldGenParamType : byte
    {
        Float = 1,
        Int = 2,
        Bool = 3,
        FixedString64 = 4
    }

    public struct WorldGenParamBlob
    {
        public FixedString64Bytes Key;
        public WorldGenParamType Type;
        public float FloatValue;
        public int IntValue;
        public byte BoolValue;
        public FixedString64Bytes StringValue;
    }

    public struct WorldGenStageBlob
    {
        public WorldGenStageKind Kind;
        public uint SeedSalt;
        public BlobArray<WorldGenParamBlob> Parameters;
    }

    public struct WorldRecipeBlob
    {
        public uint SchemaVersion;
        public uint WorldSeed;
        public Hash128 DefinitionsHash;
        public BlobArray<WorldGenStageBlob> Stages;
    }

    public struct WorldRecipeComponent : IComponentData
    {
        public Hash128 RecipeHash;
        public BlobAssetReference<WorldRecipeBlob> Recipe;
    }
}
