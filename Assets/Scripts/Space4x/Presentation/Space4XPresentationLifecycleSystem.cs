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
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
            {
                return;
            }

            if (_carrierInitQuery.IsEmptyIgnoreFilter &&
                _vesselInitQuery.IsEmptyIgnoreFilter &&
                _asteroidInitQuery.IsEmptyIgnoreFilter &&
                _individualInitQuery.IsEmptyIgnoreFilter &&
                _strikeCraftInitQuery.IsEmptyIgnoreFilter &&
                _pickupInitQuery.IsEmptyIgnoreFilter &&
                _fleetImpostorInitQuery.IsEmptyIgnoreFilter &&
                _projectileInitQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            AddCarrierPresentation(ref state, ref ecb);
            AddVesselPresentation(ref state, ref ecb);
            AddAsteroidPresentation(ref state, ref ecb);
            AddIndividualPresentation(ref state, ref ecb);
            AddStrikeCraftPresentation(ref state, ref ecb);
            AddResourcePickupPresentation(ref state, ref ecb);
            AddFleetImpostorPresentation(ref state, ref ecb);
            AddProjectilePresentation(ref state, ref ecb);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private void AddCarrierPresentation(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<Carrier>, RefRO<LocalTransform>>()
                         .WithNone<CarrierPresentationTag>()
                         .WithEntityAccess())
            {
                var factionColor = new float4(0.15f, 0.45f, 0.95f, 1f);

                ecb.AddComponent(entity, new CarrierPresentationTag());
                ecb.AddComponent(entity, new CarrierVisualState
                {
                    State = CarrierVisualStateType.Idle,
                    StateTimer = 0f
                });
                AddMaterialColor(ref ecb, entity, factionColor);

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

                AddCommonRenderComponents(ref ecb, entity,
                    Space4XRenderKeys.Carrier,
                    cullDistance: 20000f,
                    cullPriority: 200,
                    importance: 0.95f);
            }
        }

        private void AddVesselPresentation(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (vessel, _, entity) in SystemAPI
                         .Query<RefRO<MiningVessel>, RefRO<LocalTransform>>()
                         .WithNone<CraftPresentationTag>()
                         .WithEntityAccess())
            {
                var parentCarrier = vessel.ValueRO.CarrierEntity;
                float4 vesselColor = new float4(0.95f, 0.6f, 0.2f, 1f);

                if (parentCarrier != Entity.Null && SystemAPI.HasComponent<RenderTint>(parentCarrier))
                {
                    var parentColor = SystemAPI.GetComponentRO<RenderTint>(parentCarrier).ValueRO.Value;
                    vesselColor = math.lerp(parentColor, vesselColor, 0.65f);
                }

                ecb.AddComponent(entity, new CraftPresentationTag());
                ecb.AddComponent(entity, new CraftVisualState
                {
                    State = CraftVisualStateType.Idle,
                    StateTimer = 0f
                });
                ecb.AddComponent(entity, new ParentCarrier { Value = parentCarrier });
                AddMaterialColor(ref ecb, entity, vesselColor);

                if (!SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    float scale = ResolveMiningVesselScale(vessel.ValueRO.CargoCapacity);
                    ecb.AddComponent(entity, new PresentationScale { Value = scale });
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.Orbital });
                }

                AddCommonRenderComponents(ref ecb, entity,
                    Space4XRenderKeys.Miner,
                    cullDistance: 12000f,
                    cullPriority: 150,
                    importance: 0.75f);
            }
        }

        private void AddAsteroidPresentation(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (asteroid, _, entity) in SystemAPI
                         .Query<RefRO<Asteroid>, RefRO<LocalTransform>>()
                         .WithNone<AsteroidPresentationTag>()
                         .WithEntityAccess())
            {
                var color = GetResourceColor(asteroid.ValueRO.ResourceType);
                var ratio = asteroid.ValueRO.MaxResourceAmount > 0f
                    ? asteroid.ValueRO.ResourceAmount / math.max(0.0001f, asteroid.ValueRO.MaxResourceAmount)
                    : 1f;

                ecb.AddComponent(entity, new AsteroidPresentationTag());
                ecb.AddComponent(entity, new ResourceTypeColor { Value = color });
                ecb.AddComponent(entity, new AsteroidVisualState
                {
                    State = ratio > 0.1f ? AsteroidVisualStateType.Full : AsteroidVisualStateType.Depleted,
                    DepletionRatio = 1f - math.saturate(ratio),
                    StateTimer = 0f
                });

                AddMaterialColor(ref ecb, entity, color);
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.System });
                }

                AddCommonRenderComponents(ref ecb, entity,
                    Space4XRenderKeys.Asteroid,
                    cullDistance: 40000f,
                    cullPriority: 100,
                    importance: 0.6f);
            }
        }

        private void AddIndividualPresentation(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<SimIndividualTag>, RefRO<LocalTransform>>()
                         .WithNone<IndividualPresentationTag>()
                         .WithEntityAccess())
            {
                var color = new float4(0.92f, 0.92f, 0.95f, 1f);

                ecb.AddComponent(entity, new IndividualPresentationTag());
                AddMaterialColor(ref ecb, entity, color);

                if (!SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    ecb.AddComponent(entity, new PresentationScale { Value = DefaultIndividualScale });
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.Colony });
                }

                AddCommonRenderComponents(ref ecb, entity,
                    Space4XRenderKeys.Individual,
                    cullDistance: 4000f,
                    cullPriority: 50,
                    importance: 0.25f);
            }
        }

        private void AddStrikeCraftPresentation(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (profile, _, entity) in SystemAPI
                         .Query<RefRO<StrikeCraftProfile>, RefRO<LocalTransform>>()
                         .WithNone<StrikeCraftPresentationTag>()
                         .WithEntityAccess())
            {
                var color = ResolveStrikeCraftColor(profile.ValueRO.Role);

                ecb.AddComponent(entity, new StrikeCraftPresentationTag());
                ecb.AddComponent(entity, new StrikeCraftVisualState
                {
                    State = StrikeCraftVisualStateType.Docked,
                    StateTimer = 0f
                });
                AddMaterialColor(ref ecb, entity, color);

                if (!SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    var scale = ResolveStrikeCraftScale(profile.ValueRO.Role);
                    ecb.AddComponent(entity, new PresentationScale { Value = scale });
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.Orbital });
                }

                AddCommonRenderComponents(ref ecb, entity,
                    Space4XRenderKeys.StrikeCraft,
                    cullDistance: 10000f,
                    cullPriority: 140,
                    importance: 0.65f);
            }
        }

        private void AddResourcePickupPresentation(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (spawn, _, entity) in SystemAPI
                         .Query<RefRO<SpawnResource>, RefRO<LocalTransform>>()
                         .WithNone<ResourcePickupPresentationTag>()
                         .WithEntityAccess())
            {
                var color = GetResourceColor(spawn.ValueRO.Type);
                ecb.AddComponent(entity, new ResourcePickupPresentationTag());
                ecb.AddComponent(entity, new ResourceTypeColor { Value = color });
                AddMaterialColor(ref ecb, entity, color);

                if (!SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    var scale = ResolvePickupScale(spawn.ValueRO.Amount);
                    ecb.AddComponent(entity, new PresentationScale { Value = scale });
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.Orbital });
                }

                AddCommonRenderComponents(ref ecb, entity,
                    Space4XRenderKeys.ResourcePickup,
                    cullDistance: 6000f,
                    cullPriority: 120,
                    importance: 0.4f);
            }
        }

        private void AddFleetImpostorPresentation(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (iconMesh, _, entity) in SystemAPI
                         .Query<RefRO<FleetIconMesh>, RefRO<LocalTransform>>()
                         .WithAll<FleetImpostorTag>()
                         .WithNone<RenderKey>()
                         .WithEntityAccess())
            {
                var color = new float4(0.3f, 0.85f, 1f, 1f);

                AddMaterialColor(ref ecb, entity, color);

                if (!SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    var scale = math.max(DefaultFleetImpostorScale, iconMesh.ValueRO.Size);
                    ecb.AddComponent(entity, new PresentationScale { Value = scale });
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.System });
                }

                AddCommonRenderComponents(ref ecb, entity,
                    Space4XRenderKeys.FleetImpostor,
                    cullDistance: 60000f,
                    cullPriority: 220,
                    importance: 0.98f);
            }
        }

        private void AddProjectilePresentation(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<ProjectileEntity>, RefRO<LocalTransform>>()
                         .WithNone<ProjectilePresentationTag>()
                         .WithEntityAccess())
            {
                var color = new float4(1f, 0.95f, 0.25f, 1f);

                ecb.AddComponent(entity, new ProjectilePresentationTag());
                ecb.AddComponent<ProjectileTag>(entity);
                AddMaterialColor(ref ecb, entity, color);

                if (!SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    ecb.AddComponent(entity, new PresentationScale { Value = DefaultProjectileScale });
                }
                if (!SystemAPI.HasComponent<PresentationLayer>(entity))
                {
                    ecb.AddComponent(entity, new PresentationLayer { Value = PresentationLayerId.Orbital });
                }

                AddCommonRenderComponents(ref ecb, entity,
                    Space4XRenderKeys.Projectile,
                    cullDistance: 12000f,
                    cullPriority: 230,
                    importance: 0.9f);
            }
        }

        private static void AddCommonRenderComponents(
            ref EntityCommandBuffer ecb,
            Entity entity,
            int semanticKey,
            float cullDistance,
            byte cullPriority,
            float importance)
        {
            ecb.AddComponent(entity, new RenderKey
            {
                ArchetypeId = (ushort)semanticKey,
                LOD = 0
            });

            ecb.AddComponent(entity, new RenderFlags
            {
                Visible = 1,
                ShadowCaster = 1,
                HighlightMask = 0
            });

            ecb.AddComponent(entity, new RenderSemanticKey
            {
                Value = (ushort)semanticKey
            });

            ecb.AddComponent(entity, new RenderVariantKey
            {
                Value = 0
            });

            ecb.AddComponent<RenderThemeOverride>(entity);
            ecb.SetComponentEnabled<RenderThemeOverride>(entity, false);

            ecb.AddComponent<MeshPresenter>(entity);
            ecb.AddComponent<SpritePresenter>(entity);
            ecb.SetComponentEnabled<SpritePresenter>(entity, false);
            ecb.AddComponent<DebugPresenter>(entity);
            ecb.SetComponentEnabled<DebugPresenter>(entity, false);

            ecb.AddComponent(entity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = importance,
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });

            ecb.AddComponent(entity, new RenderCullable
            {
                CullDistance = cullDistance,
                Priority = cullPriority
            });

            var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, RenderSampleModulus);
            ecb.AddComponent(entity, new RenderSampleIndex
            {
                SampleIndex = sampleIndex,
                SampleModulus = RenderSampleModulus,
                ShouldRender = 1
            });

        }

        private static void AddMaterialColor(ref EntityCommandBuffer ecb, Entity entity, float4 baseColor)
        {
            var emissiveColor = new float4(baseColor.xyz * 0.75f, 1f);
            ecb.AddComponent(entity, new MaterialPropertyOverride
            {
                BaseColor = baseColor,
                EmissiveColor = emissiveColor,
                Alpha = 1f,
                PulsePhase = 0f
            });

            ecb.AddComponent(entity, new URPMaterialPropertyBaseColor
            {
                Value = baseColor
            });
            ecb.AddComponent(entity, new URPMaterialPropertyEmissionColor
            {
                Value = emissiveColor
            });

            ecb.AddComponent(entity, new RenderTint
            {
                Value = baseColor
            });
            ecb.AddComponent(entity, new RenderTexSlice
            {
                Value = 0
            });
            ecb.AddComponent(entity, new RenderUvTransform
            {
                Value = new float4(1f, 1f, 0f, 0f)
            });
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
