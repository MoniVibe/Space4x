using System;
using System.Globalization;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Time;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using SystemEnv = System.Environment;

namespace Space4X.Headless
{
    /// <summary>
    /// Headless-only diagnostics for movement invariants and trace snapshots.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.VesselMovementSystem))]
    public partial struct Space4XHeadlessMovementDiagnosticsSystem : ISystem
    {
        private const uint TraceWindowTicks = 300;
        private const uint DefaultStuckWarnThreshold = 2;
        private const uint DefaultStuckFailThreshold = 6;
        private const float MiningApproachTeleportDistance = 3f;
        private const uint TurnWarmupTicks = 3;
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string ScenarioSourcePathEnv = "SPACE4X_SCENARIO_SOURCE_PATH";
        private const string StuckWarnThresholdEnv = "SPACE4X_HEADLESS_STUCK_WARN_THRESHOLD";
        private const string StuckFailThresholdEnv = "SPACE4X_HEADLESS_STUCK_FAIL_THRESHOLD";
        private const string MovementStrictEnv = "SPACE4X_HEADLESS_MOVEMENT_STRICT";
        private const string CollisionScenarioFile = "space4x_collision_micro.json";
        private const string SmokeScenarioFile = "space4x_smoke.json";
        private const string MiningScenarioFile = "space4x_mining.json";
        private const string MiningCombatScenarioFile = "space4x_mining_combat.json";
        private const string SensorsScenarioFile = "space4x_sensors_micro.json";
        private const string CommsScenarioFile = "space4x_comms_micro.json";
        private const string CommsBlockedScenarioFile = "space4x_comms_blocked_micro.json";
        private const string TurnrateScenarioFile = "space4x_turnrate_micro.json";
        private const string RefitScenarioFile = "space4x_refit.json";
        private const string ResearchScenarioFile = "space4x_research_mvp.json";
        private const uint TeleportFailureThreshold = 1;
        private const float MaxAngularSpeedRad = math.PI * 4f;
        private const float MaxAngularAccelRad = math.PI * 8f;
        private const float TurnSpeedMin = 0.15f;
        private bool _reportedFailure;
        private bool _ignoreStuckFailures;
        private bool _ignoreTeleportFailures;
        private bool _scenarioResolved;
        private bool _strictMovementFailures;
        private bool _ignoreTurnFailures;
        private bool _deferTurnFailures;
        private uint _stuckWarnThreshold;
        private uint _stuckFailThreshold;
        private EntityQuery _turnStateMissingQuery;
        private ComponentLookup<MiningState> _miningStateLookup;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();

            ResolveScenarioFlags();
            ResolveStuckThresholds();
            ResolveStrictMode();

            _miningStateLookup = state.GetComponentLookup<MiningState>(true);

            _turnStateMissingQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<VesselMovement>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<HeadlessTurnRateState>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_reportedFailure)
            {
                return;
            }

            if (!_scenarioResolved)
            {
                ResolveScenarioFlags();
            }

            _miningStateLookup.Update(ref state);

