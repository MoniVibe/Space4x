using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Systems.AI;
using Unity.Collections;
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
        private const float HeadingEpsilon = 0.0005f;
        private const float HeadingVelocityThresholdSq = 1e-4f;

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

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var tick = timeState.Tick;
            var cadence = config.CadenceTicks > 0 ? config.CadenceTicks : 30u;
            var shouldExport = tick % cadence == 0u;

            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            if (!state.EntityManager.HasComponent<Space4XMovementOracleAccumulator>(telemetryEntity))
            {
                state.EntityManager.AddComponentData(telemetryEntity, new Space4XMovementOracleAccumulator());
            }

            var accumulator = state.EntityManager.GetComponentData<Space4XMovementOracleAccumulator>(telemetryEntity);
            var deltaSeconds = timeState.IsPaused ? 0f : math.max(0f, timeState.DeltaSeconds);
            accumulator.SampleSeconds += deltaSeconds;
            accumulator.SampleTicks += 1;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<VesselMovement>>()
                         .WithNone<Space4XMovementOracleState>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new Space4XMovementOracleState());
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            foreach (var (plan, movement, oracle) in SystemAPI
                         .Query<RefRO<MovePlan>, RefRO<VesselMovement>, RefRW<Space4XMovementOracleState>>())
            {
                UpdateHeadingOscillation(plan.ValueRO.DesiredVelocity, movement.ValueRO.Velocity, ref oracle.ValueRW, ref accumulator);
                UpdateApproachFlip(plan.ValueRO.Mode, ref oracle.ValueRW, ref accumulator);
            }

            if (!shouldExport)
            {
                state.EntityManager.SetComponentData(telemetryEntity, accumulator);
                return;
            }

            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            var nanInf = 0u;
            var speedClamp = 0u;
            var accelClampTotal = 0u;
            var sharpStart = 0u;
            var overshoot = 0u;
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
                accelClampTotal += stateDebug.AccelClampCount;
                sharpStart += stateDebug.SharpStartCount;
                overshoot += stateDebug.OvershootCount;
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
            buffer.AddMetric("space4x.movement.accelClamp", accelClampTotal, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.sharpStart", sharpStart, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.overshoot", overshoot, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.teleport", teleport, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.stuck", stuck, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.stateFlips", stateFlips, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.maxSpeedDelta", maxSpeedDelta, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.movement.maxTeleport", maxTeleport, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.movement.movingCount", movingCount, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.avgSpeed", avgSpeed, TelemetryMetricUnit.Custom);

            var seconds = math.max(0.0001f, accumulator.SampleSeconds);
            var headingScore = accumulator.HeadingFlipCount / seconds;
            var approachRate = accumulator.ApproachFlipCount / seconds;
            var deltaClamp = accelClampTotal >= accumulator.LastAccelClampTotal
                ? accelClampTotal - accumulator.LastAccelClampTotal
                : accelClampTotal;
            var turnSaturation = deltaSeconds > 0f ? deltaClamp * deltaSeconds : 0f;

            accumulator.LastAccelClampTotal = accelClampTotal;
            accumulator.HeadingFlipCount = 0;
            accumulator.ApproachFlipCount = 0;
            accumulator.SampleSeconds = 0f;
            accumulator.SampleTicks = 0;

            buffer.AddMetric("s4x.heading_oscillation_score", headingScore, TelemetryMetricUnit.Custom);
            buffer.AddMetric("s4x.turn_saturation_time_s", turnSaturation, TelemetryMetricUnit.Custom);
            buffer.AddMetric("s4x.approach_mode_flip_rate", approachRate, TelemetryMetricUnit.Custom);

            state.EntityManager.SetComponentData(telemetryEntity, accumulator);
        }

        private static void UpdateHeadingOscillation(
            float3 desiredVelocity,
            float3 actualVelocity,
            ref Space4XMovementOracleState oracle,
            ref Space4XMovementOracleAccumulator accumulator)
        {
            if (math.lengthsq(desiredVelocity) <= HeadingVelocityThresholdSq ||
                math.lengthsq(actualVelocity) <= HeadingVelocityThresholdSq)
            {
                return;
            }

            var desired2 = new float2(desiredVelocity.x, desiredVelocity.z);
            var actual2 = new float2(actualVelocity.x, actualVelocity.z);
            var cross = desired2.x * actual2.y - desired2.y * actual2.x;
            sbyte sign = 0;
            if (cross > HeadingEpsilon)
            {
                sign = 1;
            }
            else if (cross < -HeadingEpsilon)
            {
                sign = -1;
            }

            if (sign == 0)
            {
                return;
            }

            if (oracle.HeadingInitialized != 0 && oracle.LastHeadingSign != 0 && sign != oracle.LastHeadingSign)
            {
                accumulator.HeadingFlipCount += 1;
            }

            oracle.LastHeadingSign = sign;
            oracle.HeadingInitialized = 1;
        }

        private static void UpdateApproachFlip(
            MovePlanMode mode,
            ref Space4XMovementOracleState oracle,
            ref Space4XMovementOracleAccumulator accumulator)
        {
            if (oracle.PlanInitialized == 0)
            {
                oracle.PlanInitialized = 1;
                oracle.LastPlanMode = mode;
                return;
            }

            if (oracle.LastPlanMode == mode)
            {
                return;
            }

            if (oracle.LastPlanMode == MovePlanMode.Approach || mode == MovePlanMode.Approach)
            {
                accumulator.ApproachFlipCount += 1;
            }

            oracle.LastPlanMode = mode;
        }
    }
}
