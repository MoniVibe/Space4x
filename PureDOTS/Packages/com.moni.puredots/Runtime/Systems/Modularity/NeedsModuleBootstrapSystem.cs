using PureDOTS.Runtime.Modularity;
using PureDOTS.Runtime.Needs;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Systems.Modularity
{
    /// <summary>
    /// Ensures entities tagged with <see cref="NeedsModuleTag"/> have the required needs components/buffers.
    /// This keeps entities blank-by-default while allowing a single opt-in tag to attach the module.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial struct NeedsModuleBootstrapSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<NeedsModuleTag>()
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

            foreach (var (_, entity) in SystemAPI.Query<RefRO<NeedsModuleTag>>().WithEntityAccess())
            {
                if (!em.HasComponent<NeedsActivityState>(entity))
                {
                    ecb.AddComponent(entity, new NeedsActivityState
                    {
                        Current = ActivityState.Idle,
                        StateStartTick = 0,
                        StateDuration = 0f
                    });
                }

                bool hasEntityNeeds = em.HasComponent<EntityNeeds>(entity);
                bool hasNeedEntries = em.HasBuffer<NeedEntry>(entity);

                if (!hasEntityNeeds && !hasNeedEntries)
                {
                    ecb.AddComponent(entity, NeedsHelpers.CreateDefault());
                }

                if (!em.HasBuffer<NeedCriticalEvent>(entity))
                {
                    ecb.AddBuffer<NeedCriticalEvent>(entity);
                }
            }

            ecb.Playback(em);
        }
    }
}
