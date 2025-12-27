using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Systems.AI;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Aggregates movement debug counters for headless diagnostics.
    /// </summary>
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(VesselMovementSystem))]
    public partial struct Space4XMovementTelemetrySystem : ISystem
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

            var nanInf = 0u;
            var speedClamp = 0u;
            var accelClamp = 0u;
            var teleport = 0u;
            var stuck = 0u;
            var stateFlips = 0u;
            var maxSpeedDelta = 0f;
            var maxTeleport = 0f;

            foreach (var debug in SystemAPI.Query<RefRO<MovementDebugState>>())
            {
                var stateDebug = debug.ValueRO;
                nanInf += stateDebug.NaNInfCount;
                speedClamp += stateDebug.SpeedClampCount;
                accelClamp += stateDebug.AccelClampCount;
                teleport += stateDebug.TeleportCount;
                stuck += stateDebug.StuckCount;
                stateFlips += stateDebug.StateFlipCount;
                maxSpeedDelta = math.max(maxSpeedDelta, stateDebug.MaxSpeedDelta);
                maxTeleport = math.max(maxTeleport, stateDebug.MaxTeleportDistance);
            }

            var movingCount = 0;
            var speedSum = 0f;
            foreach (var movement in SystemAPI.Query<RefRO<VesselMovement>>())
            {
                speedSum += math.max(0f, movement.ValueRO.CurrentSpeed);
                if (movement.ValueRO.IsMoving != 0)
                {
                    movingCount++;
                }
            }

            var avgSpeed = movingCount > 0 ? speedSum / movingCount : 0f;

            buffer.AddMetric("space4x.movement.naninf", nanInf, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.speedClamp", speedClamp, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.accelClamp", accelClamp, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.teleport", teleport, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.stuck", stuck, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.stateFlips", stateFlips, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.maxSpeedDelta", maxSpeedDelta, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.movement.maxTeleport", maxTeleport, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.movement.movingCount", movingCount, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.avgSpeed", avgSpeed, TelemetryMetricUnit.Custom);
        }
    }
}
