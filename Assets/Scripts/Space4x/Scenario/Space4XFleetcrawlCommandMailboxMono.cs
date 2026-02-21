using System;
using System.IO;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4x.Scenario
{
    [DisallowMultipleComponent]
    public sealed class Space4XFleetcrawlCommandMailboxMono : MonoBehaviour
    {
        [Serializable]
        private sealed class FleetcrawlCommandEnvelope
        {
            public int pick_gate = -1;
            public int pick_boon = -1;
            public bool reroll;
            public bool force_end_room;
        }

        private const string CommandPathEnv = "SPACE4X_FLEETCRAWL_CMD_PATH";
        private const string DefaultCommandPath = "tmp/fleetcrawl_cmd.json";

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _scenarioQuery;
        private EntityQuery _directorQuery;
        private EntityQuery _timeQuery;
        private bool _queriesReady;
        private string _commandPath;
        private float _nextPollAtRealtime;

        private void Update()
        {
            if (!TryEnsureQueries())
            {
                return;
            }

            if (_scenarioQuery.IsEmptyIgnoreFilter || _directorQuery.IsEmptyIgnoreFilter || _timeQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (Time.realtimeSinceStartup < _nextPollAtRealtime)
            {
                return;
            }
            _nextPollAtRealtime = Time.realtimeSinceStartup + 0.2f;

            var scenario = _scenarioQuery.GetSingleton<ScenarioInfo>();
            if (!Space4XFleetcrawlUiBridge.IsFleetcrawlScenario(scenario.ScenarioId))
            {
                return;
            }

            if (!TryReadCommand(out var command))
            {
                return;
            }

            var directorEntity = _directorQuery.GetSingletonEntity();
            var director = _entityManager.GetComponentData<Space4XFleetcrawlDirectorState>(directorEntity);
            var roomIndex = director.CurrentRoomIndex;
            if (roomIndex < 0)
            {
                return;
            }

            var time = _timeQuery.GetSingleton<TimeState>();
            var anyApplied = false;

            if (command.pick_gate >= 0)
            {
                UpsertComponent(directorEntity, new Space4XRunPendingGatePick
                {
                    RoomIndex = roomIndex,
                    GateOrdinal = math.clamp(command.pick_gate, 0, 2)
                });
                Debug.Log($"[FleetcrawlInput] CMD gate_pick room={roomIndex} idx={math.clamp(command.pick_gate, 0, 2)}.");
                anyApplied = true;
            }

            if (command.pick_boon >= 0)
            {
                UpsertComponent(directorEntity, new Space4XRunPendingBoonPick
                {
                    RoomIndex = roomIndex,
                    OfferIndex = math.clamp(command.pick_boon, 0, 2)
                });
                Debug.Log($"[FleetcrawlInput] CMD boon_pick room={roomIndex} idx={math.clamp(command.pick_boon, 0, 2)}.");
                anyApplied = true;
            }

            if (command.reroll)
            {
                if (_entityManager.HasComponent<Space4XRunRerollTokens>(directorEntity))
                {
                    var reroll = _entityManager.GetComponentData<Space4XRunRerollTokens>(directorEntity);
                    reroll.Value += 1;
                    _entityManager.SetComponentData(directorEntity, reroll);
                }
                Debug.Log($"[FleetcrawlInput] CMD reroll room={roomIndex}.");
                anyApplied = true;
            }

            if (command.force_end_room)
            {
                director.RoomEndTick = time.Tick;
                _entityManager.SetComponentData(directorEntity, director);
                Debug.Log($"[FleetcrawlInput] CMD force_end_room room={roomIndex} tick={time.Tick}.");
                anyApplied = true;
            }

            if (anyApplied)
            {
                TryClearMailbox();
            }
        }

        private bool TryReadCommand(out FleetcrawlCommandEnvelope command)
        {
            command = null;
            if (!File.Exists(_commandPath))
            {
                return false;
            }

            string text;
            try
            {
                text = File.ReadAllText(_commandPath);
            }
            catch
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();
            if (trimmed == "{}")
            {
                return false;
            }

            try
            {
                command = JsonUtility.FromJson<FleetcrawlCommandEnvelope>(trimmed);
            }
            catch
            {
                command = null;
            }

            if (command == null)
            {
                return false;
            }

            var hasAnyCommand = command.pick_gate >= 0 || command.pick_boon >= 0 || command.reroll || command.force_end_room;
            return hasAnyCommand;
        }

        private void TryClearMailbox()
        {
            try
            {
                var directory = Path.GetDirectoryName(_commandPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_commandPath, "{}");
            }
            catch
            {
                // Non-fatal: mailbox remains and may be retried.
            }
        }

        private bool TryEnsureQueries()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            if (_queriesReady && world == _world)
            {
                return true;
            }

            _world = world;
            _entityManager = world.EntityManager;
            _scenarioQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ScenarioInfo>());
            _directorQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XFleetcrawlDirectorState>());
            _timeQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
            _commandPath = ResolvePathFromEnv(CommandPathEnv, DefaultCommandPath);
            _queriesReady = true;
            Debug.Log($"[FleetcrawlInput] CMD mailbox_path='{_commandPath}'.");
            return true;
        }

        private static string ResolvePathFromEnv(string envName, string defaultRelativePath)
        {
            var raw = Environment.GetEnvironmentVariable(envName);
            var candidate = string.IsNullOrWhiteSpace(raw) ? defaultRelativePath : raw.Trim();
            if (Path.IsPathRooted(candidate))
            {
                return candidate;
            }

            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), candidate));
        }

        private void UpsertComponent<T>(Entity entity, in T value) where T : unmanaged, IComponentData
        {
            if (_entityManager.HasComponent<T>(entity))
            {
                _entityManager.SetComponentData(entity, value);
            }
            else
            {
                _entityManager.AddComponentData(entity, value);
            }
        }
    }
}
