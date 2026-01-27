using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace PureDOTS.Rendering
{
    /// <summary>
    /// Development-only guard that verifies render contract assumptions so spawn/bake paths don't regress.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PresentationContractValidationSystem : ISystem
    {
        private EntityQuery _materialMeshWithoutPresenterQuery;
        private EntityQuery _missingSemanticKeyQuery;

        private bool _reportedMissingPresenter;
        private bool _reportedMissingSemanticKey;

        public void OnCreate(ref SystemState state)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            state.Enabled = false;
            return;
#else
            var queryOptions = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab;

            _materialMeshWithoutPresenterQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<MaterialMeshInfo>(),
                    ComponentType.ReadOnly<RenderVariantKey>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<MeshPresenter>(),
                    ComponentType.ReadOnly<SpritePresenter>(),
                    ComponentType.ReadOnly<DebugPresenter>(),
                    ComponentType.ReadOnly<TracerPresenter>()
                },
                Options = queryOptions
            });

            _missingSemanticKeyQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<RenderVariantKey>() },
                None = new[] { ComponentType.ReadOnly<RenderSemanticKey>() },
                Options = queryOptions
            });

            state.RequireForUpdate<RenderPresentationCatalog>();
#endif
        }

        public void OnUpdate(ref SystemState state)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            state.Enabled = false;
            return;
#else
            ReportIfNeeded(ref state, _materialMeshWithoutPresenterQuery, ref _reportedMissingPresenter,
                "[PresentationContractValidationSystem] MaterialMeshInfo detected on an archetype with no presenter (Mesh/Sprite/Tracer/Debug). Spawn/bake code must add at least one presenter component.");

            ReportIfNeeded(ref state, _missingSemanticKeyQuery, ref _reportedMissingSemanticKey,
                "[PresentationContractValidationSystem] RenderVariantKey detected without RenderSemanticKey. Author semantic keys so variant resolution can map catalog entries.");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void ReportIfNeeded(ref SystemState state, EntityQuery query, ref bool wasReported, string message)
        {
            if (wasReported)
                return;

            query.ResetFilter();
            var count = query.CalculateEntityCount();
            using var entities = query.ToEntityArray(Allocator.Temp);
            var worldName = state.WorldUnmanaged.Name;
            if (entities.Length == 0)
            {
                wasReported = false;
                return;
            }

            Debug.LogError($"{message} (count={count} array={entities.Length} world={worldName})");

            if (entities.Length > 0)
            {
                var entity = entities[0];
                var entityManager = state.EntityManager;
                var name = entityManager.GetName(entity);
                var isPrefab = entityManager.HasComponent<Prefab>(entity);
                var isDisabled = entityManager.HasComponent<Disabled>(entity);
                var hasSemantic = entityManager.HasComponent<RenderSemanticKey>(entity);
                var hasVariant = entityManager.HasComponent<RenderVariantKey>(entity);
                var variantValue = hasVariant ? entityManager.GetComponentData<RenderVariantKey>(entity).Value : -1;
                var semanticValue = hasSemantic ? entityManager.GetComponentData<RenderSemanticKey>(entity).Value : (ushort)0;
                var hasMeshPresenter = entityManager.HasComponent<MeshPresenter>(entity);
                var hasSpritePresenter = entityManager.HasComponent<SpritePresenter>(entity);
                var hasDebugPresenter = entityManager.HasComponent<DebugPresenter>(entity);
                var hasTracerPresenter = entityManager.HasComponent<TracerPresenter>(entity);

                Debug.LogError("[PresentationContractValidationSystem] Offender entity=" + entity +
                               " name='" + name + "'" +
                               " prefab=" + isPrefab +
                               " disabled=" + isDisabled +
                               " hasSemantic=" + hasSemantic +
                               " semantic=" + semanticValue +
                               " hasVariant=" + hasVariant +
                               " variant=" + variantValue +
                               " presenters(mesh=" + hasMeshPresenter +
                               " sprite=" + hasSpritePresenter +
                               " debug=" + hasDebugPresenter +
                               " tracer=" + hasTracerPresenter + ")");
            }
            wasReported = true;
        }
#endif

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