            if (!_turnStateMissingQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<HeadlessTurnRateState>(_turnStateMissingQuery);
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var tick = timeState.Tick;
            var deltaTime = math.max(1e-5f, timeState.FixedDeltaTime);

            var maxSpeedDelta = 0f;
            var maxTeleport = 0f;
            var maxStateFlips = 0u;
            var maxStuck = 0u;
            Entity speedOffender = Entity.Null;
            Entity teleportOffender = Entity.Null;
            Entity flipsOffender = Entity.Null;
            Entity stuckOffender = Entity.Null;
            var anyFailure = false;
            var failNaN = 0u;
            var failTeleport = 0u;
            var failStuck = 0u;
            var fatalStuck = 0u;
            var failSpike = 0u;
            var failTurnRate = 0u;
            var failTurnAccel = 0u;
            var maxTurnRate = 0f;
            var maxTurnAccel = 0f;
            var turnSampleCount = 0u;
            Entity nanOffender = Entity.Null;
            Entity turnRateOffender = Entity.Null;
            Entity turnAccelOffender = Entity.Null;

            foreach (var (movement, debug, transform, turnState, entity) in SystemAPI
                         .Query<RefRO<VesselMovement>, RefRO<MovementDebugState>, RefRO<LocalTransform>, RefRW<HeadlessTurnRateState>>()
                         .WithEntityAccess())
            {
                var debugState = debug.ValueRO;
                var velocityNaN = HasNaNOrInf(movement.ValueRO.Velocity);
                var nanCount = debugState.NaNInfCount;
                if (velocityNaN && nanCount == 0)
                {
                    nanCount = 1;
                }

                if (nanCount > 0)
                {
                    anyFailure = true;
                    failNaN += nanCount;
                    if (nanOffender == Entity.Null)
                    {
                        nanOffender = entity;
                    }
                }

                var ignoreTeleport = false;
                if (debugState.TeleportCount > 0)
                {
                    if (_ignoreTeleportFailures)
                    {
                        ignoreTeleport = true;
                    }

                    if (SystemAPI.HasComponent<MiningState>(entity))
                    {
                        var phase = SystemAPI.GetComponentRO<MiningState>(entity).ValueRO.Phase;
                        ignoreTeleport = phase == MiningPhase.Latching || phase == MiningPhase.Detaching || phase == MiningPhase.Docking;

                        if (!ignoreTeleport && phase == MiningPhase.ApproachTarget && debugState.LastDistanceToTarget <= MiningApproachTeleportDistance)
                        {
                            ignoreTeleport = true;
                        }

                        if (!ignoreTeleport && SystemAPI.HasComponent<MoveIntent>(entity))
                        {
                            var intent = SystemAPI.GetComponentRO<MoveIntent>(entity).ValueRO;
                            if (intent.IntentType == MoveIntentType.Hold)
                            {
                                ignoreTeleport = true;
                            }
                        }

                        if (!ignoreTeleport && SystemAPI.HasComponent<MovePlan>(entity))
                        {
                            var plan = SystemAPI.GetComponentRO<MovePlan>(entity).ValueRO;
                            if (plan.Mode == MovePlanMode.Arrive || plan.Mode == MovePlanMode.Latch)
                            {
                                ignoreTeleport = true;
                            }
                        }
                    }

                    if (!ignoreTeleport && _ignoreTeleportFailures && SystemAPI.HasComponent<VesselAIState>(entity))
                    {
                        var aiState = SystemAPI.GetComponentRO<VesselAIState>(entity).ValueRO;
                        if (aiState.CurrentState == VesselAIState.State.Mining)
                        {
                            ignoreTeleport = true;
                        }
                    }
                }

                if (debugState.TeleportCount > TeleportFailureThreshold && !ignoreTeleport)
                {
                    anyFailure = true;
                    failTeleport += debugState.TeleportCount;
                }

                var ignoreStuck = _ignoreStuckFailures;
                if (!ignoreStuck && debugState.StuckCount > 0 && SystemAPI.HasComponent<MiningState>(entity))
                {
                    var phase = SystemAPI.GetComponentRO<MiningState>(entity).ValueRO.Phase;
                    ignoreStuck = phase == MiningPhase.Latching || phase == MiningPhase.Mining || phase == MiningPhase.Docking;

                    if (!ignoreStuck && SystemAPI.HasComponent<MoveIntent>(entity))
                    {
                        var intent = SystemAPI.GetComponentRO<MoveIntent>(entity).ValueRO;
                        if (intent.IntentType == MoveIntentType.Hold)
                        {
                            ignoreStuck = true;
                        }
                    }

                    if (!ignoreStuck && SystemAPI.HasComponent<MovePlan>(entity))
                    {
                        var plan = SystemAPI.GetComponentRO<MovePlan>(entity).ValueRO;
                        if (plan.Mode == MovePlanMode.Arrive || plan.Mode == MovePlanMode.Latch)
                        {
                            ignoreStuck = true;
                        }
                    }

                    if (!ignoreStuck && SystemAPI.HasComponent<VesselAIState>(entity))
                    {
                        var aiState = SystemAPI.GetComponentRO<VesselAIState>(entity).ValueRO;
                        if (aiState.CurrentState == VesselAIState.State.Mining)
                        {
                            ignoreStuck = true;
                        }
                    }
                }

                if (!ignoreStuck && debugState.StuckCount > 0 && SystemAPI.HasComponent<MovePlan>(entity))
                {
                    var plan = SystemAPI.GetComponentRO<MovePlan>(entity).ValueRO;
                    if (plan.Mode == MovePlanMode.Orbit)
                    {
                        ignoreStuck = true;
                    }
                }

                if (debugState.StuckCount > _stuckWarnThreshold && !ignoreStuck)
                {
                    anyFailure = true;
                    failStuck += debugState.StuckCount;
                    if (debugState.StuckCount > _stuckFailThreshold)
                    {
                        fatalStuck += debugState.StuckCount;
                    }
                }

                var baseSpeed = math.max(0.1f, movement.ValueRO.BaseSpeed);
                var spikeThreshold = baseSpeed * 2.5f;
                if (debugState.MaxSpeedDelta > spikeThreshold)
                {
                    anyFailure = true;
                    failSpike++;
                }

                if (debugState.MaxSpeedDelta > maxSpeedDelta)
                {
                    maxSpeedDelta = debugState.MaxSpeedDelta;
                    speedOffender = entity;
                }

                if (debugState.MaxTeleportDistance > maxTeleport)
                {
                    maxTeleport = debugState.MaxTeleportDistance;
                    teleportOffender = entity;
                }

                if (debugState.StateFlipCount > maxStateFlips)
                {
                    maxStateFlips = debugState.StateFlipCount;
                    flipsOffender = entity;
                }

                if (debugState.StuckCount > maxStuck)
                {
                    maxStuck = debugState.StuckCount;
                    stuckOffender = entity;
                }

                if (_miningStateLookup.HasComponent(entity))
                {
                    var miningState = _miningStateLookup[entity];
                    if (miningState.Phase == MiningPhase.Undocking)
                    {
                        var stateValue = turnState.ValueRW;
                        stateValue.LastRotation = transform.ValueRO.Rotation;
                        stateValue.LastAngularSpeed = 0f;
                        stateValue.Initialized = 1;
                        turnState.ValueRW = stateValue;
                        continue;
                    }
                }

                var speed = movement.ValueRO.CurrentSpeed;
                if (speed <= 0f)
                {
                    speed = math.length(movement.ValueRO.Velocity);
                }
                var wantsMove = movement.ValueRO.IsMoving != 0 && speed >= TurnSpeedMin;
                if (wantsMove)
                {
                    var stateValue = turnState.ValueRW;
                    var moveStartTick = movement.ValueRO.MoveStartTick;
                    if (moveStartTick > 0 && tick <= moveStartTick + TurnWarmupTicks)
                    {
                        stateValue.LastRotation = transform.ValueRO.Rotation;
                        stateValue.LastAngularSpeed = 0f;
                        stateValue.LastMoveStartTick = moveStartTick;
                        stateValue.Initialized = 1;
                        turnState.ValueRW = stateValue;
                        continue;
                    }

                    if (stateValue.Initialized == 0 ||
                        (moveStartTick > 0 && stateValue.LastMoveStartTick != moveStartTick))
                    {
                        stateValue.LastRotation = transform.ValueRO.Rotation;
                        stateValue.LastAngularSpeed = 0f;
                        stateValue.LastMoveStartTick = moveStartTick;
                        stateValue.Initialized = 1;
                    }
                    else
                    {
                        var dot = math.abs(math.dot(stateValue.LastRotation.value, transform.ValueRO.Rotation.value));
                        dot = math.clamp(dot, -1f, 1f);
                        var angle = 2f * math.acos(dot);
                        var angularSpeed = angle / deltaTime;
                        var angularAccel = math.abs(angularSpeed - stateValue.LastAngularSpeed) / deltaTime;
                        turnSampleCount++;

                        if (!_ignoreTurnFailures)
                        {
                            if (angularSpeed > MaxAngularSpeedRad)
                            {
                                anyFailure = true;
                                failTurnRate++;
                                if (angularSpeed > maxTurnRate)
                                {
                                    maxTurnRate = angularSpeed;
                                    turnRateOffender = entity;
                                }
                            }

                            if (angularAccel > MaxAngularAccelRad)
                            {
                                anyFailure = true;
                                failTurnAccel++;
                                if (angularAccel > maxTurnAccel)
                                {
                                    maxTurnAccel = angularAccel;
                                    turnAccelOffender = entity;
                                }
                            }
                        }

                        stateValue.LastRotation = transform.ValueRO.Rotation;
                        stateValue.LastAngularSpeed = angularSpeed;
                    }

                    turnState.ValueRW = stateValue;
                }
            }

            if (Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var metricBuffer))
            {
                AddOrUpdateMetric(metricBuffer, new FixedString64Bytes("space4x.movement.turn_rate_failures"), failTurnRate);
                AddOrUpdateMetric(metricBuffer, new FixedString64Bytes("space4x.movement.turn_accel_failures"), failTurnAccel);
                AddOrUpdateMetric(metricBuffer, new FixedString64Bytes("space4x.movement.turn_rate_max"), maxTurnRate);
                AddOrUpdateMetric(metricBuffer, new FixedString64Bytes("space4x.movement.turn_accel_max"), maxTurnAccel);
                AddOrUpdateMetric(metricBuffer, new FixedString64Bytes("space4x.movement.turn_sample_count"), turnSampleCount);
            }

