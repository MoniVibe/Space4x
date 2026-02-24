using PureDOTS.Rendering;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Rendering;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// Ensures any entity with a semantic render key has presenter components and at least one presenter enabled
    /// before validation runs.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLifecycleSystem))]
    [UpdateAfter(typeof(PureDOTS.Rendering.ResolveRenderVariantSystem))]
    public partial struct Space4XRenderPresenterRepairSystem : ISystem
    {
        private EntityQuery _semanticQuery;
        private EntityQuery _missingPresenterQuery;
        private EntityQuery _carrierQuery;
        private EntityQuery _minerQuery;
        private EntityQuery _asteroidQuery;
        private EntityQuery _strikeCraftQuery;
        private EntityQuery _individualQuery;
        private EntityQuery _projectileQuery;
        private EntityQuery _fleetImpostorQuery;
        private EntityQuery _debrisQuery;
        private EntityQuery _meshBindingRepairQuery;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _loggedOnce;
        private int _frameCounter;
        private int _lastMissingCount;
#endif

        public void OnCreate(ref SystemState state)
        {
            _semanticQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<RenderSemanticKey>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });

            _missingPresenterQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<RenderSemanticKey>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<MeshPresenter>(),
                    ComponentType.ReadOnly<SpritePresenter>(),
                    ComponentType.ReadOnly<DebugPresenter>(),
                    ComponentType.ReadOnly<TracerPresenter>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });

            _carrierQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Carrier>() },
                None = new[] { ComponentType.ReadOnly<RenderSemanticKey>() },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });

            _minerQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<MiningVessel>() },
                None = new[] { ComponentType.ReadOnly<RenderSemanticKey>() },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });

            _asteroidQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Asteroid>() },
                None = new[] { ComponentType.ReadOnly<RenderSemanticKey>() },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });

            _strikeCraftQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<StrikeCraftProfile>() },
                None = new[] { ComponentType.ReadOnly<RenderSemanticKey>() },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });

            _individualQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<SimIndividualTag>() },
                None = new[] { ComponentType.ReadOnly<RenderSemanticKey>() },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });

            _projectileQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<ProjectileEntity>() },
                None = new[] { ComponentType.ReadOnly<RenderSemanticKey>() },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });

            _fleetImpostorQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<FleetImpostorTag>() },
                None = new[] { ComponentType.ReadOnly<RenderSemanticKey>() },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });

            _debrisQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Space4XDebrisTag>() },
                None = new[] { ComponentType.ReadOnly<RenderSemanticKey>() },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });

            _meshBindingRepairQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<RenderVariantKey>(),
                    ComponentType.ReadOnly<MeshPresenter>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _frameCounter++;
            if (!_loggedOnce)
            {
                _loggedOnce = true;
                var semanticCount = _semanticQuery.CalculateEntityCount();
                var missingCount = _missingPresenterQuery.CalculateEntityCount();
                UnityEngine.Debug.Log(
                    $"[Space4XRenderPresenterRepair] World='{state.WorldUnmanaged.Name}' semantic={semanticCount} missingPresenter={missingCount} carriersMissing={_carrierQuery.CalculateEntityCount()} minersMissing={_minerQuery.CalculateEntityCount()} asteroidsMissing={_asteroidQuery.CalculateEntityCount()} strikeCraftMissing={_strikeCraftQuery.CalculateEntityCount()} individualsMissing={_individualQuery.CalculateEntityCount()} projectilesMissing={_projectileQuery.CalculateEntityCount()} fleetImpostorMissing={_fleetImpostorQuery.CalculateEntityCount()} debrisMissing={_debrisQuery.CalculateEntityCount()}");
            }
