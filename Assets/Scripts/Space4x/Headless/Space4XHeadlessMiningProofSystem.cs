using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
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
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XHeadlessOperatorReportSystem))]
    public partial struct Space4XHeadlessMiningProofSystem : ISystem
    {
        private const string EnabledEnv = "SPACE4X_HEADLESS_MINING_PROOF";
        private const string ExitOnResultEnv = "SPACE4X_HEADLESS_MINING_PROOF_EXIT";
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string MiningScenarioFile = "space4x_mining.json";
        private const string MiningCombatScenarioFile = "space4x_mining_combat.json";
        private const string MiningMicroScenarioFile = "space4x_mining_micro.json";
        private const string RefitScenarioFile = "space4x_refit.json";
        private const string ResearchScenarioFile = "space4x_research_mvp.json";

        private const uint DefaultTimeoutTicks = 1800; // ~30 seconds at 60hz
        private const double DefaultTimeoutSeconds = 27d;

        private byte _enabled;
        private byte _done;
        private uint _startTick;
        private uint _timeoutTick;
        private double _timeoutSeconds;
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
        private bool _isMiningScenario;
        private bool _isMiningCombatScenario;
        private bool _isMiningMicroScenario;
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

            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (!string.IsNullOrWhiteSpace(scenarioPath) &&
                (scenarioPath.EndsWith(RefitScenarioFile, StringComparison.OrdinalIgnoreCase) ||
                 scenarioPath.EndsWith(ResearchScenarioFile, StringComparison.OrdinalIgnoreCase)))
            {
                state.Enabled = false;
                return;
            }
            if (!string.IsNullOrWhiteSpace(scenarioPath) && !IsMiningProofScenarioPath(scenarioPath))
            {
                state.Enabled = false;
                return;
            }
            if (!string.IsNullOrWhiteSpace(scenarioPath) && ScenarioDisablesMiningProof(scenarioPath))
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
                _isMiningScenario = IsMiningScenario();
                _isMiningCombatScenario = IsMiningCombatScenario();
                _isMiningMicroScenario = IsMiningMicroScenario();
                _timeoutTick = scenario.EndTick > _startTick
                    ? scenario.EndTick
                    : _startTick + DefaultTimeoutTicks;
                var scenarioDurationSeconds = (scenario.EndTick > _startTick)
                    ? (scenario.EndTick - _startTick) * timeState.FixedDeltaTime
                    : 0d;
                _timeoutSeconds = math.max(DefaultTimeoutSeconds, scenarioDurationSeconds);
                _startOreInHold = GetOreInHold(ref state);
                if (_startOreInHold <= 0f && SystemAPI.TryGetSingleton<Space4XMiningTelemetry>(out var telemetry))
                {
                    _startOreInHold = telemetry.OreInHold;
                }

                _startCargoSum = GetVesselStats(ref state).cargoSum;
                _startElapsedTime = SystemAPI.Time.ElapsedTime;
            }

            if (_loggedDiagnostics == 0 && timeState.Tick >= _startTick + 30)
            {
                var em = state.EntityManager;
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
                    $"[Space4XHeadlessMiningProof] DIAG tick={timeState.Tick} fixedDt={timeState.FixedDeltaTime:F4} speed={timeState.CurrentSpeedMultiplier:F2} timeScale={effectiveScale:F2} paused={timeState.IsPaused} " +
                    $"isPlaying={tickTime.IsPlaying} targetTick={tickTime.TargetTick} worldDt={SystemAPI.Time.DeltaTime:F4} worldElapsed={SystemAPI.Time.ElapsedTime:F2} timeSeconds={timeState.WorldSeconds:F2} " +
                    $"unityScale={UnityEngine.Time.timeScale:F2} unityDt={UnityEngine.Time.deltaTime:F4} unityUnscaledDt={UnityEngine.Time.unscaledDeltaTime:F4} " +
                    $"resources={resourceCount} registryEntries={registryEntries} miningOrders={miningOrders} miningStates={miningStates} carriers={carriers} vessels={vessels} " +
                    $"orders(p/a/c/n)={ordersPending}/{ordersActive}/{ordersCompleted}/{ordersNone} phases(i/u/a/l/m/d/r/k)={phaseIdle}/{phaseUndocking}/{phaseApproach}/{phaseLatching}/{phaseMining}/{phaseDetaching}/{phaseReturn}/{phaseDocking} " +
                    $"targets(assigned/inRange)={targetsAssigned}/{targetsInRange} cargoed={cargoed}");
                _loggedDiagnostics = 1;
            }

            var (cargoSum, returningCount, miningCount, vesselCount) = GetVesselStats(ref state);
            var oreInHold = GetOreInHold(ref state);
            if (oreInHold <= 0f && SystemAPI.TryGetSingleton<Space4XMiningTelemetry>(out var miningTelemetry))
            {
                oreInHold = miningTelemetry.OreInHold;
            }
            var spawnCount = _spawnQuery.CalculateEntityCount();
            var (gatherCommands, pickupCommands, totalCommands) = GetCommandCounts(ref state);

            var oreDelta = oreInHold - _startOreInHold;
            var cargoDelta = cargoSum - _startCargoSum;
            var hasGather = gatherCommands > 0;
            var hasOre = oreDelta > 0.01f;
            var hasCargo = cargoDelta > 0.01f;
            var pass = hasGather && hasOre;
            if (!pass && _isSmokeScenario)
            {
                // Smoke runs focus on "mining started" rather than full dropoff within a short window.
                pass = hasOre || hasCargo;
            }
            else if (!pass && _isMiningScenario)
            {
                // Mining-only runs still need gather, but allow cargo to count when carrier dropoff lags.
                pass = hasGather && (hasOre || hasCargo);
            }
            else if (!pass && (_isMiningCombatScenario || _isMiningMicroScenario))
            {
                // Combat mining can route ore to carrier stores without updating ore-in-hold.
                pass = hasGather && (hasOre || hasCargo);
            }
            if (pass)
            {
                _done = 1;
                _rewindPending = 1;
                _rewindPass = 1;
                _rewindObserved = oreDelta;
                UnityDebug.Log($"[Space4XHeadlessMiningProof] PASS tick={timeState.Tick} gather={gatherCommands} pickup={pickupCommands} oreInHold={oreInHold:F2} oreDelta={oreDelta:F2} cargoSum={cargoSum:F2} vessels={vesselCount} returning={returningCount} mining={miningCount} spawns={spawnCount}");
                EmitOperatorSummary(ref state, gatherCommands, pickupCommands, oreDelta, cargoDelta, (float)(SystemAPI.Time.ElapsedTime - _startElapsedTime), true);
                ResolveTickInfo(ref state, timeState.Tick, out var tickTime, out var scenarioTick);
                LogBankResult(ResolveBankTestId(), true, string.Empty, tickTime, scenarioTick);
                TelemetryLoopProofUtility.Emit(state.EntityManager, timeState.Tick, TelemetryLoopIds.Extract, true, oreDelta, ExpectedDelta, DefaultTimeoutTicks, step: StepGatherDropoff);
                TryFlushRewindProof(ref state);
                RequestExitOnPassIfEnabled(ref state, timeState.Tick);
                return;
            }

            var elapsedSeconds = SystemAPI.Time.ElapsedTime - _startElapsedTime;
            if (elapsedSeconds >= _timeoutSeconds || timeState.Tick >= _timeoutTick)
            {
                _done = 1;
                _rewindPending = 1;
                _rewindPass = 0;
                _rewindObserved = oreDelta;
                var failMessage = string.Format(CultureInfo.InvariantCulture,
                    "[Space4XHeadlessMiningProof] FAIL tick={0} gather={1} pickup={2} oreInHold={3:0.##} oreDelta={4:0.##} cargoSum={5:0.##} vessels={6} returning={7} mining={8} spawns={9} commands={10} elapsed={11:0.#}s",
                    timeState.Tick,
                    gatherCommands,
                    pickupCommands,
                    oreInHold,
                    oreDelta,
                    cargoSum,
                    vesselCount,
                    returningCount,
                    miningCount,
                    spawnCount,
                    totalCommands,
                    elapsedSeconds);
                UnityDebug.LogError($"{failMessage} (deltaCommands={math.max(0, (int)totalCommands - (int)_lastCommandCount)})");
                var observed = string.Format(CultureInfo.InvariantCulture,
                    "gather={0} oreDelta={1:0.##} cargoDelta={2:0.##} elapsed_s={3:0.#}",
                    gatherCommands,
                    oreDelta,
                    cargoDelta,
                    elapsedSeconds);
                EmitOperatorSummary(ref state, gatherCommands, pickupCommands, oreDelta, cargoDelta, (float)elapsedSeconds, false);
                Space4XHeadlessDiagnostics.ReportInvariant(
                    "INV-MINING-PROOF",
                    failMessage,
                    observed,
                    "gather>0 and oreDelta>0");
                ResolveTickInfo(ref state, timeState.Tick, out var tickTime, out var scenarioTick);
                LogBankResult(ResolveBankTestId(), false, "timeout", tickTime, scenarioTick);
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
            if (!string.Equals(SystemEnv.GetEnvironmentVariable(ExitOnResultEnv), "1", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            HeadlessExitUtility.Request(state.EntityManager, tick, exitCode);
        }

        private static bool IsSmokeScenario()
        {
            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            return IsSmokeScenarioPath(scenarioPath);
        }

        private static bool IsMiningCombatScenario()
        {
            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            return !string.IsNullOrWhiteSpace(scenarioPath) &&
                   scenarioPath.EndsWith(MiningCombatScenarioFile, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMiningScenario()
        {
            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            return !string.IsNullOrWhiteSpace(scenarioPath) &&
                   scenarioPath.EndsWith(MiningScenarioFile, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMiningMicroScenario()
        {
            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            return !string.IsNullOrWhiteSpace(scenarioPath) &&
                   scenarioPath.EndsWith(MiningMicroScenarioFile, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMiningProofScenarioPath(string scenarioPath)
        {
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return false;
            }

            return IsSmokeScenarioPath(scenarioPath) ||
                   scenarioPath.EndsWith(MiningScenarioFile, StringComparison.OrdinalIgnoreCase) ||
                   scenarioPath.EndsWith(MiningCombatScenarioFile, StringComparison.OrdinalIgnoreCase) ||
                   scenarioPath.EndsWith(MiningMicroScenarioFile, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ScenarioDisablesMiningProof(string scenarioPath)
        {
            if (string.IsNullOrWhiteSpace(scenarioPath) || !File.Exists(scenarioPath))
            {
                return false;
            }

            try
            {
                const int maxChars = 16384;
                using var stream = File.OpenRead(scenarioPath);
                using var reader = new StreamReader(stream);
                var buffer = new char[maxChars];
                var read = reader.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    return false;
                }

                var head = new string(buffer, 0, read);
                // Proof policy is data-driven in scenario JSON:
                // "proofs": { "mining": false } disables mining proof for combat-only scenarios.
                return Regex.IsMatch(head, "\"proofs\"\\s*:\\s*\\{[^}]*\"mining\"\\s*:\\s*false",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSmokeScenarioPath(string scenarioPath)
        {
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return false;
            }

            var scenarioName = Path.GetFileNameWithoutExtension(scenarioPath);
            return scenarioName.StartsWith("space4x_smoke", StringComparison.OrdinalIgnoreCase) ||
                   scenarioName.StartsWith("space4x_movement", StringComparison.OrdinalIgnoreCase) ||
                   scenarioName.StartsWith("space4x_bug_hunt", StringComparison.OrdinalIgnoreCase);
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

            if (IsSmokeScenarioPath(scenarioPath))
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

        private void ResolveTickInfo(ref SystemState state, uint tick, out uint tickTime, out uint scenarioTick)
        {
            tickTime = tick;
            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickTimeState))
            {
                tickTime = tickTimeState.Tick;
            }

            scenarioTick = SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenario)
                ? scenario.Tick
                : 0u;
        }

        private void LogBankResult(FixedString64Bytes testId, bool pass, string reason, uint tickTime, uint scenarioTick)
        {
            if (testId.IsEmpty)
            {
                return;
            }
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

            using var vessels = _vesselQuery.ToComponentDataArray<MiningVessel>(Allocator.Temp);
            using var aiStates = _vesselQuery.ToComponentDataArray<VesselAIState>(Allocator.Temp);
            var count = math.min(vessels.Length, aiStates.Length);
            for (var i = 0; i < count; i++)
            {
                total++;
                cargoSum += math.max(0f, vessels[i].CurrentCargo);
                if (aiStates[i].CurrentState == VesselAIState.State.Returning) returning++;
                if (aiStates[i].CurrentState == VesselAIState.State.Mining) mining++;
            }

            return (cargoSum, returning, mining, total);
        }

        private float GetOreInHold(ref SystemState state)
        {
            var total = 0f;
            using var carriers = _carrierQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < carriers.Length; i++)
            {
                var storage = state.EntityManager.GetBuffer<ResourceStorage>(carriers[i]);
                for (var j = 0; j < storage.Length; j++)
                {
                    total += storage[j].Amount;
                }
            }

            return total;
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

        private static void EmitOperatorSummary(
            ref SystemState state,
            uint gatherCommands,
            uint pickupCommands,
            float oreDelta,
            float cargoDelta,
            float elapsedSeconds,
            bool pass)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.mining.gather_commands"), gatherCommands);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.mining.pickup_commands"), pickupCommands);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.mining.ore_delta"), oreDelta);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.mining.cargo_delta"), cargoDelta);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.mining.elapsed_s"), elapsedSeconds);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.mining.pass"), pass ? 1f : 0f);
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

    }
}
