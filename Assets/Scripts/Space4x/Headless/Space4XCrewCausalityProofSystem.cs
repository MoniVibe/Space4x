using System;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Time;
using Space4X.Registry;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using SystemEnv = System.Environment;
using IndividualStats = Space4X.Registry.IndividualStats;

namespace Space4X.Headless
{
    /// <summary>
    /// Headless proof that an injured sensors officer produces a worse sensors outcome.
    /// Logs exactly one BANK PASS/FAIL line when the scenario ends.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.HeadlessExitSystem))]
    public partial struct Space4XCrewCausalityProofSystem : ISystem
    {
        private const string EnabledEnv = "SPACE4X_HEADLESS_CREW_CAUSALITY_PROOF";
        private const string ExitOnResultEnv = "SPACE4X_HEADLESS_CREW_CAUSALITY_PROOF_EXIT";
        private const float BaseAcquireSeconds = 12f;
        private const float MinDeltaSeconds = 1.5f;
        private static readonly FixedString64Bytes TestId = new FixedString64Bytes("S2.SPACE4X_CREW_SENSORS_CAUSALITY_MICRO");
        private static readonly FixedString64Bytes HealthyCarrierId = new FixedString64Bytes("crew-carrier-healthy");
        private static readonly FixedString64Bytes InjuredCarrierId = new FixedString64Bytes("crew-carrier-injured");

        private byte _enabled;
        private byte _done;
        private byte _bankLogged;
        private byte _aliveLogged;
        private FixedString64Bytes _bankTestId;
        private FixedString64Bytes _roleSensorsOfficer;

        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<DerivedCapacities> _capacityLookup;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            var enabled = SystemEnv.GetEnvironmentVariable(EnabledEnv);
            if (!string.Equals(enabled, "1", StringComparison.OrdinalIgnoreCase))
            {
                state.Enabled = false;
                return;
            }

            _enabled = 1;
            _bankTestId = TestId;
            _roleSensorsOfficer = BuildRoleSensorsOfficer();
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _seatOccupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _capacityLookup = state.GetComponentLookup<DerivedCapacities>(true);

            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_enabled == 0 || _done != 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (_aliveLogged == 0)
            {
                _aliveLogged = 1;
                UnityDebug.Log("S2 proof system active");
            }

            var scenario = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (timeState.Tick < scenario.EndTick)
            {
                return;
            }

            _carrierLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _capacityLookup.Update(ref state);

            if (!TryResolveSensorsOccupant(ref state, HealthyCarrierId, out var healthyCrew, out var healthyStats, out var healthyCaps))
            {
                Fail(ref state, timeState.Tick, "missing_healthy_sensors");
                return;
            }

            if (!TryResolveSensorsOccupant(ref state, InjuredCarrierId, out var injuredCrew, out var injuredStats, out var injuredCaps))
            {
                Fail(ref state, timeState.Tick, "missing_injured_sensors");
                return;
            }

            var healthyScore = ComputeCrewScore(in healthyStats, in healthyCaps);
            var injuredScore = ComputeCrewScore(in injuredStats, in injuredCaps);
            var healthyAcquire = ComputeAcquireTimeSeconds(healthyScore);
            var injuredAcquire = ComputeAcquireTimeSeconds(injuredScore);
            var delta = injuredAcquire - healthyAcquire;

            EmitMetrics(ref state, healthyCrew, injuredCrew, healthyScore, injuredScore, healthyAcquire, injuredAcquire, delta);

            if (injuredAcquire <= healthyAcquire)
            {
                Fail(ref state, timeState.Tick, "injured_not_worse");
                return;
            }

            if (delta < MinDeltaSeconds)
            {
                Fail(ref state, timeState.Tick, $"delta_too_small:{delta:F2}");
                return;
            }

            Pass(ref state, timeState.Tick);
        }

