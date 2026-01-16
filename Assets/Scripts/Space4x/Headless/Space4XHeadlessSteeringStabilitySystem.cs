using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.VesselMovementSystem))]
    public partial struct Space4XHeadlessSteeringStabilitySystem : ISystem
    {
        private const float HeadingErrorThresholdDeg = 2f;
        private const float YawRateThresholdDeg = 1f;
        private const float SteeringFlipThresholdPer10s = 1f;
        private const float TargetPositionEpsilon = 0.25f;

        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<MovePlan> _planLookup;
        private ComponentLookup<MoveIntent> _intentLookup;
        private ComponentLookup<VesselPilotLink> _pilotLookup;
        private BufferLookup<ResolvedControl> _resolvedControlLookup;
        private BufferLookup<AuthoritySeatRef> _seatRefLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;
        private BufferLookup<MoveTraceEvent> _traceLookup;
        private FixedString64Bytes _roleNavigationOfficer;
        private FixedString64Bytes _roleShipmaster;
        private FixedString64Bytes _roleCaptain;

        private Entity _shipEntity;
        private Entity _helmsmanEntity;
        private Entity _lastTargetEntity;
        private float3 _lastTargetPosition;
        private float _lastHeadingRad;
        private float _lastSteeringSign;
        private uint _retargetCount;
        private uint _steeringSignFlips;
        private float _maxHeadingErrorDeg;
        private float _maxYawRateDeg;
        private uint _sampleCount;
        private uint _eligibleSampleCount;
        private uint _speedGateSampleCount;
        private float _speedSum;
        private float _lastSpeedGateThreshold;
        private uint _measureStartTick;
        private uint _measureEndTick;
        private byte _hasLastHeading;
        private byte _hasLastSteeringSign;
        private byte _hasLastTarget;
        private byte _initialized;
        private byte _done;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<Space4XSteeringStabilityBeatConfig>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _movementLookup = state.GetComponentLookup<VesselMovement>(true);
            _planLookup = state.GetComponentLookup<MovePlan>(true);
            _intentLookup = state.GetComponentLookup<MoveIntent>(true);
            _pilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _resolvedControlLookup = state.GetBufferLookup<ResolvedControl>(true);
            _seatRefLookup = state.GetBufferLookup<AuthoritySeatRef>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _seatOccupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _traceLookup = state.GetBufferLookup<MoveTraceEvent>(false);

            _roleNavigationOfficer = BuildRoleNavigationOfficer();
            _roleShipmaster = BuildRoleShipmaster();
            _roleCaptain = BuildRoleCaptain();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _planLookup.Update(ref state);
            _intentLookup.Update(ref state);
            _pilotLookup.Update(ref state);
            _resolvedControlLookup.Update(ref state);
            _seatRefLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);
            _traceLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            var config = SystemAPI.GetSingleton<Space4XSteeringStabilityBeatConfig>();
            if (config.Initialized == 0)
            {
                var startTick = runtime.StartTick + SecondsToTicks(config.StartSeconds, timeState.FixedDeltaTime);
                config.StartTick = startTick;
                config.SettleTicks = SecondsToTicks(config.SettleSeconds, timeState.FixedDeltaTime);
                config.MeasureTicks = math.max(1u, SecondsToTicks(config.MeasureSeconds, timeState.FixedDeltaTime));
                config.Initialized = 1;
                SystemAPI.SetSingleton(config);
            }

            if (_initialized == 0)
            {
                _measureStartTick = config.StartTick + config.SettleTicks;
                _measureEndTick = _measureStartTick + config.MeasureTicks;
                _initialized = 1;
            }

            var tick = timeState.Tick;
            if (_shipEntity == Entity.Null || !state.EntityManager.Exists(_shipEntity))
            {
                _shipEntity = ResolveFleetShip(config.FleetId, ref state);
                ResetTracking();
            }

            if (_shipEntity == Entity.Null)
            {
                return;
            }

            if (tick < _measureStartTick)
            {
                PrimeHeading(_shipEntity);
                return;
            }

            if (tick > _measureEndTick)
            {
                FinalizeReport(ref state, in config, timeState.FixedDeltaTime);
                config.Completed = 1;
                SystemAPI.SetSingleton(config);
                _done = 1;
                return;
            }

            MeasureTick(_shipEntity, config.TargetPosition, timeState.FixedDeltaTime, tick);
        }

        private void MeasureTick(Entity ship, float3 targetPosition, float fixedDt, uint tick)
        {
            if (!_transformLookup.HasComponent(ship))
            {
                return;
            }

            var speedEligible = true;
            if (_movementLookup.HasComponent(ship))
            {
                var movement = _movementLookup[ship];
                var minSpeed = math.max(0.1f, movement.BaseSpeed) * 0.2f;
                _lastSpeedGateThreshold = minSpeed;
                if (movement.CurrentSpeed < minSpeed)
                {
                    speedEligible = false;
                }
            }

            var transform = _transformLookup[ship];
            var forward = math.mul(transform.Rotation, new float3(0f, 0f, 1f));
            var forwardFlat = new float3(forward.x, 0f, forward.z);
            if (math.lengthsq(forwardFlat) < 1e-6f)
            {
                return;
            }

            var targetDir = float3.zero;
            var hasDesiredDir = false;
            if (_movementLookup.HasComponent(ship))
            {
                var movement = _movementLookup[ship];
                var desiredForward = math.mul(movement.DesiredRotation, new float3(0f, 0f, 1f));
                desiredForward.y = 0f;
                if (math.lengthsq(desiredForward) > 1e-4f)
                {
                    targetDir = math.normalizesafe(desiredForward);
                    hasDesiredDir = true;
                }
            }

            if (!hasDesiredDir && _planLookup.HasComponent(ship))
            {
                var plan = _planLookup[ship];
                var desiredVelocity = plan.DesiredVelocity;
                desiredVelocity.y = 0f;
                if (math.lengthsq(desiredVelocity) > 1e-4f)
                {
                    targetDir = math.normalizesafe(desiredVelocity);
                    hasDesiredDir = true;
                }
            }

            if (!hasDesiredDir)
            {
                var toTarget = targetPosition - transform.Position;
                toTarget.y = 0f;
                targetDir = math.normalizesafe(toTarget);
                if (math.lengthsq(targetDir) < 1e-6f)
                {
                    return;
                }
            }

            var dot = math.clamp(math.dot(math.normalizesafe(forwardFlat), targetDir), -1f, 1f);
            _sampleCount++;
            if (!speedEligible)
            {
                _speedGateSampleCount++;
            }
            if (_movementLookup.HasComponent(ship))
            {
                _speedSum += _movementLookup[ship].CurrentSpeed;
            }
            if (speedEligible)
            {
                _eligibleSampleCount++;
            }

            var headingErrorRad = math.acos(dot);
            var headingErrorDeg = math.degrees(headingErrorRad);
            if (speedEligible && headingErrorDeg > _maxHeadingErrorDeg)
            {
                _maxHeadingErrorDeg = headingErrorDeg;
            }

            var headingRad = math.atan2(forwardFlat.x, forwardFlat.z);
            if (_hasLastHeading != 0)
            {
                var delta = math.abs(DeltaAngleRad(_lastHeadingRad, headingRad));
                var yawRate = math.degrees(delta) / math.max(1e-5f, fixedDt);
                if (speedEligible && yawRate > _maxYawRateDeg)
                {
                    _maxYawRateDeg = yawRate;
                }
            }
            _lastHeadingRad = headingRad;
            _hasLastHeading = 1;

            if (_planLookup.HasComponent(ship))
            {
                var plan = _planLookup[ship];
                var desiredVelocity = plan.DesiredVelocity;
                if (math.lengthsq(desiredVelocity) > 1e-4f)
                {
                    var desiredDir = math.normalizesafe(new float3(desiredVelocity.x, 0f, desiredVelocity.z));
                    var sign = math.sign(math.cross(forwardFlat, desiredDir).y);
                    if (math.abs(sign) > 0.1f)
                    {
                        if (speedEligible && _hasLastSteeringSign != 0 && sign != _lastSteeringSign)
                        {
                            _steeringSignFlips++;
                            PushTraceEvent(ship, MoveTraceEventKind.SteeringFlip, _helmsmanEntity, tick);
                        }
                        _lastSteeringSign = sign;
                        _hasLastSteeringSign = 1;
                    }
                }
            }

            if (_intentLookup.HasComponent(ship))
            {
                var intent = _intentLookup[ship];
                if (_hasLastTarget != 0)
                {
                    var targetChanged = intent.TargetEntity != _lastTargetEntity;
                    var positionChanged = math.lengthsq(intent.TargetPosition - _lastTargetPosition) > TargetPositionEpsilon * TargetPositionEpsilon;
                    if (targetChanged || (intent.TargetEntity == Entity.Null && positionChanged))
                    {
                        _retargetCount++;
                    }

                    _lastTargetEntity = intent.TargetEntity;
                    _lastTargetPosition = intent.TargetPosition;
                }
                else
                {
                    _lastTargetEntity = intent.TargetEntity;
                    _lastTargetPosition = intent.TargetPosition;
                    _hasLastTarget = 1;
                }
            }

            _helmsmanEntity = ResolveHelmsman(ship);
        }

        private void PrimeHeading(Entity ship)
        {
            if (!_transformLookup.HasComponent(ship))
            {
                return;
            }

            var transform = _transformLookup[ship];
            var forward = math.mul(transform.Rotation, new float3(0f, 0f, 1f));
            var forwardFlat = new float3(forward.x, 0f, forward.z);
            if (math.lengthsq(forwardFlat) < 1e-6f)
            {
                return;
            }

            _lastHeadingRad = math.atan2(forwardFlat.x, forwardFlat.z);
            _hasLastHeading = 1;
        }

        private void FinalizeReport(ref SystemState state, in Space4XSteeringStabilityBeatConfig config, float fixedDt)
        {
            var measureSeconds = math.max(1e-3f, config.MeasureTicks * fixedDt);
            var flipsPer10s = _steeringSignFlips / math.max(1f, measureSeconds / 10f);
            var retargets = _retargetCount;
            var retargetsPer10s = retargets / math.max(1f, measureSeconds / 10f);
            var oscillationCause = ClassifyOscillation(flipsPer10s, retargets);
            var avgSpeed = _sampleCount > 0 ? _speedSum / _sampleCount : 0f;

            if (_sampleCount == 0)
            {
                AppendSteeringSkippedBlackCat(ref state, config, measureSeconds);
            }
            else if (_eligibleSampleCount == 0)
            {
                AppendSteeringLowSpeedBlackCat(ref state, config, avgSpeed);
            }

            var fail = _maxHeadingErrorDeg > HeadingErrorThresholdDeg ||
                       _maxYawRateDeg > YawRateThresholdDeg ||
                       flipsPer10s > SteeringFlipThresholdPer10s ||
                       retargets > 0u;

            var observed = $"heading_error_max={_maxHeadingErrorDeg:F2} yaw_rate_max={_maxYawRateDeg:F2} flips_per_10s={flipsPer10s:F2} retargets={retargets}";
            var expected = $"heading_error_max<={HeadingErrorThresholdDeg} yaw_rate_max<={YawRateThresholdDeg} flips_per_10s<={SteeringFlipThresholdPer10s} retargets=0";

            if (fail)
            {
                Space4XHeadlessDiagnostics.ReportInvariant(
                    "STEERING_STABILITY_WHEN_TARGET_STABLE",
                    "Helmsman output or ship heading oscillated during stable target window.",
                    observed,
                    expected);
                HeadlessExitUtility.Request(state.EntityManager, config.StartTick + config.SettleTicks, Space4XHeadlessDiagnostics.TestFailExitCode);
            }

            if (flipsPer10s > SteeringFlipThresholdPer10s)
            {
                AppendOscillationBlackCat(ref state, config, flipsPer10s, retargets, oscillationCause);
            }

            EmitOperatorSummary(ref state, flipsPer10s, retargetsPer10s, oscillationCause, avgSpeed);
            EmitTelemetrySummary(ref state, flipsPer10s, retargetsPer10s, oscillationCause, avgSpeed);
        }

        private void EmitOperatorSummary(
            ref SystemState state,
            float flipsPer10s,
            float retargetsPer10s,
            float oscillationCause,
            float avgSpeed)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.steer.heading_error_max_deg"), _maxHeadingErrorDeg);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.steer.yaw_rate_max_deg_s"), _maxYawRateDeg);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.steer.sign_flips_per_10s"), flipsPer10s);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.steer.retarget_count"), _retargetCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.steer.retarget_per_10s"), retargetsPer10s);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.steer.oscillation_cause_code"), oscillationCause);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.steer.sample_count"), _sampleCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.steer.eligible_samples"), _eligibleSampleCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.steer.speed_gate_samples"), _speedGateSampleCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.steer.speed_gate_threshold"), _lastSpeedGateThreshold);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.steer.avg_speed"), avgSpeed);
        }

        private void EmitTelemetrySummary(
            ref SystemState state,
            float flipsPer10s,
            float retargetsPer10s,
            float oscillationCause,
            float avgSpeed)
        {
            if (!SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity) ||
                !state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                return;
            }

            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            buffer.AddMetric("space4x.steer.heading_error_max_deg", _maxHeadingErrorDeg, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.steer.yaw_rate_max_deg_s", _maxYawRateDeg, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.steer.sign_flips_per_10s", flipsPer10s, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.steer.retarget_count", _retargetCount, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.steer.retarget_per_10s", retargetsPer10s, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.steer.oscillation_cause_code", oscillationCause, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.steer.sample_count", _sampleCount, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.steer.eligible_samples", _eligibleSampleCount, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.steer.speed_gate_samples", _speedGateSampleCount, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.steer.speed_gate_threshold", _lastSpeedGateThreshold, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.steer.avg_speed", avgSpeed, TelemetryMetricUnit.Custom);
        }

        private static void AddOrUpdateMetric(
            DynamicBuffer<Space4XOperatorMetric> buffer,
            FixedString64Bytes key,
            float value)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                if (!metric.Key.Equals(key))
                {
                    continue;
                }

                metric.Value = value;
                buffer[i] = metric;
                return;
            }

            buffer.Add(new Space4XOperatorMetric
            {
                Key = key,
                Value = value
            });
        }

        private Entity ResolveFleetShip(FixedString64Bytes fleetId, ref SystemState state)
        {
            if (fleetId.Length == 0)
            {
                return Entity.Null;
            }

            foreach (var (fleet, entity) in SystemAPI.Query<RefRO<Space4XFleet>>().WithAll<Carrier>().WithEntityAccess())
            {
                if (fleet.ValueRO.FleetId.Equals(fleetId))
                {
                    return entity;
                }
            }

            UnityDebug.LogWarning($"[Space4XHeadlessSteeringStability] Fleet '{fleetId}' not found.");
            return Entity.Null;
        }

        private Entity ResolveHelmsman(Entity vesselEntity)
        {
            if (TryResolveController(vesselEntity, AgencyDomain.Movement, out var controller))
            {
                return controller != Entity.Null ? controller : vesselEntity;
            }

            if (_pilotLookup.HasComponent(vesselEntity))
            {
                var pilot = _pilotLookup[vesselEntity].Pilot;
                if (pilot != Entity.Null)
                {
                    return pilot;
                }
            }

            var navigationOfficer = ResolveSeatOccupant(vesselEntity, _roleNavigationOfficer);
            if (navigationOfficer != Entity.Null)
            {
                return navigationOfficer;
            }

            var shipmaster = ResolveSeatOccupant(vesselEntity, _roleShipmaster);
            if (shipmaster != Entity.Null)
            {
                return shipmaster;
            }

            var captain = ResolveSeatOccupant(vesselEntity, _roleCaptain);
            if (captain != Entity.Null)
            {
                return captain;
            }

            return Entity.Null;
        }

        private bool TryResolveController(Entity vesselEntity, AgencyDomain domain, out Entity controller)
        {
            controller = Entity.Null;
            if (!_resolvedControlLookup.HasBuffer(vesselEntity))
            {
                return false;
            }

            var resolved = _resolvedControlLookup[vesselEntity];
            for (int i = 0; i < resolved.Length; i++)
            {
                if (resolved[i].Domain == domain)
                {
                    controller = resolved[i].Controller;
                    return true;
                }
            }

            return false;
        }

        private Entity ResolveSeatOccupant(Entity vesselEntity, FixedString64Bytes roleId)
        {
            if (!_seatRefLookup.HasBuffer(vesselEntity))
            {
                return Entity.Null;
            }

            var seats = _seatRefLookup[vesselEntity];
            for (int i = 0; i < seats.Length; i++)
            {
                var seatEntity = seats[i].SeatEntity;
                if (seatEntity == Entity.Null || !_seatLookup.HasComponent(seatEntity))
                {
                    continue;
                }

                var seat = _seatLookup[seatEntity];
                if (!seat.RoleId.Equals(roleId))
                {
                    continue;
                }

                if (_seatOccupantLookup.HasComponent(seatEntity))
                {
                    return _seatOccupantLookup[seatEntity].OccupantEntity;
                }

                return Entity.Null;
            }

            return Entity.Null;
        }

        private void PushTraceEvent(Entity entity, MoveTraceEventKind kind, Entity target, uint tick)
        {
            if (!_traceLookup.HasBuffer(entity))
            {
                return;
            }

            var buffer = _traceLookup[entity];
            if (buffer.Length >= MovementDebugState.TraceCapacity)
            {
                buffer.RemoveAt(0);
            }

            buffer.Add(new MoveTraceEvent
            {
                Kind = kind,
                Tick = tick,
                Target = target
            });
        }

        private void ResetTracking()
        {
            _helmsmanEntity = Entity.Null;
            _lastTargetEntity = Entity.Null;
            _lastTargetPosition = default;
            _lastHeadingRad = 0f;
            _lastSteeringSign = 0f;
            _retargetCount = 0;
            _steeringSignFlips = 0;
            _maxHeadingErrorDeg = 0f;
            _maxYawRateDeg = 0f;
            _sampleCount = 0;
            _eligibleSampleCount = 0;
            _speedGateSampleCount = 0;
            _speedSum = 0f;
            _lastSpeedGateThreshold = 0f;
            _hasLastHeading = 0;
            _hasLastSteeringSign = 0;
            _hasLastTarget = 0;
        }

        private static float DeltaAngleRad(float from, float to)
        {
            var delta = to - from;
            if (delta > math.PI)
            {
                delta -= math.PI * 2f;
            }
            else if (delta < -math.PI)
            {
                delta += math.PI * 2f;
            }

            return delta;
        }

        private static float ClassifyOscillation(float flipsPer10s, uint retargets)
        {
            if (flipsPer10s <= SteeringFlipThresholdPer10s)
            {
                return 0f;
            }

            if (retargets > 0u)
            {
                return 1f;
            }

            return 2f;
        }

        private void AppendOscillationBlackCat(
            ref SystemState state,
            in Space4XSteeringStabilityBeatConfig config,
            float flipsPer10s,
            uint retargets,
            float oscillationCause)
        {
            if (!Space4XOperatorReportUtility.TryGetBlackCatBuffer(ref state, out var buffer))
            {
                return;
            }

            var id = new FixedString64Bytes("HEADING_OSCILLATION");
            buffer.Add(new Space4XOperatorBlackCat
            {
                Id = id,
                Primary = _shipEntity,
                Secondary = _helmsmanEntity,
                StartTick = _measureStartTick,
                EndTick = _measureEndTick,
                MetricA = flipsPer10s,
                MetricB = _maxYawRateDeg,
                MetricC = retargets,
                MetricD = _maxHeadingErrorDeg,
                Classification = (byte)math.clamp(oscillationCause, 0f, 255f)
            });
        }

        private void AppendSteeringSkippedBlackCat(ref SystemState state, in Space4XSteeringStabilityBeatConfig config, float measureSeconds)
        {
            if (!Space4XOperatorReportUtility.TryGetBlackCatBuffer(ref state, out var buffer))
            {
                return;
            }

            buffer.Add(new Space4XOperatorBlackCat
            {
                Id = new FixedString64Bytes("STEERING_BEAT_SKIPPED"),
                Primary = _shipEntity,
                Secondary = _helmsmanEntity,
                StartTick = _measureStartTick,
                EndTick = _measureEndTick,
                MetricA = _sampleCount,
                MetricB = measureSeconds,
                MetricC = 0f,
                MetricD = 0f,
                Classification = 1
            });
        }

        private void AppendSteeringLowSpeedBlackCat(ref SystemState state, in Space4XSteeringStabilityBeatConfig config, float avgSpeed)
        {
            if (!Space4XOperatorReportUtility.TryGetBlackCatBuffer(ref state, out var buffer))
            {
                return;
            }

            buffer.Add(new Space4XOperatorBlackCat
            {
                Id = new FixedString64Bytes("STEERING_BEAT_LOW_SPEED"),
                Primary = _shipEntity,
                Secondary = _helmsmanEntity,
                StartTick = _measureStartTick,
                EndTick = _measureEndTick,
                MetricA = _sampleCount,
                MetricB = avgSpeed,
                MetricC = _lastSpeedGateThreshold,
                MetricD = _speedGateSampleCount,
                Classification = 1
            });
        }

        private static uint SecondsToTicks(float seconds, float fixedDt)
        {
            var safeDt = math.max(1e-6f, fixedDt);
            return (uint)math.ceil(math.max(0f, seconds) / safeDt);
        }

        private static FixedString64Bytes BuildRoleNavigationOfficer()
        {
            FixedString64Bytes role = default;
            role.Append('s'); role.Append('h'); role.Append('i'); role.Append('p'); role.Append('.');
            role.Append('n'); role.Append('a'); role.Append('v'); role.Append('i'); role.Append('g');
            role.Append('a'); role.Append('t'); role.Append('i'); role.Append('o'); role.Append('n');
            role.Append('_'); role.Append('o'); role.Append('f'); role.Append('f'); role.Append('i');
            role.Append('c'); role.Append('e'); role.Append('r');
            return role;
        }

        private static FixedString64Bytes BuildRoleShipmaster()
        {
            FixedString64Bytes role = default;
            role.Append('s'); role.Append('h'); role.Append('i'); role.Append('p'); role.Append('.');
            role.Append('s'); role.Append('h'); role.Append('i'); role.Append('p'); role.Append('m');
            role.Append('a'); role.Append('s'); role.Append('t'); role.Append('e'); role.Append('r');
            return role;
        }

        private static FixedString64Bytes BuildRoleCaptain()
        {
            FixedString64Bytes role = default;
            role.Append('s'); role.Append('h'); role.Append('i'); role.Append('p'); role.Append('.');
            role.Append('c'); role.Append('a'); role.Append('p'); role.Append('t'); role.Append('a');
            role.Append('i'); role.Append('n');
            return role;
        }
    }
}
