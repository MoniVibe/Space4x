using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Updates department stats (fatigue, cohesion, stress) over time.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    public partial struct Space4XDepartmentSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<DepartmentStatsBuffer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var deltaTime = timeState.FixedDeltaTime;
            var currentTick = timeState.Tick;

            // Update department stats
            foreach (var (statsBuffer, staffingBuffer, entity) in
                SystemAPI.Query<DynamicBuffer<DepartmentStatsBuffer>, DynamicBuffer<DepartmentStaffingBuffer>>()
                    .WithEntityAccess())
            {
                var statsBufferLocal = statsBuffer;
                var staffingBufferLocal = staffingBuffer;
                UpdateDepartmentStats(ref statsBufferLocal, ref staffingBufferLocal, deltaTime, currentTick);
            }

            // Update stats for entities without staffing buffer (simplified)
            foreach (var (statsBuffer, entity) in
                SystemAPI.Query<DynamicBuffer<DepartmentStatsBuffer>>()
                    .WithNone<DepartmentStaffingBuffer>()
                    .WithEntityAccess())
            {
                var statsBufferLocal = statsBuffer;
                UpdateDepartmentStatsSimple(ref statsBufferLocal, deltaTime, currentTick);
            }
        }

        [BurstCompile]
        private static void UpdateDepartmentStats(
            ref DynamicBuffer<DepartmentStatsBuffer> statsBuffer,
            ref DynamicBuffer<DepartmentStaffingBuffer> staffingBuffer,
            float deltaTime,
            uint currentTick)
        {
            for (int i = 0; i < statsBuffer.Length; i++)
            {
                var stats = statsBuffer[i].Stats;

                // Find matching staffing
                float staffingRatio = 1f;
                for (int j = 0; j < staffingBuffer.Length; j++)
                {
                    if (staffingBuffer[j].Staffing.Type == stats.Type)
                    {
                        staffingRatio = staffingBuffer[j].Staffing.StaffingRatio;
                        break;
                    }
                }

                // Update fatigue
                float fatigue = (float)stats.Fatigue;
                float fatigueRate = DepartmentUtility.GetFatigueAccumulationRate(true, false);
                fatigue += fatigueRate * deltaTime;
                fatigue = math.clamp(fatigue, 0f, 1f);
                stats.Fatigue = (half)fatigue;

                // Stress increases with understaffing
                float stress = (float)stats.Stress;
                if (staffingRatio < 1f)
                {
                    stress += (1f - staffingRatio) * 0.005f * deltaTime;
                }
                else
                {
                    stress -= 0.002f * deltaTime; // Slow stress recovery
                }
                stress = math.clamp(stress, 0f, 1f);
                stats.Stress = (half)stress;

                // Cohesion improves slowly over time, decreases with stress
                float cohesion = (float)stats.Cohesion;
                if (stress < DepartmentThresholds.HighStress)
                {
                    cohesion += 0.001f * deltaTime; // Slow cohesion growth
                }
                else
                {
                    cohesion -= 0.002f * deltaTime; // Cohesion drops under stress
                }
                cohesion = math.clamp(cohesion, 0f, 1f);
                stats.Cohesion = (half)cohesion;

                // Calculate efficiency
                stats.Efficiency = (half)DepartmentUtility.CalculateEfficiency(stats, staffingRatio);
                stats.LastUpdateTick = currentTick;

                statsBuffer[i] = new DepartmentStatsBuffer { Stats = stats };
            }
        }

        [BurstCompile]
        private static void UpdateDepartmentStatsSimple(
            ref DynamicBuffer<DepartmentStatsBuffer> statsBuffer,
            float deltaTime,
            uint currentTick)
        {
            for (int i = 0; i < statsBuffer.Length; i++)
            {
                var stats = statsBuffer[i].Stats;

                // Simple fatigue accumulation
                float fatigue = (float)stats.Fatigue;
                fatigue += 0.002f * deltaTime;
                fatigue = math.clamp(fatigue, 0f, 1f);
                stats.Fatigue = (half)fatigue;

                // Simple efficiency calculation
                stats.Efficiency = (half)DepartmentUtility.CalculateEfficiency(stats, 1f);
                stats.LastUpdateTick = currentTick;

                statsBuffer[i] = new DepartmentStatsBuffer { Stats = stats };
            }
        }
    }

    /// <summary>
    /// Aggregates department stats to carrier-level state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XDepartmentSystem))]
    public partial struct Space4XDepartmentAggregationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<CarrierDepartmentState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var currentTick = timeState.Tick;

            foreach (var (carrierState, statsBuffer, staffingBuffer, entity) in
                SystemAPI.Query<RefRW<CarrierDepartmentState>, DynamicBuffer<DepartmentStatsBuffer>, DynamicBuffer<DepartmentStaffingBuffer>>()
                    .WithEntityAccess())
            {
                AggregateStats(ref carrierState.ValueRW, in statsBuffer, in staffingBuffer, currentTick);
            }

            // Handle entities without staffing buffer
            foreach (var (carrierState, statsBuffer, entity) in
                SystemAPI.Query<RefRW<CarrierDepartmentState>, DynamicBuffer<DepartmentStatsBuffer>>()
                    .WithNone<DepartmentStaffingBuffer>()
                    .WithEntityAccess())
            {
                AggregateStatsSimple(ref carrierState.ValueRW, in statsBuffer, currentTick);
            }
        }

        [BurstCompile]
        private static void AggregateStats(
            ref CarrierDepartmentState carrierState,
            in DynamicBuffer<DepartmentStatsBuffer> statsBuffer,
            in DynamicBuffer<DepartmentStaffingBuffer> staffingBuffer,
            uint currentTick)
        {
            if (statsBuffer.Length == 0)
            {
                return;
            }

            float totalFatigue = 0f;
            float totalCohesion = 0f;
            float totalStress = 0f;
            float totalEfficiency = 0f;
            float totalStaffingRatio = 0f;
            int criticalCount = 0;

            for (int i = 0; i < statsBuffer.Length; i++)
            {
                var stats = statsBuffer[i].Stats;
                totalFatigue += (float)stats.Fatigue;
                totalCohesion += (float)stats.Cohesion;
                totalStress += (float)stats.Stress;
                totalEfficiency += (float)stats.Efficiency;

                // Check for critical state
                if ((float)stats.Fatigue > DepartmentThresholds.CriticalFatigue ||
                    (float)stats.Stress > DepartmentThresholds.CriticalStress)
                {
                    criticalCount++;
                }
            }

            // Calculate staffing ratio
            int totalRequired = 0;
            int totalCurrent = 0;
            for (int i = 0; i < staffingBuffer.Length; i++)
            {
                totalRequired += staffingBuffer[i].Staffing.RequiredCrew;
                totalCurrent += staffingBuffer[i].Staffing.CurrentCrew;
            }
            totalStaffingRatio = totalRequired > 0 ? (float)totalCurrent / totalRequired : 1f;

            int count = statsBuffer.Length;
            carrierState.AverageFatigue = (half)(totalFatigue / count);
            carrierState.AverageCohesion = (half)(totalCohesion / count);
            carrierState.AverageStress = (half)(totalStress / count);
            carrierState.OverallEfficiency = (half)(totalEfficiency / count);
            carrierState.OverallStaffingRatio = (half)totalStaffingRatio;
            carrierState.CriticalDepartmentCount = (byte)criticalCount;
            carrierState.LastUpdateTick = currentTick;
        }

        [BurstCompile]
        private static void AggregateStatsSimple(
            ref CarrierDepartmentState carrierState,
            in DynamicBuffer<DepartmentStatsBuffer> statsBuffer,
            uint currentTick)
        {
            if (statsBuffer.Length == 0)
            {
                return;
            }

            float totalFatigue = 0f;
            float totalCohesion = 0f;
            float totalStress = 0f;
            float totalEfficiency = 0f;
            int criticalCount = 0;

            for (int i = 0; i < statsBuffer.Length; i++)
            {
                var stats = statsBuffer[i].Stats;
                totalFatigue += (float)stats.Fatigue;
                totalCohesion += (float)stats.Cohesion;
                totalStress += (float)stats.Stress;
                totalEfficiency += (float)stats.Efficiency;

                if ((float)stats.Fatigue > DepartmentThresholds.CriticalFatigue ||
                    (float)stats.Stress > DepartmentThresholds.CriticalStress)
                {
                    criticalCount++;
                }
            }

            int count = statsBuffer.Length;
            carrierState.AverageFatigue = (half)(totalFatigue / count);
            carrierState.AverageCohesion = (half)(totalCohesion / count);
            carrierState.AverageStress = (half)(totalStress / count);
            carrierState.OverallEfficiency = (half)(totalEfficiency / count);
            carrierState.OverallStaffingRatio = (half)1f;
            carrierState.CriticalDepartmentCount = (byte)criticalCount;
            carrierState.LastUpdateTick = currentTick;
        }
    }

    /// <summary>
    /// Emits department telemetry metrics.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XDepartmentAggregationSystem))]
    public partial struct Space4XDepartmentTelemetrySystem : ISystem
    {
        private EntityQuery _carrierQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();

            _carrierQuery = SystemAPI.QueryBuilder()
                .WithAll<CarrierDepartmentState>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = SystemAPI.GetSingletonEntity<TelemetryStream>();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            int carrierCount = _carrierQuery.CalculateEntityCount();
            float totalFatigue = 0f;
            float totalStress = 0f;
            float totalCohesion = 0f;
            float totalEfficiency = 0f;
            int criticalTotal = 0;

            foreach (var carrierState in SystemAPI.Query<RefRO<CarrierDepartmentState>>())
            {
                totalFatigue += (float)carrierState.ValueRO.AverageFatigue;
                totalStress += (float)carrierState.ValueRO.AverageStress;
                totalCohesion += (float)carrierState.ValueRO.AverageCohesion;
                totalEfficiency += (float)carrierState.ValueRO.OverallEfficiency;
                criticalTotal += carrierState.ValueRO.CriticalDepartmentCount;
            }

            float avgFatigue = carrierCount > 0 ? totalFatigue / carrierCount : 0f;
            float avgStress = carrierCount > 0 ? totalStress / carrierCount : 0f;
            float avgCohesion = carrierCount > 0 ? totalCohesion / carrierCount : 0f;
            float avgEfficiency = carrierCount > 0 ? totalEfficiency / carrierCount : 1f;

            buffer.AddMetric("space4x.departments.carriers", carrierCount);
            buffer.AddMetric("space4x.departments.avgFatigue", avgFatigue, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.departments.avgStress", avgStress, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.departments.avgCohesion", avgCohesion, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.departments.avgEfficiency", avgEfficiency, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.departments.criticalCount", criticalTotal);
        }
    }
}

