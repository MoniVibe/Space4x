using System;
using PureDOTS.Runtime.Scenarios;
using Unity.Entities;
using UnityEngine;

namespace Space4x.Scenario
{
    [DisallowMultipleComponent]
    public sealed class Space4XFleetcrawlStarterLoadoutOverrideMono : MonoBehaviour
    {
        private const string StartWeaponEnv = "SPACE4X_FLEETCRAWL_START_WEAPON";
        private const string StartReactorEnv = "SPACE4X_FLEETCRAWL_START_REACTOR";
        private const string StartHangarEnv = "SPACE4X_FLEETCRAWL_START_HANGAR";

        private const string DefaultWeapon = "weapon_laser_prismworks_coreA_lensBeam";
        private const string DefaultReactor = "reactor_prismworks_coreA_coolingStable";
        private const string DefaultHangar = "hangar_prismworks_guidanceDroneLink_lensBeam";

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _scenarioQuery;
        private EntityQuery _directorQuery;
        private bool _queriesReady;
        private bool _applied;

        private void Update()
        {
            if (!TryEnsureQueries())
            {
                return;
            }

            if (_applied || _scenarioQuery.IsEmptyIgnoreFilter || _directorQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var scenario = _scenarioQuery.GetSingleton<ScenarioInfo>();
            if (!Space4XFleetcrawlUiBridge.IsFleetcrawlScenario(scenario.ScenarioId))
            {
                return;
            }

            var directorEntity = _directorQuery.GetSingletonEntity();
            if (!_entityManager.HasBuffer<Space4XRunInstalledBlueprint>(directorEntity))
            {
                return;
            }

            var installed = _entityManager.GetBuffer<Space4XRunInstalledBlueprint>(directorEntity);
            var weaponId = ResolveEnvOrDefault(StartWeaponEnv, DefaultWeapon);
            var reactorId = ResolveEnvOrDefault(StartReactorEnv, DefaultReactor);
            var hangarId = ResolveEnvOrDefault(StartHangarEnv, DefaultHangar);

            TryApplyById(installed, weaponId, Space4XRunBlueprintKind.Weapon);
            TryApplyById(installed, reactorId, Space4XRunBlueprintKind.Reactor);
            TryApplyById(installed, hangarId, Space4XRunBlueprintKind.Hangar);

            _applied = true;
            Debug.Log($"[FleetcrawlInput] START loadout weapon={FindBlueprintId(installed, Space4XRunBlueprintKind.Weapon)} reactor={FindBlueprintId(installed, Space4XRunBlueprintKind.Reactor)} hangar={FindBlueprintId(installed, Space4XRunBlueprintKind.Hangar)}.");
        }

        private static string ResolveEnvOrDefault(string envName, string fallback)
        {
            var raw = Environment.GetEnvironmentVariable(envName);
            return string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();
        }

        private static string FindBlueprintId(DynamicBuffer<Space4XRunInstalledBlueprint> installed, Space4XRunBlueprintKind kind)
        {
            for (var i = 0; i < installed.Length; i++)
            {
                if (installed[i].Kind == kind)
                {
                    return installed[i].BlueprintId.ToString();
                }
            }

            return "<none>";
        }

        private static void TryApplyById(DynamicBuffer<Space4XRunInstalledBlueprint> installed, string blueprintId, Space4XRunBlueprintKind expectedKind)
        {
            if (!Space4XFleetcrawlUiBridge.TryResolveBlueprintDefinition(blueprintId, out var definition))
            {
                return;
            }

            if (definition.Kind != expectedKind)
            {
                return;
            }

            var next = Space4XFleetcrawlUiBridge.ToInstalledBlueprint(definition, version: 1);
            for (var i = 0; i < installed.Length; i++)
            {
                if (installed[i].Kind == expectedKind)
                {
                    installed[i] = next;
                    return;
                }
            }

            installed.Add(next);
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
            _queriesReady = true;
            return true;
        }
    }
}
