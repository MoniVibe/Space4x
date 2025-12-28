using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

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
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string SmokeScenarioFile = "space4x_smoke.json";
        private bool _reportedFailure;
        private bool _ignoreStuckFailures;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();

            var scenarioPath = Environment.GetEnvironmentVariable(ScenarioPathEnv);
            if (!string.IsNullOrWhiteSpace(scenarioPath) &&
                scenarioPath.EndsWith(SmokeScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                // Smoke mining undock/approach can trip stuck counters before latch; skip stuck failures there.
                _ignoreStuckFailures = true;
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_reportedFailure)
            {
                return;
            }

            var tick = SystemAPI.GetSingleton<TimeState>().Tick;

            var maxSpeedDelta = 0f;
            var maxTeleport = 0f;
            var maxStateFlips = 0u;
            Entity speedOffender = Entity.Null;
            Entity teleportOffender = Entity.Null;
            Entity flipsOffender = Entity.Null;
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

                if (debugState.TeleportCount > 0)
                {
                    anyFailure = true;
                    failTeleport += debugState.TeleportCount;
                }

                if (!_ignoreStuckFailures && debugState.StuckCount > 0)
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
            }

            if (!anyFailure)
            {
                return;
            }

            _reportedFailure = true;
            UnityDebug.LogError($"[Space4XHeadlessMovementDiag] FAIL tick={tick} nanInf={failNaN} teleport={failTeleport} stuck={failStuck} spikes={failSpike}");

            LogOffenderReport(ref state, tick, speedOffender, teleportOffender, flipsOffender, maxSpeedDelta, maxTeleport, maxStateFlips);
        }

        private void LogOffenderReport(ref SystemState state, uint tick, Entity speedOffender, Entity teleportOffender, Entity flipsOffender, float maxSpeedDelta, float maxTeleport, uint maxStateFlips)
        {
            UnityDebug.LogError($"[Space4XHeadlessMovementDiag] TopOffenders speedSpike entity={speedOffender.Index} maxDelta={maxSpeedDelta:F3} teleport entity={teleportOffender.Index} maxTeleport={maxTeleport:F3} stateFlips entity={flipsOffender.Index} flips={maxStateFlips}");

            LogSnapshot(ref state, tick, speedOffender, "speed_spike");
            LogSnapshot(ref state, tick, teleportOffender, "teleport");
            LogSnapshot(ref state, tick, flipsOffender, "state_flips");
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
