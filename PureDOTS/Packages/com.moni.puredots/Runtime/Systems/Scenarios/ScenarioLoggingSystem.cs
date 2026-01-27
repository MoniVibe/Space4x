using PureDOTS.Runtime.Scenarios;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems.Scenarios
{
    /// <summary>
    /// System that logs scenario startup information including scenario ID and entity counts.
    /// Runs once during initialization to report scenario metadata.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    // Removed invalid UpdateAfter: CoreSingletonBootstrapSystem lives in TimeSystemGroup, so cross-group ordering must be handled at the group level.
    public partial struct ScenarioLoggingSystem : ISystem
    {
        private EntityQuery _scenarioQuery;
        private bool hasLogged;

        public void OnCreate(ref SystemState state)
        {
            _scenarioQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<ScenarioInfo>(),
                ComponentType.ReadOnly<ScenarioEntityCountElement>()
            );
            hasLogged = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (hasLogged)
            {
                state.Enabled = false;
                return;
            }

            if (!SystemAPI.TryGetSingleton<ScenarioInfo>(out var scenarioInfo))
            {
                // No scenario info present, disable this system
                state.Enabled = false;
                return;
            }

            if (_scenarioQuery.IsEmpty)
            {
                state.Enabled = false;
                return;
            }

            var scenarioEntity = _scenarioQuery.GetSingletonEntity();
            var buffer = SystemAPI.GetBuffer<ScenarioEntityCountElement>(scenarioEntity);

            int totalEntities = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                totalEntities += buffer[i].Count;
            }

            // Log scenario info
            UnityEngine.Debug.Log($"[ScenarioLoggingSystem] Scenario '{scenarioInfo.ScenarioId}' started, {totalEntities} entities created (seed={scenarioInfo.Seed}, ticks={scenarioInfo.RunTicks})");

            hasLogged = true;
            state.Enabled = false;
        }
    }
}
