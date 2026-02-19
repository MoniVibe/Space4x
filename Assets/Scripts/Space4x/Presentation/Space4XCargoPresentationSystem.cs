using PureDOTS.Rendering;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Rendering;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using PDStructuralChangePresentationSystemGroup = PureDOTS.Systems.StructuralChangePresentationSystemGroup;

namespace Space4X.Presentation
{
    [UpdateInGroup(typeof(PDStructuralChangePresentationSystemGroup))]
    public partial struct Space4XCargoPresentationEnsureSystem : ISystem
    {
        private EntityQuery _missingCargoQuery;
        private ComponentLookup<PresentationLayer> _presentationLayerLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderPresentationCatalog>();
            _missingCargoQuery = SystemAPI.QueryBuilder()
                .WithAll<MiningVessel, CraftPresentationTag>()
                .WithNone<CargoVisualLink>()
                .Build();
            _presentationLayerLookup = state.GetComponentLookup<PresentationLayer>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless || !RuntimeMode.IsRenderingEnabled || _missingCargoQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var config = EnsureConfig(ref state);
            _presentationLayerLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI
                         .Query<RefRO<MiningVessel>>()
                         .WithAll<CraftPresentationTag>()
                         .WithNone<CargoVisualLink>()
                         .WithEntityAccess())
            {
                var cargoEntity = ecb.CreateEntity();
                ecb.AddComponent(cargoEntity, new CargoPresentationTag());
                ecb.AddComponent(cargoEntity, new CargoVisualParent { Value = entity });
                ecb.AddComponent(cargoEntity, new PresentationAttachTo
                {
                    ParentPresenter = entity,
                    LocalOffset = config.LocalOffset
                });
                ecb.AddComponent<PresentationLocalToWorldOverride>(cargoEntity);
                ecb.AddComponent(cargoEntity, new LocalToWorld { Value = float4x4.identity });

                var layer = _presentationLayerLookup.HasComponent(entity)
                    ? _presentationLayerLookup[entity].Value
                    : PresentationLayerId.Orbital;
                ecb.AddComponent(cargoEntity, new PresentationLayer { Value = layer });

                AddRenderComponents(ref ecb, cargoEntity, config);

                ecb.AddComponent(entity, new CargoVisualLink { CargoEntity = cargoEntity });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private Space4XCargoPresentationConfig EnsureConfig(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<Space4XCargoPresentationConfig>(out var config))
            {
                return config;
            }

            var entity = state.EntityManager.CreateEntity();
            var defaults = Space4XCargoPresentationConfig.Default;
            state.EntityManager.AddComponentData(entity, defaults);
            state.EntityManager.SetName(entity, "Space4XCargoPresentationConfig");
            return defaults;
        }

        private static void AddRenderComponents(ref EntityCommandBuffer ecb, Entity entity, in Space4XCargoPresentationConfig config)
        {
            ecb.AddComponent(entity, new RenderKey
            {
                ArchetypeId = Space4XRenderKeys.ResourcePickup,
                LOD = 0
            });
            ecb.AddComponent(entity, new RenderFlags
            {
                Visible = 1,
                ShadowCaster = 1,
                HighlightMask = 0
            });
            ecb.AddComponent(entity, new RenderSemanticKey { Value = Space4XRenderKeys.ResourcePickup });
            ecb.AddComponent(entity, new RenderVariantKey { Value = 0 });

            ecb.AddComponent<RenderThemeOverride>(entity);
            ecb.SetComponentEnabled<RenderThemeOverride>(entity, false);

            ecb.AddComponent(entity, new MeshPresenter
            {
                DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
            });
            ecb.SetComponentEnabled<MeshPresenter>(entity, false);

            ecb.AddComponent(entity, new SpritePresenter
            {
                DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
            });
            ecb.SetComponentEnabled<SpritePresenter>(entity, false);

            ecb.AddComponent(entity, new DebugPresenter
            {
                DefIndex = RenderPresentationConstants.UnassignedPresenterDefIndex
            });
            ecb.SetComponentEnabled<DebugPresenter>(entity, false);

            ecb.AddComponent(entity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = 0.4f,
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });
            ecb.AddComponent(entity, new RenderCullable
            {
                CullDistance = 6000f,
                Priority = 120
            });

