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
    public partial struct Space4XResourcePickupPresentationEnsureSystem : ISystem
    {
        private EntityQuery _missingPresenterQuery;
        private ComponentLookup<PresentationLayer> _presentationLayerLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderPresentationCatalog>();
            _missingPresenterQuery = SystemAPI.QueryBuilder()
                .WithAll<SpawnResource, LocalTransform>()
                .WithNone<ResourcePickupPresenterLink>()
                .Build();
            _presentationLayerLookup = state.GetComponentLookup<PresentationLayer>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless || !RuntimeMode.IsRenderingEnabled || _missingPresenterQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var config = EnsureConfig(ref state);
            _presentationLayerLookup.Update(ref state);

            var endEcb = state.World.GetOrCreateSystemManaged<EndPresentationECBSystem>();
            var ecb = endEcb.CreateCommandBuffer();

            foreach (var (_, entity) in SystemAPI
                         .Query<RefRO<SpawnResource>>()
                         .WithAll<LocalTransform>()
                         .WithNone<ResourcePickupPresenterLink>()
                         .WithEntityAccess())
            {
                var presenterEntity = ecb.CreateEntity();
                ecb.AddComponent(presenterEntity, new ResourcePickupPresentationTag());
                ecb.AddComponent(presenterEntity, new RenderOwner { Owner = entity });
                ecb.AddComponent<PresentationLocalToWorldOverride>(presenterEntity);
                ecb.AddComponent(presenterEntity, new LocalToWorld { Value = float4x4.identity });

                var layer = _presentationLayerLookup.HasComponent(entity)
                    ? _presentationLayerLookup[entity].Value
                    : PresentationLayerId.Orbital;
                ecb.AddComponent(presenterEntity, new PresentationLayer { Value = layer });

                AddRenderComponents(ref ecb, presenterEntity, config);

                ecb.AddComponent(entity, new ResourcePickupPresenterLink { PresenterEntity = presenterEntity });
            }
        }

        private Space4XResourcePickupPresentationConfig EnsureConfig(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<Space4XResourcePickupPresentationConfig>(out var config))
            {
                return config;
            }

            var entity = state.EntityManager.CreateEntity();
            var defaults = Space4XResourcePickupPresentationConfig.Default;
            state.EntityManager.AddComponentData(entity, defaults);
            state.EntityManager.SetName(entity, "Space4XResourcePickupPresentationConfig");
            return defaults;
        }

        private static void AddRenderComponents(ref EntityCommandBuffer ecb, Entity entity, in Space4XResourcePickupPresentationConfig config)
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

            ecb.AddComponent<MeshPresenter>(entity);
            ecb.SetComponentEnabled<MeshPresenter>(entity, false);

            ecb.AddComponent<SpritePresenter>(entity);
            ecb.SetComponentEnabled<SpritePresenter>(entity, false);

            ecb.AddComponent<DebugPresenter>(entity);
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

    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationDepthSystem))]
    public partial struct Space4XResourcePickupPresentationDriveSystem : ISystem
    {
        private ComponentLookup<SpawnResource> _spawnLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<LocalToWorld> _sourceLocalToWorldLookup;
        private ComponentLookup<LocalTransform> _sourceTransformLookup;

        private const float PickupDisableThreshold = 0.01f;

        public void OnCreate(ref SystemState state)
        {
            _spawnLookup = state.GetComponentLookup<SpawnResource>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _sourceLocalToWorldLookup = state.GetComponentLookup<LocalToWorld>(true);
            _sourceTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            _spawnLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _sourceLocalToWorldLookup.Update(ref state);
            _sourceTransformLookup.Update(ref state);

            var config = SystemAPI.TryGetSingleton<Space4XResourcePickupPresentationConfig>(out var configValue)
                ? configValue
                : Space4XResourcePickupPresentationConfig.Default;
            var deltaTime = (float)SystemAPI.Time.DeltaTime;
            var smoothing = math.max(0f, config.Smoothing);
            var lerpFactor = smoothing <= 0f ? 1f : 1f - math.exp(-smoothing * deltaTime);

            foreach (var (owner, localToWorld, tint, meshEnabled) in SystemAPI
                         .Query<RefRO<RenderOwner>, RefRW<LocalToWorld>, RefRW<RenderTint>, EnabledRefRW<MeshPresenter>>()
                         .WithAll<ResourcePickupPresentationTag>())
            {
                var pickup = owner.ValueRO.Owner;
                if (pickup == Entity.Null ||
                    !_spawnLookup.HasComponent(pickup) ||
                    !_transformLookup.HasComponent(pickup))
                {
                    meshEnabled.ValueRW = false;
                    continue;
                }

                var spawn = _spawnLookup[pickup];
                var transform = _transformLookup[pickup];

                meshEnabled.ValueRW = spawn.Amount > PickupDisableThreshold;
                if (!meshEnabled.ValueRW)
                {
                    continue;
                }

                tint.ValueRW.Value = ResolveResourceColor(spawn.Type);

                float targetScale = ResolvePickupScale(spawn.Amount, config);
                float currentScale = math.length(localToWorld.ValueRO.Value.c0.xyz);
                if (currentScale <= 0.0001f)
                {
                    currentScale = config.MinScale;
                }
                float resolvedScale = math.max(0.001f, math.lerp(currentScale, targetScale, lerpFactor));

                float3 worldPos = transform.Position + new float3(0f, config.Lift, 0f);
                if (config.UseSourceEntityAlignment &&
                    spawn.SourceEntity != Entity.Null &&
                    _sourceLocalToWorldLookup.HasComponent(spawn.SourceEntity) &&
                    _sourceTransformLookup.HasComponent(spawn.SourceEntity))
                {
                    var sourceL2W = _sourceLocalToWorldLookup[spawn.SourceEntity].Value;
                    var sourceTransform = _sourceTransformLookup[spawn.SourceEntity];
                    float3 depthOffset = sourceL2W.c3.xyz - sourceTransform.Position;
                    worldPos = transform.Position + depthOffset + config.LocalOffsetWhenAligned;
                }

                localToWorld.ValueRW.Value = float4x4.TRS(worldPos, quaternion.identity, new float3(resolvedScale));
            }
        }

        private static float ResolvePickupScale(float amount, in Space4XResourcePickupPresentationConfig config)
        {
            float denominator = math.max(0.001f, config.AmountForMaxScale);
            float t = math.saturate(amount / denominator);
            float factor = math.sqrt(t);
            return math.lerp(config.MinScale, config.MaxScale, factor);
        }

        private static float4 ResolveResourceColor(ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.Minerals => ResourceTypeColor.Minerals.Value,
                ResourceType.RareMetals => ResourceTypeColor.RareMetals.Value,
                ResourceType.EnergyCrystals => ResourceTypeColor.EnergyCrystals.Value,
                ResourceType.OrganicMatter => ResourceTypeColor.OrganicMatter.Value,
                ResourceType.Ore => ResourceTypeColor.Ore.Value,
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

    [BurstCompile]
    [UpdateInGroup(typeof(PDStructuralChangePresentationSystemGroup))]
    public partial struct Space4XResourcePickupPresentationCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (owner, entity) in SystemAPI
                         .Query<RefRO<RenderOwner>>()
                         .WithAll<ResourcePickupPresentationTag>()
                         .WithEntityAccess())
            {
                var pickup = owner.ValueRO.Owner;
                if (pickup == Entity.Null || !SystemAPI.Exists(pickup) || !SystemAPI.HasComponent<SpawnResource>(pickup))
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
