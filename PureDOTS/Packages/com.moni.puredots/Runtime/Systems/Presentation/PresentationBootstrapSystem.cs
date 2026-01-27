using PureDOTS.Runtime.Components;
using Unity.Entities;

namespace PureDOTS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct PresentationBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var entityManager = state.EntityManager;

            if (!SystemAPI.TryGetSingletonEntity<PresentationCommandQueue>(out var queueEntity))
            {
                queueEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(queueEntity, new PresentationCommandQueue());
            }

            if (!entityManager.HasComponent<PresentationRequestHub>(queueEntity))
            {
                entityManager.AddComponentData(queueEntity, new PresentationRequestHub());
            }

            if (!entityManager.HasComponent<PresentationRequestFailures>(queueEntity))
            {
                entityManager.AddComponentData(queueEntity, new PresentationRequestFailures());
            }

            if (!entityManager.HasBuffer<PresentationSpawnRequest>(queueEntity))
            {
                entityManager.AddBuffer<PresentationSpawnRequest>(queueEntity);
            }

            if (!entityManager.HasBuffer<PresentationRecycleRequest>(queueEntity))
            {
                entityManager.AddBuffer<PresentationRecycleRequest>(queueEntity);
            }

            if (!entityManager.HasBuffer<PlayEffectRequest>(queueEntity))
            {
                entityManager.AddBuffer<PlayEffectRequest>(queueEntity);
            }

            if (!entityManager.HasBuffer<SpawnCompanionRequest>(queueEntity))
            {
                entityManager.AddBuffer<SpawnCompanionRequest>(queueEntity);
            }

            if (!entityManager.HasBuffer<DespawnCompanionRequest>(queueEntity))
            {
                entityManager.AddBuffer<DespawnCompanionRequest>(queueEntity);
            }
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}

