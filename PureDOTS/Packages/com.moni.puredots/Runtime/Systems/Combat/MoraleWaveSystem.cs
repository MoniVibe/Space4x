using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Propagates morale waves when units break or rout.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MoraleWaveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;
            var deltaTime = SystemAPI.Time.DeltaTime;

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Check for units that broke/routed and emit morale waves
            var moraleLookup = SystemAPI.GetComponentLookup<CombatStats>(true);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

            foreach (var (stats, transform, entity) in SystemAPI.Query<
                RefRO<CombatStats>,
                RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                // Check if morale dropped below threshold (routed)
                float morale = stats.ValueRO.Morale;
                var threshold = MoraleWaveService.GetMoraleThreshold(morale);

                if (threshold == MoraleThreshold.Routed && !SystemAPI.HasComponent<MoraleWave>(entity))
                {
                    // Emit morale wave
                    var config = new MoralePropagationConfig
                    {
                        PropagationRadius = 20f,
                        PropagationDecay = 0.8f,
                        PropagationDelay = 0.1f,
                        MinIntensity = 0.1f
                    };

                    ecb.AddComponent(entity, new MoraleWave
                    {
                        SourceEntity = entity,
                        Intensity = -0.3f, // Negative = demoralizing
                        Radius = config.PropagationRadius,
                        EmittedTick = currentTick,
                        PropagationDelay = config.PropagationDelay
                    });
                    ecb.AddComponent(entity, config);
                }
            }

            // Propagate existing morale waves
            if (SystemAPI.TryGetSingleton(out SpatialGridConfig spatialConfig))
            {
                var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
                var cellRanges = SystemAPI.GetBuffer<SpatialGridCellRange>(gridEntity);
                var gridEntries = SystemAPI.GetBuffer<SpatialGridEntry>(gridEntity);

                foreach (var (wave, config, sourceTransform, sourceEntity) in SystemAPI.Query<
                    RefRO<MoraleWave>,
                    RefRO<MoralePropagationConfig>,
                    RefRO<LocalTransform>>()
                    .WithEntityAccess())
                {
                    // Check if delay has passed
                    float timeSinceEmission = (currentTick - wave.ValueRO.EmittedTick) * deltaTime;
                    if (timeSinceEmission < wave.ValueRO.PropagationDelay)
                        continue;

                    // Find nearby entities
                    var nearbyEntities = new NativeList<Entity>(32, Allocator.Temp);
                    var sourcePos = sourceTransform.ValueRO.Position;
                    SpatialQueryHelper.GetEntitiesWithinRadius(
                        ref sourcePos,
                        wave.ValueRO.Radius,
                        spatialConfig,
                        cellRanges,
                        gridEntries,
                        ref nearbyEntities);

                    // Apply morale change to nearby entities
                    foreach (var targetEntity in nearbyEntities)
                    {
                        if (targetEntity == sourceEntity || !moraleLookup.HasComponent(targetEntity))
                            continue;

                        if (!transformLookup.HasComponent(targetEntity))
                            continue;

                        var targetTransform = transformLookup[targetEntity];
                        float distance = math.distance(sourceTransform.ValueRO.Position, targetTransform.Position);

                        float intensity = MoraleWaveService.CalculateWaveIntensity(
                            wave.ValueRO.Intensity,
                            distance,
                            wave.ValueRO.Radius,
                            config.ValueRO.PropagationDecay);

                        if (MoraleWaveService.ShouldPropagate(intensity, distance, config.ValueRO.MinIntensity))
                        {
                            // Mark entity as affected by morale wave
                            // Actual morale application happens in a separate system that has write access
                            if (!SystemAPI.HasComponent<MoraleWaveTarget>(targetEntity))
                            {
                                ecb.AddComponent(targetEntity, new MoraleWaveTarget
                                {
                                    WaveSource = sourceEntity,
                                    AppliedIntensity = intensity,
                                    AppliedTick = currentTick
                                });
                            }
                        }
                    }

                    nearbyEntities.Dispose();

                    // Remove wave after propagation
                    ecb.RemoveComponent<MoraleWave>(sourceEntity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

