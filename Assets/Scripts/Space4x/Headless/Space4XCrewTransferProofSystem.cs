using System;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Time;
using Space4x.Scenario;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using SystemEnv = System.Environment;

namespace Space4X.Headless
{
    /// <summary>
    /// Proves a crew entity survives transfer between ships and the ledger updates.
    /// Logs exactly one BANK PASS/FAIL line when the scenario ends.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.HeadlessExitSystem))]
    public partial struct Space4XCrewTransferProofSystem : ISystem
    {
        private const string EnabledEnv = "SPACE4X_HEADLESS_CREW_TRANSFER_PROOF";
        private const string ExitOnResultEnv = "SPACE4X_HEADLESS_CREW_TRANSFER_PROOF_EXIT";
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string ScenarioFile = "space4x_crew_transfer_micro.json";
        private static readonly FixedString64Bytes TestId = new FixedString64Bytes("S3.SPACE4X_CREW_ENTITY_TRANSFER_MICRO");
        private static readonly FixedString64Bytes CarrierA = new FixedString64Bytes("crew-carrier-a");
        private static readonly FixedString64Bytes CarrierB = new FixedString64Bytes("crew-carrier-b");
        private static readonly FixedString64Bytes TransferCrewId = new FixedString64Bytes("TRANSFER-01");

        private const uint TransferDelayTicks = 10;
        private const uint TransferHoldTicks = 10;

        private byte _enabled;
        private byte _done;
        private byte _bankResolved;
        private byte _bankLogged;
        private FixedString64Bytes _bankTestId;
        private uint _transferTick;
        private byte _transferPhase;
        private byte _sawLod2;
        private Entity _transferCrew;

        private ComponentLookup<Carrier> _carrierLookup;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            var enabled = SystemEnv.GetEnvironmentVariable(EnabledEnv);
            if (!string.Equals(enabled, "1", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            {
                state.Enabled = false;
                return;
            }

            _enabled = 1;
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_enabled == 0 || _done != 0)
            {
                return;
            }

            if (!ResolveBankTestId())
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var scenario = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            _carrierLookup.Update(ref state);

            if (_transferPhase == 0 && timeState.Tick >= scenario.StartTick + TransferDelayTicks)
            {
                if (!TryRemoveCrew(ref state, out _transferCrew))
                {
                    Fail(ref state, timeState.Tick, "transfer_remove_failed");
                    return;
                }
                _transferTick = timeState.Tick;
                _transferPhase = 1;
            }

            if (_transferPhase == 1)
            {
                if (TryMarkLod2(ref state))
                {
                    _sawLod2 = 1;
                }

                if (timeState.Tick >= _transferTick + TransferHoldTicks)
                {
                    if (!TryAddCrew(ref state))
                    {
                        Fail(ref state, timeState.Tick, "transfer_add_failed");
                        return;
                    }
                    _transferPhase = 2;
                }
            }

            if (timeState.Tick < scenario.EndTick)
            {
                return;
            }

            if (_transferPhase < 2)
            {
                Fail(ref state, timeState.Tick, "transfer_incomplete");
                return;
            }

            if (_sawLod2 == 0)
            {
                Fail(ref state, timeState.Tick, "lod2_not_observed");
                return;
            }

            if (!TryValidateLedger(ref state, timeState.Tick))
            {
                return;
            }

            Pass(ref state, timeState.Tick);
        }

        private bool TryRemoveCrew(ref SystemState state, out Entity crewEntity)
        {
            crewEntity = Entity.Null;
            if (!TryFindCarrier(ref state, CarrierA, out var carrierEntity))
            {
                return false;
            }

            if (!state.EntityManager.HasBuffer<PlatformCrewMember>(carrierEntity))
            {
                return false;
            }

            var crewBuffer = state.EntityManager.GetBuffer<PlatformCrewMember>(carrierEntity);
            for (int i = 0; i < crewBuffer.Length; i++)
            {
                var candidate = crewBuffer[i].CrewEntity;
                if (!IsTransferCrew(state.EntityManager, candidate))
                {
                    continue;
                }

                crewEntity = candidate;
                crewBuffer.RemoveAt(i);
                EnsureLodTier(state.EntityManager, crewEntity, (byte)Space4XEntityLodTierKind.Lod2);
                return true;
            }

            return false;
        }

