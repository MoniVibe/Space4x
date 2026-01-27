using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Profiling;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Updates vegetation lifecycle stages over time based on growth progress.
    /// Handles transitions: Seedling -> Growing -> Mature -> Flowering -> Fruiting -> Dying -> Dead
    /// Pushes history events on stage transitions for deterministic replay.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VegetationSystemGroup))]
    public partial struct VegetationGrowthSystem : ISystem
    {
        private EntityQuery _vegetationQuery;
        private static readonly ProfilerMarker s_UpdateVegetationGrowthMarker = 
            new ProfilerMarker("VegetationGrowthSystem.Update");

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _vegetationQuery = SystemAPI.QueryBuilder()
                .WithAll<VegetationId, VegetationLifecycle, VegetationSpeciesIndex>()
                .WithNone<VegetationDeadTag, PlaybackGuardTag>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<VegetationSpeciesLookup>();
            state.RequireForUpdate(_vegetationQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (s_UpdateVegetationGrowthMarker.Auto())
            {
                var timeState = SystemAPI.GetSingleton<TimeState>();
                if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
                {
                    return;
                }
                var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
                
                // Use TimeHelpers to check if we should update (handles pause, rewind, stasis)
                var defaultMembership = default(TimeBubbleMembership);
                if (!TimeHelpers.ShouldUpdate(timeState, rewindState, defaultMembership))
                {
                    return;
                }

                // Safety check: ensure species catalog exists
                if (!SystemAPI.HasSingleton<VegetationSpeciesLookup>())
                {
#if UNITY_EDITOR
                    LogMissingSpeciesLookup();
#endif
                    return;
                }
                
                var speciesLookup = SystemAPI.GetSingleton<VegetationSpeciesLookup>();
                
                if (!speciesLookup.CatalogBlob.IsCreated)
                {
#if UNITY_EDITOR
                    LogCatalogNotCreated();
#endif
                    return;
                }

                var job = new UpdateVegetationGrowthJob
                {
                    DeltaTime = TimeHelpers.GetGlobalDelta(tickTimeState, timeState),
                    CurrentTick = timeState.Tick,
                    SpeciesCatalogBlob = speciesLookup.CatalogBlob,
                    MatureTagLookup = state.GetComponentLookup<VegetationMatureTag>(false),
                    ReadyToHarvestTagLookup = state.GetComponentLookup<VegetationReadyToHarvestTag>(false),
                    DyingTagLookup = state.GetComponentLookup<VegetationDyingTag>(false),
                    DeadTagLookup = state.GetComponentLookup<VegetationDeadTag>(false),
                    TickTimeState = tickTimeState,
                    TimeState = timeState,
                    BubbleMembershipLookup = state.GetComponentLookup<TimeBubbleMembership>(true)
                };

                state.Dependency = job.ScheduleParallel(state.Dependency);
#if UNITY_EDITOR
                LogUpdateSummary(_vegetationQuery, timeState.Tick);
#endif
            }
        }

        [BurstCompile]
        public partial struct UpdateVegetationGrowthJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;
            public BlobAssetReference<VegetationSpeciesCatalogBlob> SpeciesCatalogBlob;
            [NativeDisableParallelForRestriction] public ComponentLookup<VegetationMatureTag> MatureTagLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<VegetationReadyToHarvestTag> ReadyToHarvestTagLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<VegetationDyingTag> DyingTagLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<VegetationDeadTag> DeadTagLookup;
            public TickTimeState TickTimeState;
            public TimeState TimeState;
            [ReadOnly] public ComponentLookup<TimeBubbleMembership> BubbleMembershipLookup;

            public void Execute(
                ref VegetationLifecycle lifecycle,
                DynamicBuffer<VegetationHistoryEvent> historyEvents,
                in VegetationSpeciesIndex speciesIndex,
                in Entity entity,
                [ChunkIndexInQuery] int chunkIndex)
            {
                // Get species data from blob
                if (!SpeciesCatalogBlob.IsCreated || speciesIndex.Value >= SpeciesCatalogBlob.Value.Species.Length)
                {
                    return; // Invalid species index
                }
                
                // Get bubble membership for this entity (if any)
                var membership = BubbleMembershipLookup.HasComponent(entity)
                    ? BubbleMembershipLookup[entity]
                    : default(TimeBubbleMembership);
                
                // Use TimeHelpers to get effective delta time (handles bubbles, pause, etc.)
                var effectiveDelta = TimeHelpers.GetEffectiveDelta(TickTimeState, TimeState, membership);
                
                // Skip if entity is in stasis or paused
                if (effectiveDelta <= 0f)
                {
                    return;
                }
                
                ref var speciesData = ref SpeciesCatalogBlob.Value.Species[speciesIndex.Value];
                
                // Advance timers using effective delta
                lifecycle.StageTimer += effectiveDelta;
                lifecycle.TotalAge += effectiveDelta;

                var previousStage = lifecycle.CurrentStage;
                var stageChanged = false;

                // Check stage transitions based on timers
                switch (lifecycle.CurrentStage)
                {
                    case VegetationLifecycle.LifecycleStage.Seedling:
                        if (lifecycle.StageTimer >= speciesData.SeedlingDuration)
                        {
                            lifecycle.CurrentStage = VegetationLifecycle.LifecycleStage.Growing;
                            lifecycle.StageTimer = 0f;
                            stageChanged = true;
                        }
                        break;

                    case VegetationLifecycle.LifecycleStage.Growing:
                        if (lifecycle.StageTimer >= speciesData.GrowingDuration)
                        {
                            lifecycle.CurrentStage = VegetationLifecycle.LifecycleStage.Mature;
                            lifecycle.StageTimer = 0f;
                            MatureTagLookup.SetComponentEnabled(entity, true);
                            stageChanged = true;
                        }
                        break;

                    case VegetationLifecycle.LifecycleStage.Mature:
                        if (lifecycle.StageTimer >= speciesData.MatureDuration)
                        {
                            lifecycle.CurrentStage = VegetationLifecycle.LifecycleStage.Flowering;
                            lifecycle.StageTimer = 0f;
                            MatureTagLookup.SetComponentEnabled(entity, false);
                            stageChanged = true;
                        }
                        break;

                    case VegetationLifecycle.LifecycleStage.Flowering:
                        if (lifecycle.StageTimer >= speciesData.FloweringDuration)
                        {
                            lifecycle.CurrentStage = VegetationLifecycle.LifecycleStage.Fruiting;
                            lifecycle.StageTimer = 0f;
                            ReadyToHarvestTagLookup.SetComponentEnabled(entity, true);
                            stageChanged = true;
                        }
                        break;

                    case VegetationLifecycle.LifecycleStage.Fruiting:
                        if (lifecycle.StageTimer >= speciesData.FruitingDuration)
                        {
                            lifecycle.CurrentStage = VegetationLifecycle.LifecycleStage.Dying;
                            lifecycle.StageTimer = 0f;
                            ReadyToHarvestTagLookup.SetComponentEnabled(entity, false);
                            DyingTagLookup.SetComponentEnabled(entity, true);
                            stageChanged = true;
                        }
                        break;

                    case VegetationLifecycle.LifecycleStage.Dying:
                        if (lifecycle.StageTimer >= speciesData.DyingDuration)
                        {
                            lifecycle.CurrentStage = VegetationLifecycle.LifecycleStage.Dead;
                            lifecycle.StageTimer = 0f;

                            DyingTagLookup.SetComponentEnabled(entity, false);
                            MatureTagLookup.SetComponentEnabled(entity, false);
                            DeadTagLookup.SetComponentEnabled(entity, true);
                            stageChanged = true;
                        }
                        break;

                    case VegetationLifecycle.LifecycleStage.Dead:
                        // Already dead, no transitions
                        break;
                }

                // Record history event on stage change
                if (stageChanged)
                {
                    historyEvents.Add(new VegetationHistoryEvent
                    {
                        Type = VegetationHistoryEvent.EventType.StageTransition,
                        EventTick = CurrentTick,
                        Value = (float)lifecycle.CurrentStage
                    });
                }

                // Update growth progress (normalized within current stage)
                UpdateGrowthProgress(ref lifecycle, ref speciesData);

                if (lifecycle.CurrentStage != VegetationLifecycle.LifecycleStage.Dead)
                {
                    DeadTagLookup.SetComponentEnabled(entity, false);
                }
            }

            private void UpdateGrowthProgress(ref VegetationLifecycle lifecycle, ref VegetationSpeciesBlob speciesData)
            {
                // Simple progress calculation based on stage and timer
                var stageProgress = lifecycle.CurrentStage switch
                {
                    VegetationLifecycle.LifecycleStage.Seedling => lifecycle.StageTimer / speciesData.SeedlingDuration,
                    VegetationLifecycle.LifecycleStage.Growing => lifecycle.StageTimer / speciesData.GrowingDuration,
                    VegetationLifecycle.LifecycleStage.Mature => lifecycle.StageTimer / speciesData.MatureDuration,
                    VegetationLifecycle.LifecycleStage.Flowering => lifecycle.StageTimer / speciesData.FloweringDuration,
                    VegetationLifecycle.LifecycleStage.Fruiting => lifecycle.StageTimer / speciesData.FruitingDuration,
                    VegetationLifecycle.LifecycleStage.Dying => lifecycle.StageTimer / speciesData.DyingDuration,
                    _ => 1f
                };

                lifecycle.GrowthProgress = stageProgress;
            }
        }

#if UNITY_EDITOR
        [BurstDiscard]
        private static void LogMissingSpeciesLookup()
        {
            UnityEngine.Debug.LogWarning("[VegetationGrowthSystem] VegetationSpeciesLookup singleton not found. Skipping update.");
        }

        [BurstDiscard]
        private static void LogCatalogNotCreated()
        {
            UnityEngine.Debug.LogWarning("[VegetationGrowthSystem] Species catalog blob not created. Skipping update.");
        }

        [BurstDiscard]
        private static void LogUpdateSummary(EntityQuery query, uint tick)
        {
            UnityEngine.Debug.Log($"[VegetationGrowthSystem] Updated {query.CalculateEntityCount()} vegetation entities at tick {tick}");
        }
#endif
    }
}
