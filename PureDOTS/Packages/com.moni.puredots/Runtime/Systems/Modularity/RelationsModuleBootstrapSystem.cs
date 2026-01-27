using PureDOTS.Runtime.Modularity;
using PureDOTS.Runtime.Relations;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Systems.Modularity
{
    /// <summary>
    /// Ensures entities tagged with <see cref="RelationsModuleTag"/> have the required relationship buffers/state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial struct RelationsModuleBootstrapSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<RelationsModuleTag>()
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

            foreach (var (_, entity) in SystemAPI.Query<RefRO<RelationsModuleTag>>().WithEntityAccess())
            {
                if (!em.HasBuffer<PersonalRelation>(entity))
                {
                    ecb.AddBuffer<PersonalRelation>(entity);
                }
            }

            ecb.Playback(em);
        }
    }
}
