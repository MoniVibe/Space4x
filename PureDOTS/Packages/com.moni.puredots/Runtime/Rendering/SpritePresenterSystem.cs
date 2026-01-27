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
    /// Applies sprite/impostor presenter data so variants with RenderPresenterMask.Sprite can render.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ResolveRenderVariantSystem))]
    [UpdateBefore(typeof(Unity.Rendering.EntitiesGraphicsSystem))]
    public partial struct SpritePresenterSystem : ISystem
    {
        private EntityQuery _applyAllQuery;
        private EntityQuery _applyChangedQuery;
        private uint _lastCatalogVersion;
        private EntityQuery _missingMaterialMeshQuery;
        private EntityQuery _missingRenderBoundsQuery;
        private EntityQuery _missingRenderMeshArrayQuery;
        private EntityQuery _missingRenderFilterQuery;
        private TypeHandles _typeHandles;

        public void OnCreate(ref SystemState state)
        {
            _applyAllQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RenderVariantKey>()
                .WithAll<SpritePresenter>()
                .WithAll<MaterialMeshInfo>()
                .WithAll<RenderBounds>());

            _applyChangedQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RenderVariantKey>()
                .WithAll<SpritePresenter>()
                .WithAll<MaterialMeshInfo>()
                .WithAll<RenderBounds>());
            _applyChangedQuery.AddChangedVersionFilter(ComponentType.ReadOnly<SpritePresenter>());

            _missingMaterialMeshQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RenderVariantKey>()
                .WithAll<SpritePresenter>()
                .WithNone<MaterialMeshInfo>());

            _missingRenderBoundsQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RenderVariantKey>()
                .WithAll<SpritePresenter>()
                .WithNone<RenderBounds>());

            _missingRenderMeshArrayQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RenderVariantKey>()
                .WithAll<SpritePresenter>()
                .WithNone<RenderMeshArray>());

            _missingRenderFilterQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<RenderVariantKey>()
                .WithAll<SpritePresenter>()
                .WithNone<RenderFilterSettings>());

            state.RequireForUpdate<RenderPresentationCatalog>();
            _typeHandles = TypeHandles.Create(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out RenderPresentationCatalog catalog) || !catalog.Blob.IsCreated)
                return;

            if (!SystemAPI.TryGetSingleton<RenderCatalogVersion>(out var catalogVersion))
            {
                return;
            }

            if (!_missingMaterialMeshQuery.IsEmptyIgnoreFilter
                || !_missingRenderBoundsQuery.IsEmptyIgnoreFilter
                || !_missingRenderFilterQuery.IsEmptyIgnoreFilter)
            {
                EnsureCoreComponents(ref state);
                EnsureRenderFilterSettings(ref state);
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

            EnsureSharedComponentIfMissing(ref state, _missingRenderMeshArrayQuery, renderMeshArray);

            _typeHandles.Update(ref state);

            var job = new ApplySpritePresenterJob
            {
                Catalog = catalog.Blob,
                MeshCount = meshCount,
                MaterialCount = materialCount,
                SpritePresenterHandle = _typeHandles.SpritePresenterHandle,
                MaterialMeshHandle = _typeHandles.MaterialMeshHandle,
                RenderBoundsHandle = _typeHandles.RenderBoundsHandle
            };

            state.Dependency = job.ScheduleParallel(query, state.Dependency);
            _lastCatalogVersion = catalogVersion.Value;
        }

        [BurstCompile]
        private struct ApplySpritePresenterJob : IJobChunk
        {
            [ReadOnly] public BlobAssetReference<RenderPresentationCatalogBlob> Catalog;
            public int MeshCount;
            public int MaterialCount;

            [ReadOnly] public ComponentTypeHandle<SpritePresenter> SpritePresenterHandle;
            public ComponentTypeHandle<MaterialMeshInfo> MaterialMeshHandle;
            public ComponentTypeHandle<RenderBounds> RenderBoundsHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var spritePresenters = chunk.GetNativeArray(ref SpritePresenterHandle);
                var materialMeshes = chunk.GetNativeArray(ref MaterialMeshHandle);
                var renderBounds = chunk.GetNativeArray(ref RenderBoundsHandle);

                ref var catalog = ref Catalog.Value;
                if (catalog.Variants.Length == 0)
                    return;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var presenterIndex = spritePresenters[i].DefIndex;
                    if (presenterIndex == RenderPresentationConstants.UnassignedPresenterDefIndex)
                        continue;

                    var defIndex = math.clamp((int)presenterIndex, 0, catalog.Variants.Length - 1);
                    ref var variant = ref catalog.Variants[defIndex];
                    if ((variant.PresenterMask & RenderPresenterMask.Sprite) == 0)
                        continue;

                    var matIndex = math.clamp((int)variant.MaterialIndex, 0, math.max(MaterialCount - 1, 0));
                    var meshIndex = math.clamp((int)variant.MeshIndex, 0, math.max(MeshCount - 1, 0));

                    materialMeshes[i] = MaterialMeshInfo.FromRenderMeshArrayIndices((ushort)matIndex, (ushort)meshIndex, variant.SubMesh);

                    var localBounds = new AABB
                    {
                        Center = variant.BoundsCenter,
                        Extents = variant.BoundsExtents
                    };
                    renderBounds[i] = new RenderBounds { Value = localBounds };
                }
            }
        }

        private struct TypeHandles
        {
            public ComponentTypeHandle<SpritePresenter> SpritePresenterHandle;
            public ComponentTypeHandle<MaterialMeshInfo> MaterialMeshHandle;
            public ComponentTypeHandle<RenderBounds> RenderBoundsHandle;

            public static TypeHandles Create(ref SystemState state)
            {
                return new TypeHandles
                {
                    SpritePresenterHandle = state.GetComponentTypeHandle<SpritePresenter>(true),
                    MaterialMeshHandle = state.GetComponentTypeHandle<MaterialMeshInfo>(),
                    RenderBoundsHandle = state.GetComponentTypeHandle<RenderBounds>()
                };
            }

            public void Update(ref SystemState state)
            {
                SpritePresenterHandle.Update(ref state);
                MaterialMeshHandle.Update(ref state);
                RenderBoundsHandle.Update(ref state);
            }
        }

        private void EnsureCoreComponents(ref SystemState state)
        {
            EnsureComponentIfMissing(ref state, _missingMaterialMeshQuery,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0, 0));
            EnsureComponentIfMissing(ref state, _missingRenderBoundsQuery, new RenderBounds
            {
                Value = new AABB { Center = float3.zero, Extents = new float3(0.5f) }
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

        private static void EnsureComponentIfMissing<TComponent>(ref SystemState state, EntityQuery query, in TComponent value)
            where TComponent : unmanaged, IComponentData
        {
            if (query.IsEmptyIgnoreFilter)
                return;

            var entityManager = state.EntityManager;
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<TComponent>(entity))
                {
                    entityManager.SetComponentData(entity, value);
                }
                else
                {
                    entityManager.AddComponentData(entity, value);
                }
            }
        }

        private static void EnsureSharedComponentIfMissing(ref SystemState state, EntityQuery query, RenderMeshArray renderMeshArray)
        {
            if (query.IsEmptyIgnoreFilter || renderMeshArray == null)
                return;

            var entityManager = state.EntityManager;
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (!entityManager.HasComponent<RenderMeshArray>(entity))
                {
                    entityManager.AddSharedComponentManaged(entity, renderMeshArray);
                }
            }
        }
    }
}
