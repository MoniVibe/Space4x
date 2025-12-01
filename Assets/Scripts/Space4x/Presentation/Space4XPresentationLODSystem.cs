using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that assigns LOD levels to presentation entities based on camera distance.
    /// Runs early in PresentationSystemGroup before other presentation systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial struct Space4XPresentationLODSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XCameraState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get camera position
            if (!SystemAPI.TryGetSingleton<Space4XCameraState>(out var cameraState))
            {
                return;
            }

            // Get LOD config or use defaults
            var lodConfig = SystemAPI.HasSingleton<PresentationLODConfig>()
                ? SystemAPI.GetSingleton<PresentationLODConfig>()
                : PresentationLODConfig.Default;

            var cameraPosition = cameraState.Position;

            // Update LOD for all entities with PresentationLOD component
            new UpdateLODJob
            {
                CameraPosition = cameraPosition,
                FullDetailMaxDistance = lodConfig.FullDetailMaxDistance,
                ReducedDetailMaxDistance = lodConfig.ReducedDetailMaxDistance,
                ImpostorMaxDistance = lodConfig.ImpostorMaxDistance
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct UpdateLODJob : IJobEntity
        {
            public float3 CameraPosition;
            public float FullDetailMaxDistance;
            public float ReducedDetailMaxDistance;
            public float ImpostorMaxDistance;

            public void Execute(ref PresentationLOD lod, in LocalTransform transform)
            {
                // Calculate distance to camera
                float distance = math.distance(transform.Position, CameraPosition);
                lod.DistanceToCamera = distance;

                // Assign LOD level based on distance
                if (distance <= FullDetailMaxDistance)
                {
                    lod.Level = PresentationLODLevel.FullDetail;
                }
                else if (distance <= ReducedDetailMaxDistance)
                {
                    lod.Level = PresentationLODLevel.ReducedDetail;
                }
                else if (distance <= ImpostorMaxDistance)
                {
                    lod.Level = PresentationLODLevel.Impostor;
                }
                else
                {
                    lod.Level = PresentationLODLevel.Hidden;
                }
            }
        }
    }

    /// <summary>
    /// System that manages render density by enabling/disabling ShouldRenderTag based on density settings.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XRenderDensitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Create default config if none exists
            if (!SystemAPI.HasSingleton<RenderDensityConfig>())
            {
                var configEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(configEntity, RenderDensityConfig.Default);
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get render density config
            if (!SystemAPI.TryGetSingleton<RenderDensityConfig>(out var densityConfig))
            {
                return;
            }

            // If density is 1.0, all entities should render - no need to filter
            if (densityConfig.Density >= 1f)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // For crafts, apply density sampling
            foreach (var (sampleIndex, lod, entity) in SystemAPI
                         .Query<RefRO<RenderSampleIndex>, RefRO<PresentationLOD>>()
                         .WithAll<CraftPresentationTag>()
                         .WithEntityAccess())
            {
                bool shouldRender = ShouldRenderEntity(sampleIndex.ValueRO.Index, densityConfig.Density);
                bool hasRenderTag = state.EntityManager.HasComponent<ShouldRenderTag>(entity);

                if (shouldRender && !hasRenderTag)
                {
                    ecb.AddComponent<ShouldRenderTag>(entity);
                }
                else if (!shouldRender && hasRenderTag)
                {
                    ecb.RemoveComponent<ShouldRenderTag>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static bool ShouldRenderEntity(uint sampleIndex, float density)
        {
            // Stable sampling based on entity index
            float sampleValue = (sampleIndex % 1000) / 1000.0f;
            return sampleValue < density;
        }
    }

    /// <summary>
    /// Authoring component for LOD configuration singleton.
    /// </summary>
    public class PresentationLODConfigAuthoring : UnityEngine.MonoBehaviour
    {
        [UnityEngine.Header("LOD Distance Thresholds")]
        [UnityEngine.Tooltip("Distance threshold for FullDetail → ReducedDetail")]
        public float FullDetailMaxDistance = 100f;

        [UnityEngine.Tooltip("Distance threshold for ReducedDetail → Impostor")]
        public float ReducedDetailMaxDistance = 500f;

        [UnityEngine.Tooltip("Distance threshold for Impostor → Hidden")]
        public float ImpostorMaxDistance = 2000f;
    }

    /// <summary>
    /// Baker for PresentationLODConfigAuthoring.
    /// </summary>
    public class PresentationLODConfigBaker : Baker<PresentationLODConfigAuthoring>
    {
        public override void Bake(PresentationLODConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PresentationLODConfig
            {
                FullDetailMaxDistance = authoring.FullDetailMaxDistance,
                ReducedDetailMaxDistance = authoring.ReducedDetailMaxDistance,
                ImpostorMaxDistance = authoring.ImpostorMaxDistance
            });
        }
    }

    /// <summary>
    /// Authoring component for render density configuration singleton.
    /// </summary>
    public class RenderDensityConfigAuthoring : UnityEngine.MonoBehaviour
    {
        [UnityEngine.Header("Render Density Settings")]
        [UnityEngine.Range(0f, 1f)]
        [UnityEngine.Tooltip("Density value from 0.0 (none) to 1.0 (all)")]
        public float Density = 1f;

        [UnityEngine.Tooltip("Enable automatic density adjustment based on performance")]
        public bool AutoAdjust = false;
    }

    /// <summary>
    /// Baker for RenderDensityConfigAuthoring.
    /// </summary>
    public class RenderDensityConfigBaker : Baker<RenderDensityConfigAuthoring>
    {
        public override void Bake(RenderDensityConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new RenderDensityConfig
            {
                Density = authoring.Density,
                AutoAdjust = authoring.AutoAdjust
            });
        }
    }
}