        private bool TryAddCrew(ref SystemState state)
        {
            if (_transferCrew == Entity.Null || !state.EntityManager.Exists(_transferCrew))
            {
                return false;
            }

            if (!TryFindCarrier(ref state, CarrierB, out var carrierEntity))
            {
                return false;
            }

            var crewBuffer = state.EntityManager.HasBuffer<PlatformCrewMember>(carrierEntity)
                ? state.EntityManager.GetBuffer<PlatformCrewMember>(carrierEntity)
                : state.EntityManager.AddBuffer<PlatformCrewMember>(carrierEntity);

            crewBuffer.Add(new PlatformCrewMember
            {
                CrewEntity = _transferCrew,
                RoleId = 0
            });
            EnsureLodTier(state.EntityManager, _transferCrew, (byte)Space4XEntityLodTierKind.Lod0);
            return true;
        }

        private bool TryMarkLod2(ref SystemState state)
        {
            if (_transferCrew == Entity.Null || !state.EntityManager.Exists(_transferCrew))
            {
                return false;
            }

            if (!state.EntityManager.HasComponent<Space4XEntityLodTier>(_transferCrew))
            {
                return false;
            }

            return state.EntityManager.GetComponentData<Space4XEntityLodTier>(_transferCrew).Tier == (byte)Space4XEntityLodTierKind.Lod2;
        }

        private bool TryValidateLedger(ref SystemState state, uint tick)
        {
            if (!TryGetLedger(state.EntityManager, out var ledger))
            {
                Fail(ref state, tick, "ledger_missing");
                return false;
            }

            for (int i = 0; i < ledger.Length; i++)
            {
                var entry = ledger[i];
                if (!entry.EntityId.Equals(TransferCrewId))
                {
                    continue;
                }

                EmitMetrics(ref state, entry, tick);

                if (!entry.CarrierId.Equals(CarrierB))
                {
                    Fail(ref state, tick, "ledger_not_transferred");
                    return false;
                }

                if (entry.LodTier != (byte)Space4XEntityLodTierKind.Lod0)
                {
                    Fail(ref state, tick, "ledger_lod_not_active");
                    return false;
                }

                if (entry.LastSeenTick < _transferTick)
                {
                    Fail(ref state, tick, "ledger_stale");
                    return false;
                }

                return true;
            }

            Fail(ref state, tick, "ledger_entry_missing");
            return false;
        }

        private static void EmitMetrics(ref SystemState state, Space4XEntityLedgerEntry entry, uint tick)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.ledger.transfer_tick"), Value = tick });
            buffer.Add(new Space4XOperatorMetric { Key = new FixedString64Bytes("space4x.ledger.transfer_last_seen"), Value = entry.LastSeenTick });
        }

        private bool TryFindCarrier(ref SystemState state, in FixedString64Bytes carrierId, out Entity carrierEntity)
        {
            carrierEntity = Entity.Null;
            foreach (var (carrier, entity) in SystemAPI.Query<RefRO<Carrier>>().WithEntityAccess())
            {
                if (carrier.ValueRO.CarrierId.Equals(carrierId))
                {
                    carrierEntity = entity;
                    return true;
                }
            }

            return false;
        }

        private static bool IsTransferCrew(EntityManager entityManager, Entity candidate)
        {
            if (candidate == Entity.Null || !entityManager.Exists(candidate))
            {
                return false;
            }

            if (!entityManager.HasComponent<Space4XEntityId>(candidate))
            {
                return false;
            }

            return entityManager.GetComponentData<Space4XEntityId>(candidate).Id.Equals(TransferCrewId);
        }

        private static void EnsureLodTier(EntityManager entityManager, Entity crew, byte tier)
        {
            if (entityManager.HasComponent<Space4XEntityLodTier>(crew))
            {
                entityManager.SetComponentData(crew, new Space4XEntityLodTier { Tier = tier });
            }
            else
            {
                entityManager.AddComponentData(crew, new Space4XEntityLodTier { Tier = tier });
            }
        }

        private static bool TryGetLedger(EntityManager entityManager, out DynamicBuffer<Space4XEntityLedgerEntry> ledger)
        {
            ledger = default;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XEntityLedgerTag>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var entity = query.GetSingletonEntity();
            if (!entityManager.HasBuffer<Space4XEntityLedgerEntry>(entity))
            {
                return false;
            }

            ledger = entityManager.GetBuffer<Space4XEntityLedgerEntry>(entity);
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

        private bool ResolveBankTestId()
        {
            if (_bankResolved != 0)
            {
                return !_bankTestId.IsEmpty;
            }

            _bankResolved = 1;
            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (!string.IsNullOrWhiteSpace(scenarioPath)
                && !scenarioPath.EndsWith(ScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                _enabled = 0;
                return false;
            }

            _bankTestId = TestId;
            return true;
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
    }
}
