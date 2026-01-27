using PureDOTS.Runtime.Knowledge;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Knowledge
{
    /// <summary>
    /// Ensures incident learning buffers and default config exist.
    /// </summary>
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    public partial struct IncidentLearningBootstrapSystem : ISystem
    {
        private EntityQuery _missingMemory;

        public void OnCreate(ref SystemState state)
        {
            _missingMemory = SystemAPI.QueryBuilder()
                .WithAll<IncidentLearningAgent>()
                .WithNone<IncidentLearningMemory>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            EnsureSingletons(ref state);

            if (_missingMemory.IsEmptyIgnoreFilter)
            {
                return;
            }

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<IncidentLearningAgent>>()
                .WithNone<IncidentLearningMemory>()
                .WithEntityAccess())
            {
                ecb.AddBuffer<IncidentLearningMemory>(entity);
            }
            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void EnsureSingletons(ref SystemState state)
        {
            var em = state.EntityManager;
            Entity entity;
            using var configQuery = em.CreateEntityQuery(ComponentType.ReadOnly<IncidentLearningConfig>());
            if (configQuery.TryGetSingletonEntity<IncidentLearningConfig>(out var configEntity))
            {
                entity = configEntity;
            }
            else
            {
                using var bufferQuery = em.CreateEntityQuery(ComponentType.ReadOnly<IncidentLearningEventBuffer>());
                if (bufferQuery.TryGetSingletonEntity<IncidentLearningEventBuffer>(out var bufferEntity))
                {
                    entity = bufferEntity;
                }
                else
                {
                    entity = em.CreateEntity();
                }
            }

            if (!em.HasComponent<IncidentLearningConfig>(entity))
            {
                em.AddComponentData(entity, IncidentLearningConfig.Default);
            }

            if (!em.HasComponent<IncidentLearningEventBuffer>(entity))
            {
                em.AddComponent<IncidentLearningEventBuffer>(entity);
            }

            if (!em.HasBuffer<IncidentLearningEvent>(entity))
            {
                em.AddBuffer<IncidentLearningEvent>(entity);
            }
        }
    }
}
