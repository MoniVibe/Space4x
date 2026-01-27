using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Performance;
using PureDOTS.Runtime.Core;
using PureDOTS.Systems.Performance;

namespace PureDOTS.Systems
{
    /// <summary>
    /// WARM path: History recording for important entities (sampled rate).
    /// Narrative/debug logs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(UniversalPerformanceBudgetSystem))]
    public partial struct TimeHistoryRecordSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<HistorySettings>();
            state.RequireForUpdate<UniversalPerformanceBudget>();
            state.RequireForUpdate<UniversalPerformanceCounters>();
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            // Guard: Do not mutate history/snapshots in multiplayer modes
            if (SystemAPI.TryGetSingleton<TimeSystemFeatureFlags>(out var flags) &&
                flags.IsMultiplayerSession)
            {
                // For now, do not mutate history or snapshots in multiplayer modes.
                // When we implement MP, we can selectively allow modes like MP_SnapshotsOnly.
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }
            var historySettings = SystemAPI.GetSingleton<HistorySettings>();
            if (historySettings.StrideScale <= 0f)
            {
                return;
            }

            // Only record during Record mode
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            uint currentTick = timeState.Tick;

            // Update HistoryActiveTag based on HistoryProfile.IsEnabled
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Add HistoryActiveTag to enabled profiles without it
            foreach (var (profile, entity) in SystemAPI.Query<RefRO<HistoryProfile>>()
                .WithNone<HistoryActiveTag>()
                .WithAll<RewindableTag>()
                .WithEntityAccess())
            {
                if (profile.ValueRO.IsEnabled)
                {
                    ecb.AddComponent<HistoryActiveTag>(entity);
                }
            }

            // Remove HistoryActiveTag from disabled profiles
            foreach (var (profile, entity) in SystemAPI.Query<RefRO<HistoryProfile>>()
                .WithAll<HistoryActiveTag>()
                .WithEntityAccess())
            {
                if (!profile.ValueRO.IsEnabled)
                {
                    ecb.RemoveComponent<HistoryActiveTag>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Record transform history for entities with transform and history profile
            RecordTransformHistory(ref state, currentTick, historySettings.StrideScale);

            // Update history state singleton
            UpdateHistoryState(ref state);
        }

        [BurstCompile]
        private void RecordTransformHistory(ref SystemState state, uint currentTick, float strideScale)
        {
            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();
            var importanceLookup = SystemAPI.GetComponentLookup<AIImportance>(true);
            var updateCadenceLookup = SystemAPI.GetComponentLookup<UpdateCadence>(true);
            
            importanceLookup.Update(ref state);
            updateCadenceLookup.Update(ref state);
            
            int recordsThisTick = 0;
            var strideMultiplier = math.max(1f, strideScale);
            
            foreach (var (profile, transform, historyBuffer, entity) in SystemAPI
                .Query<RefRW<HistoryProfile>, RefRO<LocalTransform>, DynamicBuffer<ComponentHistory<LocalTransform>>>()
                .WithAll<HistoryActiveTag, RewindableTag>()
                .WithEntityAccess())
            {
                // Check budget
                if (recordsThisTick >= budget.MaxHistoryRecordsPerTick)
                {
                    break;
                }
                
                // Check if transform recording is enabled
                if ((profile.ValueRO.RecordFlags & HistoryRecordFlags.Transform) == 0)
                {
                    continue;
                }

                // Check sampling frequency
                var baseFrequency = math.max(1u, profile.ValueRO.SamplingFrequencyTicks);
                var scaledFrequency = (uint)math.max(1f, math.ceil(baseFrequency * strideMultiplier));

                if (profile.ValueRO.LastSampleTick != 0 && 
                    currentTick - profile.ValueRO.LastSampleTick < scaledFrequency)
                {
                    continue;
                }
                
                // Check update cadence (staggered updates)
                if (updateCadenceLookup.HasComponent(entity))
                {
                    var cadence = updateCadenceLookup[entity];
                    if (!UpdateCadenceHelpers.ShouldUpdate(currentTick, cadence))
                    {
                        continue;
                    }
                }
                
                // Sample based on importance (higher importance = more frequent sampling)
                byte importanceLevel = 3; // Default to background
                if (importanceLookup.HasComponent(entity))
                {
                    importanceLevel = importanceLookup[entity].Level;
                }
                
                // Adjust sampling frequency based on importance
                // Level 0 (hero): sample every tick if enabled
                // Level 1 (important): sample every 2-5 ticks
                // Level 2 (normal): sample every 10-20 ticks
                // Level 3 (background): sample every 50+ ticks
                uint importanceMultiplier = importanceLevel switch
                {
                    0 => 1u,
                    1 => 2u,
                    2 => 5u,
                    _ => 10u
                };
                
                var scaledImportanceFrequency = (uint)math.max(1f, math.ceil(scaledFrequency * importanceMultiplier));
                if (profile.ValueRO.LastSampleTick != 0 && 
                    currentTick - profile.ValueRO.LastSampleTick < scaledImportanceFrequency)
                {
                    continue;
                }

                // Record the sample
                var sample = new ComponentHistory<LocalTransform>
                {
                    Tick = currentTick,
                    Value = transform.ValueRO
                };

                historyBuffer.Add(sample);
                profile.ValueRW.LastSampleTick = currentTick;
                recordsThisTick++;

                // Prune old samples based on horizon
                PruneHistory(historyBuffer, currentTick, profile.ValueRO.HorizonTicks);
            }
            
            // Update counters
            counters.ValueRW.HistoryRecordsThisTick += recordsThisTick;
            counters.ValueRW.TotalWarmOperationsThisTick += recordsThisTick;
        }

        [BurstCompile]
        private static void PruneHistory<T>(DynamicBuffer<ComponentHistory<T>> buffer, uint currentTick, uint horizonTicks)
            where T : unmanaged
        {
            if (buffer.Length == 0)
            {
                return;
            }

            uint cutoffTick = currentTick > horizonTicks ? currentTick - horizonTicks : 0;

            // Find first valid entry
            int firstValidIndex = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Tick >= cutoffTick)
                {
                    firstValidIndex = i;
                    break;
                }
                firstValidIndex = i + 1;
            }

            // Remove old entries
            if (firstValidIndex > 0 && firstValidIndex < buffer.Length)
            {
                buffer.RemoveRange(0, firstValidIndex);
            }
            else if (firstValidIndex >= buffer.Length)
            {
                // All entries are too old
                buffer.Clear();
            }
        }

        private void UpdateHistoryState(ref SystemState state)
        {
            // Count active entities
            int activeCount = 0;
            foreach (var _ in SystemAPI.Query<RefRO<HistoryProfile>>().WithAll<HistoryActiveTag>())
            {
                activeCount++;
            }

            // Get or create history state singleton
            Entity stateEntity;
            if (!SystemAPI.TryGetSingletonEntity<TimeHistoryState>(out stateEntity))
            {
                stateEntity = state.EntityManager.CreateEntity(typeof(TimeHistoryState));
            }

            var historyState = state.EntityManager.GetComponentData<TimeHistoryState>(stateEntity);
            historyState.ActiveEntityCount = activeCount;
            // Note: Memory estimation would require iterating all buffers - do this less frequently
            state.EntityManager.SetComponentData(stateEntity, historyState);
        }
    }
}