#endif
            RepairMissingPresenterContracts(ref state);
            RepairCoreSemanticContracts(ref state);
            RepairMeshBindings(ref state);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var missingAfter = _missingPresenterQuery.CalculateEntityCount();
            if (missingAfter > 0 && (missingAfter != _lastMissingCount || (_frameCounter % 120) == 0))
            {
                _lastMissingCount = missingAfter;
                using var missing = _missingPresenterQuery.ToEntityArray(Allocator.Temp);
                var sampleCount = math.min(3, missing.Length);
                for (var i = 0; i < sampleCount; i++)
                {
                    var entity = missing[i];
                    if (!entityManager.Exists(entity))
                    {
                        continue;
                    }

                    var semantic = entityManager.HasComponent<RenderSemanticKey>(entity)
                        ? entityManager.GetComponentData<RenderSemanticKey>(entity).Value
                        : (ushort)0;
                    var hasMesh = entityManager.HasComponent<MeshPresenter>(entity);
                    var hasSprite = entityManager.HasComponent<SpritePresenter>(entity);
                    var hasDebug = entityManager.HasComponent<DebugPresenter>(entity);
                    var hasTracer = entityManager.HasComponent<TracerPresenter>(entity);
                    UnityEngine.Debug.Log(
                        $"[Space4XRenderPresenterRepair] missing_after={missingAfter} sample={i} entity={entity} semantic={semantic} mesh={hasMesh} sprite={hasSprite} debug={hasDebug} tracer={hasTracer}");
                }
            }