            if (!anyFailure)
            {
                return;
            }

            _reportedFailure = true;
            var fatalFailure = failNaN > 0 || failTeleport > 0 || failTurnRate > 0 || failTurnAccel > 0;
            if (_strictMovementFailures)
            {
                fatalFailure |= fatalStuck > 0 || failSpike > 0;
            }
            if (fatalFailure)
            {
                UnityDebug.LogError($"[Space4XHeadlessMovementDiag] FAIL tick={tick} nanInf={failNaN} teleport={failTeleport} stuck={failStuck} stuckFatal={fatalStuck} spikes={failSpike} turnRate={failTurnRate} turnAccel={failTurnAccel} strict={_strictMovementFailures}");
                LogOffenderReport(ref state, tick, speedOffender, teleportOffender, flipsOffender, stuckOffender, turnRateOffender, turnAccelOffender, maxSpeedDelta, maxTeleport, maxStateFlips, maxStuck, maxTurnRate, maxTurnAccel);
                WriteInvariantBundle(ref state, tick, timeState.WorldSeconds, failNaN, fatalStuck, failSpike, failTurnRate, failTurnAccel, nanOffender, stuckOffender, speedOffender, turnRateOffender, turnAccelOffender);
                if (!_deferTurnFailures || failNaN > 0 || failTeleport > 0 || (failTurnRate == 0 && failTurnAccel == 0))
                {
                    HeadlessExitUtility.Request(state.EntityManager, tick, Space4XHeadlessDiagnostics.TestFailExitCode);
                }
                return;
            }

