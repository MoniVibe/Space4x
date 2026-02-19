using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Aggregates routine phase/goal counts for headless validation.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.Space4XRoutinePlannerSystem))]
    public partial struct Space4XRoutineTelemetrySystem : ISystem
    {
        private EntityQuery _telemetryQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TelemetryExportConfig>();

            _telemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config) ||
                config.Enabled == 0 ||
                (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var cadence = config.CadenceTicks > 0 ? config.CadenceTicks : 30u;
            if (tick % cadence != 0)
            {
                return;
            }

            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            var goalMining = 0;
            var goalSupport = 0;
            var goalPatrol = 0;
            var goalEscort = 0;
            var goalStandby = 0;
            var phaseIdle = 0;
            var phaseTransit = 0;
            var phaseApproach = 0;
            var phaseWork = 0;
            var phaseReturn = 0;
            var phaseDock = 0;
            var phaseHold = 0;

            foreach (var routine in SystemAPI.Query<RefRO<Space4XRoutineState>>())
            {
                switch (routine.ValueRO.Goal)
                {
                    case Space4XRoutineGoal.Mining:
                        goalMining++;
                        break;
                    case Space4XRoutineGoal.MiningSupport:
                        goalSupport++;
                        break;
                    case Space4XRoutineGoal.Patrol:
                        goalPatrol++;
                        break;
                    case Space4XRoutineGoal.Escort:
                        goalEscort++;
                        break;
                    case Space4XRoutineGoal.Standby:
                        goalStandby++;
                        break;
                }

                switch (routine.ValueRO.Phase)
                {
                    case Space4XRoutinePhase.Idle:
                        phaseIdle++;
                        break;
                    case Space4XRoutinePhase.Transit:
                        phaseTransit++;
                        break;
                    case Space4XRoutinePhase.Approach:
                        phaseApproach++;
                        break;
                    case Space4XRoutinePhase.Work:
                        phaseWork++;
                        break;
                    case Space4XRoutinePhase.Return:
                        phaseReturn++;
                        break;
                    case Space4XRoutinePhase.Dock:
                        phaseDock++;
                        break;
                    case Space4XRoutinePhase.Hold:
                        phaseHold++;
                        break;
                }
            }

            buffer.AddMetric("space4x.routine.goal.mining", goalMining, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.routine.goal.support", goalSupport, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.routine.goal.patrol", goalPatrol, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.routine.goal.escort", goalEscort, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.routine.goal.standby", goalStandby, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.routine.phase.idle", phaseIdle, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.routine.phase.transit", phaseTransit, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.routine.phase.approach", phaseApproach, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.routine.phase.work", phaseWork, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.routine.phase.return", phaseReturn, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.routine.phase.dock", phaseDock, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.routine.phase.hold", phaseHold, TelemetryMetricUnit.Count);
        }
    }
}