            var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, 1024);
            ecb.AddComponent(entity, new RenderSampleIndex
            {
                SampleIndex = sampleIndex,
                SampleModulus = 1024,
                ShouldRender = 1
            });

            var bounds = math.max(0.01f, config.BoundsExtents);
            ecb.AddComponent(entity, new RenderBounds
            {
                Value = new AABB
                {
                    Center = float3.zero,
                    Extents = new float3(bounds)
                }
            });

            ecb.AddComponent(entity, new RenderTint { Value = new float4(1f, 1f, 1f, 1f) });
            ecb.AddComponent(entity, new RenderTexSlice { Value = 0 });
            ecb.AddComponent(entity, new RenderUvTransform { Value = new float4(1f, 1f, 0f, 0f) });
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationDepthSystem))]
    public partial struct Space4XCargoPresentationDriveSystem : ISystem
    {
        private ComponentLookup<MiningVessel> _vesselLookup;
        private ComponentLookup<LocalToWorld> _parentLocalToWorldLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _vesselLookup = state.GetComponentLookup<MiningVessel>(true);
            _parentLocalToWorldLookup = state.GetComponentLookup<LocalToWorld>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            _vesselLookup.Update(ref state);
            _parentLocalToWorldLookup.Update(ref state);

            var config = SystemAPI.TryGetSingleton<Space4XCargoPresentationConfig>(out var configValue)
                ? configValue
                : Space4XCargoPresentationConfig.Default;
            var deltaTime = (float)SystemAPI.Time.DeltaTime;
            var smoothing = math.max(0f, config.Smoothing);
            var lerpFactor = smoothing <= 0f ? 1f : 1f - math.exp(-smoothing * deltaTime);

            foreach (var (parentLink, attach, cargoLocalToWorld, cargoTint, meshEnabled) in SystemAPI
                         .Query<RefRO<CargoVisualParent>, RefRW<PresentationAttachTo>, RefRW<LocalToWorld>, RefRW<RenderTint>, EnabledRefRW<MeshPresenter>>()
                         .WithAll<CargoPresentationTag>())
            {
                var parent = parentLink.ValueRO.Value;
                if (parent == Entity.Null ||
                    !_vesselLookup.HasComponent(parent) ||
                    !_parentLocalToWorldLookup.HasComponent(parent))
                {
                    meshEnabled.ValueRW = false;
                    continue;
                }

                var vessel = _vesselLookup[parent];
                var cargoAmount = math.max(0f, vessel.CurrentCargo);
                var cargoCapacity = math.max(0.0001f, vessel.CargoCapacity);
                var ratio = math.saturate(cargoAmount / cargoCapacity);

                meshEnabled.ValueRW = cargoAmount > 0.01f;
                cargoTint.ValueRW.Value = ResolveResourceColor(vessel.CargoResourceType);

                if (math.any(attach.ValueRO.LocalOffset != config.LocalOffset))
                {
                    attach.ValueRW.LocalOffset = config.LocalOffset;
                }

                float targetScale = math.lerp(config.BaseScale, config.MaxScale, ratio);
                float parentScale = math.length(_parentLocalToWorldLookup[parent].Value.c0.xyz);
                float currentScale = parentScale > 0f
                    ? math.length(cargoLocalToWorld.ValueRO.Value.c0.xyz) / parentScale
                    : config.BaseScale;
                float resolvedScale = math.lerp(currentScale, targetScale, lerpFactor);
                resolvedScale = math.max(0.001f, resolvedScale);

                var parentLocalToWorld = _parentLocalToWorldLookup[parent].Value;
                var localMatrix = float4x4.TRS(attach.ValueRO.LocalOffset, quaternion.identity, new float3(resolvedScale));
                cargoLocalToWorld.ValueRW.Value = math.mul(parentLocalToWorld, localMatrix);
            }
        }

        private static float4 ResolveResourceColor(ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.Minerals => new float4(0.6f, 0.6f, 0.6f, 1f),
                ResourceType.RareMetals => new float4(0.8f, 0.7f, 0.2f, 1f),
                ResourceType.EnergyCrystals => new float4(0.2f, 0.8f, 1f, 1f),
                ResourceType.OrganicMatter => new float4(0.2f, 0.8f, 0.3f, 1f),
                ResourceType.Ore => new float4(0.5f, 0.3f, 0.2f, 1f),
                ResourceType.Volatiles => ResourceTypeColor.Volatiles.Value,
                ResourceType.TransplutonicOre => ResourceTypeColor.TransplutonicOre.Value,
                ResourceType.ExoticGases => ResourceTypeColor.ExoticGases.Value,
                ResourceType.VolatileMotes => ResourceTypeColor.VolatileMotes.Value,
                ResourceType.IndustrialCrystals => ResourceTypeColor.IndustrialCrystals.Value,
                ResourceType.Isotopes => ResourceTypeColor.Isotopes.Value,
                ResourceType.HeavyWater => ResourceTypeColor.HeavyWater.Value,
                ResourceType.LiquidOzone => ResourceTypeColor.LiquidOzone.Value,
                ResourceType.StrontiumClathrates => ResourceTypeColor.StrontiumClathrates.Value,
                ResourceType.SalvageComponents => ResourceTypeColor.SalvageComponents.Value,
                ResourceType.BoosterGas => ResourceTypeColor.BoosterGas.Value,
                ResourceType.RelicData => ResourceTypeColor.RelicData.Value,
                ResourceType.Food => ResourceTypeColor.Food.Value,
                ResourceType.Water => ResourceTypeColor.Water.Value,
                ResourceType.Supplies => ResourceTypeColor.Supplies.Value,
                ResourceType.Fuel => ResourceTypeColor.Fuel.Value,
                _ => new float4(1f, 1f, 1f, 1f)
            };
        }
    }
}
