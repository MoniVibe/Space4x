using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Knowledge;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Editor/development helper that removes duplicate singleton components to keep dev playmode productive.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct SingletonDeduperSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!ShouldRun())
            {
                state.Enabled = false;
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!ShouldRun())
            {
                state.Enabled = false;
                return;
            }

            var entityManager = state.EntityManager;
            var removed = 0;
            removed += DedupeComponent<TimeState>(entityManager);
            removed += DedupeComponent<TickTimeState>(entityManager);
            removed += DedupeComponent<HistorySettings>(entityManager);
            removed += DedupeComponent<SpatialGridConfig>(entityManager);
            removed += DedupeComponent<SpatialGridState>(entityManager);
            removed += DedupeComponent<RewindState>(entityManager);
            removed += DedupeComponent<KnowledgeLessonEffectCatalog>(entityManager);

            if (removed > 0)
            {
                UnityEngine.Debug.LogWarning($"[SingletonDeduperSystem] Removed {removed} duplicate singleton component(s). Please clean up duplicate authoring objects.");
            }

            state.Enabled = false;
        }

        private static bool ShouldRun()
        {
#if UNITY_EDITOR
            return true;
#else
            return UnityEngine.Debug.isDebugBuild;
#endif
        }

        private static int DedupeComponent<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length <= 1)
            {
                return 0;
            }

            for (var i = 1; i < entities.Length; i++)
            {
                if (entityManager.HasComponent<T>(entities[i]))
                {
                    entityManager.RemoveComponent<T>(entities[i]);
                }
            }

            return entities.Length - 1;
        }
    }
}
