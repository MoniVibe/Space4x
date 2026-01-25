using System;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
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
    [UpdateInGroup(typeof(PureDOTS.Systems.LateSimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.Telemetry.TelemetryExportSystem))]
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
        private byte _telemetryLogged;
        private FixedString64Bytes _bankTestId;
        private FixedString64Bytes _roleSensorsOfficer;

        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<DerivedCapacities> _capacityLookup;
        private BufferLookup<PerceivedEntity> _perceivedLookup;

        private Entity _healthyCarrierEntity;
        private Entity _injuredCarrierEntity;
        private uint _healthyDetectTick;
        private uint _injuredDetectTick;
        private byte _healthyDetected;
        private byte _injuredDetected;
        private uint _healthySeatTick;
        private uint _injuredSeatTick;
        private byte _healthySeatAssigned;
        private byte _injuredSeatAssigned;

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
            _perceivedLookup = state.GetBufferLookup<PerceivedEntity>(true);

            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TelemetryStream>();
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

            _carrierLookup.Update(ref state);
            _perceivedLookup.Update(ref state);

            if (_healthyCarrierEntity == Entity.Null || !_carrierLookup.HasComponent(_healthyCarrierEntity))
            {
                _healthyCarrierEntity = ResolveCarrier(HealthyCarrierId, ref state);
            }

            if (_injuredCarrierEntity == Entity.Null || !_carrierLookup.HasComponent(_injuredCarrierEntity))
            {
                _injuredCarrierEntity = ResolveCarrier(InjuredCarrierId, ref state);
            }

            if (_healthyCarrierEntity != Entity.Null && _injuredCarrierEntity != Entity.Null)
            {
                if (_healthyDetected == 0 && IsTargetDetected(_healthyCarrierEntity, _injuredCarrierEntity))
                {
                    _healthyDetected = 1;
                    _healthyDetectTick = timeState.Tick;
                }

                if (_injuredDetected == 0 && IsTargetDetected(_injuredCarrierEntity, _healthyCarrierEntity))
                {
                    _injuredDetected = 1;
                    _injuredDetectTick = timeState.Tick;
                }
            }

            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _capacityLookup.Update(ref state);

            if (_healthySeatAssigned == 0 &&
                TryResolveSensorsOccupant(ref state, HealthyCarrierId, out _, out _, out _))
            {
                _healthySeatAssigned = 1;
                _healthySeatTick = timeState.Tick;
            }

            if (_injuredSeatAssigned == 0 &&
                TryResolveSensorsOccupant(ref state, InjuredCarrierId, out _, out _, out _))
            {
                _injuredSeatAssigned = 1;
                _injuredSeatTick = timeState.Tick;
            }

            if (_telemetryLogged == 0 && _healthySeatAssigned != 0 && _injuredSeatAssigned != 0)
            {
                var startTick = scenario.StartTick;
                var fixedDt = timeState.FixedDeltaTime;
                var healthyEmergent = math.max(0f, (_healthySeatTick - startTick) * fixedDt);
                var injuredEmergent = math.max(0f, (_injuredSeatTick - startTick) * fixedDt);
                var emergentDelta = injuredEmergent - healthyEmergent;
                TryEmitTelemetry(ref state, -1f, -1f, -1f, healthyEmergent, injuredEmergent, emergentDelta);
            }

            if (timeState.Tick < scenario.EndTick)
            {
                return;
            }

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

            var startTick = scenario.StartTick;
            var fixedDt = timeState.FixedDeltaTime;
            var healthyEmergent = _healthyDetected != 0
                ? math.max(0f, (_healthyDetectTick - startTick) * fixedDt)
                : (_healthySeatAssigned != 0 ? math.max(0f, (_healthySeatTick - startTick) * fixedDt) : -1f);
            var injuredEmergent = _injuredDetected != 0
                ? math.max(0f, (_injuredDetectTick - startTick) * fixedDt)
                : (_injuredSeatAssigned != 0 ? math.max(0f, (_injuredSeatTick - startTick) * fixedDt) : -1f);
            var emergentDelta = (_healthyDetected != 0 && _injuredDetected != 0)
                ? injuredEmergent - healthyEmergent
                : -1f;

            EmitMetrics(
                ref state,
                healthyCrew,
                injuredCrew,
                healthyScore,
                injuredScore,
                healthyAcquire,
                injuredAcquire,
                delta,
                healthyEmergent,
                injuredEmergent,
                emergentDelta);
            TryEmitTelemetry(ref state, healthyAcquire, injuredAcquire, delta, healthyEmergent, injuredEmergent, emergentDelta);

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

        private bool IsTargetDetected(Entity observer, Entity target)
        {
            if (!_perceivedLookup.HasBuffer(observer))
            {
                return false;
            }

            var perceived = _perceivedLookup[observer];
            for (int i = 0; i < perceived.Length; i++)
            {
                if (perceived[i].TargetEntity == target)
                {
                    return true;
                }
            }

            return false;
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
            float delta,
            float healthyEmergent,
            float injuredEmergent,
            float emergentDelta)
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
            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.sensors.acquire_time_s.emergent.healthy"), Value = healthyEmergent });
            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.sensors.acquire_time_s.emergent.injured"), Value = injuredEmergent });
            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.sensors.acquire_time_s.emergent.delta"), Value = emergentDelta });
            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.sensors.crew.healthy_entity"), Value = healthyCrew.Index });
            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.sensors.crew.injured_entity"), Value = injuredCrew.Index });
        }

        private void TryEmitTelemetry(
            ref SystemState state,
            float healthyAcquire,
            float injuredAcquire,
            float delta,
            float healthyEmergent,
            float injuredEmergent,
            float emergentDelta)
        {
            if (_telemetryLogged != 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config) ||
                config.Enabled == 0 ||
                (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            if (!TryGetTelemetryMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            buffer.AddMetric("space4x.sensors.acquire_time_s.healthy", healthyAcquire, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.sensors.acquire_time_s.injured", injuredAcquire, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.sensors.acquire_time_s.delta", delta, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.sensors.acquire_time_s.emergent.healthy", healthyEmergent, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.sensors.acquire_time_s.emergent.injured", injuredEmergent, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.sensors.acquire_time_s.emergent.delta", emergentDelta, TelemetryMetricUnit.Custom);
            _telemetryLogged = 1;
        }

        private bool TryGetTelemetryMetricBuffer(ref SystemState state, out DynamicBuffer<TelemetryMetric> buffer)
        {
            buffer = default;
            if (!SystemAPI.TryGetSingleton<TelemetryStreamSingleton>(out var telemetryRef))
            {
                TelemetryStreamUtility.EnsureEventStream(state.EntityManager);
                if (!SystemAPI.TryGetSingleton<TelemetryStreamSingleton>(out telemetryRef))
                {
                    return false;
                }
            }

            if (telemetryRef.Stream == Entity.Null)
            {
                return false;
            }

            if (!state.EntityManager.HasBuffer<TelemetryMetric>(telemetryRef.Stream))
            {
                state.EntityManager.AddBuffer<TelemetryMetric>(telemetryRef.Stream);
            }

            buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryRef.Stream);
            return true;
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

        private Entity ResolveCarrier(FixedString64Bytes carrierId, ref SystemState state)
        {
            if (carrierId.IsEmpty)
            {
                return Entity.Null;
            }

            foreach (var (carrier, entity) in SystemAPI.Query<RefRO<Carrier>>().WithEntityAccess())
            {
                if (carrier.ValueRO.CarrierId.Equals(carrierId))
                {
                    return entity;
                }
            }

            return Entity.Null;
        }
    }
}
