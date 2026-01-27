using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Systems.Rendering
{
    /// <summary>
    /// Assigns and updates render sample indices for density control.
    /// Runs in Unity.Entities.PresentationSystemGroup (non-deterministic, frame-time).
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateBefore(typeof(AggregateRenderSummarySystem))]
    public partial struct RenderDensitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Create default density settings if not present
            if (!SystemAPI.TryGetSingleton<RenderDensitySettings>(out _))
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new RenderDensitySettings
                {
                    CurrentDensity = 1,
                    MaxRenderCount = 0,
                    LastUpdateTick = 0
                });
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RenderDensitySettings>(out var densitySettings))
            {
                return;
            }

            var currentDensity = densitySettings.CurrentDensity;
            if (currentDensity == 0) currentDensity = 1;

            // Update ShouldRender flag for all entities with RenderSampleIndex
            foreach (var (sampleIndex, entity) in 
                SystemAPI.Query<RefRW<RenderSampleIndex>>()
                    .WithEntityAccess())
            {
                var shouldRender = RenderLODHelpers.ShouldRenderAtDensity(
                    sampleIndex.ValueRO.SampleIndex, 
                    currentDensity);
                
                sampleIndex.ValueRW.ShouldRender = shouldRender ? (byte)1 : (byte)0;
            }
        }
    }

    /// <summary>
    /// Assigns RenderSampleIndex to new entities that have RenderCullable.
    /// Runs in SimulationSystemGroup to catch newly created entities.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct RenderSampleIndexAssignmentSystem : ISystem
    {
        private const ushort DefaultModulus = 100;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderCullable>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Assign sample index to entities with RenderCullable but no RenderSampleIndex
            foreach (var (cullable, entity) in 
                SystemAPI.Query<RefRO<RenderCullable>>()
                    .WithNone<RenderSampleIndex>()
                    .WithEntityAccess())
            {
                var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, DefaultModulus);
                
                ecb.AddComponent(entity, new RenderSampleIndex
                {
                    SampleIndex = sampleIndex,
                    SampleModulus = DefaultModulus,
                    ShouldRender = 1 // Default to visible
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Updates RenderLODData based on camera distance and thresholds.
    /// This system should be updated by game-side camera systems with actual camera position.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct RenderLODUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderLODData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Get LOD thresholds if available
            var hasThresholds = SystemAPI.TryGetSingleton<LODThresholds>(out var thresholds);
            if (!hasThresholds)
            {
                // Use default thresholds
                thresholds = new LODThresholds
                {
                    LOD1Distance = 50f,
                    LOD2Distance = 100f,
                    LOD3Distance = 200f,
                    Hysteresis = 5f
                };
            }

            var tick = 0u;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                tick = timeState.Tick;
            }

            // Update LOD recommendations based on camera distance
            // Note: CameraDistance should be updated by game-side camera systems
            foreach (var lodData in SystemAPI.Query<RefRW<RenderLODData>>())
            {
                var distance = lodData.ValueRO.CameraDistance;
                var currentLOD = lodData.ValueRO.RecommendedLOD;
                
                // Calculate new LOD with hysteresis to prevent flickering
                var newLOD = RenderLODHelpers.CalculateLOD(distance, in thresholds);
                
                // Apply hysteresis - only change LOD if we've moved significantly past threshold
                if (newLOD != currentLOD)
                {
                    var shouldChange = true;
                    
                    // Check hysteresis based on direction of change
                    if (newLOD > currentLOD)
                    {
                        // Moving to lower detail - check we're past threshold + hysteresis
                        var threshold = GetThresholdForLOD(currentLOD, in thresholds);
                        shouldChange = distance > threshold + thresholds.Hysteresis;
                    }
                    else
                    {
                        // Moving to higher detail - check we're past threshold - hysteresis
                        var threshold = GetThresholdForLOD(newLOD, in thresholds);
                        shouldChange = distance < threshold - thresholds.Hysteresis;
                    }

                    if (shouldChange)
                    {
                        lodData.ValueRW.RecommendedLOD = newLOD;
                        lodData.ValueRW.LastUpdateTick = tick;
                    }
                }
            }
        }

        private static float GetThresholdForLOD(byte lod, in LODThresholds thresholds)
        {
            return lod switch
            {
                0 => thresholds.LOD1Distance,
                1 => thresholds.LOD2Distance,
                2 => thresholds.LOD3Distance,
                _ => float.MaxValue
            };
        }
    }
}

