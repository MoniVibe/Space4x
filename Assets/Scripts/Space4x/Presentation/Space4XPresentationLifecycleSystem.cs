using PureDOTS.Rendering;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Rendering;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Presentation
{
    /// <summary>
    /// Ensures Space4X gameplay entities receive presentation components compatible with the RenderKey/catalog pipeline.
    /// Assigns RenderKey/RenderFlags, colors, and visual-state tags per entity type.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct Space4XPresentationLifecycleSystem : ISystem
    {
        private EntityQuery _carrierInitQuery;
        private EntityQuery _vesselInitQuery;
        private EntityQuery _asteroidInitQuery;
        private EntityQuery _individualInitQuery;
        private EntityQuery _strikeCraftInitQuery;
        private EntityQuery _pickupInitQuery;
        private EntityQuery _fleetImpostorInitQuery;
        private EntityQuery _projectileInitQuery;
        private EntityQuery _debrisInitQuery;
        private bool _loggedRenderingGate;
        private bool _loggedFirstUpdate;

        private const ushort RenderSampleModulus = 1024;
        private const float DefaultCarrierScale = 0.5f;
        private const float DefaultMiningVesselScale = 0.02f;
        private const float DefaultIndividualScale = 0.003f;
        private const float DefaultStrikeCraftScale = 0.012f;
        private const float DefaultPickupScale = 0.015f;
        private const float DefaultProjectileScale = 0.008f;
        private const float DefaultFleetImpostorScale = 0.4f;
        private const float CarrierCapacityReference = 20f;
        private const float MiningCargoReference = 100f;
        private const float PickupAmountReference = 80f;
        private const float CarrierScaleMin = 0.6f;
        private const float CarrierScaleMax = 2.5f;
        private const float VesselScaleMin = 0.6f;
        private const float VesselScaleMax = 1.8f;
        private const float PickupScaleMin = 0.5f;
        private const float PickupScaleMax = 3f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderPresentationCatalog>();

            _carrierInitQuery = SystemAPI.QueryBuilder()
                .WithAll<Carrier, LocalTransform>()
                .WithNone<CarrierPresentationTag>()
                .Build();

            _vesselInitQuery = SystemAPI.QueryBuilder()
                .WithAll<MiningVessel, LocalTransform>()
                .WithNone<CraftPresentationTag>()
                .Build();

            _asteroidInitQuery = SystemAPI.QueryBuilder()
                .WithAll<Asteroid, LocalTransform>()
                .WithNone<AsteroidPresentationTag>()
                .Build();

            _individualInitQuery = SystemAPI.QueryBuilder()
                .WithAll<SimIndividualTag, LocalTransform>()
                .WithNone<IndividualPresentationTag>()
                .Build();

            _strikeCraftInitQuery = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftProfile, LocalTransform>()
                .WithNone<StrikeCraftPresentationTag>()
                .Build();

            _pickupInitQuery = SystemAPI.QueryBuilder()
                .WithAll<SpawnResource, LocalTransform>()
                .WithNone<ResourcePickupPresentationTag>()
                .Build();

            _fleetImpostorInitQuery = SystemAPI.QueryBuilder()
                .WithAll<FleetImpostorTag, LocalTransform>()
                .WithNone<RenderKey>()
                .Build();

            _projectileInitQuery = SystemAPI.QueryBuilder()
                .WithAll<ProjectileEntity, LocalTransform>()
                .WithNone<ProjectilePresentationTag>()
                .Build();

            _debrisInitQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XDebrisTag, LocalTransform>()
                .WithNone<RenderKey>()
                .Build();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityDebug.Log($"[Space4XPresentationLifecycle] OnCreate World='{state.WorldUnmanaged.Name}' RenderingEnabled={RuntimeMode.IsRenderingEnabled} Headless={RuntimeMode.IsHeadless} HasCatalog={SystemAPI.HasSingleton<RenderPresentationCatalog>()}");
#endif
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_loggedRenderingGate && !RuntimeMode.IsRenderingEnabled)
            {
                _loggedRenderingGate = true;
                UnityDebug.LogWarning($"[Space4XPresentationLifecycle] Rendering gate active. World='{state.WorldUnmanaged.Name}' RenderingEnabled={RuntimeMode.IsRenderingEnabled} Headless={RuntimeMode.IsHeadless} HasCatalog={SystemAPI.HasSingleton<RenderPresentationCatalog>()}");
            }
#endif
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_loggedFirstUpdate)
            {
                _loggedFirstUpdate = true;
                UnityDebug.Log($"[Space4XPresentationLifecycle] FirstUpdate World='{state.WorldUnmanaged.Name}' HasCatalog={SystemAPI.HasSingleton<RenderPresentationCatalog>()} CarrierInit={_carrierInitQuery.CalculateEntityCount()} VesselInit={_vesselInitQuery.CalculateEntityCount()} AsteroidInit={_asteroidInitQuery.CalculateEntityCount()}");
            }
