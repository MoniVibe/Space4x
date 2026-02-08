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
using PDUpdatePresentationSystemGroup = PureDOTS.Systems.UpdatePresentationSystemGroup;

namespace Space4X.Presentation
{
    [UpdateInGroup(typeof(PDStructuralChangePresentationSystemGroup))]
    public partial struct Space4XCarrierStorageMarkerEnsureSystem : ISystem
    {
        private EntityQuery _missingMarkerQuery;
        private ComponentLookup<PresentationLayer> _presentationLayerLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderPresentationCatalog>();
            _missingMarkerQuery = SystemAPI.QueryBuilder()
                .WithAll<Carrier, CarrierPresentationTag, ResourceStorage>()
                .WithNone<CarrierStorageMarkerLink>()
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
                         .Query<RefRO<Carrier>>()
                         .WithAll<CarrierPresentationTag, ResourceStorage>()
                         .WithNone<CarrierStorageMarkerLink>()
                         .WithEntityAccess())
            {
                var markerEntity = ecb.CreateEntity();
                ecb.AddComponent(markerEntity, new StorageMarkerPresentationTag());
                ecb.AddComponent(markerEntity, new CarrierStorageMarkerParent { Value = entity });
                ecb.AddComponent(markerEntity, new PresentationAttachTo
                {
                    ParentPresenter = entity,
                    LocalOffset = config.LocalOffset
                });
                ecb.AddComponent<PresentationLocalToWorldOverride>(markerEntity);
                ecb.AddComponent(markerEntity, new LocalToWorld { Value = float4x4.identity });

                var layer = _presentationLayerLookup.HasComponent(entity)
                    ? _presentationLayerLookup[entity].Value
                    : PresentationLayerId.System;
                ecb.AddComponent(markerEntity, new PresentationLayer { Value = layer });

                AddRenderComponents(ref ecb, markerEntity, config);

                ecb.AddComponent(entity, new CarrierStorageMarkerLink { MarkerEntity = markerEntity });
                ecb.AddComponent(markerEntity, new CarrierIntakePulseState
                {
                    Timer = 0f,
                    LastTotal = 0f,
                    LastType = ResourceType.Minerals
                });
            }
        }

        private Space4XCarrierStorageMarkerPresentationConfig EnsureConfig(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<Space4XCarrierStorageMarkerPresentationConfig>(out var config))
            {
                return config;
            }

            var entity = state.EntityManager.CreateEntity();
            var defaults = Space4XCarrierStorageMarkerPresentationConfig.Default;
            state.EntityManager.AddComponentData(entity, defaults);
            state.EntityManager.SetName(entity, "Space4XCarrierStorageMarkerPresentationConfig");
            return defaults;
        }

        private static void AddRenderComponents(ref EntityCommandBuffer ecb, Entity entity, in Space4XCarrierStorageMarkerPresentationConfig config)
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
                ImportanceScore = 0.6f,
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });
            ecb.AddComponent(entity, new RenderCullable
            {
                CullDistance = 60000f,
                Priority = 160
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
    [UpdateInGroup(typeof(PDUpdatePresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationDepthSystem))]
    [UpdateBefore(typeof(Unity.Rendering.EntitiesGraphicsSystem))]
    public partial struct Space4XCarrierStorageMarkerDriveSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<LocalToWorld> _parentLocalToWorldLookup;
        private BufferLookup<ResourceStorage> _storageLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _parentLocalToWorldLookup = state.GetComponentLookup<LocalToWorld>(true);
            _storageLookup = state.GetBufferLookup<ResourceStorage>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            _carrierLookup.Update(ref state);
            _parentLocalToWorldLookup.Update(ref state);
            _storageLookup.Update(ref state);

            var config = SystemAPI.TryGetSingleton<Space4XCarrierStorageMarkerPresentationConfig>(out var configValue)
                ? configValue
                : Space4XCarrierStorageMarkerPresentationConfig.Default;
            var deltaTime = (float)SystemAPI.Time.DeltaTime;
            var smoothing = math.max(0f, config.Smoothing);
            var lerpFactor = smoothing <= 0f ? 1f : 1f - math.exp(-smoothing * deltaTime);

            foreach (var (parentLink, attach, markerLocalToWorld, markerTint, meshEnabled, pulseState) in SystemAPI
                         .Query<RefRO<CarrierStorageMarkerParent>, RefRW<PresentationAttachTo>, RefRW<LocalToWorld>, RefRW<RenderTint>, EnabledRefRW<MeshPresenter>, RefRW<CarrierIntakePulseState>>()
                         .WithAll<StorageMarkerPresentationTag>())
            {
                var parent = parentLink.ValueRO.Value;
                if (parent == Entity.Null ||
                    !_carrierLookup.HasComponent(parent) ||
                    !_parentLocalToWorldLookup.HasComponent(parent) ||
                    !_storageLookup.HasBuffer(parent))
                {
                    meshEnabled.ValueRW = false;
                    continue;
                }

                var storage = _storageLookup[parent];
                ResolveStorageTotals(storage, out var totalAmount, out var totalCapacity, out var dominantType);

                float fill = totalCapacity > 0f ? math.saturate(totalAmount / totalCapacity) : 0f;
                bool isEnabled = meshEnabled.ValueRO;
                bool shouldEnable;
                if (config.UseHysteresis)
                {
                    shouldEnable = isEnabled
                        ? fill >= config.HideFillThreshold
                        : fill > config.ShowFillThreshold;
                }
                else
                {
                    shouldEnable = fill > config.DepletedThreshold;
                }

                meshEnabled.ValueRW = shouldEnable;
                if (!shouldEnable)
                {
                    pulseState.ValueRW.Timer = 0f;
                    pulseState.ValueRW.LastTotal = totalAmount;
                    continue;
                }

                UpdatePulse(ref pulseState.ValueRW, totalAmount, dominantType, config.PulseDuration, deltaTime);

                var baseColor = ResolveResourceColor(dominantType);
                markerTint.ValueRW.Value = ApplyPulse(baseColor, pulseState.ValueRO, config);

                float targetScale = math.lerp(config.BaseScale, config.MaxScale, fill);
                float parentScale = math.length(_parentLocalToWorldLookup[parent].Value.c0.xyz);
                float currentScale = parentScale > 0f
                    ? math.length(markerLocalToWorld.ValueRO.Value.c0.xyz) / parentScale
                    : config.BaseScale;
                float resolvedScale = math.max(0.001f, math.lerp(currentScale, targetScale, lerpFactor));

                if (math.any(attach.ValueRO.LocalOffset != config.LocalOffset))
                {
                    attach.ValueRW.LocalOffset = config.LocalOffset;
                }

                var parentLocalToWorld = _parentLocalToWorldLookup[parent].Value;
                var localMatrix = float4x4.TRS(attach.ValueRO.LocalOffset, quaternion.identity, new float3(resolvedScale));
                markerLocalToWorld.ValueRW.Value = math.mul(parentLocalToWorld, localMatrix);
            }
        }

        private static void UpdatePulse(ref CarrierIntakePulseState pulseState, float totalAmount, ResourceType dominantType, float duration, float deltaTime)
        {
            if (totalAmount - pulseState.LastTotal > 0.001f)
            {
                pulseState.Timer = math.max(duration, 0f);
                pulseState.LastType = dominantType;
            }

            pulseState.LastTotal = totalAmount;
            if (pulseState.Timer > 0f)
            {
                pulseState.Timer = math.max(0f, pulseState.Timer - deltaTime);
            }
        }

        private static float4 ApplyPulse(float4 baseColor, in CarrierIntakePulseState pulseState, in Space4XCarrierStorageMarkerPresentationConfig config)
        {
            if (config.PulseDuration <= 0f || pulseState.Timer <= 0f || config.PulseIntensity <= 0f)
            {
                return baseColor;
            }

            float strength = math.saturate(pulseState.Timer / config.PulseDuration);
            float intensity = 1f + strength * config.PulseIntensity;
            float3 rgb = math.min(baseColor.xyz * intensity, new float3(1f));
            return new float4(rgb, baseColor.w);
        }

        private static void ResolveStorageTotals(in DynamicBuffer<ResourceStorage> storage, out float totalAmount, out float totalCapacity, out ResourceType dominantType)
        {
            totalAmount = 0f;
            totalCapacity = 0f;
            dominantType = ResourceType.Minerals;
            float dominantAmount = 0f;
            const float tieEpsilon = 0.0001f;

            for (int i = 0; i < storage.Length; i++)
            {
                var entry = storage[i];
                totalAmount += math.max(0f, entry.Amount);
                totalCapacity += math.max(0f, entry.Capacity);

                if (entry.Amount > dominantAmount + tieEpsilon ||
                    (math.abs(entry.Amount - dominantAmount) <= tieEpsilon && entry.Type < dominantType))
                {
                    dominantAmount = entry.Amount;
                    dominantType = entry.Type;
                }
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
