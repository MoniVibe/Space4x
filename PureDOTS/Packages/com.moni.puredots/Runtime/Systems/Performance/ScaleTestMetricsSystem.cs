using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Runtime.Systems.Performance
{
    /// <summary>
    /// Collects performance metrics during scale test scenarios.
    /// Runs in SimulationSystemGroup to measure simulation performance.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct ScaleTestMetricsSystem : ISystem
    {
        private double _lastTickStartTime;
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScaleTestMetricsConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var currentTime = UnityEngine.Time.realtimeSinceStartupAsDouble;
            
            if (!_initialized)
            {
                _lastTickStartTime = currentTime;
                _initialized = true;
                
                // Initialize metrics singleton if not present
                if (!SystemAPI.TryGetSingleton<ScaleTestMetrics>(out _))
                {
                    var entity = state.EntityManager.CreateEntity();
                    state.EntityManager.AddComponentData(entity, new ScaleTestMetrics
                    {
                        MinTickTime = float.MaxValue
                    });
                    state.EntityManager.AddBuffer<TickTimeSample>(entity);
                }
                return;
            }

            var tickTime = (float)((currentTime - _lastTickStartTime) * 1000.0); // Convert to ms
            _lastTickStartTime = currentTime;

            if (!SystemAPI.TryGetSingleton<ScaleTestMetricsConfig>(out var config))
            {
                return;
            }

            var tick = 0u;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                tick = timeState.Tick;
            }

            // Check if we should sample this tick
            if (config.SampleInterval > 0 && tick % config.SampleInterval != 0)
            {
                return;
            }

            // Update metrics
            foreach (var (metrics, samples) in 
                SystemAPI.Query<RefRW<ScaleTestMetrics>, DynamicBuffer<TickTimeSample>>())
            {
                // Update tick time statistics
                metrics.ValueRW.CurrentTick = tick;
                metrics.ValueRW.TotalTickTime = tickTime;
                metrics.ValueRW.SampleCount++;
                metrics.ValueRW.SumTickTime += tickTime;
                metrics.ValueRW.SumSquaredTickTime += tickTime * tickTime;
                metrics.ValueRW.AverageTickTime = metrics.ValueRO.SumTickTime / metrics.ValueRO.SampleCount;
                metrics.ValueRW.MaxTickTime = math.max(metrics.ValueRO.MaxTickTime, tickTime);
                metrics.ValueRW.MinTickTime = math.min(metrics.ValueRO.MinTickTime, tickTime);

                // Store sample for percentile calculation
                if (samples.Length < samples.Capacity)
                {
                    samples.Add(new TickTimeSample
                    {
                        TickTimeMs = tickTime,
                        Tick = tick
                    });
                }

                // Count entities by type
                CountEntities(ref state, ref metrics.ValueRW);

                // Log if interval reached
                if (config.LogInterval > 0 && tick % config.LogInterval == 0)
                {
                    LogMetrics(in metrics.ValueRO, tick, in config);
                }
            }
        }

        private void CountEntities(ref SystemState state, ref ScaleTestMetrics metrics)
        {
            metrics.VillagerCount = 0;
            metrics.ResourceChunkCount = 0;
            metrics.ProjectileCount = 0;
            metrics.CarrierCount = 0;
            metrics.AggregateCount = 0;
            metrics.TotalEntityCount = 0;

            // Count villagers
            foreach (var _ in SystemAPI.Query<RefRO<VillagerId>>())
            {
                metrics.VillagerCount++;
            }

            // Count resource chunks
            foreach (var _ in SystemAPI.Query<RefRO<ResourceChunkState>>())
            {
                metrics.ResourceChunkCount++;
            }

            // Count projectiles (if component exists)
            // Note: ProjectileEntity may not be available in all builds
            // metrics.ProjectileCount = ...

            // Count carriers (if component exists)
            // Note: CarrierOwner may not be available in all builds
            // metrics.CarrierCount = ...

            // Count aggregates
            foreach (var _ in SystemAPI.Query<RefRO<PureDOTS.Runtime.Rendering.AggregateTag>>())
            {
                metrics.AggregateCount++;
            }

            metrics.TotalEntityCount = metrics.VillagerCount + 
                                       metrics.ResourceChunkCount + 
                                       metrics.ProjectileCount + 
                                       metrics.CarrierCount + 
                                       metrics.AggregateCount;
        }

        private static void LogMetrics(in ScaleTestMetrics metrics, uint tick, in ScaleTestMetricsConfig config)
        {
            var budgetStatus = metrics.TotalTickTime <= config.TargetTickTimeMs ? "OK" : "OVER";
            
            Debug.Log($"[ScaleTest] Tick {tick}: " +
                      $"TickTime={metrics.TotalTickTime:F2}ms (avg={metrics.AverageTickTime:F2}ms, max={metrics.MaxTickTime:F2}ms) [{budgetStatus}] | " +
                      $"Entities: Villagers={metrics.VillagerCount}, Resources={metrics.ResourceChunkCount}, " +
                      $"Aggregates={metrics.AggregateCount}, Total={metrics.TotalEntityCount}");
        }
    }

    /// <summary>
    /// Validates performance metrics against budgets and logs warnings.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(ScaleTestMetricsSystem))]
    public partial struct PerformanceBudgetValidationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PerformanceBudget>();
            state.RequireForUpdate<ScaleTestMetrics>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<PerformanceBudget>(out var budget))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ScaleTestMetrics>(out var metrics))
            {
                return;
            }

            // Only validate periodically (every 100 ticks)
            if (metrics.CurrentTick % 100 != 0)
            {
                return;
            }

            // Check tick time budget
            if (metrics.TotalTickTime > budget.MaxTickTimeMs)
            {
                Debug.LogWarning($"[PerformanceBudget] Tick time {metrics.TotalTickTime:F2}ms exceeds budget {budget.MaxTickTimeMs:F2}ms");
            }

            // Check average tick time
            if (metrics.AverageTickTime > budget.MaxTickTimeMs * 0.8f)
            {
                Debug.LogWarning($"[PerformanceBudget] Average tick time {metrics.AverageTickTime:F2}ms approaching budget {budget.MaxTickTimeMs:F2}ms");
            }

            // Check entity counts
            if (metrics.VillagerCount > 100000)
            {
                Debug.LogWarning($"[PerformanceBudget] Villager count ({metrics.VillagerCount}) exceeds recommended 100k. Consider LOD/aggregation.");
            }
        }
    }

    /// <summary>
    /// Collects LOD debug metrics when enabled.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(ScaleTestMetricsSystem))]
    public partial struct LODDebugMetricsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScaleTestMetricsConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScaleTestMetricsConfig>(out var config))
            {
                return;
            }

            if (config.EnableLODDebug == 0)
            {
                return;
            }

            var tick = 0u;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                tick = timeState.Tick;
            }

            // Only collect periodically
            if (config.SampleInterval > 0 && tick % config.SampleInterval != 0)
            {
                return;
            }

            // Initialize or get debug metrics singleton
            if (!SystemAPI.TryGetSingleton<LODDebugMetrics>(out _))
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new LODDebugMetrics());
            }

            foreach (var lodMetrics in SystemAPI.Query<RefRW<LODDebugMetrics>>())
            {
                CollectLODMetrics(ref state, ref lodMetrics.ValueRW, tick, config);
            }
        }

        private void CollectLODMetrics(ref SystemState state, ref LODDebugMetrics metrics, uint tick, ScaleTestMetricsConfig config)
        {
            metrics.LOD0Count = 0;
            metrics.LOD1Count = 0;
            metrics.LOD2Count = 0;
            metrics.LOD3Count = 0;
            metrics.ShouldRenderCount = 0;
            metrics.CulledCount = 0;

            float sumDistance = 0f;
            float sumImportance = 0f;
            int lodDataCount = 0;

            // Count LOD levels
            foreach (var lodData in SystemAPI.Query<RefRO<PureDOTS.Runtime.Rendering.RenderLODData>>())
            {
                var lod = lodData.ValueRO.RecommendedLOD;
                if (lod == 0) metrics.LOD0Count++;
                else if (lod == 1) metrics.LOD1Count++;
                else if (lod == 2) metrics.LOD2Count++;
                else if (lod >= 3) metrics.LOD3Count++;

                sumDistance += lodData.ValueRO.CameraDistance;
                sumImportance += lodData.ValueRO.ImportanceScore;
                lodDataCount++;
            }

            // Count render density
            foreach (var sample in SystemAPI.Query<RefRO<PureDOTS.Runtime.Rendering.RenderSampleIndex>>())
            {
                if (sample.ValueRO.ShouldRender != 0)
                {
                    metrics.ShouldRenderCount++;
                }
            }

            // Count culled
            foreach (var cullable in SystemAPI.Query<RefRO<PureDOTS.Runtime.Rendering.RenderCullable>>())
            {
                // Note: Actual culling check would require camera distance
                // This is a placeholder for the count
                metrics.CulledCount++;
            }

            if (lodDataCount > 0)
            {
                metrics.AverageCameraDistance = sumDistance / lodDataCount;
                metrics.AverageImportanceScore = sumImportance / lodDataCount;
            }

            // Log if interval reached
            if (config.LogInterval > 0 && tick % config.LogInterval == 0)
            {
                Debug.Log($"[LODDebug] Tick {tick}: " +
                          $"LOD0={metrics.LOD0Count}, LOD1={metrics.LOD1Count}, LOD2={metrics.LOD2Count}, LOD3={metrics.LOD3Count} | " +
                          $"ShouldRender={metrics.ShouldRenderCount}, AvgDistance={metrics.AverageCameraDistance:F1}, AvgImportance={metrics.AverageImportanceScore:F2}");
            }
        }
    }

    /// <summary>
    /// Collects aggregate debug metrics when enabled.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(ScaleTestMetricsSystem))]
    public partial struct AggregateDebugMetricsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScaleTestMetricsConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScaleTestMetricsConfig>(out var config))
            {
                return;
            }

            if (config.EnableAggregateDebug == 0)
            {
                return;
            }

            var tick = 0u;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                tick = timeState.Tick;
            }

            // Only collect periodically
            if (config.SampleInterval > 0 && tick % config.SampleInterval != 0)
            {
                return;
            }

            // Initialize or get debug metrics singleton
            if (!SystemAPI.TryGetSingleton<AggregateDebugMetrics>(out _))
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new AggregateDebugMetrics());
            }

            foreach (var aggMetrics in SystemAPI.Query<RefRW<AggregateDebugMetrics>>())
            {
                CollectAggregateMetrics(ref state, ref aggMetrics.ValueRW, tick, config);
            }
        }

        private void CollectAggregateMetrics(ref SystemState state, ref AggregateDebugMetrics metrics, uint tick, ScaleTestMetricsConfig config)
        {
            metrics.AggregateCount = 0;
            metrics.TotalMemberCount = 0;
            metrics.MinMembersPerAggregate = int.MaxValue;
            metrics.MaxMembersPerAggregate = 0;

            float sumHealth = 0f;
            float sumStrength = 0f;
            uint lastUpdateTick = 0;

            // Count aggregates and members
            foreach (var (summary, stateComp) in 
                SystemAPI.Query<RefRO<PureDOTS.Runtime.Rendering.AggregateRenderSummary>, RefRO<PureDOTS.Runtime.Rendering.AggregateState>>())
            {
                metrics.AggregateCount++;
                var memberCount = summary.ValueRO.MemberCount;
                metrics.TotalMemberCount += memberCount;
                metrics.MinMembersPerAggregate = math.min(metrics.MinMembersPerAggregate, memberCount);
                metrics.MaxMembersPerAggregate = math.max(metrics.MaxMembersPerAggregate, memberCount);

                sumHealth += summary.ValueRO.TotalHealth;
                sumStrength += summary.ValueRO.TotalStrength;
                lastUpdateTick = math.max(lastUpdateTick, stateComp.ValueRO.LastAggregationTick);
            }

            if (metrics.AggregateCount > 0)
            {
                metrics.AverageMembersPerAggregate = metrics.TotalMemberCount / metrics.AggregateCount;
                metrics.AverageTotalHealth = sumHealth / metrics.AggregateCount;
                metrics.AverageTotalStrength = sumStrength / metrics.AggregateCount;
            }
            else
            {
                metrics.MinMembersPerAggregate = 0;
            }

            metrics.LastAggregationUpdateTick = lastUpdateTick;

            // Log if interval reached
            if (config.LogInterval > 0 && tick % config.LogInterval == 0)
            {
                Debug.Log($"[AggregateDebug] Tick {tick}: " +
                          $"Aggregates={metrics.AggregateCount}, TotalMembers={metrics.TotalMemberCount}, " +
                          $"AvgMembers={metrics.AverageMembersPerAggregate}, Range=[{metrics.MinMembersPerAggregate}, {metrics.MaxMembersPerAggregate}] | " +
                          $"AvgHealth={metrics.AverageTotalHealth:F1}, AvgStrength={metrics.AverageTotalStrength:F1}");
            }
        }
    }
}

