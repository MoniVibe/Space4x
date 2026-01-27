using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;

namespace PureDOTS.Rendering
{
    /// <summary>
    /// Applies tracer presenter state by writing MaterialMeshInfo, RenderBounds, and tracer material properties.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ResolveRenderVariantSystem))]
    [UpdateBefore(typeof(Unity.Rendering.EntitiesGraphicsSystem))]
    public partial struct TracerPresenterSystem : ISystem
    {
        private EntityQuery _applyAllQuery;
        private EntityQuery _applyChangedQuery;
        private EntityQuery _missingMaterialMeshQuery;
        private EntityQuery _missingRenderBoundsQuery;
        private EntityQuery _missingRenderFilterQuery;
        private EntityQuery _missingShapePropertyQuery;
        private EntityQuery _missingColorPropertyQuery;
        private TypeHandles _typeHandles;
        private uint _lastCatalogVersion;

        public void OnCreate(ref SystemState state)
        {
            _applyAllQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RenderVariantKey>()
                .WithAll<TracerPresenter>()
                .WithAll<MaterialMeshInfo>()
                .WithAll<RenderBounds>()
                .WithAll<TracerShapeProperty>()
                .WithAll<TracerColorProperty>());

            _applyChangedQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RenderVariantKey>()
                .WithAll<TracerPresenter>()
                .WithAll<MaterialMeshInfo>()
                .WithAll<RenderBounds>()
                .WithAll<TracerShapeProperty>()
                .WithAll<TracerColorProperty>());
            _applyChangedQuery.AddChangedVersionFilter(ComponentType.ReadOnly<TracerPresenter>());

