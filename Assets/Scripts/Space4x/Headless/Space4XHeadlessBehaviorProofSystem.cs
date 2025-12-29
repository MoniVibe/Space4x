using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Time;
using Space4X.Registry;
using Space4X.Runtime;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Headless
{
    /// <summary>
    /// Headless proofs for attack, patrol, escort, and docking loops.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CarrierPatrolSystem))]
    [UpdateAfter(typeof(Space4XStrikeCraftSystem))]
    public partial struct Space4XHeadlessBehaviorProofSystem : ISystem
    {
        private EntityQuery _strikeCraftQuery;
        private EntityQuery _wingDirectiveQuery;
        private EntityQuery _escortQuery;
        private EntityQuery _patrolQuery;
        private EntityQuery _miningQuery;

        private bool _attackExpected;
        private bool _attackStarted;
        private bool _attackPassed;
        private Entity _attackCraft;
        private bool _capAttackSeen;
        private bool _capAttackPassed;
        private uint _capSeenTick;

        private bool _patrolExpected;
        private bool _patrolPassed;
        private bool _escortExpected;
        private bool _escortPassed;

        private bool _dockingExpected;
        private bool _dockingSeen;
        private bool _dockingUndocked;
        private bool _dockingPassed;
        private Entity _dockEntity;
        private int _dockingPeakDocked;

        private bool _wingDirectiveExpected;
        private bool _wingDirectivePassed;

        private bool _profileActionExpected;
        private bool _profileActionPassed;

        private bool _reportedEnd;

        private static readonly FixedString32Bytes ExpectedComplete = new FixedString32Bytes("complete");
        private static readonly FixedString32Bytes StepPatrol = new FixedString32Bytes("patrol");
        private static readonly FixedString32Bytes StepEscort = new FixedString32Bytes("escort");
        private static readonly FixedString32Bytes StepAttackRun = new FixedString32Bytes("attack_run");
        private static readonly FixedString32Bytes StepCapToAttack = new FixedString32Bytes("cap_to_attack");
        private static readonly FixedString32Bytes StepDocking = new FixedString32Bytes("docking");
        private static readonly FixedString32Bytes StepWingDirective = new FixedString32Bytes("wing_directive");
        private static readonly FixedString32Bytes StepProfileAction = new FixedString32Bytes("profile_action");
        private static readonly FixedString64Bytes RewindPatrolId = new FixedString64Bytes("space4x.patrol");
        private static readonly FixedString64Bytes RewindEscortId = new FixedString64Bytes("space4x.escort");
        private static readonly FixedString64Bytes RewindAttackId = new FixedString64Bytes("space4x.attack");
        private static readonly FixedString64Bytes RewindDockingId = new FixedString64Bytes("space4x.docking");
        private const byte RewindRequiredMask = (byte)HeadlessRewindProofStage.RecordReturn;
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string SmokeScenarioFile = "space4x_smoke.json";
        private const string MiningScenarioFile = "space4x_mining.json";
        private const string MiningCombatScenarioFile = "space4x_mining_combat.json";
        private const string BehaviorProofEnv = "SPACE4X_HEADLESS_BEHAVIOR_PROOF";
        private const string BehaviorProofExitEnv = "SPACE4X_HEADLESS_BEHAVIOR_PROOF_EXIT";

        private byte _rewindPatrolRegistered;
        private byte _rewindEscortRegistered;
        private byte _rewindAttackRegistered;
        private byte _rewindDockingRegistered;
        private byte _rewindPatrolPending;
        private byte _rewindEscortPending;
        private byte _rewindAttackPending;
        private byte _rewindDockingPending;
        private byte _rewindPatrolPass;
        private byte _rewindEscortPass;
        private byte _rewindAttackPass;
        private byte _rewindDockingPass;
        private bool _patrolEnabled;
        private bool _attackEnabled;
        private bool _wingDirectiveEnabled;
        private bool _exitOnFail;
        private bool _scenarioResolved;
        private FixedString64Bytes _bankTestId;
        private byte _bankResolved;
        private bool _bankLogged;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            var behaviorEnv = global::System.Environment.GetEnvironmentVariable(BehaviorProofEnv);
            if (!IsTruthy(behaviorEnv))
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();

            _strikeCraftQuery = SystemAPI.QueryBuilder().WithAll<StrikeCraftProfile>().Build();
            _wingDirectiveQuery = SystemAPI.QueryBuilder().WithAll<StrikeCraftWingDirective>().Build();
            _escortQuery = SystemAPI.QueryBuilder().WithAll<EscortAssignment>().Build();
            _patrolQuery = SystemAPI.QueryBuilder().WithAll<PatrolBehavior>().Build();
            _miningQuery = SystemAPI.QueryBuilder().WithAll<MiningVessel>().Build();

            _patrolEnabled = true;
            _attackEnabled = true;
            _wingDirectiveEnabled = true;
            ResolveScenarioFlags();

            var exitOnFailEnv = global::System.Environment.GetEnvironmentVariable(BehaviorProofExitEnv);
            _exitOnFail = string.IsNullOrWhiteSpace(exitOnFailEnv) || IsTruthy(exitOnFailEnv);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!_scenarioResolved)
            {
                ResolveScenarioFlags();
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            var scenario = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            var timeoutTicks = scenario.EndTick > scenario.StartTick ? scenario.EndTick - scenario.StartTick : 0u;

            if (_patrolEnabled && !_patrolExpected && !_patrolQuery.IsEmptyIgnoreFilter)
            {
                _patrolExpected = true;
            }

            if (!_escortExpected && !_escortQuery.IsEmptyIgnoreFilter)
            {
                _escortExpected = true;
            }

            if (_attackEnabled && !_attackExpected && !_strikeCraftQuery.IsEmptyIgnoreFilter)
            {
                _attackExpected = true;
            }

            if (_wingDirectiveEnabled && !_wingDirectiveExpected && !_wingDirectiveQuery.IsEmptyIgnoreFilter)
            {
                _wingDirectiveExpected = true;
            }

            if (!_dockingExpected && !_miningQuery.IsEmptyIgnoreFilter)
            {
                _dockingExpected = true;
            }

            if (!_profileActionExpected && (_attackExpected || _dockingExpected || _wingDirectiveExpected))
            {
                _profileActionExpected = true;
            }

            if (_attackEnabled && !_capAttackSeen)
            {
                foreach (var profile in SystemAPI.Query<RefRO<StrikeCraftProfile>>())
                {
                    if (profile.ValueRO.Phase == AttackRunPhase.CombatAirPatrol)
                    {
                        _capAttackSeen = true;
                        _capSeenTick = time.Tick;
                        break;
                    }
                }
            }

            EnsureRewindSubjects(ref state);
            TryFlushRewindProofs(ref state);

            if (_patrolExpected && !_patrolPassed && CheckPatrolLoop(ref state))
            {
                _patrolPassed = true;
                _rewindPatrolPending = 1;
                _rewindPatrolPass = 1;
                UnityDebug.Log($"[Space4XHeadlessLoopProof] PASS Patrol tick={time.Tick}");
                TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Exploration, true, 1f, ExpectedComplete, timeoutTicks, step: StepPatrol);
                TryFlushRewindProofs(ref state);
            }

            if (_escortExpected && !_escortPassed && CheckEscortLoop(ref state))
            {
                _escortPassed = true;
                _rewindEscortPending = 1;
                _rewindEscortPass = 1;
                UnityDebug.Log($"[Space4XHeadlessLoopProof] PASS Escort tick={time.Tick}");
                TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Combat, true, 1f, ExpectedComplete, timeoutTicks, step: StepEscort);
                TryFlushRewindProofs(ref state);
            }

            if (_attackExpected && !_attackPassed && CheckAttackRunLoop(ref state))
            {
                _attackPassed = true;
                _rewindAttackPending = 1;
                _rewindAttackPass = 1;
                UnityDebug.Log($"[Space4XHeadlessLoopProof] PASS AttackRun tick={time.Tick} craft={_attackCraft.Index}");
                TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Combat, true, 1f, ExpectedComplete, timeoutTicks, step: StepAttackRun);
                TryFlushRewindProofs(ref state);
            }

            if (_attackEnabled && _capAttackSeen && !_capAttackPassed && CheckCapToAttack(ref state, time.Tick))
            {
                _capAttackPassed = true;
                UnityDebug.Log($"[Space4XHeadlessLoopProof] PASS CapToAttack tick={time.Tick}");
                TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Combat, true, 1f, ExpectedComplete, timeoutTicks, step: StepCapToAttack);
            }

            if (_wingDirectiveExpected && !_wingDirectivePassed && CheckWingDirectiveLoop(ref state, time.Tick))
            {
                _wingDirectivePassed = true;
                UnityDebug.Log($"[Space4XHeadlessLoopProof] PASS WingDirective tick={time.Tick}");
                TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Combat, true, 1f, ExpectedComplete, timeoutTicks, step: StepWingDirective);
            }

            if (_dockingExpected && !_dockingPassed && CheckDockingLoop(ref state))
            {
                _dockingPassed = true;
                _rewindDockingPending = 1;
                _rewindDockingPass = 1;
                UnityDebug.Log($"[Space4XHeadlessLoopProof] PASS Docking tick={time.Tick} vessel={_dockEntity.Index}");
                TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Logistics, true, 1f, ExpectedComplete, timeoutTicks, step: StepDocking);
                TryFlushRewindProofs(ref state);
            }

            if (_profileActionExpected && !_profileActionPassed && CheckProfileActionLoop(ref state))
            {
                _profileActionPassed = true;
                UnityDebug.Log($"[Space4XHeadlessLoopProof] PASS ProfileAction tick={time.Tick}");
                TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Combat, true, 1f, ExpectedComplete, timeoutTicks, step: StepProfileAction);
            }

            if (!_reportedEnd && time.Tick >= scenario.EndTick)
            {
                var anyFail = false;
                if (_attackExpected && !_attackPassed)
                {
                    _rewindAttackPending = 1;
                    _rewindAttackPass = 0;
                    UnityDebug.LogError($"[Space4XHeadlessLoopProof] FAIL AttackRun tick={time.Tick}");
                    TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Combat, false, 0f, ExpectedComplete, timeoutTicks, step: StepAttackRun);
                    anyFail = true;
                }

                if (_attackEnabled && _capAttackSeen && !_capAttackPassed)
                {
                    UnityDebug.LogError($"[Space4XHeadlessLoopProof] FAIL CapToAttack tick={time.Tick}");
                    TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Combat, false, 0f, ExpectedComplete, timeoutTicks, step: StepCapToAttack);
                    anyFail = true;
                }

                if (_wingDirectiveExpected && !_wingDirectivePassed)
                {
                    UnityDebug.LogError($"[Space4XHeadlessLoopProof] FAIL WingDirective tick={time.Tick}");
                    TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Combat, false, 0f, ExpectedComplete, timeoutTicks, step: StepWingDirective);
                    anyFail = true;
                }

                if (_patrolExpected && !_patrolPassed)
                {
                    _rewindPatrolPending = 1;
                    _rewindPatrolPass = 0;
                    UnityDebug.LogError($"[Space4XHeadlessLoopProof] FAIL Patrol tick={time.Tick}");
                    TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Exploration, false, 0f, ExpectedComplete, timeoutTicks, step: StepPatrol);
                    anyFail = true;
                }

                if (_escortExpected && !_escortPassed)
                {
                    _rewindEscortPending = 1;
                    _rewindEscortPass = 0;
                    UnityDebug.LogError($"[Space4XHeadlessLoopProof] FAIL Escort tick={time.Tick}");
                    TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Combat, false, 0f, ExpectedComplete, timeoutTicks, step: StepEscort);
                    anyFail = true;
                }

                if (_dockingExpected && !_dockingPassed)
                {
                    _rewindDockingPending = 1;
                    _rewindDockingPass = 0;
                    UnityDebug.LogError($"[Space4XHeadlessLoopProof] FAIL Docking tick={time.Tick}");
                    TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Logistics, false, 0f, ExpectedComplete, timeoutTicks, step: StepDocking);
                    anyFail = true;
                }

                if (_profileActionExpected && !_profileActionPassed)
                {
                    UnityDebug.LogError($"[Space4XHeadlessLoopProof] FAIL ProfileAction tick={time.Tick}");
                    TelemetryLoopProofUtility.Emit(state.EntityManager, time.Tick, TelemetryLoopIds.Combat, false, 0f, ExpectedComplete, timeoutTicks, step: StepProfileAction);
                    anyFail = true;
                }

                TryFlushRewindProofs(ref state);
                LogBankResult(ref state, ResolveBankTestId(), !anyFail, "missing_loops", time.Tick);
                if (_exitOnFail && anyFail)
                {
                    HeadlessExitUtility.Request(state.EntityManager, time.Tick, 1);
                }
                _reportedEnd = true;
            }
        }

        private void ResolveScenarioFlags()
        {
            var scenarioPath = global::System.Environment.GetEnvironmentVariable(ScenarioPathEnv);
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return;
            }

            _scenarioResolved = true;
            if (scenarioPath.EndsWith(SmokeScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                // Smoke scenario doesn't guarantee patrol/combat loops; avoid false negatives.
                _patrolEnabled = false;
                _attackEnabled = false;
                _wingDirectiveEnabled = false;
                return;
            }

            if (scenarioPath.EndsWith(MiningScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                // Mining-only runs should not require combat/patrol loops.
                _patrolEnabled = false;
                _attackEnabled = false;
                _wingDirectiveEnabled = false;
            }
        }

        private FixedString64Bytes ResolveBankTestId()
        {
            if (_bankResolved != 0)
            {
                return _bankTestId;
            }

            _bankResolved = 1;
            var scenarioPath = global::System.Environment.GetEnvironmentVariable(ScenarioPathEnv);
            if (!string.IsNullOrWhiteSpace(scenarioPath) &&
                scenarioPath.EndsWith(MiningCombatScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                _bankTestId = new FixedString64Bytes("S5.SPACE4X_BEHAVIOR_LOOPS");
            }

            return _bankTestId;
        }

        private void LogBankResult(ref SystemState state, FixedString64Bytes testId, bool pass, string reason, uint tick)
        {
            if (_bankLogged || testId.IsEmpty)
            {
                return;
            }

            _bankLogged = true;
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

        private bool CheckWingDirectiveLoop(ref SystemState state, uint tick)
        {
            foreach (var directive in SystemAPI.Query<RefRO<StrikeCraftWingDirective>>())
            {
                if (directive.ValueRO.LastDecisionTick > 0 && directive.ValueRO.LastDecisionTick <= tick)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CheckProfileActionLoop(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<ProfileActionEventStream>(out var streamEntity))
            {
                return false;
            }

            var buffer = SystemAPI.GetBuffer<ProfileActionEvent>(streamEntity);
            return buffer.Length > 0;
        }

        private void EnsureRewindSubjects(ref SystemState state)
        {
            if (_patrolExpected && _rewindPatrolRegistered == 0 &&
                HeadlessRewindProofUtility.TryEnsureSubject(state.EntityManager, RewindPatrolId, RewindRequiredMask))
            {
                _rewindPatrolRegistered = 1;
            }

            if (_escortExpected && _rewindEscortRegistered == 0 &&
                HeadlessRewindProofUtility.TryEnsureSubject(state.EntityManager, RewindEscortId, RewindRequiredMask))
            {
                _rewindEscortRegistered = 1;
            }

            if (_attackExpected && _rewindAttackRegistered == 0 &&
                HeadlessRewindProofUtility.TryEnsureSubject(state.EntityManager, RewindAttackId, RewindRequiredMask))
            {
                _rewindAttackRegistered = 1;
            }

            if (_dockingExpected && _rewindDockingRegistered == 0 &&
                HeadlessRewindProofUtility.TryEnsureSubject(state.EntityManager, RewindDockingId, RewindRequiredMask))
            {
                _rewindDockingRegistered = 1;
            }
        }

        private void TryFlushRewindProofs(ref SystemState state)
        {
            if (_rewindPatrolPending == 0 && _rewindEscortPending == 0 && _rewindAttackPending == 0 && _rewindDockingPending == 0)
            {
                return;
            }

            if (!HeadlessRewindProofUtility.TryGetState(state.EntityManager, out var rewindProof) || rewindProof.SawRecord == 0)
            {
                return;
            }

            if (_rewindPatrolPending != 0)
            {
                HeadlessRewindProofUtility.TryMarkResult(state.EntityManager, RewindPatrolId, _rewindPatrolPass != 0, _rewindPatrolPass != 0 ? 1f : 0f, ExpectedComplete, RewindRequiredMask);
                _rewindPatrolPending = 0;
            }

            if (_rewindEscortPending != 0)
            {
                HeadlessRewindProofUtility.TryMarkResult(state.EntityManager, RewindEscortId, _rewindEscortPass != 0, _rewindEscortPass != 0 ? 1f : 0f, ExpectedComplete, RewindRequiredMask);
                _rewindEscortPending = 0;
            }

            if (_rewindAttackPending != 0)
            {
                HeadlessRewindProofUtility.TryMarkResult(state.EntityManager, RewindAttackId, _rewindAttackPass != 0, _rewindAttackPass != 0 ? 1f : 0f, ExpectedComplete, RewindRequiredMask);
                _rewindAttackPending = 0;
            }

            if (_rewindDockingPending != 0)
            {
                HeadlessRewindProofUtility.TryMarkResult(state.EntityManager, RewindDockingId, _rewindDockingPass != 0, _rewindDockingPass != 0 ? 1f : 0f, ExpectedComplete, RewindRequiredMask);
                _rewindDockingPending = 0;
            }
        }

        private bool CheckPatrolLoop(ref SystemState state)
        {
            foreach (var patrol in SystemAPI.Query<RefRO<PatrolBehavior>>())
            {
                if (patrol.ValueRO.WaitTimer > 0.01f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CheckEscortLoop(ref SystemState state)
        {
            foreach (var escort in SystemAPI.Query<RefRO<EscortAssignment>>())
            {
                if (escort.ValueRO.Released != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CheckAttackRunLoop(ref SystemState state)
        {
            if (!_attackStarted)
            {
                foreach (var (profile, entity) in SystemAPI.Query<RefRO<StrikeCraftProfile>>().WithEntityAccess())
                {
                    if (profile.ValueRO.Phase == AttackRunPhase.Execute)
                    {
                        _attackStarted = true;
                        _attackCraft = entity;
                        break;
                    }
                }
            }

            if (!_attackStarted || _attackCraft == Entity.Null)
            {
                return false;
            }

            if (!state.EntityManager.Exists(_attackCraft) || !state.EntityManager.HasComponent<StrikeCraftProfile>(_attackCraft))
            {
                return false;
            }

            var craftProfile = state.EntityManager.GetComponentData<StrikeCraftProfile>(_attackCraft);
            return craftProfile.Phase == AttackRunPhase.Docked;
        }

        private bool CheckCapToAttack(ref SystemState state, uint currentTick)
        {
            if (!_capAttackSeen || currentTick <= _capSeenTick)
            {
                return false;
            }

            foreach (var profile in SystemAPI.Query<RefRO<StrikeCraftProfile>>())
            {
                if (profile.ValueRO.Phase == AttackRunPhase.Launching ||
                    profile.ValueRO.Phase == AttackRunPhase.Approach ||
                    profile.ValueRO.Phase == AttackRunPhase.Execute)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CheckDockingLoop(ref SystemState state)
        {
            var dockedCount = 0;
            foreach (var (_, entity) in SystemAPI.Query<RefRO<DockedTag>>().WithAll<MiningVessel>().WithEntityAccess())
            {
                dockedCount++;
                if (_dockEntity == Entity.Null)
                {
                    _dockEntity = entity;
                    _dockingSeen = true;
                }
            }

            if (!_dockingSeen)
            {
                return false;
            }

            if (dockedCount > _dockingPeakDocked)
            {
                _dockingPeakDocked = dockedCount;
            }

            if (_dockingPeakDocked > 0 && dockedCount < _dockingPeakDocked)
            {
                _dockingUndocked = true;
            }

            return _dockingUndocked;
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            value = value.Trim();
            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }
    }
}
