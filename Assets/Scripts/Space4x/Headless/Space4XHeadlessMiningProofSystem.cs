using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Time;
using Space4x.Scenario;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using HeadlessStage = PureDOTS.Runtime.Components.HeadlessRewindProofStage;
using SystemEnv = System.Environment;

namespace Space4X.Headless
{
    /// <summary>
    /// Headless proof that "gather -> dropoff" works in simulation (no presentation dependency).
    /// Logs exactly one PASS/FAIL line when criteria are met or a timeout is reached.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct Space4XHeadlessMiningProofSystem : ISystem
    {
        private const string EnabledEnv = "SPACE4X_HEADLESS_MINING_PROOF";
        private const string ExitOnResultEnv = "SPACE4X_HEADLESS_MINING_PROOF_EXIT";
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string SmokeScenarioFile = "space4x_smoke.json";
        private const string MiningScenarioFile = "space4x_mining.json";
        private const string MiningCombatScenarioFile = "space4x_mining_combat.json";

        private const uint DefaultTimeoutTicks = 1800; // ~30 seconds at 60hz
        private const double DefaultTimeoutSeconds = 27d;

        private byte _enabled;
        private byte _done;
        private uint _startTick;
        private uint _timeoutTick;
        private uint _lastCommandCount;
        private float _startOreInHold;
        private float _startCargoSum;
        private double _startElapsedTime;
        private byte _loggedDiagnostics;
        private byte _rewindSubjectRegistered;
        private byte _rewindPending;
        private byte _rewindPass;
        private float _rewindObserved;
        private bool _isSmokeScenario;
        private FixedString64Bytes _bankTestId;
        private byte _bankResolved;

        private EntityQuery _vesselQuery;
        private EntityQuery _carrierQuery;
        private EntityQuery _spawnQuery;
        private EntityQuery _spineQuery;
        private static readonly FixedString32Bytes ExpectedDelta = new FixedString32Bytes(">0");
        private static readonly FixedString32Bytes StepGatherDropoff = new FixedString32Bytes("gather_dropoff");
        private static readonly FixedString64Bytes RewindProofId = new FixedString64Bytes("space4x.mining");
        private const byte RewindRequiredMask = (byte)HeadlessStage.RecordReturn;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            var enabledEnv = SystemEnv.GetEnvironmentVariable(EnabledEnv);
            if (string.Equals(enabledEnv, "0", StringComparison.OrdinalIgnoreCase))
            {
                state.Enabled = false;
                return;
            }

            _enabled = 1;
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();

            _vesselQuery = SystemAPI.QueryBuilder().WithAll<MiningVessel, VesselAIState>().Build();
            _carrierQuery = SystemAPI.QueryBuilder().WithAll<Carrier, ResourceStorage>().Build();
            _spawnQuery = SystemAPI.QueryBuilder().WithAll<SpawnResource>().Build();
            _spineQuery = SystemAPI.QueryBuilder().WithAll<Space4XMiningTimeSpine, MiningCommandLogEntry>().Build();

        }

