using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Scenarios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Profile
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct BehaviorDispositionSeedSystem : ISystem
    {
        private ComponentLookup<BehaviorDispositionDistribution> _distributionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BehaviorDispositionSeedRequest>();
            _distributionLookup = state.GetComponentLookup<BehaviorDispositionDistribution>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _distributionLookup.Update(ref state);

            var defaultDistribution = BehaviorDispositionDistribution.Default;
            uint seedSalt = 0u;
            if (SystemAPI.TryGetSingleton<BehaviorDispositionSeedConfig>(out var seedConfig))
            {
                defaultDistribution = seedConfig.Distribution;
                seedSalt = seedConfig.SeedSalt;
            }

            defaultDistribution = defaultDistribution.Sanitize();

            uint scenarioSeed = 0u;
            if (SystemAPI.TryGetSingleton<ScenarioInfo>(out var scenarioInfo))
            {
                scenarioSeed = scenarioInfo.Seed;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (request, entity) in SystemAPI.Query<RefRO<BehaviorDispositionSeedRequest>>()
                         .WithNone<BehaviorDisposition>()
                         .WithEntityAccess())
            {
                var distribution = defaultDistribution;
                if (_distributionLookup.HasComponent(entity))
                {
                    distribution = _distributionLookup[entity].Sanitize();
                }

                uint seed = request.ValueRO.Seed;
                if (seed == 0u)
                {
                    seed = math.hash(new uint4(
                        (uint)entity.Index + 1u,
                        (uint)entity.Version + 1u,
                        scenarioSeed + request.ValueRO.SeedSalt + seedSalt,
                        0x9E3779B9u));
                }
                if (seed == 0u)
                {
                    seed = 1u;
                }

                var random = Random.CreateFromIndex(seed);
                var disposition = distribution.Sample(ref random);
                ecb.AddComponent(entity, disposition);
                ecb.RemoveComponent<BehaviorDispositionSeedRequest>(entity);

                if (_distributionLookup.HasComponent(entity))
                {
                    ecb.RemoveComponent<BehaviorDispositionDistribution>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
