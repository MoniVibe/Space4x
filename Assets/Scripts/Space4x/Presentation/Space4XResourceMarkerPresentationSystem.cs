using PureDOTS.Rendering;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Rendering;
using PureDOTS.Systems;
using Space4X.Presentation.Camera;
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
    public partial struct Space4XResourceMarkerEnsureSystem : ISystem
    {
        private EntityQuery _missingMarkerQuery;
        private ComponentLookup<PresentationLayer> _presentationLayerLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderPresentationCatalog>();
            _missingMarkerQuery = SystemAPI.QueryBuilder()
                .WithAll<Asteroid, AsteroidPresentationTag>()
                .WithNone<ResourceMarkerLink>()
                .Build();
            _presentationLayerLookup = state.GetComponentLookup<PresentationLayer>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless || !RuntimeMode.IsRenderingEnabled || _missingMarkerQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var config = EnsureConfig(ref state);
            _presentationLayerLookup.Update(ref state);

            var endEcb = state.World.GetOrCreateSystemManaged<EndPresentationECBSystem>();
            var ecb = endEcb.CreateCommandBuffer();

            foreach (var (_, entity) in SystemAPI
                         .Query<RefRO<Asteroid>>()
                         .WithAll<AsteroidPresentationTag>()
                         .WithNone<ResourceMarkerLink>()
                         .WithEntityAccess())
            {
                var markerEntity = ecb.CreateEntity();
                ecb.AddComponent(markerEntity, new ResourceMarkerPresentationTag());
                ecb.AddComponent(markerEntity, new ResourceMarkerParent { Value = entity });
                ecb.AddComponent(markerEntity, new PresentationAttachTo
                {
                    ParentPresenter = entity,
                    LocalOffset = float3.zero
                });
                ecb.AddComponent<PresentationLocalToWorldOverride>(markerEntity);
                ecb.AddComponent(markerEntity, new LocalToWorld { Value = float4x4.identity });

                var layer = _presentationLayerLookup.HasComponent(entity)
                    ? _presentationLayerLookup[entity].Value
                    : PresentationLayerId.System;
                ecb.AddComponent(markerEntity, new PresentationLayer { Value = layer });

                AddRenderComponents(ref ecb, markerEntity, config);

                ecb.AddComponent(entity, new ResourceMarkerLink { MarkerEntity = markerEntity });
            }
        }

        private Space4XResourceMarkerPresentationConfig EnsureConfig(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<Space4XResourceMarkerPresentationConfig>(out var config))
            {
                return config;
            }

            var entity = state.EntityManager.CreateEntity();
            var defaults = Space4XResourceMarkerPresentationConfig.Default;
            state.EntityManager.AddComponentData(entity, defaults);
            state.EntityManager.SetName(entity, "Space4XResourceMarkerPresentationConfig");
            return defaults;
        }

        private static void AddRenderComponents(ref EntityCommandBuffer ecb, Entity entity, in Space4XResourceMarkerPresentationConfig config)
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
                ImportanceScore = 0.5f,
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });
            ecb.AddComponent(entity, new RenderCullable
            {
                CullDistance = 40000f,
                Priority = 110
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
    public partial struct Space4XResourceMarkerDriveSystem : ISystem
    {
        private ComponentLookup<Asteroid> _asteroidLookup;
        private ComponentLookup<LocalToWorld> _parentLocalToWorldLookup;
        private ComponentLookup<ResourceTypeColor> _resourceColorLookup;

        public void OnCreate(ref SystemState state)
        {
            _asteroidLookup = state.GetComponentLookup<Asteroid>(true);
            _parentLocalToWorldLookup = state.GetComponentLookup<LocalToWorld>(true);
            _resourceColorLookup = state.GetComponentLookup<ResourceTypeColor>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            _asteroidLookup.Update(ref state);
            _parentLocalToWorldLookup.Update(ref state);
            _resourceColorLookup.Update(ref state);

            var config = SystemAPI.TryGetSingleton<Space4XResourceMarkerPresentationConfig>(out var configValue)
                ? configValue
                : Space4XResourceMarkerPresentationConfig.Default;
            var hasCamera = SystemAPI.TryGetSingleton<Space4XCameraState>(out var cameraState);
            var timeSeconds = (float)SystemAPI.Time.ElapsedTime;
            var deltaTime = (float)SystemAPI.Time.DeltaTime;
            var smoothing = math.max(0f, config.Smoothing);
            var lerpFactor = smoothing <= 0f ? 1f : 1f - math.exp(-smoothing * deltaTime);

            foreach (var (parentLink, attach, markerLocalToWorld, markerTint, meshEnabled, entity) in SystemAPI
                         .Query<RefRO<ResourceMarkerParent>, RefRW<PresentationAttachTo>, RefRW<LocalToWorld>, RefRW<RenderTint>, EnabledRefRW<MeshPresenter>>()
                         .WithAll<ResourceMarkerPresentationTag>()
                         .WithEntityAccess())
            {
                var parent = parentLink.ValueRO.Value;
                if (parent == Entity.Null ||
                    !_asteroidLookup.HasComponent(parent) ||
                    !_parentLocalToWorldLookup.HasComponent(parent))
                {
                    meshEnabled.ValueRW = false;
                    continue;
                }

                var asteroid = _asteroidLookup[parent];
                float maxAmount = math.max(0.0001f, asteroid.MaxResourceAmount);
                float ratio = math.saturate(asteroid.ResourceAmount / maxAmount);
                bool hasResource = asteroid.ResourceAmount > 0f;
                meshEnabled.ValueRW = hasResource;
                if (!hasResource)
                {
                    continue;
                }

                var baseTint = ResolveResourceTint(parent, asteroid.ResourceType);
                bool isLow = asteroid.ResourceAmount <= config.DepletedThreshold;
                float pulse = isLow
                    ? 0.8f + 0.2f * math.sin(timeSeconds * 2.6f + entity.Index * 0.07f)
                    : 1f;
                float alpha = math.lerp(0.25f, 1f, ratio);
                markerTint.ValueRW.Value = new float4(baseTint.xyz * pulse, baseTint.w * alpha);

                float3 direction = ResolveDirection(asteroid.AsteroidId, entity);
                if (config.UseCameraHemisphere != 0 && hasCamera)
                {
                    float3 parentPos = _parentLocalToWorldLookup[parent].Value.c3.xyz;
                    float3 toCamera = cameraState.Position - parentPos;
                    float distanceSq = math.lengthsq(toCamera);
                    if (distanceSq > 0.0001f)
                    {
                        toCamera *= math.rsqrt(distanceSq);
                        if (math.dot(direction, toCamera) < 0f)
                        {
                            direction = -direction;
                        }
                    }
                }
                float parentScale = math.length(_parentLocalToWorldLookup[parent].Value.c0.xyz);
                float targetScale = math.lerp(config.BaseScale * 0.45f, config.MaxScale, ratio);
                float currentScale = parentScale > 0f
                    ? math.length(markerLocalToWorld.ValueRO.Value.c0.xyz) / parentScale
                    : config.BaseScale;
                float resolvedScale = math.max(0.001f, math.lerp(currentScale, targetScale, lerpFactor));
                float3 offset = direction * math.max(0f, config.OffsetMultiplier) * parentScale;

                if (math.any(attach.ValueRO.LocalOffset != offset))
                {
                    attach.ValueRW.LocalOffset = offset;
                }

                var parentLocalToWorld = _parentLocalToWorldLookup[parent].Value;
                var localMatrix = float4x4.TRS(offset, quaternion.identity, new float3(resolvedScale));
                markerLocalToWorld.ValueRW.Value = math.mul(parentLocalToWorld, localMatrix);
            }
        }

        private float4 ResolveResourceTint(Entity parent, ResourceType resourceType)
        {
            if (_resourceColorLookup.HasComponent(parent))
            {
                return _resourceColorLookup[parent].Value;
            }

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

        private static float3 ResolveDirection(in FixedString64Bytes asteroidId, Entity entity)
        {
            uint hash = asteroidId.Length > 0
                ? (uint)asteroidId.GetHashCode()
                : (uint)math.hash(new int3(entity.Index, entity.Version, 917));
            float u = (hash & 0xFFFF) / 65535f;
            float v = ((hash >> 16) & 0xFFFF) / 65535f;
            float theta = u * math.PI * 2f;
            float z = v * 2f - 1f;
            float r = math.sqrt(math.max(0f, 1f - z * z));
            return new float3(r * math.cos(theta), z, r * math.sin(theta));
        }
    }
}