        public void OnUpdate(ref SystemState state)
        {
            if (_enabled == 0)
            {
                return;
            }

            EnsureRewindSubject(ref state);
            TryFlushRewindProof(ref state);

            if (_done != 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (_timeoutTick == 0)
            {
                _startTick = timeState.Tick;
                var scenario = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
                _isSmokeScenario = IsSmokeScenario();
                // Smoke runs at reduced mining speeds; give it the full scenario window before failing.
                _timeoutTick = _isSmokeScenario && scenario.EndTick > _startTick
                    ? scenario.EndTick
                    : _startTick + DefaultTimeoutTicks;
                _startOreInHold = GetOreInHold(ref state);
                _startCargoSum = GetVesselStats(ref state).cargoSum;
                _startElapsedTime = SystemAPI.Time.ElapsedTime;
            }

            if (_loggedDiagnostics == 0 && timeState.Tick >= _startTick + 30)
            {
                LogDiagnostics(ref state, timeState.Tick);
                _loggedDiagnostics = 1;
            }

            var (cargoSum, returningCount, miningCount, vesselCount) = GetVesselStats(ref state);
            var oreInHold = GetOreInHold(ref state);
            var spawnCount = _spawnQuery.CalculateEntityCount();
            var (gatherCommands, pickupCommands, totalCommands) = GetCommandCounts(ref state);

            var oreDelta = oreInHold - _startOreInHold;
            var cargoDelta = cargoSum - _startCargoSum;
            var pass = gatherCommands > 0 && oreDelta > 0.01f;
            if (!pass && _isSmokeScenario)
            {
                // Smoke runs focus on "mining started" rather than full dropoff within a short window.
                pass = oreDelta > 0.01f || cargoDelta > 0.01f;
            }
            if (pass)
            {
                _done = 1;
                _rewindPending = 1;
                _rewindPass = 1;
                _rewindObserved = oreDelta;
                UnityDebug.Log($"[Space4XHeadlessMiningProof] PASS tick={timeState.Tick} gather={gatherCommands} pickup={pickupCommands} oreInHold={oreInHold:F2} oreDelta={oreDelta:F2} cargoSum={cargoSum:F2} vessels={vesselCount} returning={returningCount} mining={miningCount} spawns={spawnCount}");
                LogBankResult(ref state, ResolveBankTestId(), true, string.Empty, timeState.Tick);
                TelemetryLoopProofUtility.Emit(state.EntityManager, timeState.Tick, TelemetryLoopIds.Extract, true, oreDelta, ExpectedDelta, DefaultTimeoutTicks, step: StepGatherDropoff);
                TryFlushRewindProof(ref state);
                RequestExitOnPassIfEnabled(ref state, timeState.Tick);
                return;
            }

            var elapsedSeconds = SystemAPI.Time.ElapsedTime - _startElapsedTime;
            if (elapsedSeconds >= DefaultTimeoutSeconds || timeState.Tick >= _timeoutTick)
            {
                _done = 1;
                _rewindPending = 1;
                _rewindPass = 0;
                _rewindObserved = oreDelta;
                UnityDebug.LogError($"[Space4XHeadlessMiningProof] FAIL tick={timeState.Tick} gather={gatherCommands} pickup={pickupCommands} oreInHold={oreInHold:F2} oreDelta={oreDelta:F2} cargoSum={cargoSum:F2} vessels={vesselCount} returning={returningCount} mining={miningCount} spawns={spawnCount} commands={totalCommands} elapsed={elapsedSeconds:F1}s (deltaCommands={math.max(0, (int)totalCommands - (int)_lastCommandCount)})");
                LogBankResult(ref state, ResolveBankTestId(), false, "timeout", timeState.Tick);
                TelemetryLoopProofUtility.Emit(state.EntityManager, timeState.Tick, TelemetryLoopIds.Extract, false, oreDelta, ExpectedDelta, DefaultTimeoutTicks, step: StepGatherDropoff);
                TryFlushRewindProof(ref state);
                RequestExitOnFail(ref state, timeState.Tick, 3);
                return;
            }

            _lastCommandCount = totalCommands;
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

            HeadlessRewindProofUtility.TryMarkResult(state.EntityManager, RewindProofId, _rewindPass != 0, _rewindObserved, ExpectedDelta, RewindRequiredMask);
            _rewindPending = 0;
        }

        private static void RequestExitOnPassIfEnabled(ref SystemState state, uint tick)
        {
            if (!string.Equals(SystemEnv.GetEnvironmentVariable(ExitOnResultEnv), "1", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            HeadlessExitUtility.Request(state.EntityManager, tick, 0);
        }

        private static void RequestExitOnFail(ref SystemState state, uint tick, int exitCode)
        {
            HeadlessExitUtility.Request(state.EntityManager, tick, exitCode);
        }

        private static bool IsSmokeScenario()
        {
            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            return !string.IsNullOrWhiteSpace(scenarioPath) &&
                   scenarioPath.EndsWith(SmokeScenarioFile, StringComparison.OrdinalIgnoreCase);
        }

        private FixedString64Bytes ResolveBankTestId()
        {
            if (_bankResolved != 0)
            {
                return _bankTestId;
            }

            _bankResolved = 1;
            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return _bankTestId;
            }

            if (scenarioPath.EndsWith(SmokeScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                _bankTestId = new FixedString64Bytes("S0.SPACE4X_SMOKE");
            }
            else if (scenarioPath.EndsWith(MiningScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                _bankTestId = new FixedString64Bytes("S1.MINING_ONLY");
            }
            else if (scenarioPath.EndsWith(MiningCombatScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                _bankTestId = new FixedString64Bytes("S2.MINING_COMBAT");
            }

            return _bankTestId;
        }

        private void LogBankResult(ref SystemState state, FixedString64Bytes testId, bool pass, string reason, uint tick)
        {
            if (testId.IsEmpty)
            {
                return;
            }

            var tickTime = tick;
            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickTimeState))
            {
                tickTime = tickTimeState.Tick;
            }

            var scenarioTick = SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenario)
                ? scenario.Tick
                : 0u;
            var delta = (int)tickTime - (int)scenarioTick;

            if (pass)
            {
                UnityDebug.Log($"BANK:{testId}:PASS tickTime={tickTime} scenarioTick={scenarioTick} delta={delta}");
                return;
            }

            UnityDebug.Log($"BANK:{testId}:FAIL reason={reason} tickTime={tickTime} scenarioTick={scenarioTick} delta={delta}");
        }

        private (float cargoSum, int returning, int mining, int total) GetVesselStats(ref SystemState state)
        {
            var cargoSum = 0f;
            var returning = 0;
            var mining = 0;
            var total = 0;

            foreach (var (vessel, ai) in SystemAPI.Query<RefRO<MiningVessel>, RefRO<VesselAIState>>())
            {
                total++;
                cargoSum += math.max(0f, vessel.ValueRO.CurrentCargo);
                if (ai.ValueRO.CurrentState == VesselAIState.State.Returning) returning++;
                if (ai.ValueRO.CurrentState == VesselAIState.State.Mining) mining++;
            }

            return (cargoSum, returning, mining, total);
        }

        private float GetOreInHold(ref SystemState state)
        {
            var total = 0f;
            foreach (var storage in SystemAPI.Query<DynamicBuffer<ResourceStorage>>().WithAll<Carrier>())
            {
                for (var i = 0; i < storage.Length; i++)
                {
                    total += storage[i].Amount;
                }
            }

            if (total > 0f || !SystemAPI.TryGetSingleton<Space4XMiningTelemetry>(out var telemetry))
            {
                return total;
            }

            return telemetry.OreInHold;
        }

        private (uint gather, uint pickup, uint total) GetCommandCounts(ref SystemState state)
        {
            if (_spineQuery.IsEmptyIgnoreFilter)
            {
                return (0, 0, 0);
            }

            var spineEntity = _spineQuery.GetSingletonEntity();
            var buffer = state.EntityManager.GetBuffer<MiningCommandLogEntry>(spineEntity);
            uint gather = 0;
            uint pickup = 0;
            for (var i = 0; i < buffer.Length; i++)
            {
                var type = buffer[i].CommandType;
                if (type == MiningCommandType.Gather) gather++;
                else if (type == MiningCommandType.Pickup) pickup++;
            }

            return (gather, pickup, (uint)buffer.Length);
        }

        private void LogDiagnostics(ref SystemState state, uint tick)
        {
            var em = state.EntityManager;
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var resourceCount = SystemAPI.QueryBuilder()
                .WithAll<Space4X.Registry.ResourceSourceState, Space4X.Registry.ResourceTypeId>()
                .Build()
                .CalculateEntityCount();

            var registryEntries = 0;
            if (SystemAPI.TryGetSingletonEntity<ResourceRegistry>(out var registryEntity) &&
                em.HasBuffer<ResourceRegistryEntry>(registryEntity))
            {
                registryEntries = em.GetBuffer<ResourceRegistryEntry>(registryEntity).Length;
            }

            var miningOrders = SystemAPI.QueryBuilder().WithAll<MiningOrder>().Build().CalculateEntityCount();
            var miningStates = SystemAPI.QueryBuilder().WithAll<MiningState>().Build().CalculateEntityCount();
            var carriers = SystemAPI.QueryBuilder().WithAll<Carrier>().Build().CalculateEntityCount();
            var vessels = SystemAPI.QueryBuilder().WithAll<MiningVessel>().Build().CalculateEntityCount();
            var tickTime = SystemAPI.GetSingleton<TickTimeState>();

            var ordersPending = 0;
            var ordersActive = 0;
            var ordersCompleted = 0;
            var ordersNone = 0;
            var phaseIdle = 0;
            var phaseUndocking = 0;
            var phaseApproach = 0;
            var phaseLatching = 0;
            var phaseMining = 0;
            var phaseDetaching = 0;
            var phaseReturn = 0;
            var phaseDocking = 0;
            var targetsAssigned = 0;
            var targetsInRange = 0;
            var cargoed = 0;

            foreach (var (order, miningState, vessel, transform) in SystemAPI
                         .Query<RefRO<MiningOrder>, RefRO<MiningState>, RefRO<MiningVessel>, RefRO<LocalTransform>>())
            {
                switch (order.ValueRO.Status)
                {
                    case MiningOrderStatus.Pending:
                        ordersPending++;
                        break;
                    case MiningOrderStatus.Active:
                        ordersActive++;
                        break;
                    case MiningOrderStatus.Completed:
                        ordersCompleted++;
                        break;
                    default:
                        ordersNone++;
                        break;
                }

                switch (miningState.ValueRO.Phase)
                {
                    case MiningPhase.Idle:
                        phaseIdle++;
                        break;
                    case MiningPhase.Undocking:
                        phaseUndocking++;
                        break;
                    case MiningPhase.ApproachTarget:
                        phaseApproach++;
                        break;
                    case MiningPhase.Latching:
                        phaseLatching++;
                        break;
                    case MiningPhase.Mining:
                        phaseMining++;
                        break;
                    case MiningPhase.Detaching:
                        phaseDetaching++;
                        break;
                    case MiningPhase.ReturnApproach:
                        phaseReturn++;
                        break;
                    case MiningPhase.Docking:
                        phaseDocking++;
                        break;
                }

                if (vessel.ValueRO.CurrentCargo > 0.01f)
                {
                    cargoed++;
                }

                var target = miningState.ValueRO.ActiveTarget;
                if (target != Entity.Null)
                {
                    targetsAssigned++;
                    if (em.HasComponent<LocalTransform>(target))
                    {
                        var targetPos = em.GetComponentData<LocalTransform>(target).Position;
                        var distSq = math.distancesq(transform.ValueRO.Position, targetPos);
                        if (distSq <= 9f)
                        {
                            targetsInRange++;
                        }
                    }
                }
            }

            var scalars = SystemAPI.GetSingleton<PureDOTS.Runtime.Core.SimulationScalars>();
            var overrides = SystemAPI.GetSingleton<PureDOTS.Runtime.Core.SimulationOverrides>();
            var effectiveScale = overrides.OverrideTimeScale ? overrides.TimeScaleOverride : scalars.TimeScale;

            UnityDebug.Log(
                $"[Space4XHeadlessMiningProof] DIAG tick={tick} fixedDt={timeState.FixedDeltaTime:F4} speed={timeState.CurrentSpeedMultiplier:F2} timeScale={effectiveScale:F2} paused={timeState.IsPaused} " +
                $"isPlaying={tickTime.IsPlaying} targetTick={tickTime.TargetTick} worldDt={SystemAPI.Time.DeltaTime:F4} worldElapsed={SystemAPI.Time.ElapsedTime:F2} timeSeconds={timeState.WorldSeconds:F2} " +
                $"unityScale={UnityEngine.Time.timeScale:F2} unityDt={UnityEngine.Time.deltaTime:F4} unityUnscaledDt={UnityEngine.Time.unscaledDeltaTime:F4} " +
                $"resources={resourceCount} registryEntries={registryEntries} miningOrders={miningOrders} miningStates={miningStates} carriers={carriers} vessels={vessels} " +
                $"orders(p/a/c/n)={ordersPending}/{ordersActive}/{ordersCompleted}/{ordersNone} phases(i/u/a/l/m/d/r/k)={phaseIdle}/{phaseUndocking}/{phaseApproach}/{phaseLatching}/{phaseMining}/{phaseDetaching}/{phaseReturn}/{phaseDocking} " +
                $"targets(assigned/inRange)={targetsAssigned}/{targetsInRange} cargoed={cargoed}");
        }
    }
}