            _missingMaterialMeshQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RenderVariantKey>()
                .WithAll<TracerPresenter>()
                .WithNone<MaterialMeshInfo>());

            _missingRenderBoundsQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RenderVariantKey>()
                .WithAll<TracerPresenter>()
                .WithNone<RenderBounds>());

            _missingRenderFilterQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RenderVariantKey>()
                .WithAll<TracerPresenter>()
                .WithNone<RenderFilterSettings>());

            _missingShapePropertyQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RenderVariantKey>()
                .WithAll<TracerPresenter>()
                .WithNone<TracerShapeProperty>());

            _missingColorPropertyQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RenderVariantKey>()
                .WithAll<TracerPresenter>()
                .WithNone<TracerColorProperty>());

            state.RequireForUpdate<RenderPresentationCatalog>();
            _typeHandles = TypeHandles.Create(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out RenderPresentationCatalog catalog) || !catalog.Blob.IsCreated)
                return;

            if (!SystemAPI.TryGetSingleton<RenderCatalogVersion>(out var catalogVersion))
                return;

            if (!_missingMaterialMeshQuery.IsEmptyIgnoreFilter
                || !_missingRenderBoundsQuery.IsEmptyIgnoreFilter
                || !_missingRenderFilterQuery.IsEmptyIgnoreFilter
                || !_missingShapePropertyQuery.IsEmptyIgnoreFilter
                || !_missingColorPropertyQuery.IsEmptyIgnoreFilter)
            {
                EnsureCoreComponents(ref state);
            }

            var catalogChanged = catalogVersion.Value != _lastCatalogVersion;
            var query = catalogChanged ? _applyAllQuery : _applyChangedQuery;
            if (query.IsEmptyIgnoreFilter)
            {
                if (catalogChanged)
                {
                    _lastCatalogVersion = catalogVersion.Value;
                }
                return;
            }

            var renderMeshArray = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(catalog.RenderMeshArrayEntity);
            var meshCount = renderMeshArray.MeshReferences?.Length ?? 0;
            var materialCount = renderMeshArray.MaterialReferences?.Length ?? 0;
            if (meshCount == 0 || materialCount == 0)
            {
                return;
            }

            _typeHandles.Update(ref state);

            var job = new ApplyTracerPresenterJob
            {
                Catalog = catalog.Blob,
                MeshCount = meshCount,
                MaterialCount = materialCount,
                TracerPresenterHandle = _typeHandles.TracerPresenterHandle,
                MaterialMeshHandle = _typeHandles.MaterialMeshHandle,
                RenderBoundsHandle = _typeHandles.RenderBoundsHandle,
                TracerShapeHandle = _typeHandles.TracerShapeHandle,
                TracerColorHandle = _typeHandles.TracerColorHandle,
                ProjectileVisualHandle = _typeHandles.ProjectileVisualHandle
            };

            state.Dependency = job.ScheduleParallel(query, state.Dependency);
            _lastCatalogVersion = catalogVersion.Value;
        }

        [BurstCompile]
        private struct ApplyTracerPresenterJob : IJobChunk
        {
            [ReadOnly] public BlobAssetReference<RenderPresentationCatalogBlob> Catalog;
            public int MeshCount;
            public int MaterialCount;

            [ReadOnly] public ComponentTypeHandle<TracerPresenter> TracerPresenterHandle;
            public ComponentTypeHandle<MaterialMeshInfo> MaterialMeshHandle;
            public ComponentTypeHandle<RenderBounds> RenderBoundsHandle;
            public ComponentTypeHandle<TracerShapeProperty> TracerShapeHandle;
            public ComponentTypeHandle<TracerColorProperty> TracerColorHandle;
            [ReadOnly] public ComponentTypeHandle<ProjectileVisual> ProjectileVisualHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var tracerPresenters = chunk.GetNativeArray(ref TracerPresenterHandle);
                var materialMeshes = chunk.GetNativeArray(ref MaterialMeshHandle);
                var renderBounds = chunk.GetNativeArray(ref RenderBoundsHandle);
                var tracerShapes = chunk.GetNativeArray(ref TracerShapeHandle);
                var tracerColors = chunk.GetNativeArray(ref TracerColorHandle);
                var hasProjectileVisual = chunk.Has(ref ProjectileVisualHandle);
                var projectileVisuals = hasProjectileVisual ? chunk.GetNativeArray(ref ProjectileVisualHandle) : default;

                ref var catalog = ref Catalog.Value;
                if (catalog.Variants.Length == 0)
                    return;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var presenterIndex = tracerPresenters[i].DefIndex;
                    if (presenterIndex == RenderPresentationConstants.UnassignedPresenterDefIndex)
                        continue;

                    var defIndex = math.clamp((int)presenterIndex, 0, catalog.Variants.Length - 1);
                    ref var variant = ref catalog.Variants[defIndex];
                    if ((variant.PresenterMask & RenderPresenterMask.Tracer) == 0)
                        continue;

                    var matIndex = math.clamp((int)variant.MaterialIndex, 0, math.max(MaterialCount - 1, 0));
                    var meshIndex = math.clamp((int)variant.MeshIndex, 0, math.max(MeshCount - 1, 0));
                    materialMeshes[i] = MaterialMeshInfo.FromRenderMeshArrayIndices((ushort)matIndex, (ushort)meshIndex, variant.SubMesh);

                    var width = math.max(variant.TracerWidth, 0.01f);
                    var length = math.max(variant.TracerLength, 0.01f);
                    var color = variant.TracerColor;

                    if (hasProjectileVisual)
                    {
                        var visual = projectileVisuals[i];
                        if (visual.Width > 0f)
                            width = visual.Width;
                        if (visual.Length > 0f)
                            length = visual.Length;
                        if (!visual.Color.Equals(float4.zero))
                            color = visual.Color;
                    }

                    tracerShapes[i] = new TracerShapeProperty
                    {
                        Value = new float2(width, length)
                    };
                    tracerColors[i] = new TracerColorProperty
                    {
                        Value = color
                    };

                    var extents = variant.BoundsExtents;
                    extents.x = math.max(extents.x, width * 0.5f);
                    extents.y = math.max(extents.y, width * 0.5f);
                    extents.z = math.max(extents.z, length * 0.5f);

                    renderBounds[i] = new RenderBounds
                    {
                        Value = new AABB
                        {
                            Center = variant.BoundsCenter,
                            Extents = extents
                        }
                    };
                }
            }
        }

        private void EnsureCoreComponents(ref SystemState state)
        {
            EnsureComponentIfMissing(ref state, _missingMaterialMeshQuery, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0, 0));
            EnsureComponentIfMissing(ref state, _missingRenderBoundsQuery, new RenderBounds
            {
                Value = new AABB { Center = float3.zero, Extents = new float3(0.5f) }
            });
            EnsureRenderFilterSettings(ref state);
            EnsureComponentIfMissing(ref state, _missingShapePropertyQuery, new TracerShapeProperty
            {
                Value = new float2(0.25f, 1f)
            });
            EnsureComponentIfMissing(ref state, _missingColorPropertyQuery, new TracerColorProperty
            {
                Value = new float4(1f, 1f, 1f, 1f)
            });
        }

        private void EnsureRenderFilterSettings(ref SystemState state)
        {
            if (_missingRenderFilterQuery.IsEmptyIgnoreFilter)
                return;

            var entityManager = state.EntityManager;
            using var entities = _missingRenderFilterQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                entityManager.AddSharedComponentManaged(entity, RenderFilterSettings.Default);
            }
        }

        private static void EnsureComponentIfMissing<T>(ref SystemState state, EntityQuery query, in T value)
            where T : unmanaged, IComponentData
        {
            if (query.IsEmptyIgnoreFilter)
                return;

            var entityManager = state.EntityManager;
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<T>(entity))
                {
                    entityManager.SetComponentData(entity, value);
                }
                else
                {
                    entityManager.AddComponentData(entity, value);
                }
            }
        }

        private struct TypeHandles
        {
            public ComponentTypeHandle<TracerPresenter> TracerPresenterHandle;
            public ComponentTypeHandle<MaterialMeshInfo> MaterialMeshHandle;
            public ComponentTypeHandle<RenderBounds> RenderBoundsHandle;
            public ComponentTypeHandle<TracerShapeProperty> TracerShapeHandle;
            public ComponentTypeHandle<TracerColorProperty> TracerColorHandle;
            public ComponentTypeHandle<ProjectileVisual> ProjectileVisualHandle;

            public static TypeHandles Create(ref SystemState state)
            {
                return new TypeHandles
                {
                    TracerPresenterHandle = state.GetComponentTypeHandle<TracerPresenter>(true),
                    MaterialMeshHandle = state.GetComponentTypeHandle<MaterialMeshInfo>(),
                    RenderBoundsHandle = state.GetComponentTypeHandle<RenderBounds>(),
                    TracerShapeHandle = state.GetComponentTypeHandle<TracerShapeProperty>(),
                    TracerColorHandle = state.GetComponentTypeHandle<TracerColorProperty>(),
                    ProjectileVisualHandle = state.GetComponentTypeHandle<ProjectileVisual>(true)
                };
            }

            public void Update(ref SystemState state)
            {
                TracerPresenterHandle.Update(ref state);
                MaterialMeshHandle.Update(ref state);
                RenderBoundsHandle.Update(ref state);
                TracerShapeHandle.Update(ref state);
                TracerColorHandle.Update(ref state);
                ProjectileVisualHandle.Update(ref state);
            }
        }
    }
}