#endif
            if (_carrierInitQuery.IsEmptyIgnoreFilter &&
                _vesselInitQuery.IsEmptyIgnoreFilter &&
                _asteroidInitQuery.IsEmptyIgnoreFilter &&
                _individualInitQuery.IsEmptyIgnoreFilter &&
                _strikeCraftInitQuery.IsEmptyIgnoreFilter &&
                _pickupInitQuery.IsEmptyIgnoreFilter &&
                _fleetImpostorInitQuery.IsEmptyIgnoreFilter &&
                _projectileInitQuery.IsEmptyIgnoreFilter &&
                _debrisInitQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var hasVisualConfig = SystemAPI.TryGetSingleton<Space4XMiningVisualConfig>(out var visualConfig);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            AddCarrierPresentation(ref state, ref ecb, hasVisualConfig, visualConfig);
            AddVesselPresentation(ref state, ref ecb, hasVisualConfig, visualConfig);
            AddAsteroidPresentation(ref state, ref ecb, hasVisualConfig, visualConfig);
            AddIndividualPresentation(ref state, ref ecb, hasVisualConfig, visualConfig);
            AddStrikeCraftPresentation(ref state, ref ecb, hasVisualConfig, visualConfig);
            AddResourcePickupPresentation(ref state, ref ecb, hasVisualConfig, visualConfig);
            AddFleetImpostorPresentation(ref state, ref ecb, hasVisualConfig, visualConfig);
            AddProjectilePresentation(ref state, ref ecb, hasVisualConfig, visualConfig);
            AddDebrisPresentation(ref state, ref ecb, hasVisualConfig, visualConfig);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private void AddCarrierPresentation(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            bool hasVisualConfig,
            in Space4XMiningVisualConfig visualConfig)
        {
            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<Carrier>, RefRO<LocalTransform>>()
                         .WithNone<CarrierPresentationTag>()
                         .WithEntityAccess())
            {
                float4 baseColor = default;
                bool hasBaseColor = false;
                if (SystemAPI.HasComponent<RenderTint>(entity))
                {
                    baseColor = SystemAPI.GetComponentRO<RenderTint>(entity).ValueRO.Value;
                    hasBaseColor = true;
                }
                else if (hasVisualConfig)
                {
                    baseColor = visualConfig.CarrierColor;
                    hasBaseColor = true;
                }

                ecb.AddComponent(entity, new CarrierPresentationTag());
                ecb.AddComponent(entity, new CarrierVisualState
                {
                    State = CarrierVisualStateType.Idle,
                    StateTimer = 0f
                });
                if (hasBaseColor)
                {
                    AddMaterialColor(ref state, ref ecb, entity, baseColor);
                }

                if (!SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    float scale = DefaultCarrierScale;
                    if (SystemAPI.HasComponent<DockingCapacity>(entity))
                    {
                        var capacity = SystemAPI.GetComponentRO<DockingCapacity>(entity).ValueRO;
                        scale = ResolveCarrierScale(capacity);
                    }
                    ecb.AddComponent(entity, new PresentationScale { Value = scale });
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.System });
                }

                AddCommonRenderComponents(ref state, ref ecb, entity,
                    Space4XRenderKeys.Carrier,
                    cullDistance: 20000f,
                    cullPriority: 200,
                    importance: 0.95f);
            }
        }

        private void AddVesselPresentation(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            bool hasVisualConfig,
            in Space4XMiningVisualConfig visualConfig)
        {
            foreach (var (vessel, _, entity) in SystemAPI
                         .Query<RefRO<MiningVessel>, RefRO<LocalTransform>>()
                         .WithNone<CraftPresentationTag>()
                         .WithEntityAccess())
            {
                var parentCarrier = vessel.ValueRO.CarrierEntity;
                float4 baseColor = default;
                bool hasBaseColor = false;

                if (SystemAPI.HasComponent<RenderTint>(entity))
                {
                    baseColor = SystemAPI.GetComponentRO<RenderTint>(entity).ValueRO.Value;
                    hasBaseColor = true;
                }
                else if (parentCarrier != Entity.Null && SystemAPI.HasComponent<RenderTint>(parentCarrier))
                {
                    var parentColor = SystemAPI.GetComponentRO<RenderTint>(parentCarrier).ValueRO.Value;
                    baseColor = hasVisualConfig
                        ? math.lerp(parentColor, visualConfig.MiningVesselColor, 0.65f)
                        : parentColor;
                    hasBaseColor = true;
                }
                else if (hasVisualConfig)
                {
                    baseColor = visualConfig.MiningVesselColor;
                    hasBaseColor = true;
                }

                ecb.AddComponent(entity, new CraftPresentationTag());
                ecb.AddComponent(entity, new CraftVisualState
                {
                    State = CraftVisualStateType.Idle,
                    StateTimer = 0f
                });
                ecb.AddComponent(entity, new ParentCarrier { Value = parentCarrier });
                if (hasBaseColor)
                {
                    AddMaterialColor(ref state, ref ecb, entity, baseColor);
                }

                if (!SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    float scale = ResolveMiningVesselScale(vessel.ValueRO.CargoCapacity);
                    ecb.AddComponent(entity, new PresentationScale { Value = scale });
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.Orbital });
                }

                AddCommonRenderComponents(ref state, ref ecb, entity,
                    Space4XRenderKeys.Miner,
                    cullDistance: 12000f,
                    cullPriority: 150,
                    importance: 0.75f);
            }
        }

        private void AddAsteroidPresentation(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            bool hasVisualConfig,
            in Space4XMiningVisualConfig visualConfig)
        {
            foreach (var (asteroid, _, entity) in SystemAPI
                         .Query<RefRO<Asteroid>, RefRO<LocalTransform>>()
                         .WithNone<AsteroidPresentationTag>()
                         .WithEntityAccess())
            {
                float4 baseColor = default;
                bool hasBaseColor = false;

                if (SystemAPI.HasComponent<ResourceTypeColor>(entity))
                {
                    baseColor = SystemAPI.GetComponentRO<ResourceTypeColor>(entity).ValueRO.Value;
                    hasBaseColor = true;
                }
                else if (hasVisualConfig)
                {
                    baseColor = visualConfig.AsteroidColor;
                    hasBaseColor = true;
                }
                else
                {
                    baseColor = GetResourceColor(asteroid.ValueRO.ResourceType);
                    hasBaseColor = true;
                }

                var ratio = asteroid.ValueRO.MaxResourceAmount > 0f
                    ? asteroid.ValueRO.ResourceAmount / math.max(0.0001f, asteroid.ValueRO.MaxResourceAmount)
                    : 1f;

                ecb.AddComponent(entity, new AsteroidPresentationTag());
                if (!SystemAPI.HasComponent<ResourceTypeColor>(entity) && hasBaseColor)
                {
                    ecb.AddComponent(entity, new ResourceTypeColor { Value = baseColor });
                }
                ecb.AddComponent(entity, new AsteroidVisualState
                {
                    State = ratio > 0.1f ? AsteroidVisualStateType.Full : AsteroidVisualStateType.Depleted,
                    DepletionRatio = 1f - math.saturate(ratio),
                    StateTimer = 0f
                });

                if (hasBaseColor)
                {
                    AddMaterialColor(ref state, ref ecb, entity, baseColor);
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.System });
                }

                AddCommonRenderComponents(ref state, ref ecb, entity,
                    Space4XRenderKeys.Asteroid,
                    cullDistance: 40000f,
                    cullPriority: 100,
                    importance: 0.6f);
            }
        }

        private void AddIndividualPresentation(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            bool hasVisualConfig,
            in Space4XMiningVisualConfig visualConfig)
        {
            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<SimIndividualTag>, RefRO<LocalTransform>>()
                         .WithNone<IndividualPresentationTag>()
                         .WithEntityAccess())
            {
                float4 baseColor = default;
                bool hasBaseColor = false;
                if (SystemAPI.HasComponent<RenderTint>(entity))
                {
                    baseColor = SystemAPI.GetComponentRO<RenderTint>(entity).ValueRO.Value;
                    hasBaseColor = true;
                }

                ecb.AddComponent(entity, new IndividualPresentationTag());
                if (hasBaseColor)
                {
                    AddMaterialColor(ref state, ref ecb, entity, baseColor);
                }

                if (!SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    ecb.AddComponent(entity, new PresentationScale { Value = DefaultIndividualScale });
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.Colony });
                }

                AddCommonRenderComponents(ref state, ref ecb, entity,
                    Space4XRenderKeys.Individual,
                    cullDistance: 4000f,
                    cullPriority: 50,
                    importance: 0.25f);
            }
        }

        private void AddStrikeCraftPresentation(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            bool hasVisualConfig,
            in Space4XMiningVisualConfig visualConfig)
        {
            foreach (var (profile, _, entity) in SystemAPI
                         .Query<RefRO<StrikeCraftProfile>, RefRO<LocalTransform>>()
                         .WithNone<StrikeCraftPresentationTag>()
                         .WithEntityAccess())
            {
                float4 baseColor = default;
                bool hasBaseColor = false;
                if (SystemAPI.HasComponent<RenderTint>(entity))
                {
                    baseColor = SystemAPI.GetComponentRO<RenderTint>(entity).ValueRO.Value;
                    hasBaseColor = true;
                }
                else
                {
                    baseColor = ResolveStrikeCraftColor(profile.ValueRO.Role);
                    hasBaseColor = true;
                }

                ecb.AddComponent(entity, new StrikeCraftPresentationTag());
                ecb.AddComponent(entity, new StrikeCraftVisualState
                {
                    State = StrikeCraftVisualStateType.Docked,
                    StateTimer = 0f
                });
                if (hasBaseColor)
                {
                    AddMaterialColor(ref state, ref ecb, entity, baseColor);
                }

                if (!SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    var scale = ResolveStrikeCraftScale(profile.ValueRO.Role);
                    ecb.AddComponent(entity, new PresentationScale { Value = scale });
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.Orbital });
                }

                AddCommonRenderComponents(ref state, ref ecb, entity,
                    Space4XRenderKeys.StrikeCraft,
                    cullDistance: 10000f,
                    cullPriority: 140,
                    importance: 0.65f);
            }
        }

        private void AddResourcePickupPresentation(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            bool hasVisualConfig,
            in Space4XMiningVisualConfig visualConfig)
        {
            foreach (var (spawn, _, entity) in SystemAPI
                         .Query<RefRO<SpawnResource>, RefRO<LocalTransform>>()
                         .WithNone<ResourcePickupPresentationTag>()
                         .WithEntityAccess())
            {
                float4 baseColor = default;
                bool hasBaseColor = false;

                if (SystemAPI.HasComponent<RenderTint>(entity))
                {
                    baseColor = SystemAPI.GetComponentRO<RenderTint>(entity).ValueRO.Value;
                    hasBaseColor = true;
                }
                else if (SystemAPI.HasComponent<ResourceTypeColor>(entity))
                {
                    baseColor = SystemAPI.GetComponentRO<ResourceTypeColor>(entity).ValueRO.Value;
                    hasBaseColor = true;
                }
                else
                {
                    baseColor = GetResourceColor(spawn.ValueRO.Type);
                    hasBaseColor = true;
                }

                ecb.AddComponent(entity, new ResourcePickupPresentationTag());
                if (!SystemAPI.HasComponent<ResourceTypeColor>(entity) && hasBaseColor)
                {
                    ecb.AddComponent(entity, new ResourceTypeColor { Value = baseColor });
                }
                if (hasBaseColor)
                {
                    AddMaterialColor(ref state, ref ecb, entity, baseColor);
                }

                if (!SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    var scale = ResolvePickupScale(spawn.ValueRO.Amount);
                    ecb.AddComponent(entity, new PresentationScale { Value = scale });
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.Orbital });
                }

                AddCommonRenderComponents(ref state, ref ecb, entity,
                    Space4XRenderKeys.ResourcePickup,
                    cullDistance: 6000f,
                    cullPriority: 120,
                    importance: 0.4f);
            }
        }

        private void AddFleetImpostorPresentation(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            bool hasVisualConfig,
            in Space4XMiningVisualConfig visualConfig)
        {
            foreach (var (iconMesh, _, entity) in SystemAPI
                         .Query<RefRO<FleetIconMesh>, RefRO<LocalTransform>>()
                         .WithAll<FleetImpostorTag>()
                         .WithNone<RenderKey>()
                         .WithEntityAccess())
            {
                float4 baseColor = default;
                bool hasBaseColor = false;
                if (SystemAPI.HasComponent<RenderTint>(entity))
                {
                    baseColor = SystemAPI.GetComponentRO<RenderTint>(entity).ValueRO.Value;
                    hasBaseColor = true;
                }
                if (hasBaseColor)
                {
                    AddMaterialColor(ref state, ref ecb, entity, baseColor);
                }

                if (!SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    var scale = math.max(DefaultFleetImpostorScale, iconMesh.ValueRO.Size);
                    ecb.AddComponent(entity, new PresentationScale { Value = scale });
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.System });
                }

                AddCommonRenderComponents(ref state, ref ecb, entity,
                    Space4XRenderKeys.FleetImpostor,
                    cullDistance: 60000f,
                    cullPriority: 220,
                    importance: 0.98f);
            }
        }

        private void AddProjectilePresentation(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            bool hasVisualConfig,
            in Space4XMiningVisualConfig visualConfig)
        {
            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<ProjectileEntity>, RefRO<LocalTransform>>()
                         .WithNone<ProjectilePresentationTag>()
                         .WithEntityAccess())
            {
                float4 baseColor = default;
                bool hasBaseColor = false;
                if (SystemAPI.HasComponent<RenderTint>(entity))
                {
                    baseColor = SystemAPI.GetComponentRO<RenderTint>(entity).ValueRO.Value;
                    hasBaseColor = true;
                }

                ecb.AddComponent(entity, new ProjectilePresentationTag());
                ecb.AddComponent<ProjectileTag>(entity);
                if (hasBaseColor)
                {
                    AddMaterialColor(ref state, ref ecb, entity, baseColor);
                }

                if (!SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    ecb.AddComponent(entity, new PresentationScale { Value = DefaultProjectileScale });
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.Orbital });
                }

                AddCommonRenderComponents(ref state, ref ecb, entity,
                    Space4XRenderKeys.Projectile,
                    cullDistance: 12000f,
                    cullPriority: 230,
                    importance: 0.9f);
            }
        }

        private void AddDebrisPresentation(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            bool hasVisualConfig,
            in Space4XMiningVisualConfig visualConfig)
        {
            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<Space4XDebrisTag>, RefRO<LocalTransform>>()
                         .WithNone<RenderKey>()
                         .WithEntityAccess())
            {
                float4 baseColor = new float4(0.48f, 0.45f, 0.42f, 1f);
                if (SystemAPI.HasComponent<RenderTint>(entity))
                {
                    baseColor = SystemAPI.GetComponentRO<RenderTint>(entity).ValueRO.Value;
                }
                else
                {
                    ecb.AddComponent(entity, new RenderTint { Value = baseColor });
                }

                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.Orbital });
                }

                AddCommonRenderComponents(ref state, ref ecb, entity,
                    Space4XRenderKeys.ResourcePickup,
                    cullDistance: 4000f,
                    cullPriority: 140,
                    importance: 0.35f);
            }
        }

        private void AddCommonRenderComponents(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            Entity entity,
            int semanticKey,
            float cullDistance,
            byte cullPriority,
            float importance)
        {
            ushort resolvedSemanticKey = (ushort)semanticKey;
            if (SystemAPI.HasComponent<RenderSemanticKey>(entity))
            {
                resolvedSemanticKey = SystemAPI.GetComponentRO<RenderSemanticKey>(entity).ValueRO.Value;
            }

            if (!SystemAPI.HasComponent<RenderKey>(entity))
            {
                ecb.AddComponent(entity, new RenderKey
                {
                    ArchetypeId = resolvedSemanticKey,
                    LOD = 0
                });
            }

            if (!SystemAPI.HasComponent<RenderFlags>(entity))
            {
                ecb.AddComponent(entity, new RenderFlags
                {
                    Visible = 1,
                    ShadowCaster = 1,
                    HighlightMask = 0
                });
            }

            if (!SystemAPI.HasComponent<RenderSemanticKey>(entity))
            {
                ecb.AddComponent(entity, new RenderSemanticKey
                {
                    Value = resolvedSemanticKey
                });
            }

            if (!SystemAPI.HasComponent<RenderVariantKey>(entity))
            {
                ecb.AddComponent(entity, new RenderVariantKey
                {
                    Value = 0
                });
            }

            if (!SystemAPI.HasComponent<RenderThemeOverride>(entity))
            {
                ecb.AddComponent<RenderThemeOverride>(entity);
                ecb.SetComponentEnabled<RenderThemeOverride>(entity, false);
            }

            if (!SystemAPI.HasComponent<MeshPresenter>(entity))
            {
                ecb.AddComponent<MeshPresenter>(entity);
                ecb.SetComponentEnabled<MeshPresenter>(entity, true);
            }

            if (!SystemAPI.HasComponent<SpritePresenter>(entity))
            {
                ecb.AddComponent<SpritePresenter>(entity);
                ecb.SetComponentEnabled<SpritePresenter>(entity, false);
            }

            if (!SystemAPI.HasComponent<DebugPresenter>(entity))
            {
                ecb.AddComponent<DebugPresenter>(entity);
                ecb.SetComponentEnabled<DebugPresenter>(entity, false);
            }

            if (!SystemAPI.HasComponent<RenderLODData>(entity))
            {
                ecb.AddComponent(entity, new RenderLODData
                {
                    CameraDistance = 0f,
                    ImportanceScore = importance,
                    RecommendedLOD = 0,
                    LastUpdateTick = 0
                });
            }

            if (!SystemAPI.HasComponent<RenderCullable>(entity))
            {
                ecb.AddComponent(entity, new RenderCullable
                {
                    CullDistance = cullDistance,
                    Priority = cullPriority
                });
            }

            if (!SystemAPI.HasComponent<RenderSampleIndex>(entity))
            {
                var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, RenderSampleModulus);
                ecb.AddComponent(entity, new RenderSampleIndex
                {
                    SampleIndex = sampleIndex,
                    SampleModulus = RenderSampleModulus,
                    ShouldRender = 1
                });
            }

        }

        private void AddMaterialColor(ref SystemState state, ref EntityCommandBuffer ecb, Entity entity, float4 baseColor)
        {
            var emissiveColor = new float4(baseColor.xyz * 0.75f, 1f);
            if (!SystemAPI.HasComponent<MaterialPropertyOverride>(entity))
            {
                ecb.AddComponent(entity, new MaterialPropertyOverride
                {
                    BaseColor = baseColor,
                    EmissiveColor = emissiveColor,
                    Alpha = 1f,
                    PulsePhase = 0f
                });
            }

            if (!SystemAPI.HasComponent<URPMaterialPropertyBaseColor>(entity))
            {
                ecb.AddComponent(entity, new URPMaterialPropertyBaseColor
                {
                    Value = baseColor
                });
            }

            if (!SystemAPI.HasComponent<URPMaterialPropertyEmissionColor>(entity))
            {
                ecb.AddComponent(entity, new URPMaterialPropertyEmissionColor
                {
                    Value = emissiveColor
                });
            }

            if (!SystemAPI.HasComponent<RenderTint>(entity))
            {
                ecb.AddComponent(entity, new RenderTint
                {
                    Value = baseColor
                });
            }

            if (!SystemAPI.HasComponent<RenderTexSlice>(entity))
            {
                ecb.AddComponent(entity, new RenderTexSlice
                {
                    Value = 0
                });
            }

            if (!SystemAPI.HasComponent<RenderUvTransform>(entity))
            {
                ecb.AddComponent(entity, new RenderUvTransform
                {
                    Value = new float4(1f, 1f, 0f, 0f)
                });
            }
        }

        private static float4 GetResourceColor(ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.Minerals => new float4(0.6f, 0.6f, 0.6f, 1f),
                ResourceType.RareMetals => new float4(0.8f, 0.7f, 0.2f, 1f),
                ResourceType.EnergyCrystals => new float4(0.2f, 0.8f, 1f, 1f),
                ResourceType.OrganicMatter => new float4(0.2f, 0.8f, 0.3f, 1f),
                ResourceType.Ore => new float4(0.5f, 0.3f, 0.2f, 1f),
                _ => new float4(1f, 1f, 1f, 1f)
            };
        }

        private static float ResolveCarrierScale(in DockingCapacity capacity)
        {
            float total = capacity.MaxSmallCraft + capacity.MaxMediumCraft + capacity.MaxLargeCraft
                          + capacity.MaxExternalMooring + capacity.MaxUtility;
            if (total <= 0f)
            {
                return DefaultCarrierScale;
            }

            float normalized = math.max(0.1f, total / CarrierCapacityReference);
            float factor = math.sqrt(normalized);
            factor = math.clamp(factor, CarrierScaleMin, CarrierScaleMax);
            return DefaultCarrierScale * factor;
        }

        private static float ResolveMiningVesselScale(float cargoCapacity)
        {
            float normalized = math.max(0.1f, cargoCapacity / MiningCargoReference);
            float factor = math.sqrt(normalized);
            factor = math.clamp(factor, VesselScaleMin, VesselScaleMax);
            return DefaultMiningVesselScale * factor;
        }

        private static float ResolveStrikeCraftScale(StrikeCraftRole role)
        {
            return role switch
            {
                StrikeCraftRole.Interceptor => 0.009f,
                StrikeCraftRole.Bomber => 0.016f,
                StrikeCraftRole.Recon => 0.0105f,
                StrikeCraftRole.Suppression => 0.013f,
                StrikeCraftRole.EWar => 0.012f,
                _ => DefaultStrikeCraftScale
            };
        }

        private static float ResolvePickupScale(float amount)
        {
            float normalized = math.max(0.05f, amount / PickupAmountReference);
            float factor = math.sqrt(normalized);
            factor = math.clamp(factor, PickupScaleMin, PickupScaleMax);
            return DefaultPickupScale * factor;
        }

        private static float4 ResolveStrikeCraftColor(StrikeCraftRole role)
        {
            return role switch
            {
                StrikeCraftRole.Interceptor => new float4(0.35f, 0.8f, 1f, 1f),
                StrikeCraftRole.Bomber => new float4(1f, 0.45f, 0.35f, 1f),
                StrikeCraftRole.Recon => new float4(0.55f, 1f, 0.65f, 1f),
                StrikeCraftRole.Suppression => new float4(1f, 0.7f, 0.35f, 1f),
                StrikeCraftRole.EWar => new float4(0.8f, 0.45f, 1f, 1f),
                _ => new float4(0.95f, 0.6f, 0.2f, 1f)
            };
        }
    }
}
