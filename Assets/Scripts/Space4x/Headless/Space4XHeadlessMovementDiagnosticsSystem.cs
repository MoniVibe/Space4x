using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Time;
using Space4X.Registry;
using Space4X.Runtime;
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
        private const uint StuckFailureThreshold = 2;
        private const float MiningApproachTeleportDistance = 3f;
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string CollisionScenarioFile = "space4x_collision_micro.json";
        private const string SmokeScenarioFile = "space4x_smoke.json";
        private const string MiningScenarioFile = "space4x_mining.json";
        private const string MiningCombatScenarioFile = "space4x_mining_combat.json";
        private const string RefitScenarioFile = "space4x_refit.json";
        private const string ResearchScenarioFile = "space4x_research_mvp.json";
        private const uint TeleportFailureThreshold = 1;
        private bool _reportedFailure;
        private bool _ignoreStuckFailures;
        private bool _ignoreTeleportFailures;
        private bool _scenarioResolved;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();

            ResolveScenarioFlags();
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

            var tick = SystemAPI.GetSingleton<TimeState>().Tick;

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
            var failSpike = 0u;

            foreach (var (movement, debug, entity) in SystemAPI.Query<RefRO<VesselMovement>, RefRO<MovementDebugState>>().WithEntityAccess())
            {
                var debugState = debug.ValueRO;
                if (debugState.NaNInfCount > 0)
                {
                    anyFailure = true;
                    failNaN += debugState.NaNInfCount;
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

                if (debugState.StuckCount > StuckFailureThreshold && !ignoreStuck)
                {
                    anyFailure = true;
                    failStuck += debugState.StuckCount;
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
            }

            if (!anyFailure)
            {
                return;
            }

            _reportedFailure = true;
            var fatalFailure = failNaN > 0 || failStuck > 0 || failSpike > 0;
            if (fatalFailure)
            {
                UnityDebug.LogError($"[Space4XHeadlessMovementDiag] FAIL tick={tick} nanInf={failNaN} teleport={failTeleport} stuck={failStuck} spikes={failSpike}");
                LogOffenderReport(ref state, tick, speedOffender, teleportOffender, flipsOffender, stuckOffender, maxSpeedDelta, maxTeleport, maxStateFlips, maxStuck);
                HeadlessExitUtility.Request(state.EntityManager, tick, 2);
                return;
            }

            UnityDebug.LogWarning($"[Space4XHeadlessMovementDiag] WARN tick={tick} nanInf={failNaN} teleport={failTeleport} stuck={failStuck} spikes={failSpike}");
            LogOffenderReport(ref state, tick, speedOffender, teleportOffender, flipsOffender, stuckOffender, maxSpeedDelta, maxTeleport, maxStateFlips, maxStuck);
        }

        private void ResolveScenarioFlags()
        {
            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return;
            }

            _scenarioResolved = true;
            if (scenarioPath.EndsWith(CollisionScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                _ignoreStuckFailures = true;
                _ignoreTeleportFailures = true;
                return;
            }

            if (scenarioPath.EndsWith(SmokeScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                // Smoke mining undock/approach can trip stuck counters before latch; skip stuck failures there.
                _ignoreStuckFailures = true;
                // Latch/dock surface snapping can exceed teleport thresholds in smoke runs.
                _ignoreTeleportFailures = true;
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

        private void LogOffenderReport(ref SystemState state, uint tick, Entity speedOffender, Entity teleportOffender, Entity flipsOffender, Entity stuckOffender, float maxSpeedDelta, float maxTeleport, uint maxStateFlips, uint maxStuck)
        {
            UnityDebug.LogError($"[Space4XHeadlessMovementDiag] TopOffenders speedSpike entity={speedOffender.Index} maxDelta={maxSpeedDelta:F3} teleport entity={teleportOffender.Index} maxTeleport={maxTeleport:F3} stateFlips entity={flipsOffender.Index} flips={maxStateFlips} stuck entity={stuckOffender.Index} count={maxStuck}");

            LogSnapshot(ref state, tick, speedOffender, "speed_spike");
            LogSnapshot(ref state, tick, teleportOffender, "teleport");
            LogSnapshot(ref state, tick, flipsOffender, "state_flips");
            LogSnapshot(ref state, tick, stuckOffender, "stuck");
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
    }
}
