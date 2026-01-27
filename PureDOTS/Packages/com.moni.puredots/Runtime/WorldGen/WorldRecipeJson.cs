using System;

namespace PureDOTS.Runtime.WorldGen
{
    [Serializable]
    public sealed class WorldRecipeJson
    {
        public uint schemaVersion = WorldGenSchema.WorldRecipeSchemaVersion;
        public uint worldSeed;
        public string definitionsHash;
        public WorldGenStageJson[] stages;
    }

    [Serializable]
    public sealed class WorldGenStageJson
    {
        public string kind;
        public uint seedSalt;
        public WorldGenParamJson[] parameters;
    }

    [Serializable]
    public sealed class WorldGenParamJson
    {
        public string key;
        public string type;
        public float floatValue;
        public int intValue;
        public bool boolValue;
        public string stringValue;
    }
}

