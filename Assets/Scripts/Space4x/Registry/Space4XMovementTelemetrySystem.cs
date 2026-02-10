using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Systems.AI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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
        private const float LateralVelocityThresholdSq = 1e-6f;
        private const float TurnStartDot = 0.6f;
        private const float TurnEndDot = 0.95f;
        private const float TargetDeltaEpsilon = 0.25f;

        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<MiningVessel> _miningLookup;
        private ComponentLookup<StrikeCraftProfile> _strikeLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TelemetryExportConfig>();

            _telemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();

            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _miningLookup = state.GetComponentLookup<MiningVessel>(true);
            _strikeLookup = state.GetComponentLookup<StrikeCraftProfile>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _carrierLookup.Update(ref state);
            _miningLookup.Update(ref state);
            _strikeLookup.Update(ref state);

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
            if (!state.EntityManager.HasComponent<Space4XMovementObserveAccumulator>(telemetryEntity))
            {
                state.EntityManager.AddComponentData(telemetryEntity, new Space4XMovementObserveAccumulator());
            }

            var accumulator = state.EntityManager.GetComponentData<Space4XMovementOracleAccumulator>(telemetryEntity);
            var observeAccumulator = state.EntityManager.GetComponentData<Space4XMovementObserveAccumulator>(telemetryEntity);
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
            foreach (var (_, entity) in SystemAPI.Query<RefRO<VesselMovement>>()
                         .WithNone<Space4XMovementObserveState>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new Space4XMovementObserveState());
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            foreach (var (plan, movement, oracle) in SystemAPI
                         .Query<RefRO<MovePlan>, RefRO<VesselMovement>, RefRW<Space4XMovementOracleState>>())
            {
                UpdateHeadingOscillation(plan.ValueRO.DesiredVelocity, movement.ValueRO.Velocity, ref oracle.ValueRW, ref accumulator);
                UpdateApproachFlip(plan.ValueRO.Mode, ref oracle.ValueRW, ref accumulator);
            }

            var fixedDelta = math.max(1e-5f, timeState.FixedDeltaTime);
            foreach (var (intent, plan, movement, transform, observe, entity) in SystemAPI
                         .Query<RefRO<MoveIntent>, RefRO<MovePlan>, RefRO<VesselMovement>, RefRO<LocalTransform>, RefRW<Space4XMovementObserveState>>()
                         .WithEntityAccess())
            {
                if (intent.ValueRO.IntentType != MoveIntentType.MoveTo)
                {
                    observe.ValueRW.HasIntent = 0;
                    continue;
                }

                if (IsIntentChanged(intent.ValueRO, observe.ValueRO))
                {
                    ResetObserveState(ref observe.ValueRW, intent.ValueRO.TargetEntity, intent.ValueRO.TargetPosition, intent.ValueRO.IntentType, tick);
                }

                UpdateObserveState(intent.ValueRO, plan.ValueRO, movement.ValueRO, transform.ValueRO.Position, tick, ref observe.ValueRW);

                if (observe.ValueRW.Reported == 0 && observe.ValueRW.Reached != 0 && observe.ValueRW.Settled != 0)
                {
                    var timeToTarget = (observe.ValueRW.ReachedTick - observe.ValueRW.IntentStartTick) * fixedDelta;
                    var settleTime = (observe.ValueRW.SettledTick - observe.ValueRW.ReachedTick) * fixedDelta;
                    var turnTime = observe.ValueRW.TurnCount > 0
                        ? (observe.ValueRW.TurnDurationTicks / (float)observe.ValueRW.TurnCount) * fixedDelta
                        : 0f;

                    AccumulateObserveMetrics(entity, timeToTarget, observe.ValueRW.MaxOvershoot, settleTime, observe.ValueRW.PeakLateralSpeed, turnTime,
                        ref observeAccumulator);
                    observe.ValueRW.Reported = 1;
                }
            }

            if (!shouldExport)
            {
                state.EntityManager.SetComponentData(telemetryEntity, accumulator);
                state.EntityManager.SetComponentData(telemetryEntity, observeAccumulator);
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
            var gravitySamples = 0u;
            var gravityPeakAccel = 0f;

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
                gravitySamples += stateDebug.GravitySampleCount;
                gravityPeakAccel = math.max(gravityPeakAccel, stateDebug.GravityPeakAccel);
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
            buffer.AddMetric("space4x.movement.gravitySamples", gravitySamples, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.movement.gravityPeakAccel", gravityPeakAccel, TelemetryMetricUnit.Custom);

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
            EmitObserveMetrics(buffer, observeAccumulator);
            state.EntityManager.SetComponentData(telemetryEntity, observeAccumulator);
        }

        private void EmitObserveMetrics(DynamicBuffer<TelemetryMetric> buffer, Space4XMovementObserveAccumulator acc)
        {
            EmitObserveBucket(buffer, "carrier", acc.CarrierCount, acc.CarrierTimeToTargetSum, acc.CarrierOvershootSum,
                acc.CarrierSettleTimeSum, acc.CarrierPeakLateralSum, acc.CarrierTurnTimeSum);
            EmitObserveBucket(buffer, "miner", acc.MinerCount, acc.MinerTimeToTargetSum, acc.MinerOvershootSum,
                acc.MinerSettleTimeSum, acc.MinerPeakLateralSum, acc.MinerTurnTimeSum);
            EmitObserveBucket(buffer, "strike", acc.StrikeCount, acc.StrikeTimeToTargetSum, acc.StrikeOvershootSum,
                acc.StrikeSettleTimeSum, acc.StrikePeakLateralSum, acc.StrikeTurnTimeSum);
        }

        private static void EmitObserveBucket(DynamicBuffer<TelemetryMetric> buffer, string label, uint count,
            float timeToTargetSum, float overshootSum, float settleTimeSum, float lateralSum, float turnTimeSum)
        {
            var denom = count > 0 ? count : 1u;
            buffer.AddMetric($"space4x.movement.observe.{label}.count", count, TelemetryMetricUnit.Count);
            buffer.AddMetric($"space4x.movement.observe.{label}.time_to_target_s", timeToTargetSum / denom, TelemetryMetricUnit.Custom);
            buffer.AddMetric($"space4x.movement.observe.{label}.overshoot_distance", overshootSum / denom, TelemetryMetricUnit.Custom);
            buffer.AddMetric($"space4x.movement.observe.{label}.settle_time_s", settleTimeSum / denom, TelemetryMetricUnit.Custom);
            buffer.AddMetric($"space4x.movement.observe.{label}.peak_lateral_speed", lateralSum / denom, TelemetryMetricUnit.Custom);
            buffer.AddMetric($"space4x.movement.observe.{label}.turn_time_s", turnTimeSum / denom, TelemetryMetricUnit.Custom);
        }

        private static void ResetObserveState(ref Space4XMovementObserveState state, Entity target, float3 targetPosition, MoveIntentType intentType, uint tick)
        {
            state.LastTargetEntity = target;
            state.LastTargetPosition = targetPosition;
            state.LastIntentType = intentType;
            state.IntentStartTick = tick;
            state.MinDistance = float.MaxValue;
            state.MaxOvershoot = 0f;
            state.PeakLateralSpeed = 0f;
            state.ReachedTick = 0u;
            state.SettledTick = 0u;
            state.TurnDurationTicks = 0u;
            state.TurnCount = 0u;
            state.TurnStartTick = 0u;
            state.HasIntent = 1;
            state.Reached = 0;
            state.Settled = 0;
            state.Turning = 0;
            state.Reported = 0;
        }

        private static bool IsIntentChanged(in MoveIntent intent, in Space4XMovementObserveState state)
        {
            if (state.HasIntent == 0)
            {
                return true;
            }

            if (state.LastIntentType != intent.IntentType)
            {
                return true;
            }

            if (state.LastTargetEntity != intent.TargetEntity)
            {
                return true;
            }

            var delta = intent.TargetPosition - state.LastTargetPosition;
            return math.lengthsq(delta) > (TargetDeltaEpsilon * TargetDeltaEpsilon);
        }

        private static void UpdateObserveState(
            in MoveIntent intent,
            in MovePlan plan,
            in VesselMovement movement,
            float3 position,
            uint tick,
            ref Space4XMovementObserveState state)
        {
            if (intent.IntentType != MoveIntentType.MoveTo)
            {
                return;
            }

            var toTarget = intent.TargetPosition - position;
            var distance = math.length(toTarget);
            state.MinDistance = math.min(state.MinDistance, distance);

            var arrivalDistance = math.max(0.1f, movement.ArrivalDistance);
            if (distance <= arrivalDistance && state.Reached == 0)
            {
                state.Reached = 1;
                state.ReachedTick = tick;
            }

            if (state.Reached != 0)
            {
                state.MaxOvershoot = math.max(state.MaxOvershoot, math.max(0f, distance - arrivalDistance));
            }

            var settleThreshold = math.max(0.05f, movement.BaseSpeed * 0.05f);
            if (state.Reached != 0 && state.Settled == 0 && movement.CurrentSpeed <= settleThreshold)
            {
                state.Settled = 1;
                state.SettledTick = tick;
            }

            if (distance > 0.001f)
            {
                var dir = toTarget / distance;
                var velocity = movement.Velocity;
                var lateral = velocity - dir * math.dot(velocity, dir);
                var lateralSpeed = math.sqrt(math.lengthsq(lateral));
                state.PeakLateralSpeed = math.max(state.PeakLateralSpeed, lateralSpeed);
            }

            var desiredSpeedSq = math.lengthsq(plan.DesiredVelocity);
            var actualSpeedSq = math.lengthsq(movement.Velocity);
            if (desiredSpeedSq > LateralVelocityThresholdSq && actualSpeedSq > LateralVelocityThresholdSq)
            {
                var desiredDir = math.normalize(plan.DesiredVelocity);
                var actualDir = math.normalize(movement.Velocity);
                var dot = math.dot(desiredDir, actualDir);

                if (state.Turning == 0 && dot < TurnStartDot)
                {
                    state.Turning = 1;
                    state.TurnStartTick = tick;
                }
                else if (state.Turning != 0 && dot >= TurnEndDot)
                {
                    if (state.TurnStartTick > 0)
                    {
                        state.TurnDurationTicks += math.max(1u, tick - state.TurnStartTick);
                        state.TurnCount += 1;
                    }

                    state.Turning = 0;
                    state.TurnStartTick = 0u;
                }
            }
            else if (state.Turning != 0)
            {
                if (state.TurnStartTick > 0)
                {
                    state.TurnDurationTicks += math.max(1u, tick - state.TurnStartTick);
                    state.TurnCount += 1;
                }

                state.Turning = 0;
                state.TurnStartTick = 0u;
            }

            state.LastTargetPosition = intent.TargetPosition;
        }

        private void AccumulateObserveMetrics(Entity entity, float timeToTarget, float overshoot, float settleTime,
            float peakLateral, float turnTime, ref Space4XMovementObserveAccumulator acc)
        {
            if (_carrierLookup.HasComponent(entity))
            {
                acc.CarrierCount += 1;
                acc.CarrierTimeToTargetSum += timeToTarget;
                acc.CarrierOvershootSum += overshoot;
                acc.CarrierSettleTimeSum += settleTime;
                acc.CarrierPeakLateralSum += peakLateral;
                acc.CarrierTurnTimeSum += turnTime;
                return;
            }

            if (_miningLookup.HasComponent(entity))
            {
                acc.MinerCount += 1;
                acc.MinerTimeToTargetSum += timeToTarget;
                acc.MinerOvershootSum += overshoot;
                acc.MinerSettleTimeSum += settleTime;
                acc.MinerPeakLateralSum += peakLateral;
                acc.MinerTurnTimeSum += turnTime;
                return;
            }

            if (_strikeLookup.HasComponent(entity))
            {
                acc.StrikeCount += 1;
                acc.StrikeTimeToTargetSum += timeToTarget;
                acc.StrikeOvershootSum += overshoot;
                acc.StrikeSettleTimeSum += settleTime;
                acc.StrikePeakLateralSum += peakLateral;
                acc.StrikeTurnTimeSum += turnTime;
            }
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