        private bool TryResolveSensorsOccupant(
            ref SystemState state,
            in FixedString64Bytes carrierId,
            out Entity crew,
            out IndividualStats stats,
            out DerivedCapacities capacities)
        {
            crew = Entity.Null;
            stats = default;
            capacities = default;

            foreach (var (seat, occupant) in SystemAPI.Query<RefRO<AuthoritySeat>, RefRO<AuthoritySeatOccupant>>())
            {
                if (!seat.ValueRO.RoleId.Equals(_roleSensorsOfficer))
                {
                    continue;
                }

                var body = seat.ValueRO.BodyEntity;
                if (body == Entity.Null || !_carrierLookup.HasComponent(body))
                {
                    continue;
                }

                if (!_carrierLookup[body].CarrierId.Equals(carrierId))
                {
                    continue;
                }

                var occupantEntity = occupant.ValueRO.OccupantEntity;
                if (occupantEntity == Entity.Null || !state.EntityManager.Exists(occupantEntity))
                {
                    return false;
                }

                if (!_statsLookup.HasComponent(occupantEntity) || !_capacityLookup.HasComponent(occupantEntity))
                {
                    return false;
                }

                crew = occupantEntity;
                stats = _statsLookup[occupantEntity];
                capacities = _capacityLookup[occupantEntity];
                return true;
            }

            return false;
        }

        private static float ComputeCrewScore(in IndividualStats stats, in DerivedCapacities capacities)
        {
            var statScore = ((float)stats.Command + (float)stats.Tactics + (float)stats.Engineering) / 300f;
            statScore = math.clamp(statScore, 0f, 1f);

            var sight = math.clamp(capacities.Sight, 0.4f, 1.25f);
            var reaction = math.clamp(capacities.ReactionTime, 0.5f, 1.5f);

            var baseScore = 0.5f + 0.5f * statScore;
            return math.max(0.1f, baseScore * sight * reaction);
        }

        private static float ComputeAcquireTimeSeconds(float crewScore)
        {
            return BaseAcquireSeconds / math.max(0.1f, crewScore);
        }

        private static void EmitMetrics(
            ref SystemState state,
            Entity healthyCrew,
            Entity injuredCrew,
            float healthyScore,
            float injuredScore,
            float healthyAcquire,
            float injuredAcquire,
            float delta)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.sensors.crew_factor.healthy"), Value = healthyScore });
            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.sensors.crew_factor.injured"), Value = injuredScore });
            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.sensors.acquire_time_s.healthy"), Value = healthyAcquire });
            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.sensors.acquire_time_s.injured"), Value = injuredAcquire });
            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.sensors.acquire_time_s.delta"), Value = delta });
            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.sensors.crew.healthy_entity"), Value = healthyCrew.Index });
            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.sensors.crew.injured_entity"), Value = injuredCrew.Index });
        }

        private void Pass(ref SystemState state, uint tick)
        {
            _done = 1;
            LogBankResult(ref state, true, "pass", tick);
            ExitIfRequested(ref state, tick, 0);
        }

        private void Fail(ref SystemState state, uint tick, string reason)
        {
            _done = 1;
            LogBankResult(ref state, false, reason, tick);
            ExitIfRequested(ref state, tick, 4);
        }

        private static void ExitIfRequested(ref SystemState state, uint tick, int exitCode)
        {
            if (!string.Equals(SystemEnv.GetEnvironmentVariable(ExitOnResultEnv), "1", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            HeadlessExitUtility.Request(state.EntityManager, tick, exitCode);
        }

        private void LogBankResult(ref SystemState state, bool pass, string reason, uint tick)
        {
            if (_bankLogged != 0 || _bankTestId.IsEmpty)
            {
                return;
            }

            ResolveTickInfo(ref state, tick, out var tickTime, out var scenarioTick);
            var delta = (int)tickTime - (int)scenarioTick;
            _bankLogged = 1;

            if (pass)
            {
                UnityDebug.Log($"BANK:{_bankTestId}:PASS tickTime={tickTime} scenarioTick={scenarioTick} delta={delta}");
                return;
            }

            UnityDebug.Log($"BANK:{_bankTestId}:FAIL reason={reason} tickTime={tickTime} scenarioTick={scenarioTick} delta={delta}");
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

        private static FixedString64Bytes BuildRoleSensorsOfficer()
        {
            var id = new FixedString64Bytes();
            id.Append('s');
            id.Append('h');
            id.Append('i');
            id.Append('p');
            id.Append('.');
            id.Append('s');
            id.Append('e');
            id.Append('n');
            id.Append('s');
            id.Append('o');
            id.Append('r');
            id.Append('s');
            id.Append('_');
            id.Append('o');
            id.Append('f');
            id.Append('f');
            id.Append('i');
            id.Append('c');
            id.Append('e');
            id.Append('r');
            return id;
        }
    }
}
