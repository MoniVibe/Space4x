using System;
using System.Collections.Generic;
using System.IO;
using PureDOTS.Runtime.Scenarios;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4x.Scenario
{
    [DisallowMultipleComponent]
    public sealed class Space4XFleetcrawlMetaProgressionMono : MonoBehaviour
    {
        [Serializable]
        private sealed class FleetcrawlMetaSaveData
        {
            public List<string> unlocked_starters = new();
            public List<string> unlocked_blueprints = new();
            public int run_count;
            public int best_room;
            public uint best_digest;
        }

        private sealed class FleetcrawlMetaStore
        {
            public FleetcrawlMetaSaveData Data { get; private set; } = new();
            private readonly string _path;

            public FleetcrawlMetaStore(string path)
            {
                _path = path;
            }

            public bool TryLoadSave()
            {
                if (!File.Exists(_path))
                {
                    Data = new FleetcrawlMetaSaveData();
                    return false;
                }

                try
                {
                    var raw = File.ReadAllText(_path);
                    Data = string.IsNullOrWhiteSpace(raw)
                        ? new FleetcrawlMetaSaveData()
                        : JsonUtility.FromJson<FleetcrawlMetaSaveData>(raw) ?? new FleetcrawlMetaSaveData();
                    Data.unlocked_starters ??= new List<string>();
                    Data.unlocked_blueprints ??= new List<string>();
                    return true;
                }
                catch
                {
                    Data = new FleetcrawlMetaSaveData();
                    return false;
                }
            }

            public bool WriteSave()
            {
                try
                {
                    var dir = Path.GetDirectoryName(_path);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.WriteAllText(_path, JsonUtility.ToJson(Data, true));
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public bool UnlockStarter(string id)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return false;
                }

                if (Data.unlocked_starters.Contains(id))
                {
                    return false;
                }

                Data.unlocked_starters.Add(id);
                return true;
            }

            public bool UnlockBlueprint(string id)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return false;
                }

                if (Data.unlocked_blueprints.Contains(id))
                {
                    return false;
                }

                Data.unlocked_blueprints.Add(id);
                return true;
            }
        }

        private const string SavePathEnv = "SPACE4X_FLEETCRAWL_SAVE_PATH";
        private const string DefaultSavePath = "tmp/fleetcrawl_save.json";

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _scenarioQuery;
        private EntityQuery _directorQuery;
        private bool _queriesReady;
        private FleetcrawlMetaStore _store;
        private bool _storeLoaded;
        private int _lastRoomIndex = -1;
        private byte _lastRunCompleted;
        private bool _runCountRegistered;
        private string _savePath;

        private void Update()
        {
            if (!TryEnsureQueries())
            {
                return;
            }

            if (_scenarioQuery.IsEmptyIgnoreFilter || _directorQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var scenario = _scenarioQuery.GetSingleton<ScenarioInfo>();
            if (!Space4XFleetcrawlUiBridge.IsFleetcrawlScenario(scenario.ScenarioId))
            {
                return;
            }

            if (!_storeLoaded)
            {
                _storeLoaded = true;
                _store.TryLoadSave();
            }

            var directorEntity = _directorQuery.GetSingletonEntity();
            var director = _entityManager.GetComponentData<Space4XFleetcrawlDirectorState>(directorEntity);
            if (!_entityManager.HasBuffer<Space4XFleetcrawlRoom>(directorEntity))
            {
                return;
            }

            var rooms = _entityManager.GetBuffer<Space4XFleetcrawlRoom>(directorEntity);
            var hasInstalled = _entityManager.HasBuffer<Space4XRunInstalledBlueprint>(directorEntity);
            var installed = hasInstalled
                ? _entityManager.GetBuffer<Space4XRunInstalledBlueprint>(directorEntity)
                : default;

            var dirty = false;

            if (director.Initialized != 0 && !_runCountRegistered)
            {
                _runCountRegistered = true;
                _store.Data.run_count += 1;
                dirty = true;
            }

            if (_lastRoomIndex >= 0 && director.CurrentRoomIndex != _lastRoomIndex)
            {
                dirty |= HandleRoomCompleted(_lastRoomIndex, rooms, installed, hasInstalled);
            }

            if (director.RunCompleted != 0 && _lastRunCompleted == 0)
            {
                var finalRoomIndex = math.clamp(director.CurrentRoomIndex, 0, math.max(0, rooms.Length - 1));
                dirty |= HandleRoomCompleted(finalRoomIndex, rooms, installed, hasInstalled);
            }

            var currentRoomsCleared = math.max(0, director.CurrentRoomIndex + (director.RunCompleted != 0 ? 1 : 0));
            if (currentRoomsCleared > _store.Data.best_room)
            {
                _store.Data.best_room = currentRoomsCleared;
                _store.Data.best_digest = director.StableDigest;
                dirty = true;
            }
            else if (currentRoomsCleared == _store.Data.best_room && director.StableDigest != 0 && director.StableDigest != _store.Data.best_digest)
            {
                _store.Data.best_digest = director.StableDigest;
                dirty = true;
            }

            if (dirty && _store.WriteSave())
            {
                Debug.Log($"[FleetcrawlMeta] META save_written path={_savePath}.");
            }

            _lastRoomIndex = director.CurrentRoomIndex;
            _lastRunCompleted = director.RunCompleted;
        }

        private bool HandleRoomCompleted(int roomIndex, DynamicBuffer<Space4XFleetcrawlRoom> rooms, DynamicBuffer<Space4XRunInstalledBlueprint> installed, bool hasInstalled)
        {
            if (roomIndex < 0 || roomIndex >= rooms.Length)
            {
                return false;
            }

            var dirty = false;
            if (rooms[roomIndex].Kind == Space4XFleetcrawlRoomKind.Boss)
            {
                var starterId = $"starter_boss_{roomIndex:000}";
                if (_store.UnlockStarter(starterId))
                {
                    dirty = true;
                    Debug.Log($"[FleetcrawlMeta] META unlock_starter id={starterId}.");
                }
            }

            if (!hasInstalled)
            {
                return dirty;
            }

            for (var i = 0; i < installed.Length; i++)
            {
                var blueprintId = installed[i].BlueprintId.ToString();
                if (_store.UnlockBlueprint(blueprintId))
                {
                    dirty = true;
                }
            }

            return dirty;
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
            _savePath = ResolvePathFromEnv(SavePathEnv, DefaultSavePath);
            _store = new FleetcrawlMetaStore(_savePath);
            _queriesReady = true;
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
    }
}
