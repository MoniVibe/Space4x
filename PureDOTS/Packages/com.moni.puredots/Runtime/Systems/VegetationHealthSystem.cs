using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Processes environmental effects on vegetation health.
    /// Compares environment state to species thresholds and applies health deltas.
    /// Runs before VegetationGrowthSystem to ensure health affects growth.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VegetationSystemGroup))]
    [UpdateBefore(typeof(VegetationGrowthSystem))]
    public partial struct VegetationHealthSystem : ISystem
    {
        private EntityQuery _vegetationQuery;
        private static readonly ProfilerMarker s_UpdateVegetationHealthMarker = 
            new ProfilerMarker("VegetationHealthSystem.Update");

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _vegetationQuery = SystemAPI.QueryBuilder()
                .WithAll<VegetationId, VegetationLifecycle, VegetationHealth, VegetationEnvironmentState, VegetationSpeciesIndex>()
                .WithNone<VegetationDeadTag, PlaybackGuardTag>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<VegetationSpeciesLookup>();
            state.RequireForUpdate(_vegetationQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (s_UpdateVegetationHealthMarker.Auto())
            {
                var timeState = SystemAPI.GetSingleton<TimeState>();
                if (timeState.IsPaused)
                {
                    return;
                }

                if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                {
                    return;
                }

                // Safety check: ensure species catalog exists
                if (!SystemAPI.HasSingleton<VegetationSpeciesLookup>())
                {
#if UNITY_EDITOR
                    UnityEngine.Debug.LogWarning("[VegetationHealthSystem] VegetationSpeciesLookup singleton not found. Skipping update.");
#endif
                    return;
                }

                var speciesLookup = SystemAPI.GetSingleton<VegetationSpeciesLookup>();

                if (!speciesLookup.CatalogBlob.IsCreated)
                {
#if UNITY_EDITOR
                    UnityEngine.Debug.LogWarning("[VegetationHealthSystem] Species catalog blob not created. Skipping update.");
#endif
                    return;
                }

                var job = new UpdateVegetationHealthJob
                {
                    DeltaTime = timeState.FixedDeltaTime,
                    CurrentTick = timeState.Tick,

                    SpeciesCatalogBlob = speciesLookup.CatalogBlob,
                    StressedTagLookup = state.GetComponentLookup<VegetationStressedTag>(false),
                    DyingTagLookup = state.GetComponentLookup<VegetationDyingTag>(false),
                    DeadTagLookup = state.GetComponentLookup<VegetationDeadTag>(false)
                };

                state.Dependency = job.ScheduleParallel(state.Dependency);

#if UNITY_EDITOR
                UnityEngine.Debug.Log($"[VegetationHealthSystem] Updated {_vegetationQuery.CalculateEntityCount()} vegetation entities at tick {timeState.Tick}");
#endif
            }
        }

        [BurstCompile]
public partial struct UpdateVegetationHealthJob : IJobEntity
{
    public float DeltaTime;
    public uint CurrentTick;
    public BlobAssetReference<VegetationSpeciesCatalogBlob> SpeciesCatalogBlob;
    [NativeDisableParallelForRestriction] public ComponentLookup<VegetationStressedTag> StressedTagLookup;
    [NativeDisableParallelForRestriction] public ComponentLookup<VegetationDyingTag> DyingTagLookup;
    [NativeDisableParallelForRestriction] public ComponentLookup<VegetationDeadTag> DeadTagLookup;

            public void Execute(
                ref VegetationHealth health,
                ref VegetationLifecycle lifecycle,
                DynamicBuffer<VegetationHistoryEvent> historyEvents,
                in VegetationEnvironmentState environment,
                in VegetationSpeciesIndex speciesIndex,
                in Entity entity,
                [ChunkIndexInQuery] int chunkIndex)
            {
                // Get species data from blob
                if (!SpeciesCatalogBlob.IsCreated || speciesIndex.Value >= SpeciesCatalogBlob.Value.Species.Length)
                {
                    return; // Invalid species index
                }

                ref var speciesData = ref SpeciesCatalogBlob.Value.Species[speciesIndex.Value];

                // Calculate environmental deficits (normalized 0-1)
                var waterDeficit = CalculateDeficit(environment.Water, speciesData.DesiredMinWater, speciesData.DesiredMaxWater);
                var lightDeficit = CalculateDeficit(environment.Light, speciesData.DesiredMinLight, speciesData.DesiredMaxLight);
                var soilDeficit = CalculateDeficit(environment.Soil, speciesData.DesiredMinSoilQuality, speciesData.DesiredMaxSoilQuality);
                
                // Guard against division by zero for tolerance values
                var pollutionDeficit = speciesData.PollutionTolerance > 0f 
                    ? math.clamp(environment.Pollution / speciesData.PollutionTolerance, 0f, 1f)
                    : 1f; // Treat as full deficit if tolerance is invalid
                
                var windDeficit = speciesData.WindTolerance > 0f
                    ? math.clamp(environment.Wind / speciesData.WindTolerance, 0f, 1f)
                    : 1f; // Treat as full deficit if tolerance is invalid

                // Calculate total deficit magnitude
                var totalDeficit = (waterDeficit + lightDeficit + soilDeficit + pollutionDeficit + windDeficit) / 5f;

                // Apply health deltas based on environmental conditions
                if (totalDeficit < 0.1f)
                {
                    // All values within ideal band - regenerate health
                    var regenRate = speciesData.BaselineRegen * DeltaTime;
                    health.Health = math.min(health.MaxHealth, health.Health + regenRate);
                }
                else
                {
                    // Deficits present - apply damage
                    var damage = speciesData.DamagePerDeficit * totalDeficit * DeltaTime;
                    health.Health = math.max(0f, health.Health - damage);

                    // Track drought/frost tolerance timers
                    if (waterDeficit > 0.5f && health.WaterLevel < speciesData.DesiredMinWater)
                    {
                        // Drought stress - check tolerance
                        if (totalDeficit > 0.7f)
                        {
                            // Severe drought - can add stress tag here
                        }
                    }
                }

                // Update derived flags - handle stressed tag using SetComponentEnabled
var dyingThreshold = speciesData.MaxHealth * 0.3f; // 30% of max health
                var isStressed = totalDeficit > 0.4f;
                
                StressedTagLookup.SetComponentEnabled(entity, isStressed);

                // Check if dying threshold hit
                if (health.Health < dyingThreshold)
                {
                    DyingTagLookup.SetComponentEnabled(entity, true);

                    // Update lifecycle stage to Dying
                    if (lifecycle.CurrentStage != VegetationLifecycle.LifecycleStage.Dying)
                    {
                        lifecycle.CurrentStage = VegetationLifecycle.LifecycleStage.Dying;
                        
                        // Record history event
                        historyEvents.Add(new VegetationHistoryEvent
                        {
                            Type = VegetationHistoryEvent.EventType.Damage,
                            EventTick = CurrentTick,
                            Value = totalDeficit
                        });
                    }
                }
                else
                {
                    DyingTagLookup.SetComponentEnabled(entity, false);
                    DeadTagLookup.SetComponentEnabled(entity, false);
                }

                // Check if dead
                if (health.Health <= 0f && lifecycle.CurrentStage != VegetationLifecycle.LifecycleStage.Dead)
                {
                    lifecycle.CurrentStage = VegetationLifecycle.LifecycleStage.Dead;
                    DeadTagLookup.SetComponentEnabled(entity, true);

                    // Disable dying and stressed tags when dead
                    if (DyingTagLookup.HasComponent(entity))
                    {
                        DyingTagLookup.SetComponentEnabled(entity, false);
                    }
                    if (StressedTagLookup.HasComponent(entity))
                    {
                        StressedTagLookup.SetComponentEnabled(entity, false);
                    }
                }
            }

            private static float CalculateDeficit(float value, float minThreshold, float maxThreshold)
            {
                if (value >= minThreshold && value <= maxThreshold)
                {
                    return 0f; // Within ideal range
                }
                else if (value < minThreshold)
                {
                    return 1f - (value / minThreshold); // Below minimum
                }
                else
                {
                    return (value - maxThreshold) / (100f - maxThreshold); // Above maximum
                }
            }
        }
    }
}

