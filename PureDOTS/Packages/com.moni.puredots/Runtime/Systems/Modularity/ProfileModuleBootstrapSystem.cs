using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Modularity;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Systems.Modularity
{
    /// <summary>
    /// Ensures entities tagged with <see cref="ProfileModuleTag"/> have the required profile components.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial struct ProfileModuleBootstrapSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<ProfileModuleTag>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<ProfileModuleTag>>().WithEntityAccess())
            {
                if (!em.HasComponent<AlignmentTriplet>(entity))
                {
                    ecb.AddComponent(entity, AlignmentTriplet.FromFloats(0f, 0f, 0f));
                }

                if (!em.HasComponent<PersonalityAxes>(entity))
                {
                    ecb.AddComponent(entity, PersonalityAxes.FromValues(0f, 0f, 0f, 0f, 0f));
                }

                if (!em.HasComponent<MoraleState>(entity))
                {
                    ecb.AddComponent(entity, MoraleState.FromValues(0f, 0f));
                }

                if (!em.HasComponent<BehaviorTuning>(entity))
                {
                    ecb.AddComponent(entity, BehaviorTuning.Neutral());
                }
            }

            ecb.Playback(em);
        }
    }
}
