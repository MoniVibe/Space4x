using PureDOTS.Runtime.Modularity;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Systems.Villagers
{
    /// <summary>
    /// Ensures village aggregates opt into the group knowledge module.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial struct VillageGroupKnowledgeBootstrapSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<VillageTag>()
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

            foreach (var (_, entity) in SystemAPI.Query<RefRO<VillageTag>>().WithEntityAccess())
            {
                if (!em.HasComponent<GroupKnowledgeModuleTag>(entity))
                {
                    ecb.AddComponent<GroupKnowledgeModuleTag>(entity);
                }
            }

            ecb.Playback(em);
        }
    }
}