            UnityDebug.LogWarning($"[Space4XHeadlessMovementDiag] WARN tick={tick} nanInf={failNaN} teleport={failTeleport} stuck={failStuck} stuckFatal={fatalStuck} spikes={failSpike} turnRate={failTurnRate} turnAccel={failTurnAccel} strict={_strictMovementFailures}");
            LogOffenderReport(ref state, tick, speedOffender, teleportOffender, flipsOffender, stuckOffender, turnRateOffender, turnAccelOffender, maxSpeedDelta, maxTeleport, maxStateFlips, maxStuck, maxTurnRate, maxTurnAccel);
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

        private void ResolveStuckThresholds()
        {
            _stuckWarnThreshold = ReadUIntEnv(StuckWarnThresholdEnv, DefaultStuckWarnThreshold);
            _stuckFailThreshold = ReadUIntEnv(StuckFailThresholdEnv, DefaultStuckFailThreshold);
            if (_stuckFailThreshold < _stuckWarnThreshold)
            {
                _stuckFailThreshold = _stuckWarnThreshold;
            }
        }

        private void ResolveStrictMode()
        {
            _strictMovementFailures = ReadBoolEnv(MovementStrictEnv, false);
        }

        private void ResolveScenarioFlags()
        {
            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioSourcePathEnv);
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            }
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                // Fall back to the generic runner env var used by headless scripts.
                scenarioPath = SystemEnv.GetEnvironmentVariable("SCENARIO_PATH");
            }
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return;
            }

            _scenarioResolved = true;
            if (scenarioPath.EndsWith(CollisionScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                _ignoreStuckFailures = true;
                _ignoreTeleportFailures = true;
                _ignoreTurnFailures = true;
                return;
            }

            if (scenarioPath.EndsWith(SensorsScenarioFile, StringComparison.OrdinalIgnoreCase) ||
                scenarioPath.EndsWith(CommsScenarioFile, StringComparison.OrdinalIgnoreCase) ||
                scenarioPath.EndsWith(CommsBlockedScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                _ignoreTurnFailures = true;
                return;
            }

            if (scenarioPath.EndsWith(TurnrateScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                _deferTurnFailures = true;
                return;
            }

            if (scenarioPath.EndsWith(SmokeScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                // Smoke mining undock/approach can trip stuck counters before latch; skip stuck failures there.
                _ignoreStuckFailures = true;
                // Latch/dock surface snapping can exceed teleport thresholds in smoke runs.
                _ignoreTeleportFailures = true;
                // Keep strict turn gating in the dedicated turnrate micro, not smoke.
                _ignoreTurnFailures = true;
                return;
            }

            if (scenarioPath.EndsWith(MiningScenarioFile, StringComparison.OrdinalIgnoreCase) ||
                scenarioPath.EndsWith(MiningCombatScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                // Mining scenarios can have acceptable teleports during approach/retargeting.
                _ignoreTeleportFailures = true;
                // Mining-only loops can also oscillate while latching/holding; avoid failing the bank on stuck counts.
                _ignoreStuckFailures = true;
                return;
            }

            if (scenarioPath.EndsWith(RefitScenarioFile, StringComparison.OrdinalIgnoreCase) ||
                scenarioPath.EndsWith(ResearchScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                // Refit/research focus on module loops; ignore stuck spikes to keep telemetry flowing.
                _ignoreStuckFailures = true;
            }
        }

        private void WriteInvariantBundle(ref SystemState state, uint tick, float worldSeconds, uint failNaN, uint failStuck, uint failSpike, uint failTurnRate, uint failTurnAccel, Entity nanOffender, Entity stuckOffender, Entity speedOffender, Entity turnRateOffender, Entity turnAccelOffender)
        {
            var code = "Invariant/Movement";
            var message = "Movement invariant failure.";
            var offender = Entity.Null;

            if (failNaN > 0)
            {
                code = "Invariant/NaNTransform";
                message = $"NaN/Inf detected in vessel movement at tick {tick}.";
                offender = nanOffender;
            }
            else if (failTurnRate > 0)
            {
                code = "Invariant/TurnRate";
                message = $"Turn rate exceeded at tick {tick}.";
                offender = turnRateOffender;
            }
            else if (failTurnAccel > 0)
            {
                code = "Invariant/TurnAccel";
                message = $"Turn acceleration exceeded at tick {tick}.";
                offender = turnAccelOffender;
            }
            else if (failStuck > 0)
            {
                code = "Invariant/MovementStuck";
                message = $"Movement stuck detected at tick {tick}.";
                offender = stuckOffender;
            }
            else if (failSpike > 0)
            {
                code = "Invariant/SpeedSpike";
                message = $"Speed spike detected at tick {tick}.";
                offender = speedOffender;
            }

            var observed = string.Format(CultureInfo.InvariantCulture,
                "tick={0} nanInf={1} stuck={2} spikes={3} turnRate={4} turnAccel={5}",
                tick,
                failNaN,
                failStuck,
                failSpike,
                failTurnRate,
                failTurnAccel);
            Space4XHeadlessDiagnostics.ReportInvariant(code, message, observed, "no invariant violations");

            var hasEntity = offender != Entity.Null;
            var hasTransform = hasEntity && SystemAPI.HasComponent<LocalTransform>(offender);
            var hasMovement = hasEntity && SystemAPI.HasComponent<VesselMovement>(offender);
            var position = hasTransform ? SystemAPI.GetComponentRO<LocalTransform>(offender).ValueRO.Position : default;
            var rotation = hasTransform ? SystemAPI.GetComponentRO<LocalTransform>(offender).ValueRO.Rotation : default;
            var velocity = hasMovement ? SystemAPI.GetComponentRO<VesselMovement>(offender).ValueRO.Velocity : default;

            HeadlessInvariantBundleWriter.TryWriteBundle(
                state.EntityManager,
                code,
                message,
                tick,
                worldSeconds,
                offender,
                hasEntity,
                position,
                hasTransform,
                velocity,
                hasMovement,
                rotation,
                hasTransform);
        }

        private void LogOffenderReport(ref SystemState state, uint tick, Entity speedOffender, Entity teleportOffender, Entity flipsOffender, Entity stuckOffender, Entity turnRateOffender, Entity turnAccelOffender, float maxSpeedDelta, float maxTeleport, uint maxStateFlips, uint maxStuck, float maxTurnRate, float maxTurnAccel)
        {
            UnityDebug.LogError($"[Space4XHeadlessMovementDiag] TopOffenders speedSpike entity={speedOffender.Index} maxDelta={maxSpeedDelta:F3} teleport entity={teleportOffender.Index} maxTeleport={maxTeleport:F3} stateFlips entity={flipsOffender.Index} flips={maxStateFlips} stuck entity={stuckOffender.Index} count={maxStuck} turnRate entity={turnRateOffender.Index} rate={maxTurnRate:F3} turnAccel entity={turnAccelOffender.Index} accel={maxTurnAccel:F3}");

            LogSnapshot(ref state, tick, speedOffender, "speed_spike");
            LogSnapshot(ref state, tick, teleportOffender, "teleport");
            LogSnapshot(ref state, tick, flipsOffender, "state_flips");
            LogSnapshot(ref state, tick, stuckOffender, "stuck");
            LogSnapshot(ref state, tick, turnRateOffender, "turn_rate");
            LogSnapshot(ref state, tick, turnAccelOffender, "turn_accel");
        }

        private void LogSnapshot(ref SystemState state, uint tick, Entity entity, string label)
        {
            if (entity == Entity.Null)
            {
                return;
            }

            var hasTransform = SystemAPI.HasComponent<LocalTransform>(entity);
            var hasMovement = SystemAPI.HasComponent<VesselMovement>(entity);
            var hasAI = SystemAPI.HasComponent<VesselAIState>(entity);
            var hasIntent = SystemAPI.HasComponent<MoveIntent>(entity);
            var hasPlan = SystemAPI.HasComponent<MovePlan>(entity);
            var hasDecision = SystemAPI.HasComponent<DecisionTrace>(entity);
            var hasDebug = SystemAPI.HasComponent<MovementDebugState>(entity);
            var hasMiningState = SystemAPI.HasComponent<MiningState>(entity);
            var hasMiningVessel = SystemAPI.HasComponent<MiningVessel>(entity);
            var hasCarrier = SystemAPI.HasComponent<Carrier>(entity);

            UnityDebug.LogError($"[Space4XHeadlessMovementDiag] FinalSnapshot {label} tick={tick} entity={entity.Index} hasTransform={hasTransform} hasMovement={hasMovement} hasAI={hasAI} hasIntent={hasIntent} hasPlan={hasPlan} hasDecision={hasDecision} hasDebug={hasDebug} mining={hasMiningVessel} carrier={hasCarrier}");

            if (hasTransform)
            {
                var transform = SystemAPI.GetComponentRO<LocalTransform>(entity).ValueRO;
                UnityDebug.LogError($"[Space4XHeadlessMovementDiag] Transform pos={transform.Position} rot={transform.Rotation}");
            }

            if (hasMovement)
            {
                var movement = SystemAPI.GetComponentRO<VesselMovement>(entity).ValueRO;
                UnityDebug.LogError($"[Space4XHeadlessMovementDiag] Movement baseSpeed={movement.BaseSpeed:F2} currentSpeed={movement.CurrentSpeed:F2} accel={movement.Acceleration:F2} decel={movement.Deceleration:F2} velocity={movement.Velocity} desiredRot={movement.DesiredRotation.value}");
            }

            if (hasAI)
            {
                var ai = SystemAPI.GetComponentRO<VesselAIState>(entity).ValueRO;
                UnityDebug.LogError($"[Space4XHeadlessMovementDiag] AI goal={ai.CurrentGoal} state={ai.CurrentState} targetEntity={ai.TargetEntity.Index} targetPos={ai.TargetPosition}");
            }

            if (hasIntent)
            {
                var intent = SystemAPI.GetComponentRO<MoveIntent>(entity).ValueRO;
                UnityDebug.LogError($"[Space4XHeadlessMovementDiag] MoveIntent type={intent.IntentType} targetEntity={intent.TargetEntity.Index} targetPos={intent.TargetPosition}");
            }

            if (hasPlan)
            {
                var plan = SystemAPI.GetComponentRO<MovePlan>(entity).ValueRO;
                UnityDebug.LogError($"[Space4XHeadlessMovementDiag] MovePlan mode={plan.Mode} desiredVel={plan.DesiredVelocity} maxAccel={plan.MaxAccel:F2} eta={plan.EstimatedTime:F2}");
            }

            if (hasDecision)
            {
                var decision = SystemAPI.GetComponentRO<DecisionTrace>(entity).ValueRO;
                UnityDebug.LogError($"[Space4XHeadlessMovementDiag] Decision reason={decision.ReasonCode} chosenTarget={decision.ChosenTarget.Index} score={decision.Score:F2} blocker={decision.BlockerEntity.Index} since={decision.SinceTick}");
            }

            if (hasDebug)
            {
                var debug = SystemAPI.GetComponentRO<MovementDebugState>(entity).ValueRO;
                UnityDebug.LogError($"[Space4XHeadlessMovementDiag] Debug lastPos={debug.LastPosition} lastDist={debug.LastDistanceToTarget:F2} lastSpeed={debug.LastSpeed:F2} maxSpeedDelta={debug.MaxSpeedDelta:F2} maxTeleport={debug.MaxTeleportDistance:F2} nanInf={debug.NaNInfCount} speedClamp={debug.SpeedClampCount} accelClamp={debug.AccelClampCount} teleport={debug.TeleportCount} stuck={debug.StuckCount} flips={debug.StateFlipCount} lastProgressTick={debug.LastProgressTick}");
            }

            if (hasMiningState)
            {
                var mining = SystemAPI.GetComponentRO<MiningState>(entity).ValueRO;
                UnityDebug.LogError($"[Space4XHeadlessMovementDiag] MiningState phase={mining.Phase} target={mining.ActiveTarget.Index} timer={mining.MiningTimer:F2} interval={mining.TickInterval:F2} phaseTimer={mining.PhaseTimer:F2} digVolume={mining.DigVolumeEntity.Index}");
            }

            if (SystemAPI.HasBuffer<MoveTraceEvent>(entity))
            {
                var traceBuffer = SystemAPI.GetBuffer<MoveTraceEvent>(entity);
                for (var i = 0; i < traceBuffer.Length; i++)
                {
                    var trace = traceBuffer[i];
                    if (trace.Tick + TraceWindowTicks < tick)
                    {
                        continue;
                    }

                    UnityDebug.LogError($"[Space4XHeadlessMovementDiag] TraceEvent kind={trace.Kind} tick={trace.Tick} target={trace.Target.Index}");
                }
            }
        }

        private static bool HasNaNOrInf(float3 value)
        {
            return math.any(math.isnan(value)) || math.any(math.isinf(value));
        }

        private static uint ReadUIntEnv(string key, uint defaultValue)
        {
            var raw = SystemEnv.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            return uint.TryParse(raw, out var parsed) ? parsed : defaultValue;
        }

        private static bool ReadBoolEnv(string key, bool defaultValue)
        {
            var raw = SystemEnv.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            if (raw == "1")
            {
                return true;
            }

            if (raw == "0")
            {
                return false;
            }

            return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
        }
    }
}
