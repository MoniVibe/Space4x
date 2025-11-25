using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Publishes telemetry metrics for stat influences on gameplay systems.
    /// Tracks how stats affect various gameplay outcomes for tuning and debugging.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TelemetrySystemGroup))]
    [UpdateAfter(typeof(Space4XTelemetryBootstrapSystem))]
    public partial struct Space4XStatsTelemetrySystem : ISystem
    {
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<PhysiqueFinesseWill> _physiqueLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TelemetryStream>();
            
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _physiqueLookup = state.GetComponentLookup<PhysiqueFinesseWill>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            // Only publish telemetry every N ticks to avoid spam
            if (timeState.Tick % 60 != 0) // Every 60 ticks (~1 second at 60 FPS)
            {
                return;
            }

            _statsLookup.Update(ref state);
            _physiqueLookup.Update(ref state);

            if (!SystemAPI.TryGetSingletonBuffer<TelemetryMetric>(out var telemetryBuffer))
            {
                return;
            }

            // Aggregate stat influences across entities
            float totalCommand = 0f;
            float totalTactics = 0f;
            float totalLogistics = 0f;
            float totalDiplomacy = 0f;
            float totalEngineering = 0f;
            float totalResolve = 0f;
            float totalPhysique = 0f;
            float totalFinesse = 0f;
            float totalWill = 0f;
            int entityCount = 0;

            foreach (var (stats, entity) in SystemAPI.Query<RefRO<IndividualStats>>().WithEntityAccess())
            {
                totalCommand += stats.ValueRO.Command;
                totalTactics += stats.ValueRO.Tactics;
                totalLogistics += stats.ValueRO.Logistics;
                totalDiplomacy += stats.ValueRO.Diplomacy;
                totalEngineering += stats.ValueRO.Engineering;
                totalResolve += stats.ValueRO.Resolve;
                entityCount++;
            }

            int physiqueCount = 0;
            foreach (var (physique, entity) in SystemAPI.Query<RefRO<PhysiqueFinesseWill>>().WithEntityAccess())
            {
                totalPhysique += physique.ValueRO.Physique;
                totalFinesse += physique.ValueRO.Finesse;
                totalWill += physique.ValueRO.Will;
                physiqueCount++;
            }

            // Publish aggregated metrics
            if (entityCount > 0)
            {
                var avgCommand = totalCommand / entityCount;
                var avgTactics = totalTactics / entityCount;
                var avgLogistics = totalLogistics / entityCount;
                var avgDiplomacy = totalDiplomacy / entityCount;
                var avgEngineering = totalEngineering / entityCount;
                var avgResolve = totalResolve / entityCount;

                telemetryBuffer.Add(new TelemetryMetric
                {
                    Key = new FixedString64Bytes("space4x.stats.command.avg"),
                    Value = avgCommand,
                    Unit = TelemetryMetricUnit.None,
                    Timestamp = timeState.Tick
                });

                telemetryBuffer.Add(new TelemetryMetric
                {
                    Key = new FixedString64Bytes("space4x.stats.tactics.avg"),
                    Value = avgTactics,
                    Unit = TelemetryMetricUnit.None,
                    Timestamp = timeState.Tick
                });

                telemetryBuffer.Add(new TelemetryMetric
                {
                    Key = new FixedString64Bytes("space4x.stats.logistics.avg"),
                    Value = avgLogistics,
                    Unit = TelemetryMetricUnit.None,
                    Timestamp = timeState.Tick
                });

                telemetryBuffer.Add(new TelemetryMetric
                {
                    Key = new FixedString64Bytes("space4x.stats.diplomacy.avg"),
                    Value = avgDiplomacy,
                    Unit = TelemetryMetricUnit.None,
                    Timestamp = timeState.Tick
                });

                telemetryBuffer.Add(new TelemetryMetric
                {
                    Key = new FixedString64Bytes("space4x.stats.engineering.avg"),
                    Value = avgEngineering,
                    Unit = TelemetryMetricUnit.None,
                    Timestamp = timeState.Tick
                });

                telemetryBuffer.Add(new TelemetryMetric
                {
                    Key = new FixedString64Bytes("space4x.stats.resolve.avg"),
                    Value = avgResolve,
                    Unit = TelemetryMetricUnit.None,
                    Timestamp = timeState.Tick
                });
            }

            if (physiqueCount > 0)
            {
                var avgPhysique = totalPhysique / physiqueCount;
                var avgFinesse = totalFinesse / physiqueCount;
                var avgWill = totalWill / physiqueCount;

                telemetryBuffer.Add(new TelemetryMetric
                {
                    Key = new FixedString64Bytes("space4x.stats.physique.avg"),
                    Value = avgPhysique,
                    Unit = TelemetryMetricUnit.None,
                    Timestamp = timeState.Tick
                });

                telemetryBuffer.Add(new TelemetryMetric
                {
                    Key = new FixedString64Bytes("space4x.stats.finesse.avg"),
                    Value = avgFinesse,
                    Unit = TelemetryMetricUnit.None,
                    Timestamp = timeState.Tick
                });

                telemetryBuffer.Add(new TelemetryMetric
                {
                    Key = new FixedString64Bytes("space4x.stats.will.avg"),
                    Value = avgWill,
                    Unit = TelemetryMetricUnit.None,
                    Timestamp = timeState.Tick
                });
            }
        }
    }
}

