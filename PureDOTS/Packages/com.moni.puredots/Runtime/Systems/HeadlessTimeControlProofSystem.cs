using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems.Telemetry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using SystemEnv = System.Environment;
using UnityDebug = UnityEngine.Debug;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Headless proof for global time controls (pause/step/resume/speed) and local time bubbles.
    /// </summary>
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(TimeTickSystem))]
    [UpdateAfter(typeof(RewindCoordinatorSystem))]
    public partial struct HeadlessTimeControlProofSystem : ISystem
    {
        private const string EnabledEnv = "PUREDOTS_HEADLESS_TIME_PROOF";
        private const string ExitOnResultEnv = "PUREDOTS_HEADLESS_TIME_PROOF_EXIT";
        private const string TimeoutSecondsEnv = "PUREDOTS_HEADLESS_TIME_PROOF_TIMEOUT_S";
        private const string StartTickEnv = "PUREDOTS_HEADLESS_TIME_PROOF_START_TICK";

        private const float DefaultTimeoutSeconds = 10f;
        private const uint DefaultStartTick = 240;
        private const uint ScenarioStartTick = 10;
        private const int PauseHoldFrames = 4;
        private const int LocalHoldFrames = 4;
        private const int StepTicks = 2;
        private const float SpeedFast = 2f;
        private const float SpeedNormal = 1f;
        private const float LocalScale = 0.5f;
        private const float ProbeRadius = 6f;
        private const byte RewindRequiredMask = (byte)HeadlessRewindProofStage.RecordReturn;

        private static readonly FixedString32Bytes ExpectedPaused = new FixedString32Bytes("paused");
        private static readonly FixedString32Bytes ExpectedSteps = new FixedString32Bytes("+2");
        private static readonly FixedString32Bytes ExpectedPlaying = new FixedString32Bytes("play");
        private static readonly FixedString32Bytes ExpectedSpeed = new FixedString32Bytes("2");
        private static readonly FixedString32Bytes ExpectedLocalPause = new FixedString32Bytes("0");
        private static readonly FixedString32Bytes ExpectedLocalScale = new FixedString32Bytes("0.5");
        private static readonly FixedString32Bytes ExpectedLocalRewind = new FixedString32Bytes("<0");
        private static readonly FixedString32Bytes ExpectedRewindSubject = new FixedString32Bytes("time_control");
        private static readonly FixedString64Bytes RewindProofId = new FixedString64Bytes("time.control");

        private enum Phase : byte
        {
            Init = 0,
            GlobalPause,
            GlobalPauseHold,
            GlobalStep,
            GlobalResume,
            GlobalSpeedUp,
            GlobalSpeedReset,
            LocalPause,
            LocalScale,
            LocalRewind,
            Complete,
            Failed
        }

        private byte _enabled;
        private Phase _phase;
        private double _phaseStartTime;
        private int _holdFrames;
        private uint _stepTargetTick;
        private Entity _probeEntity;
        private byte _commandIssued;
        private byte _bubbleRequested;
        private byte _bubbleRemovalRequested;
        private uint _pendingBubbleId;
        private float _baselineAccumulated;
        private int _baselineUpdates;
        private float _timeoutSeconds;
        private uint _startTick;
        private byte _startTickFromEnv;
        private byte _useScenarioTick;
        private byte _loggedWaitingForStart;
        private byte _loggedWaitingForStableMode;
        private byte _rewindSubjectRegistered;
        private byte _rewindPending;
        private byte _rewindPass;
        private float _rewindObserved;
        private byte _loggedStartup;
        private byte _loggedPhaseInit;
        private uint _lastObservedTick;
        private int _stallFrames;
        private byte _loggedStall;
        private Phase _lastPhase;
        private byte _loggedGlobalPauseEnter;
        private int _updateCounter;
        private Entity _proofEntity;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            if (!ResolveEnabled())
            {
                state.Enabled = false;
                return;
            }

            _enabled = 1;
            _phase = Phase.Init;
            _timeoutSeconds = ReadEnvFloat(TimeoutSecondsEnv, ReadEnvFloat("TIMEOUT_S", DefaultTimeoutSeconds));
            if (TryReadEnvUInt(StartTickEnv, out var startTick) || TryReadEnvUInt("START_TICK", out startTick))
            {
                _startTick = startTick;
                _startTickFromEnv = 1;
            }
            else
            {
                _startTick = DefaultStartTick;
                _startTickFromEnv = 0;
            }
            _loggedWaitingForStart = 0;
            _loggedWaitingForStableMode = 0;
            _loggedStartup = 0;
            _loggedPhaseInit = 0;
            _lastObservedTick = 0;
            _stallFrames = 0;
            _loggedStall = 0;
            _lastPhase = _phase;
            _loggedGlobalPauseEnter = 0;
            _updateCounter = 0;
            EnsureProofState(ref state);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_enabled == 0)
            {
                return;
            }

            EnsureRewindSubject(ref state);
            TryFlushRewindProof(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            timeState.Tick = tickTimeState.Tick;
            timeState.FixedDeltaTime = tickTimeState.FixedDeltaTime;
            timeState.DeltaTime = tickTimeState.FixedDeltaTime;
            timeState.DeltaSeconds = tickTimeState.FixedDeltaTime;
            timeState.WorldSeconds = tickTimeState.WorldSeconds;
            timeState.ElapsedTime = tickTimeState.WorldSeconds;
            timeState.CurrentSpeedMultiplier = tickTimeState.CurrentSpeedMultiplier;
            timeState.IsPaused = tickTimeState.IsPaused;

            if (_loggedStartup == 0)
            {
                _loggedStartup = 1;
                var tickCount = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TickTimeState>()).CalculateEntityCount();
                var rewindCount = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>()).CalculateEntityCount();
                UnityDebug.Log($"[HeadlessTimeControlProof] active startTick={_startTick} timeout_s={_timeoutSeconds} envStart={(_startTickFromEnv != 0 ? "yes" : "no")} world={state.WorldUnmanaged.Name} tickTimeCount={tickCount} rewindCount={rewindCount}");
            }

            if (_updateCounter < 5)
            {
                UnityDebug.Log($"[HeadlessTimeControlProof] update#{_updateCounter} phase={_phase} tickTime={tickTimeState.Tick} paused={tickTimeState.IsPaused} playing={tickTimeState.IsPlaying}");
                _updateCounter++;
            }

            var tickSource = tickTimeState.Tick;
            if (_useScenarioTick == 0 && _startTickFromEnv == 0 && _startTick == DefaultStartTick)
            {
                if (SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenarioTick))
                {
                    _useScenarioTick = 1;
                    _startTick = ScenarioStartTick;
                    tickSource = scenarioTick.Tick;
                }
            }
            else if (_useScenarioTick != 0 && SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenarioTick))
            {
                tickSource = scenarioTick.Tick;
            }

            if (_lastObservedTick == tickTimeState.Tick)
            {
                _stallFrames++;
            }
            else
            {
                _stallFrames = 0;
                _lastObservedTick = tickTimeState.Tick;
            }

            if (_loggedStall == 0 && _stallFrames >= 120)
            {
                _loggedStall = 1;
                var scenarioTick = SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenarioState) ? scenarioState.Tick : 0u;
                UnityDebug.LogWarning($"[HeadlessTimeControlProof] tick stall frames={_stallFrames} tickTime={tickTimeState.Tick} target={tickTimeState.TargetTick} paused={tickTimeState.IsPaused} playing={tickTimeState.IsPlaying} rewind={SystemAPI.GetSingleton<RewindState>().Mode} scenarioTick={scenarioTick}");
            }

            if (tickSource < _startTick)
            {
                if (_loggedWaitingForStart == 0)
                {
                    _loggedWaitingForStart = 1;
                    var sourceLabel = _useScenarioTick != 0 ? "scenario" : "time";
                    UnityDebug.Log($"[HeadlessTimeControlProof] waiting startTick={_startTick} currentTick={tickSource} source={sourceLabel}");
                }
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode == RewindMode.Rewind || rewindState.Mode == RewindMode.Step)
            {
                if (_loggedWaitingForStableMode == 0)
                {
                    _loggedWaitingForStableMode = 1;
                    UnityDebug.Log($"[HeadlessTimeControlProof] waiting rewindMode=Play/Paused currentMode={rewindState.Mode}");
                }
                return;
            }

            EnsureProbe(ref state);
            UpdateProbe(ref state, tickTimeState, timeState, rewindState);

            if (_phase == Phase.Complete || _phase == Phase.Failed)
            {
                return;
            }

            if (_phase == Phase.GlobalPause && _loggedGlobalPauseEnter == 0)
            {
                _loggedGlobalPauseEnter = 1;
                UnityDebug.Log($"[HeadlessTimeControlProof] enter global pause commandIssued={_commandIssued} tickTime={tickTimeState.Tick} paused={tickTimeState.IsPaused}");
            }

            var elapsed = SystemAPI.Time.ElapsedTime;
            if (_phaseStartTime <= 0)
            {
                _phaseStartTime = elapsed;
            }

            switch (_phase)
            {
                case Phase.Init:
                    if (_loggedPhaseInit == 0)
                    {
                        _loggedPhaseInit = 1;
                        var scenarioTick = SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenarioState) ? scenarioState.Tick : 0u;
                        UnityDebug.Log($"[HeadlessTimeControlProof] phase=init tickTime={tickTimeState.Tick} target={tickTimeState.TargetTick} paused={tickTimeState.IsPaused} playing={tickTimeState.IsPlaying} rewind={rewindState.Mode} scenarioTick={scenarioTick}");
                    }
                    AdvancePhase(Phase.GlobalPause, elapsed);
                    break;

                case Phase.GlobalPause:
                    if (_commandIssued == 0)
                    {
                        EnqueueCommand(ref state, TimeControlCommandType.Pause);
                        _commandIssued = 1;
                        _phaseStartTime = elapsed;
                        var rewindEntity = SystemAPI.GetSingletonEntity<RewindState>();
                        var bufferLen = state.EntityManager.HasBuffer<TimeControlCommand>(rewindEntity)
                            ? state.EntityManager.GetBuffer<TimeControlCommand>(rewindEntity).Length
                            : 0;
                        UnityDebug.Log($"[HeadlessTimeControlProof] issued pause tickTime={tickTimeState.Tick} bufferLen={bufferLen}");
                    }

                    if (timeState.IsPaused)
                    {
                        _holdFrames = 0;
                        AdvancePhase(Phase.GlobalPauseHold, elapsed);
                        break;
                    }

                    if (IsTimedOut(elapsed))
                    {
                        Fail(ref state, timeState.Tick, "global.pause");
                    }
                    break;

                case Phase.GlobalPauseHold:
                    if (!timeState.IsPaused)
                    {
                        Fail(ref state, timeState.Tick, "global.pause_hold");
                        break;
                    }

                    if (TimeHelpers.GetGlobalDelta(tickTimeState, timeState) <= 0f)
                    {
                        _holdFrames++;
                    }

                    if (_holdFrames >= PauseHoldFrames)
                    {
                        EmitStep(ref state, timeState.Tick, "global.pause", true, 1f, ExpectedPaused);
                        _commandIssued = 0;
                        AdvancePhase(Phase.GlobalStep, elapsed);
                        break;
                    }

                    if (IsTimedOut(elapsed))
                    {
                        Fail(ref state, timeState.Tick, "global.pause_hold");
                    }
                    break;

                case Phase.GlobalStep:
                    if (_commandIssued == 0)
                    {
                        _stepTargetTick = timeState.Tick + StepTicks;
                        EnqueueCommand(ref state, TimeControlCommandType.StepTicks, StepTicks);
                        _commandIssued = 1;
                        _phaseStartTime = elapsed;
                    }

                    if (timeState.Tick >= _stepTargetTick && timeState.IsPaused)
                    {
                        EmitStep(ref state, timeState.Tick, "global.step", true, StepTicks, ExpectedSteps);
                        _commandIssued = 0;
                        AdvancePhase(Phase.GlobalResume, elapsed);
                        break;
                    }

                    if (IsTimedOut(elapsed))
                    {
                        Fail(ref state, timeState.Tick, "global.step");
                    }
                    break;

                case Phase.GlobalResume:
                    if (_commandIssued == 0)
                    {
                        EnqueueCommand(ref state, TimeControlCommandType.Resume);
                        _commandIssued = 1;
                        _phaseStartTime = elapsed;
                    }

                    if (!timeState.IsPaused && timeState.Tick > _stepTargetTick)
                    {
                        EmitStep(ref state, timeState.Tick, "global.resume", true, timeState.Tick - _stepTargetTick, ExpectedPlaying);
                        _commandIssued = 0;
                        AdvancePhase(Phase.GlobalSpeedUp, elapsed);
                        break;
                    }

                    if (IsTimedOut(elapsed))
                    {
                        Fail(ref state, timeState.Tick, "global.resume");
                    }
                    break;

                case Phase.GlobalSpeedUp:
                    if (_commandIssued == 0)
                    {
                        EnqueueCommand(ref state, TimeControlCommandType.SetSpeed, SpeedFast);
                        _commandIssued = 1;
                        _phaseStartTime = elapsed;
                    }

                    if (math.abs(timeState.CurrentSpeedMultiplier - SpeedFast) < 0.01f)
                    {
                        EmitStep(ref state, timeState.Tick, "global.speed_up", true, timeState.CurrentSpeedMultiplier, ExpectedSpeed);
                        _commandIssued = 0;
                        AdvancePhase(Phase.GlobalSpeedReset, elapsed);
                        break;
                    }

                    if (IsTimedOut(elapsed))
                    {
                        Fail(ref state, timeState.Tick, "global.speed_up");
                    }
                    break;

                case Phase.GlobalSpeedReset:
                    if (_commandIssued == 0)
                    {
                        EnqueueCommand(ref state, TimeControlCommandType.SetSpeed, SpeedNormal);
                        _commandIssued = 1;
                        _phaseStartTime = elapsed;
                    }

                    if (math.abs(timeState.CurrentSpeedMultiplier - SpeedNormal) < 0.01f)
                    {
                        EmitStep(ref state, timeState.Tick, "global.speed_reset", true, timeState.CurrentSpeedMultiplier, new FixedString32Bytes("1"));
                        _commandIssued = 0;
                        AdvancePhase(Phase.LocalPause, elapsed);
                        break;
                    }

                    if (IsTimedOut(elapsed))
                    {
                        Fail(ref state, timeState.Tick, "global.speed_reset");
                    }
                    break;

                case Phase.LocalPause:
                    if (!EnsureBubbleCleared(ref state))
                    {
                        break;
                    }

                    if (!IssueBubbleOnce(ref state, TimeBubbleMode.Pause, 0f))
                    {
                        break;
                    }

                    if (!TryGetProbeMembership(ref state, out var membership) || membership.LocalMode != TimeBubbleMode.Pause)
                    {
                        if (IsTimedOut(elapsed))
                        {
                            Fail(ref state, timeState.Tick, "local.pause");
                        }
                        break;
                    }

                    if (_holdFrames == 0)
                    {
                        CaptureProbeBaseline(ref state);
                    }

                    if (ProbeStable(ref state))
                    {
                        _holdFrames++;
                    }

                    if (_holdFrames >= LocalHoldFrames)
                    {
                        EmitStep(ref state, timeState.Tick, "local.pause", true, 0f, ExpectedLocalPause);
                        _commandIssued = 0;
                        _bubbleRequested = 0;
                        _holdFrames = 0;
                        AdvancePhase(Phase.LocalScale, elapsed);
                        break;
                    }

                    if (IsTimedOut(elapsed))
                    {
                        Fail(ref state, timeState.Tick, "local.pause_hold");
                    }
                    break;

                case Phase.LocalScale:
                    if (!EnsureBubbleCleared(ref state))
                    {
                        break;
                    }

                    if (!IssueBubbleOnce(ref state, TimeBubbleMode.Scale, LocalScale))
                    {
                        break;
                    }

                    if (!TryGetProbeMembership(ref state, out var scaleMembership) || scaleMembership.LocalMode != TimeBubbleMode.Scale)
                    {
                        if (IsTimedOut(elapsed))
                        {
                            Fail(ref state, timeState.Tick, "local.scale");
                        }
                        break;
                    }

                    if (ProbeDeltaMatches(ref state, LocalScale))
                    {
                        EmitStep(ref state, timeState.Tick, "local.scale", true, GetProbeDelta(ref state), ExpectedLocalScale);
                        _commandIssued = 0;
                        _bubbleRequested = 0;
                        AdvancePhase(Phase.LocalRewind, elapsed);
                        break;
                    }

                    if (IsTimedOut(elapsed))
                    {
                        Fail(ref state, timeState.Tick, "local.scale_verify");
                    }
                    break;

                case Phase.LocalRewind:
                    if (!EnsureBubbleCleared(ref state))
                    {
                        break;
                    }

                    if (!IssueBubbleOnce(ref state, TimeBubbleMode.Rewind, -1f))
                    {
                        break;
                    }

                    if (!TryGetProbeMembership(ref state, out var rewindMembership) || rewindMembership.LocalMode != TimeBubbleMode.Rewind)
                    {
                        if (IsTimedOut(elapsed))
                        {
                            Fail(ref state, timeState.Tick, "local.rewind");
                        }
                        break;
                    }

                    if (GetProbeDelta(ref state) < 0f)
                    {
                        EmitStep(ref state, timeState.Tick, "local.rewind", true, GetProbeDelta(ref state), ExpectedLocalRewind);
                        _commandIssued = 0;
                        _bubbleRequested = 0;
                        AdvancePhase(Phase.Complete, elapsed);
                        Complete(ref state, timeState.Tick);
                        break;
                    }

                    if (IsTimedOut(elapsed))
                    {
                        Fail(ref state, timeState.Tick, "local.rewind_verify");
                    }
                    break;
            }

            if (_phase != _lastPhase)
            {
                var scenarioTick = SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenarioState) ? scenarioState.Tick : 0u;
                UnityDebug.Log($"[HeadlessTimeControlProof] phase={_phase} tickTime={tickTimeState.Tick} target={tickTimeState.TargetTick} paused={tickTimeState.IsPaused} playing={tickTimeState.IsPlaying} rewind={rewindState.Mode} scenarioTick={scenarioTick}");
                _lastPhase = _phase;
            }
        }

        private void EnsureProbe(ref SystemState state)
        {
            if (_probeEntity != Entity.Null && state.EntityManager.Exists(_probeEntity))
            {
                return;
            }

            _probeEntity = state.EntityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(TimeBubbleAffectableTag),
                typeof(HeadlessTimeProofProbe));

            state.EntityManager.SetComponentData(_probeEntity,
                LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            state.EntityManager.SetComponentData(_probeEntity, new HeadlessTimeProofProbe());
        }

        private void UpdateProbe(ref SystemState state, in TickTimeState tickTimeState, in TimeState timeState, in RewindState rewindState)
        {
            if (_probeEntity == Entity.Null || !state.EntityManager.Exists(_probeEntity))
            {
                return;
            }

            var membership = state.EntityManager.HasComponent<TimeBubbleMembership>(_probeEntity)
                ? state.EntityManager.GetComponentData<TimeBubbleMembership>(_probeEntity)
                : default;

            var probe = state.EntityManager.GetComponentData<HeadlessTimeProofProbe>(_probeEntity);
            var delta = TimeHelpers.GetEffectiveDelta(tickTimeState, timeState, membership);
            probe.LastDelta = delta;

            if (TimeHelpers.ShouldUpdate(timeState, rewindState, membership))
            {
                probe.UpdateCount++;
                probe.Accumulated += delta;
            }

            state.EntityManager.SetComponentData(_probeEntity, probe);
        }

        private bool TryGetProbeMembership(ref SystemState state, out TimeBubbleMembership membership)
        {
            if (_probeEntity != Entity.Null && state.EntityManager.HasComponent<TimeBubbleMembership>(_probeEntity))
            {
                membership = state.EntityManager.GetComponentData<TimeBubbleMembership>(_probeEntity);
                return true;
            }

            membership = default;
            return false;
        }

        private bool IssueBubbleOnce(ref SystemState state, TimeBubbleMode mode, float scale)
        {
            if (_bubbleRequested != 0)
            {
                return true;
            }

            var requestEntity = state.EntityManager.CreateEntity(typeof(TimeBubbleCreateRequest));
            state.EntityManager.SetComponentData(requestEntity, new TimeBubbleCreateRequest
            {
                Center = float3.zero,
                Radius = ProbeRadius,
                Mode = mode,
                Scale = scale,
                Priority = 200,
                DurationTicks = 0,
                SourceEntity = _probeEntity,
                IsPending = true
            });

            _bubbleRequested = 1;
            _phaseStartTime = SystemAPI.Time.ElapsedTime;
            return true;
        }

        private bool EnsureBubbleCleared(ref SystemState state)
        {
            if (_bubbleRequested != 0)
            {
                return true;
            }

            if (_probeEntity == Entity.Null || !state.EntityManager.Exists(_probeEntity))
            {
                return false;
            }

            foreach (var (bubbleParams, bubbleId, entity) in SystemAPI.Query<RefRO<TimeBubbleParams>, RefRO<TimeBubbleId>>()
                .WithEntityAccess())
            {
                if (bubbleParams.ValueRO.SourceEntity != _probeEntity)
                {
                    continue;
                }

                if (_bubbleRemovalRequested == 0 || _pendingBubbleId != bubbleId.ValueRO.Id)
                {
                    RequestBubbleRemoval(ref state, bubbleId.ValueRO.Id);
                    _pendingBubbleId = bubbleId.ValueRO.Id;
                    _bubbleRemovalRequested = 1;
                }

                return false;
            }

            if (state.EntityManager.HasComponent<TimeBubbleMembership>(_probeEntity))
            {
                return false;
            }

            _bubbleRemovalRequested = 0;
            _pendingBubbleId = 0;
            return true;
        }

        private void RequestBubbleRemoval(ref SystemState state, uint bubbleId)
        {
            var requestEntity = state.EntityManager.CreateEntity(typeof(TimeBubbleRemoveRequest));
            state.EntityManager.SetComponentData(requestEntity, new TimeBubbleRemoveRequest
            {
                BubbleId = bubbleId,
                IsPending = true
            });
        }

        private void CaptureProbeBaseline(ref SystemState state)
        {
            var probe = state.EntityManager.GetComponentData<HeadlessTimeProofProbe>(_probeEntity);
            _baselineAccumulated = probe.Accumulated;
            _baselineUpdates = probe.UpdateCount;
        }

        private bool ProbeStable(ref SystemState state)
        {
            var probe = state.EntityManager.GetComponentData<HeadlessTimeProofProbe>(_probeEntity);
            return probe.UpdateCount == _baselineUpdates &&
                   math.abs(probe.Accumulated - _baselineAccumulated) < 0.0001f;
        }

        private bool ProbeDeltaMatches(ref SystemState state, float scale)
        {
            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var expected = tickTimeState.FixedDeltaTime * scale;
            var probe = state.EntityManager.GetComponentData<HeadlessTimeProofProbe>(_probeEntity);
            return math.abs(probe.LastDelta - expected) < 0.0001f && probe.UpdateCount > 0;
        }

        private float GetProbeDelta(ref SystemState state)
        {
            var probe = state.EntityManager.GetComponentData<HeadlessTimeProofProbe>(_probeEntity);
            return probe.LastDelta;
        }

        private void AdvancePhase(Phase nextPhase, double now)
        {
            _phase = nextPhase;
            _phaseStartTime = now;
            _commandIssued = 0;
            _holdFrames = 0;
        }

        private bool IsTimedOut(double now)
        {
            return _timeoutSeconds > 0f && (now - _phaseStartTime) > _timeoutSeconds;
        }

        private void Complete(ref SystemState state, uint tick)
        {
            _rewindPending = 1;
            _rewindPass = 1;
            _rewindObserved = 1f;
            TryFlushRewindProof(ref state);
            UnityDebug.Log($"[HeadlessTimeControlProof] PASS tick={tick}");
            SetProofResult(ref state, tick, 1);
            ExitIfRequested(ref state, tick, 0);
        }

        private void Fail(ref SystemState state, uint tick, string step)
        {
            _phase = Phase.Failed;
            _rewindPending = 1;
            _rewindPass = 0;
            _rewindObserved = 0f;
            TryFlushRewindProof(ref state);
            UnityDebug.LogError($"[HeadlessTimeControlProof] FAIL tick={tick} step={step}");
            SetProofResult(ref state, tick, 2);
            EmitStep(ref state, tick, step, false, 0f, default);
            ExitIfRequested(ref state, tick, 2);
        }

        private void EmitStep(ref SystemState state, uint tick, string step, bool pass, float observed, in FixedString32Bytes expected)
        {
            var timeoutTicks = ResolveTimeoutTicks();
            TelemetryLoopProofUtility.Emit(state.EntityManager, tick, TelemetryLoopIds.Time, pass, observed, expected, timeoutTicks, step: new FixedString32Bytes(step));
        }

        private uint ResolveTimeoutTicks()
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.FixedDeltaTime <= 0f)
            {
                return 0;
            }

            var ticks = (uint)math.ceil(_timeoutSeconds / timeState.FixedDeltaTime);
            return ticks == 0 ? 1u : ticks;
        }

        private void EnqueueCommand(ref SystemState state, TimeControlCommandType type, int ticks = 0)
        {
            var rewindEntity = SystemAPI.GetSingletonEntity<RewindState>();
            if (!state.EntityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                state.EntityManager.AddBuffer<TimeControlCommand>(rewindEntity);
            }

            var buffer = state.EntityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            buffer.Add(new TimeControlCommand
            {
                Type = type,
                UintParam = ticks > 0 ? (uint)ticks : 0
            });
        }

        private void EnqueueCommand(ref SystemState state, TimeControlCommandType type, float speed)
        {
            var rewindEntity = SystemAPI.GetSingletonEntity<RewindState>();
            if (!state.EntityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                state.EntityManager.AddBuffer<TimeControlCommand>(rewindEntity);
            }

            var buffer = state.EntityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            buffer.Add(new TimeControlCommand
            {
                Type = type,
                FloatParam = speed
            });
        }

        private void EnsureRewindSubject(ref SystemState state)
        {
            if (_rewindSubjectRegistered != 0)
            {
                return;
            }

            if (HeadlessRewindProofUtility.TryEnsureSubject(state.EntityManager, RewindProofId, RewindRequiredMask))
            {
                _rewindSubjectRegistered = 1;
            }
        }

        private void TryFlushRewindProof(ref SystemState state)
        {
            if (_rewindPending == 0)
            {
                return;
            }

            if (!HeadlessRewindProofUtility.TryGetState(state.EntityManager, out var rewindProof) || rewindProof.SawRecord == 0)
            {
                return;
            }

            HeadlessRewindProofUtility.TryMarkResult(state.EntityManager, RewindProofId, _rewindPass != 0, _rewindObserved, ExpectedRewindSubject, RewindRequiredMask);
            _rewindPending = 0;
        }

        private void EnsureProofState(ref SystemState state)
        {
            if (_proofEntity != Entity.Null && state.EntityManager.Exists(_proofEntity))
            {
                return;
            }

            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<HeadlessTimeControlProofState>());
            if (!query.IsEmptyIgnoreFilter)
            {
                _proofEntity = query.GetSingletonEntity();
                return;
            }

            _proofEntity = state.EntityManager.CreateEntity(typeof(HeadlessTimeControlProofState));
            state.EntityManager.SetComponentData(_proofEntity, new HeadlessTimeControlProofState());
        }

        private void SetProofResult(ref SystemState state, uint tick, byte result)
        {
            EnsureProofState(ref state);
            if (_proofEntity == Entity.Null || !state.EntityManager.Exists(_proofEntity))
            {
                return;
            }

            state.EntityManager.SetComponentData(_proofEntity, new HeadlessTimeControlProofState
            {
                Result = result,
                Tick = tick
            });
        }

        private static bool ResolveEnabled()
        {
            if (TryReadEnvFlag(EnabledEnv, out var enabled))
            {
                return enabled;
            }

            return RuntimeMode.IsHeadless;
        }

        private static bool TryReadEnvFlag(string key, out bool value)
        {
            var env = SystemEnv.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(env))
            {
                value = false;
                return false;
            }

            value = string.Equals(env, "1", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(env, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(env, "yes", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        private static float ReadEnvFloat(string key, float fallback)
        {
            var env = SystemEnv.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(env))
            {
                return fallback;
            }

            return float.TryParse(env, out var parsed) ? parsed : fallback;
        }

        private static uint ReadEnvUInt(string key, uint fallback)
        {
            var env = SystemEnv.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(env))
            {
                return fallback;
            }

            return uint.TryParse(env, out var parsed) ? parsed : fallback;
        }

        private static bool TryReadEnvUInt(string key, out uint value)
        {
            var env = SystemEnv.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(env))
            {
                value = 0;
                return false;
            }

            if (uint.TryParse(env, out var parsed))
            {
                value = parsed;
                return true;
            }

            value = 0;
            return false;
        }

        private static void ExitIfRequested(ref SystemState state, uint tick, int exitCode)
        {
            if (!string.Equals(SystemEnv.GetEnvironmentVariable(ExitOnResultEnv), "1", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            HeadlessExitUtility.Request(state.EntityManager, tick, exitCode);
        }
    }
}