#endif

            if (_semanticQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var entities = _semanticQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!entityManager.Exists(entity))
                {
                    continue;
                }

                var hasMesh = entityManager.HasComponent<MeshPresenter>(entity);
                var hasSprite = entityManager.HasComponent<SpritePresenter>(entity);
                var hasDebug = entityManager.HasComponent<DebugPresenter>(entity);
                var hasTracer = entityManager.HasComponent<TracerPresenter>(entity);

                if (!hasMesh)
                {
                    entityManager.AddComponentData(entity, new MeshPresenter
                    {
                        DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                    });
                    hasMesh = true;
                }

                if (!hasSprite)
                {
                    entityManager.AddComponentData(entity, new SpritePresenter
                    {
                        DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                    });
                    hasSprite = true;
                }

                if (!hasDebug)
                {
                    entityManager.AddComponentData(entity, new DebugPresenter
                    {
                        DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                    });
                    hasDebug = true;
                }

                if (!hasTracer)
                {
                    entityManager.AddComponentData(entity, new TracerPresenter
                    {
                        DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                    });
                    hasTracer = true;
                }

                var meshEnabled = hasMesh && entityManager.IsComponentEnabled<MeshPresenter>(entity);
                var spriteEnabled = hasSprite && entityManager.IsComponentEnabled<SpritePresenter>(entity);
                var debugEnabled = hasDebug && entityManager.IsComponentEnabled<DebugPresenter>(entity);
                var tracerEnabled = hasTracer && entityManager.IsComponentEnabled<TracerPresenter>(entity);
                var hasEnabledPresenter = meshEnabled || spriteEnabled || debugEnabled || tracerEnabled;

                if (!hasEnabledPresenter)
                {
                    entityManager.SetComponentEnabled<MeshPresenter>(entity, true);
                }
            }
        }

        private void RepairMissingPresenterContracts(ref SystemState state)
        {
            if (_missingPresenterQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var em = state.EntityManager;
            using var missing = _missingPresenterQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < missing.Length; i++)
            {
                var entity = missing[i];
                if (!em.Exists(entity) || !em.HasComponent<RenderSemanticKey>(entity))
                {
                    continue;
                }

                var semantic = em.GetComponentData<RenderSemanticKey>(entity).Value;
                EnsureCoreRenderContract(em, entity, semantic);
            }
        }

        private void RepairCoreSemanticContracts(ref SystemState state)
        {
            var em = state.EntityManager;
            RepairSemanticOnQuery(ref state, _carrierQuery, Space4XRenderKeys.Carrier);
            RepairSemanticOnQuery(ref state, _minerQuery, Space4XRenderKeys.Miner);
            RepairSemanticOnQuery(ref state, _asteroidQuery, Space4XRenderKeys.Asteroid);
            RepairSemanticOnQuery(ref state, _strikeCraftQuery, Space4XRenderKeys.StrikeCraft);
            RepairSemanticOnQuery(ref state, _individualQuery, Space4XRenderKeys.Individual);
            RepairSemanticOnQuery(ref state, _projectileQuery, Space4XRenderKeys.Projectile);
            RepairSemanticOnQuery(ref state, _fleetImpostorQuery, Space4XRenderKeys.FleetImpostor);
            RepairSemanticOnQuery(ref state, _debrisQuery, Space4XRenderKeys.ResourcePickup);

            // Also repair partial contracts on already-semantic entities.
            using var semanticEntities = _semanticQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < semanticEntities.Length; i++)
            {
                var entity = semanticEntities[i];
                if (!em.Exists(entity))
                {
                    continue;
                }

                var semantic = em.GetComponentData<RenderSemanticKey>(entity).Value;
                EnsureCoreRenderContract(em, entity, semantic);
            }
        }

        private static void RepairSemanticOnQuery(ref SystemState state, EntityQuery query, ushort semantic)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var em = state.EntityManager;
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.Exists(entity))
                {
                    continue;
                }

                EnsureCoreRenderContract(em, entity, semantic);
            }
        }

        private static void EnsureCoreRenderContract(EntityManager em, Entity entity, ushort semantic)
        {
            if (!em.HasComponent<RenderSemanticKey>(entity))
            {
                em.AddComponentData(entity, new RenderSemanticKey { Value = semantic });
            }

            if (!em.HasComponent<RenderKey>(entity))
            {
                em.AddComponentData(entity, new RenderKey
                {
                    ArchetypeId = semantic,
                    LOD = 0
                });
            }

            if (!em.HasComponent<RenderFlags>(entity))
            {
                em.AddComponentData(entity, new RenderFlags
                {
                    Visible = 1,
                    ShadowCaster = 1,
                    HighlightMask = 0
                });
            }

            if (!em.HasComponent<RenderVariantKey>(entity))
            {
                em.AddComponentData(entity, new RenderVariantKey { Value = 0 });
            }

            if (!em.HasComponent<RenderThemeOverride>(entity))
            {
                em.AddComponentData(entity, new RenderThemeOverride { Value = 0 });
                em.SetComponentEnabled<RenderThemeOverride>(entity, false);
            }

            if (!em.HasComponent<MeshPresenter>(entity))
            {
                em.AddComponentData(entity, new MeshPresenter
                {
                    DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                });
            }
            if (em.HasComponent<MeshPresenter>(entity))
            {
                var meshPresenter = em.GetComponentData<MeshPresenter>(entity);
                if (meshPresenter.DefIndex == RenderPresentationConstants.UnassignedPresenterDefIndex)
                {
                    var fallbackVariant = 0;
                    if (em.HasComponent<RenderVariantKey>(entity))
                    {
                        fallbackVariant = math.max(0, em.GetComponentData<RenderVariantKey>(entity).Value);
                    }

                    meshPresenter.DefIndex = (ushort)math.min(fallbackVariant, RenderPresentationConstants.UnassignedPresenterDefIndex - 1);
                    em.SetComponentData(entity, meshPresenter);
                }
            }
            if (!em.IsComponentEnabled<MeshPresenter>(entity))
            {
                em.SetComponentEnabled<MeshPresenter>(entity, true);
            }

            if (!em.HasComponent<SpritePresenter>(entity))
            {
                em.AddComponentData(entity, new SpritePresenter
                {
                    DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                });
                em.SetComponentEnabled<SpritePresenter>(entity, false);
            }

            if (!em.HasComponent<DebugPresenter>(entity))
            {
                em.AddComponentData(entity, new DebugPresenter
                {
                    DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                });
                em.SetComponentEnabled<DebugPresenter>(entity, false);
            }

            if (!em.HasComponent<TracerPresenter>(entity))
            {
                em.AddComponentData(entity, new TracerPresenter
                {
                    DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
                });
                em.SetComponentEnabled<TracerPresenter>(entity, false);
            }

            if (!em.HasComponent<RenderLODData>(entity))
            {
                em.AddComponentData(entity, new RenderLODData
                {
                    CameraDistance = 0f,
                    ImportanceScore = 0.75f,
                    RecommendedLOD = 0,
                    LastUpdateTick = 0
                });
            }

            if (!em.HasComponent<RenderCullable>(entity))
            {
                em.AddComponentData(entity, new RenderCullable
                {
                    CullDistance = 40000f,
                    Priority = 140
                });
            }

            if (!em.HasComponent<RenderSampleIndex>(entity))
            {
                var sampleIndex = (ushort)math.abs(entity.Index % 1024);
                em.AddComponentData(entity, new RenderSampleIndex
                {
                    SampleIndex = sampleIndex,
                    SampleModulus = 1024,
                    ShouldRender = 1
                });
            }

            if (em.HasComponent<LocalTransform>(entity) && !em.HasComponent<LocalToWorld>(entity))
            {
                em.AddComponentData(entity, new LocalToWorld { Value = float4x4.identity });
            }
        }

        private void RepairMeshBindings(ref SystemState state)
        {
            if (_meshBindingRepairQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton(out RenderPresentationCatalog catalog) || !catalog.Blob.IsCreated)
            {
                return;
            }
            if (catalog.Blob.Value.Variants.Length == 0)
            {
                return;
            }

            var em = state.EntityManager;
            if (!em.Exists(catalog.RenderMeshArrayEntity) || !em.HasComponent<RenderMeshArray>(catalog.RenderMeshArrayEntity))
            {
                return;
            }

            var renderMeshArray = em.GetSharedComponentManaged<RenderMeshArray>(catalog.RenderMeshArrayEntity);
            var meshCount = renderMeshArray.MeshReferences?.Length ?? 0;
            var materialCount = renderMeshArray.MaterialReferences?.Length ?? 0;
            if (meshCount == 0 || materialCount == 0)
            {
                return;
            }

            using var entities = _meshBindingRepairQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.Exists(entity))
                {
                    continue;
                }

                var meshPresenter = em.GetComponentData<MeshPresenter>(entity);
                var variantIndex = meshPresenter.DefIndex == RenderPresentationConstants.UnassignedPresenterDefIndex
                    ? math.max(0, em.GetComponentData<RenderVariantKey>(entity).Value)
                    : meshPresenter.DefIndex;

                variantIndex = math.clamp(variantIndex, 0, catalog.Blob.Value.Variants.Length - 1);
                ref var variant = ref catalog.Blob.Value.Variants[variantIndex];

                var matIndex = math.clamp((int)variant.MaterialIndex, 0, math.max(materialCount - 1, 0));
                var meshIndex = math.clamp((int)variant.MeshIndex, 0, math.max(meshCount - 1, 0));
                var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices((ushort)matIndex, (ushort)meshIndex, variant.SubMesh);

                if (em.HasComponent<MaterialMeshInfo>(entity))
                {
                    em.SetComponentData(entity, materialMeshInfo);
                }
                else
                {
                    em.AddComponentData(entity, materialMeshInfo);
                }

                var bounds = new RenderBounds
                {
                    Value = new AABB
                    {
                        Center = variant.BoundsCenter,
                        Extents = variant.BoundsExtents
                    }
                };

                if (em.HasComponent<RenderBounds>(entity))
                {
                    em.SetComponentData(entity, bounds);
                }
                else
                {
                    em.AddComponentData(entity, bounds);
                }

                if (!em.HasComponent<RenderFilterSettings>(entity))
                {
                    em.AddSharedComponentManaged(entity, RenderFilterSettings.Default);
                }

                if (!em.HasComponent<RenderMeshArray>(entity))
                {
                    em.AddSharedComponentManaged(entity, renderMeshArray);
                }
            }
        }
    }
}
