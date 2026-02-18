using PureDOTS.Runtime.Scenarios;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4x.Scenario
{
    [DisallowMultipleComponent]
    public sealed class Space4XFleetcrawlManualPickInjectorMono : MonoBehaviour
    {
        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _scenarioQuery;
        private EntityQuery _directorQuery;
        private bool _queriesReady;
        private Space4XFleetcrawlInputMode _inputMode;
        private bool _configLogged;
        private int _lastInjectedRoom = int.MinValue;

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

            if (!_configLogged)
            {
                _configLogged = true;
                Debug.Log($"[FleetcrawlInput] input_mode={_inputMode} env_gate=SPACE4X_FLEETCRAWL_PICK_GATE env_boon=SPACE4X_FLEETCRAWL_PICK_BOON.");
            }

            if (_inputMode != Space4XFleetcrawlInputMode.Manual)
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

            if (_lastInjectedRoom == roomIndex)
            {
                return;
            }

            var anyApplied = false;
            if (Space4XFleetcrawlUiBridge.TryReadPickIndex("SPACE4X_FLEETCRAWL_PICK_GATE", out var gatePick))
            {
                UpsertComponent(directorEntity, new Space4XRunPendingGatePick
                {
                    RoomIndex = roomIndex,
                    GateOrdinal = math.clamp(gatePick, 0, 2)
                });
                Space4XFleetcrawlUiBridge.ClearPickEnv("SPACE4X_FLEETCRAWL_PICK_GATE");
                anyApplied = true;
                Debug.Log($"[FleetcrawlInput] MANUAL_PICK room={roomIndex} kind=gate value={gatePick}.");
            }

            if (Space4XFleetcrawlUiBridge.TryReadPickIndex("SPACE4X_FLEETCRAWL_PICK_BOON", out var boonPick))
            {
                UpsertComponent(directorEntity, new Space4XRunPendingBoonPick
                {
                    RoomIndex = roomIndex,
                    OfferIndex = math.clamp(boonPick, 0, 2)
                });
                Space4XFleetcrawlUiBridge.ClearPickEnv("SPACE4X_FLEETCRAWL_PICK_BOON");
                anyApplied = true;
                Debug.Log($"[FleetcrawlInput] MANUAL_PICK room={roomIndex} kind=boon value={boonPick}.");
            }

            if (anyApplied)
            {
                _lastInjectedRoom = roomIndex;
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
            _inputMode = Space4XFleetcrawlUiBridge.ReadInputMode();
            _queriesReady = true;
            return true;
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
