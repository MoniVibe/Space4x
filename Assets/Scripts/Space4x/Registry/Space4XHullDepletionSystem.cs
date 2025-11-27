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
    /// Processes hull damage and applies permanent reductions from critical hits.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    public partial struct Space4XHullDepletionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<HullIntegrity>();
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

            var currentTick = timeState.Tick;

            // Process entities with critical damage history
            foreach (var (hull, damageHistory, entity) in
                SystemAPI.Query<RefRW<HullIntegrity>, DynamicBuffer<CriticalDamageHistory>>()
                    .WithEntityAccess())
            {
                ProcessCriticalDamage(ref hull.ValueRW, in damageHistory, currentTick);
            }

            // Sync hull integrity to VesselResourceLevels if present
            foreach (var (hull, resourceLevels, entity) in
                SystemAPI.Query<RefRO<HullIntegrity>, RefRW<VesselResourceLevels>>()
                    .WithEntityAccess())
            {
                resourceLevels.ValueRW.MaxHull = hull.ValueRO.Max;
                resourceLevels.ValueRW.CurrentHull = hull.ValueRO.Current;
            }
        }

        [BurstCompile]
        private static void ProcessCriticalDamage(
            ref HullIntegrity hull,
            in DynamicBuffer<CriticalDamageHistory> damageHistory,
            uint currentTick)
        {
            // Calculate total max hull reduction from unrepaired critical damage
            float totalReduction = 0f;
            for (int i = 0; i < damageHistory.Length; i++)
            {
                var damage = damageHistory[i];
                if (damage.Repaired == 0)
                {
                    totalReduction += damage.MaxHullReduction;
                }
            }

            // Apply permanent damage to Max (but not below minimum threshold)
            float newMax = hull.BaseMax - totalReduction;
            float minimumMax = hull.BaseMax * 0.2f; // Can't go below 20% of base
            hull.Max = math.max(minimumMax, newMax);

            // Clamp current to new max
            hull.Current = math.min(hull.Current, hull.Max);
        }
    }

    /// <summary>
    /// Handles field repair (capped at 80% of current max).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XHullDepletionSystem))]
    public partial struct Space4XFieldRepairHullSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
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

            // Field repair for entities not in dockyard
            foreach (var (hull, entity) in SystemAPI.Query<RefRW<HullIntegrity>>()
                .WithNone<DockyardRepairInProgress>()
                .WithEntityAccess())
            {
                // Only repair if below 80% of current max
                float fieldRepairCap = hull.ValueRO.Max * 0.8f;
                if (hull.ValueRO.Current < fieldRepairCap)
                {
                    // Slow field repair rate (1 HP per second base)
                    float repairAmount = 1f * deltaTime;
                    hull.ValueRW.Current = math.min(hull.ValueRO.Current + repairAmount, fieldRepairCap);
                    hull.ValueRW.LastRepairTick = currentTick;
                }
            }
        }
    }

    /// <summary>
    /// Handles dockyard repair (full restoration including permanent damage).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XFieldRepairHullSystem))]
    public partial struct Space4XDockyardRepairSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
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
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process entities undergoing dockyard repair
            foreach (var (hull, repairProgress, damageHistory, entity) in
                SystemAPI.Query<RefRW<HullIntegrity>, RefRW<DockyardRepairInProgress>, DynamicBuffer<CriticalDamageHistory>>()
                    .WithEntityAccess())
            {
                var damageHistoryBuffer = damageHistory;
                // Get dockyard repair rate
                float repairRate = 10f; // Default rate
                if (SystemAPI.HasComponent<DockyardFacility>(repairProgress.ValueRO.Dockyard))
                {
                    var dockyard = SystemAPI.GetComponent<DockyardFacility>(repairProgress.ValueRO.Dockyard);
                    repairRate = dockyard.RepairRate;
                }

                // Calculate total repair needed
                float hullRepairNeeded = hull.ValueRO.Max - hull.ValueRO.Current;
                float permanentRepairNeeded = hull.ValueRO.BaseMax - hull.ValueRO.Max;
                float totalRepairNeeded = hullRepairNeeded + (permanentRepairNeeded * 2f); // Permanent takes 2x

                if (totalRepairNeeded <= 0f)
                {
                    // Repair complete
                    repairProgress.ValueRW.Progress = (half)1f;

                    // Mark all critical damage as repaired
                    for (int i = 0; i < damageHistoryBuffer.Length; i++)
                    {
                        var damage = damageHistoryBuffer[i];
                        damage.Repaired = 1;
                        damageHistoryBuffer[i] = damage;
                    }

                    // Remove repair in progress component
                    ecb.RemoveComponent<DockyardRepairInProgress>(entity);
                    ecb.RemoveComponent<DockyardRepairRequest>(entity);
                    continue;
                }

                // Apply repair
                float repairThisTick = repairRate * deltaTime;

                // First restore current hull
                if (hull.ValueRO.Current < hull.ValueRO.Max)
                {
                    float hullRepair = math.min(repairThisTick, hull.ValueRO.Max - hull.ValueRO.Current);
                    hull.ValueRW.Current += hullRepair;
                    repairThisTick -= hullRepair;
                }

                // Then restore max hull (permanent damage)
                if (repairThisTick > 0f && hull.ValueRO.Max < hull.ValueRO.BaseMax)
                {
                    float maxRepair = math.min(repairThisTick * 0.5f, hull.ValueRO.BaseMax - hull.ValueRO.Max);
                    hull.ValueRW.Max += maxRepair;
                    hull.ValueRW.Current = math.min(hull.ValueRO.Current, hull.ValueRO.Max);
                }

                hull.ValueRW.LastRepairTick = currentTick;

                // Update progress
                float repairDone = (hull.ValueRO.Current / hull.ValueRO.Max) + ((hull.ValueRO.Max / hull.ValueRO.BaseMax) - 1f);
                repairProgress.ValueRW.Progress = (half)math.clamp(repairDone, 0f, 1f);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Emits hull integrity telemetry metrics.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XDockyardRepairSystem))]
    public partial struct Space4XHullTelemetrySystem : ISystem
    {
        private EntityQuery _hullQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();

            _hullQuery = SystemAPI.QueryBuilder()
                .WithAll<HullIntegrity>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = SystemAPI.GetSingletonEntity<TelemetryStream>();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            int vesselCount = _hullQuery.CalculateEntityCount();
            float totalHullRatio = 0f;
            int damagedCount = 0;
            int criticalCount = 0;
            int permanentDamageCount = 0;
            int inRepairCount = 0;

            foreach (var hull in SystemAPI.Query<RefRO<HullIntegrity>>())
            {
                float ratio = hull.ValueRO.Ratio;
                totalHullRatio += ratio;

                if (ratio < 1f)
                {
                    damagedCount++;
                }
                if (ratio < HullThresholds.CriticalVulnerability)
                {
                    criticalCount++;
                }
                if (hull.ValueRO.HasPermanentDamage)
                {
                    permanentDamageCount++;
                }
            }

            foreach (var _ in SystemAPI.Query<RefRO<DockyardRepairInProgress>>())
            {
                inRepairCount++;
            }

            float avgHullRatio = vesselCount > 0 ? totalHullRatio / vesselCount : 1f;

            buffer.AddMetric("space4x.hull.vessels", vesselCount);
            buffer.AddMetric("space4x.hull.avgRatio", avgHullRatio, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.hull.damaged", damagedCount);
            buffer.AddMetric("space4x.hull.critical", criticalCount);
            buffer.AddMetric("space4x.hull.permanentDamage", permanentDamageCount);
            buffer.AddMetric("space4x.hull.inRepair", inRepairCount);
        }
    }
}

