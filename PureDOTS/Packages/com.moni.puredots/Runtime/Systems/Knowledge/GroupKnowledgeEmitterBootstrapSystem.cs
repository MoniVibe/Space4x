using PureDOTS.Runtime.Knowledge;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Villagers;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Knowledge
{
    /// <summary>
    /// Adds emitter throttle state to entities that should publish group knowledge updates.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    public partial struct GroupKnowledgeEmitterBootstrapSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<VillagerVillageRef, PerceptionState>()
                .WithNone<GroupKnowledgeEmitterState>()
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

            foreach (var (_, _, entity) in SystemAPI.Query<RefRO<VillagerVillageRef>, RefRO<PerceptionState>>()
                .WithNone<GroupKnowledgeEmitterState>()
                .WithEntityAccess())
            {
                ecb.AddComponent(entity, new GroupKnowledgeEmitterState());
            }

            ecb.Playback(em);
        }
    }
}
