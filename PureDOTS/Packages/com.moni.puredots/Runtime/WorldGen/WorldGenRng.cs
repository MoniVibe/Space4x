using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen
{
    public static class WorldGenRng
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static uint HashStep(uint hash, uint data)
        {
            return (hash ^ data) * FnvPrime;
        }

        public static uint ComputeSeed(
            uint worldSeed,
            WorldGenStageKind stageKind,
            uint stageIndex,
            uint stageSalt,
            int3 chunkCoord,
            uint stream = 0)
        {
            uint hash = FnvOffsetBasis;
            hash = HashStep(hash, worldSeed);
            hash = HashStep(hash, (uint)stageKind);
            hash = HashStep(hash, stageIndex);
            hash = HashStep(hash, stageSalt);
            hash = HashStep(hash, (uint)chunkCoord.x);
            hash = HashStep(hash, (uint)chunkCoord.y);
            hash = HashStep(hash, (uint)chunkCoord.z);
            hash = HashStep(hash, stream);

            // Unity.Mathematics.Random cannot be initialized with state=0.
            return hash == 0 ? 1u : hash;
        }

        public static uint ComputeStageSeed(
            uint worldSeed,
            WorldGenStageKind stageKind,
            uint stageIndex,
            uint stageSalt,
            uint stream = 0)
        {
            uint hash = FnvOffsetBasis;
            hash = HashStep(hash, worldSeed);
            hash = HashStep(hash, (uint)stageKind);
            hash = HashStep(hash, stageIndex);
            hash = HashStep(hash, stageSalt);
            hash = HashStep(hash, stream);
            return hash == 0 ? 1u : hash;
        }

        public static Random CreateRandom(uint seed)
        {
            return Random.CreateFromIndex(seed == 0 ? 1u : seed);
        }

        public static Random CreateStageChunkRandom(
            ref WorldRecipeBlob recipe,
            ref WorldGenStageBlob stage,
            uint stageIndex,
            int3 chunkCoord,
            uint stream = 0)
        {
            var seed = ComputeSeed(recipe.WorldSeed, stage.Kind, stageIndex, stage.SeedSalt, chunkCoord, stream);
            return CreateRandom(seed);
        }

        public static Random CreateStageChunkRandom(
            in WorldRecipeComponent recipe,
            uint stageIndex,
            int3 chunkCoord,
            uint stream = 0)
        {
            if (!recipe.Recipe.IsCreated)
            {
                return CreateRandom(1u);
            }

            ref var blob = ref recipe.Recipe.Value;
            if ((uint)stageIndex >= (uint)blob.Stages.Length)
            {
                return CreateRandom(1u);
            }

            ref var stage = ref blob.Stages[(int)stageIndex];
            return CreateStageChunkRandom(ref blob, ref stage, (uint)stageIndex, chunkCoord, stream);
        }
    }
}
