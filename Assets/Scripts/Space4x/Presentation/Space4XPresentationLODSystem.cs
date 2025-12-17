using PureDOTS.Rendering;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Rendering;
using Space4X.Presentation.Camera;
using Unity.Burst;
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
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial struct Space4XPresentationLODSystem : ISystem
    {
        private uint _tick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _tick = 0;
            state.RequireForUpdate<Space4XCameraState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
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

            var thresholds = new LODThresholds
            {
                LOD1Distance = lodConfig.FullDetailMaxDistance,
                LOD2Distance = lodConfig.ReducedDetailMaxDistance,
                LOD3Distance = lodConfig.ImpostorMaxDistance,
                Hysteresis = 0f
            };

            new UpdateLODJob
            {
                CameraPosition = cameraState.Position,
                Thresholds = thresholds,
                CurrentTick = ++_tick
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct UpdateLODJob : IJobEntity
        {
            public float3 CameraPosition;
            public LODThresholds Thresholds;
            public uint CurrentTick;

            public void Execute(ref RenderLODData lodData, in LocalTransform transform)
            {
                float distance = math.distance(transform.Position, CameraPosition);
                lodData.CameraDistance = distance;
                lodData.RecommendedLOD = RenderLODHelpers.CalculateLOD(distance, in Thresholds);
                lodData.LastUpdateTick = CurrentTick;
            }
        }
    }

    /// <summary>
    /// Manages render density by toggling RenderSampleIndex.ShouldRender based on the configured density values.
    /// Targets craft entities to keep scene density manageable when scaling entity counts.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XRenderDensitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<RenderDensityConfig>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, RenderDensityConfig.Default);
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
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
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XPresentationModeSystem : ISystem
    {
        private EntityQuery _presenterQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _presenterQuery = SystemAPI.QueryBuilder()
                .WithAll<RenderLODData, MeshPresenter, SpritePresenter>()
                .Build();

            state.RequireForUpdate(_presenterQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
            {
                return;
            }

            state.Dependency = new UpdatePresentationModeJob().ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public partial struct UpdatePresentationModeJob : IJobEntity
        {
            public void Execute(in RenderLODData lodData,
                                EnabledRefRW<MeshPresenter> meshEnabled,
                                EnabledRefRW<SpritePresenter> spriteEnabled)
            {
                bool wantSprite = lodData.RecommendedLOD >= 2;
                meshEnabled.ValueRW = !wantSprite;
                spriteEnabled.ValueRW = wantSprite;
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
            FullDetailMaxDistance = 100f,
            ReducedDetailMaxDistance = 500f,
            ImpostorMaxDistance = 2000f
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
}
