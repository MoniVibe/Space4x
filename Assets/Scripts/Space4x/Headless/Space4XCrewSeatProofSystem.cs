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
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using SystemEnv = System.Environment;

namespace Space4X.Headless
{
    /// <summary>
    /// Headless proof that crew seat selection favors healthy sensors officers.
    /// Logs exactly one BANK PASS/FAIL line when the scenario ends.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.HeadlessExitSystem))]
    public partial struct Space4XCrewSeatProofSystem : ISystem
    {
        private const string EnabledEnv = "SPACE4X_HEADLESS_CREW_PROOF";
        private const string ExitOnResultEnv = "SPACE4X_HEADLESS_CREW_PROOF_EXIT";
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string CrewScenarioFile = "space4x_crew_sensors_micro.json";
        private static readonly FixedString64Bytes CrewTestId = new FixedString64Bytes("S1.SPACE4X_CREW_SENSORS_MICRO");

        private byte _enabled;
        private byte _done;
        private byte _bankResolved;
        private byte _bankLogged;
        private FixedString64Bytes _bankTestId;
        private FixedString64Bytes _roleSensorsOfficer;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            var enabled = SystemEnv.GetEnvironmentVariable(EnabledEnv);
            if (string.Equals(enabled, "0", StringComparison.OrdinalIgnoreCase))
            {
                state.Enabled = false;
                return;
            }

            _enabled = 1;
            _roleSensorsOfficer = BuildRoleSensorsOfficer();
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
            if (timeState.Tick < scenario.EndTick)
            {
                return;
            }

            var foundSeat = false;
            Entity occupantEntity = Entity.Null;
            foreach (var (seat, occupant, entity) in SystemAPI
                         .Query<RefRO<AuthoritySeat>, RefRO<AuthoritySeatOccupant>>()
                         .WithEntityAccess())
            {
                if (!seat.ValueRO.RoleId.Equals(_roleSensorsOfficer))
                {
                    continue;
                }

                foundSeat = true;
                occupantEntity = occupant.ValueRO.OccupantEntity;
                break;
            }

            if (!foundSeat)
            {
                Fail(ref state, timeState.Tick, "missing_sensors_seat");
                return;
            }

            if (occupantEntity == Entity.Null || !state.EntityManager.Exists(occupantEntity))
            {
                Fail(ref state, timeState.Tick, "missing_occupant");
                return;
            }

            if (IsInjured(occupantEntity, state.EntityManager))
            {
                Fail(ref state, timeState.Tick, "injured_selected");
                return;
            }

            Pass(ref state, timeState.Tick);
        }

        private static bool IsInjured(Entity crewEntity, EntityManager entityManager)
        {
            if (!entityManager.HasBuffer<Condition>(crewEntity))
            {
                return false;
            }

            var conditions = entityManager.GetBuffer<Condition>(crewEntity);
            for (int i = 0; i < conditions.Length; i++)
            {
                var condition = conditions[i];
                if ((condition.Flags & ConditionFlags.OneEyeMissing) != 0)
                {
                    return true;
                }

                if ((condition.Flags & ConditionFlags.Missing) != 0 &&
                    (condition.TargetPartId == AnatomyPartIds.EyeLeft ||
                     condition.TargetPartId == AnatomyPartIds.EyeRight))
                {
                    return true;
                }
            }

            return false;
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

            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return false;
            }

            _bankResolved = 1;
            if (scenarioPath.EndsWith(CrewScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                _bankTestId = CrewTestId;
                return true;
            }

            _enabled = 0;
            return false;
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
