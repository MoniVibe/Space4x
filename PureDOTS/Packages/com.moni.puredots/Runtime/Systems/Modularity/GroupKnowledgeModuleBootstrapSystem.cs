using PureDOTS.Runtime.Knowledge;
using PureDOTS.Runtime.Modularity;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Systems.Modularity
{
    /// <summary>
    /// Ensures entities tagged with <see cref="GroupKnowledgeModuleTag"/> have group knowledge cache components/buffers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial struct GroupKnowledgeModuleBootstrapSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<GroupKnowledgeModuleTag>()
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

            foreach (var (_, entity) in SystemAPI.Query<RefRO<GroupKnowledgeModuleTag>>().WithEntityAccess())
            {
                if (!em.HasComponent<GroupKnowledgeCache>(entity))
                {
                    ecb.AddComponent(entity, new GroupKnowledgeCache());
                }

                if (!em.HasComponent<GroupKnowledgeConfig>(entity))
                {
                    ecb.AddComponent(entity, GroupKnowledgeConfig.Default);
                }

                if (!em.HasBuffer<GroupKnowledgeEntry>(entity))
                {
                    ecb.AddBuffer<GroupKnowledgeEntry>(entity);
                }
            }

            ecb.Playback(em);
        }
    }
}
