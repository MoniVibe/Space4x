using PureDOTS.Rendering;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Rendering;
using Space4X.Presentation.Camera;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// Updates RenderLODData for Space4X entities based on the active camera position and configurable thresholds.
    /// Runs first in the presentation phase so downstream systems can respect RecommendedLOD values.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial struct Space4XPresentationLODSystem : ISystem
    {
        private uint _tick;
        private ComponentLookup<PresentationLayer> _layerLookup;
        private ComponentLookup<Space4XRenderFrameState> _renderFrameLookup;

        public void OnCreate(ref SystemState state)
        {
            _tick = 0;
            state.RequireForUpdate<Space4XCameraState>();
            _layerLookup = state.GetComponentLookup<PresentationLayer>(true);
            _renderFrameLookup = state.GetComponentLookup<Space4XRenderFrameState>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<Space4XCameraState>(out var cameraState))
            {
                return;
            }

            var lodConfig = SystemAPI.TryGetSingleton<PresentationLODConfig>(out var config)
                ? config
                : PresentationLODConfig.Default;

            var layerConfig = SystemAPI.TryGetSingleton<PresentationLayerConfig>(out var layerOverride)
                ? layerOverride
                : PresentationLayerConfig.Default;

            _layerLookup.Update(ref state);
            _renderFrameLookup.Update(ref state);

            var useRenderFrame = SystemAPI.TryGetSingletonEntity<Space4XRenderFrameState>(out var renderFrameEntity) &&
                                 SystemAPI.TryGetSingleton<Space4XRenderFrameConfig>(out var renderFrameConfig) &&
                                 renderFrameConfig.Enabled != 0;
            var renderFrameState = useRenderFrame ? _renderFrameLookup[renderFrameEntity] : default;
            var cameraPosition = cameraState.Position;
            if (useRenderFrame && renderFrameState.AnchorFrame != Entity.Null)
            {
                var scale = math.max(0.01f, renderFrameState.Scale);
                if (math.abs(scale - 1f) > 0.0001f)
                {
                    cameraPosition = renderFrameState.AnchorPosition + (cameraPosition - renderFrameState.AnchorPosition) * scale;
                }
            }

            new UpdateLODJob
            {
                CameraPosition = cameraPosition,
                UseRenderFrame = useRenderFrame ? 1 : 0,
                RenderFrameAnchor = renderFrameState.AnchorPosition,
                RenderFrameScale = renderFrameState.Scale,
                BaseConfig = lodConfig,
                LayerConfig = layerConfig,
                LayerLookup = _layerLookup,
                CurrentTick = ++_tick
            }.ScheduleParallel();
        }

        private partial struct UpdateLODJob : IJobEntity
        {
            public float3 CameraPosition;
            public byte UseRenderFrame;
            public float3 RenderFrameAnchor;
            public float RenderFrameScale;
            public PresentationLODConfig BaseConfig;
            public PresentationLayerConfig LayerConfig;
            [ReadOnly] public ComponentLookup<PresentationLayer> LayerLookup;
            public uint CurrentTick;

            public void Execute(Entity entity,
                                ref RenderLODData lodData,
                                ref RenderKey renderKey,
                                ref RenderCullable cullable,
                                in LocalTransform transform)
            {
                var layer = LayerLookup.HasComponent(entity)
                    ? LayerLookup[entity].Value
                    : PresentationLayerId.System;

                float multiplier = ResolveLayerMultiplier(layer);
                var lod1 = math.max(1f, BaseConfig.FullDetailMaxDistance * multiplier);
                var lod2 = math.max(BaseConfig.FullDetailMaxDistance, BaseConfig.ReducedDetailMaxDistance) * multiplier;
                var lod3 = math.max(lod2, BaseConfig.ImpostorMaxDistance * multiplier);
                var thresholds = new LODThresholds
                {
                    LOD1Distance = lod1,
                    LOD2Distance = lod2,
                    LOD3Distance = lod3,
                    Hysteresis = 0f
                };

                var position = transform.Position;
                if (UseRenderFrame != 0)
                {
                    var scale = math.max(0.01f, RenderFrameScale);
                    if (math.abs(scale - 1f) > 0.0001f)
                    {
                        position = RenderFrameAnchor + (position - RenderFrameAnchor) * scale;
                    }
                }

                float distance = math.distance(position, CameraPosition);
                lodData.CameraDistance = distance;
                lodData.RecommendedLOD = RenderLODHelpers.CalculateLOD(distance, in thresholds);
                lodData.LastUpdateTick = CurrentTick;

                var lod = lodData.RecommendedLOD;
                renderKey.LOD = (byte)math.min((int)lod, 2);
                cullable.CullDistance = thresholds.LOD3Distance;
            }

            private float ResolveLayerMultiplier(PresentationLayerId layer)
            {
                return layer switch
                {
                    PresentationLayerId.Colony => LayerConfig.ColonyMultiplier,
                    PresentationLayerId.Island => LayerConfig.IslandMultiplier,
                    PresentationLayerId.Continent => LayerConfig.ContinentMultiplier,
                    PresentationLayerId.Planet => LayerConfig.PlanetMultiplier,
                    PresentationLayerId.Orbital => LayerConfig.OrbitalMultiplier,
                    PresentationLayerId.Galactic => LayerConfig.GalacticMultiplier,
                    _ => LayerConfig.SystemMultiplier
                };
            }
        }
    }

    /// <summary>
    /// Manages render density by toggling RenderSampleIndex.ShouldRender based on the configured density values.
    /// Targets craft entities to keep scene density manageable when scaling entity counts.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XRenderDensitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<RenderDensityConfig>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, RenderDensityConfig.Default);
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RenderDensityConfig>(out var densityConfig))
            {
                return;
            }

            var density = math.clamp(densityConfig.Density, 0f, 1f);
            if (density >= 0.999f)
            {
                return;
            }

            foreach (var sampleIndex in SystemAPI
                         .Query<RefRW<RenderSampleIndex>>()
                         .WithAll<CraftPresentationTag>())
            {
                float normalized = sampleIndex.ValueRO.SampleModulus > 0
                    ? (sampleIndex.ValueRO.SampleIndex % sampleIndex.ValueRO.SampleModulus) / (float)sampleIndex.ValueRO.SampleModulus
                    : 0f;

                bool shouldRender = normalized <= density;
                sampleIndex.ValueRW.ShouldRender = shouldRender ? (byte)1 : (byte)0;
            }
        }
    }

    /// <summary>
    /// Toggles presenter enablement based on the resolved LOD so impostors can render via SpritePresenter.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    [UpdateAfter(typeof(RenderVariantResolveSystem))]
    public partial struct Space4XPresentationModeSystem : ISystem
    {
        private EntityQuery _presenterQuery;

        public void OnCreate(ref SystemState state)
        {
            _presenterQuery = SystemAPI.QueryBuilder()
                .WithAll<RenderLODData, RenderVariantResolved, MeshPresenter, SpritePresenter>()
                .Build();

            state.RequireForUpdate(_presenterQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            state.Dependency = new UpdatePresentationModeJob().ScheduleParallel(state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public partial struct UpdatePresentationModeJob : IJobEntity
        {
            public void Execute(in RenderLODData lodData,
                                in RenderVariantResolved resolved,
                                EnabledRefRW<MeshPresenter> meshEnabled,
                                EnabledRefRW<SpritePresenter> spriteEnabled)
            {
                bool spriteAvailable = (resolved.LastMask & RenderPresenterMask.Sprite) != 0;
                bool meshAvailable = (resolved.LastMask & RenderPresenterMask.Mesh) != 0;
                bool useSprite = spriteAvailable && lodData.RecommendedLOD > 0;

                meshEnabled.ValueRW = meshAvailable && !useSprite;
                spriteEnabled.ValueRW = spriteAvailable && useSprite;
            }
        }
    }

    /// <summary>
    /// Configurable LOD distance thresholds for presentation systems.
    /// </summary>
    public struct PresentationLODConfig : IComponentData
    {
        public float FullDetailMaxDistance;
        public float ReducedDetailMaxDistance;
        public float ImpostorMaxDistance;

        public static PresentationLODConfig Default => new PresentationLODConfig
        {
            FullDetailMaxDistance = 2000f,
            ReducedDetailMaxDistance = 10000f,
            ImpostorMaxDistance = 40000f
        };
    }

    /// <summary>
    /// Multiplier configuration for distance layers (colony → galactic).
    /// </summary>
    public struct PresentationLayerConfig : IComponentData
    {
        public float ColonyMultiplier;
        public float IslandMultiplier;
        public float ContinentMultiplier;
        public float PlanetMultiplier;
        public float OrbitalMultiplier;
        public float SystemMultiplier;
        public float GalacticMultiplier;

        public static PresentationLayerConfig Default => new PresentationLayerConfig
        {
            ColonyMultiplier = 0.15f,
            IslandMultiplier = 0.3f,
            ContinentMultiplier = 0.6f,
            PlanetMultiplier = 1f,
            OrbitalMultiplier = 2f,
            SystemMultiplier = 6f,
            GalacticMultiplier = 20f
        };
    }

    /// <summary>
    /// Presentation scale multipliers per layer (colony → galactic).
    /// </summary>
    public struct PresentationScaleConfig : IComponentData
    {
        public float ColonyMultiplier;
        public float IslandMultiplier;
        public float ContinentMultiplier;
        public float PlanetMultiplier;
        public float OrbitalMultiplier;
        public float SystemMultiplier;
        public float GalacticMultiplier;

        public static PresentationScaleConfig Default => new PresentationScaleConfig
        {
            ColonyMultiplier = 1f,
            IslandMultiplier = 1.1f,
            ContinentMultiplier = 1.2f,
            PlanetMultiplier = 1.35f,
            OrbitalMultiplier = 1.6f,
            SystemMultiplier = 2f,
            GalacticMultiplier = 3f
        };
    }

    /// <summary>
    /// Configures render density sampling for presentation entities.
    /// </summary>
    public struct RenderDensityConfig : IComponentData
    {
        public float Density;
        public bool AutoAdjust;

        public static RenderDensityConfig Default => new RenderDensityConfig
        {
            Density = 1f,
            AutoAdjust = false
        };
    }

    [DisallowMultipleComponent]
    public sealed class PresentationLODConfigAuthoring : MonoBehaviour
    {
        [Header("LOD Distance Thresholds")]
        [Tooltip("Distance threshold for FullDetail → ReducedDetail")]
        public float FullDetailMaxDistance = PresentationLODConfig.Default.FullDetailMaxDistance;

        [Tooltip("Distance threshold for ReducedDetail → Impostor")]
        public float ReducedDetailMaxDistance = PresentationLODConfig.Default.ReducedDetailMaxDistance;

        [Tooltip("Distance threshold for Impostor → Hidden")]
        public float ImpostorMaxDistance = PresentationLODConfig.Default.ImpostorMaxDistance;
    }

    public sealed class PresentationLODConfigBaker : Baker<PresentationLODConfigAuthoring>
    {
        public override void Bake(PresentationLODConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PresentationLODConfig
            {
                FullDetailMaxDistance = math.max(1f, authoring.FullDetailMaxDistance),
                ReducedDetailMaxDistance = math.max(authoring.FullDetailMaxDistance, authoring.ReducedDetailMaxDistance),
                ImpostorMaxDistance = math.max(authoring.ReducedDetailMaxDistance, authoring.ImpostorMaxDistance)
            });
        }
    }

    [DisallowMultipleComponent]
    public sealed class RenderDensityConfigAuthoring : MonoBehaviour
    {
        [Range(0f, 1f)]
        public float Density = RenderDensityConfig.Default.Density;
        public bool AutoAdjust = RenderDensityConfig.Default.AutoAdjust;
    }

    public sealed class RenderDensityConfigBaker : Baker<RenderDensityConfigAuthoring>
    {
        public override void Bake(RenderDensityConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new RenderDensityConfig
            {
                Density = math.clamp(authoring.Density, 0f, 1f),
                AutoAdjust = authoring.AutoAdjust
            });
        }
    }

    [DisallowMultipleComponent]
    public sealed class PresentationLayerConfigAuthoring : MonoBehaviour
    {
        [Header("Layer Distance Multipliers")]
        public float ColonyMultiplier = PresentationLayerConfig.Default.ColonyMultiplier;
        public float IslandMultiplier = PresentationLayerConfig.Default.IslandMultiplier;
        public float ContinentMultiplier = PresentationLayerConfig.Default.ContinentMultiplier;
        public float PlanetMultiplier = PresentationLayerConfig.Default.PlanetMultiplier;
        public float OrbitalMultiplier = PresentationLayerConfig.Default.OrbitalMultiplier;
        public float SystemMultiplier = PresentationLayerConfig.Default.SystemMultiplier;
        public float GalacticMultiplier = PresentationLayerConfig.Default.GalacticMultiplier;
    }

    public sealed class PresentationLayerConfigBaker : Baker<PresentationLayerConfigAuthoring>
    {
        public override void Bake(PresentationLayerConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PresentationLayerConfig
            {
                ColonyMultiplier = authoring.ColonyMultiplier,
                IslandMultiplier = authoring.IslandMultiplier,
                ContinentMultiplier = authoring.ContinentMultiplier,
                PlanetMultiplier = authoring.PlanetMultiplier,
                OrbitalMultiplier = authoring.OrbitalMultiplier,
                SystemMultiplier = authoring.SystemMultiplier,
                GalacticMultiplier = authoring.GalacticMultiplier
            });
        }
    }

    [DisallowMultipleComponent]
    public sealed class PresentationScaleConfigAuthoring : MonoBehaviour
    {
        [Header("Layer Scale Multipliers")]
        public float ColonyMultiplier = PresentationScaleConfig.Default.ColonyMultiplier;
        public float IslandMultiplier = PresentationScaleConfig.Default.IslandMultiplier;
        public float ContinentMultiplier = PresentationScaleConfig.Default.ContinentMultiplier;
        public float PlanetMultiplier = PresentationScaleConfig.Default.PlanetMultiplier;
        public float OrbitalMultiplier = PresentationScaleConfig.Default.OrbitalMultiplier;
        public float SystemMultiplier = PresentationScaleConfig.Default.SystemMultiplier;
        public float GalacticMultiplier = PresentationScaleConfig.Default.GalacticMultiplier;
    }

    public sealed class PresentationScaleConfigBaker : Baker<PresentationScaleConfigAuthoring>
    {
        public override void Bake(PresentationScaleConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PresentationScaleConfig
            {
                ColonyMultiplier = authoring.ColonyMultiplier,
                IslandMultiplier = authoring.IslandMultiplier,
                ContinentMultiplier = authoring.ContinentMultiplier,
                PlanetMultiplier = authoring.PlanetMultiplier,
                OrbitalMultiplier = authoring.OrbitalMultiplier,
                SystemMultiplier = authoring.SystemMultiplier,
                GalacticMultiplier = authoring.GalacticMultiplier
            });
        }
    }
}
