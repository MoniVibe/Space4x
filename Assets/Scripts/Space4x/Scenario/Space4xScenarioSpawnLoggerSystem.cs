using PureDOTS.Runtime.Scenarios;
using Unity.Entities;
using UnityEngine;

namespace Space4x.Scenario
{
    /// <summary>
    /// Temporary hook to surface ScenarioRunner entity counts inside Space4x.
    /// Replace with actual spawn/bootstrap wiring using Space4x registries.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4xScenarioSpawnLoggerSystem : ISystem
    {
        private bool _logOnce;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<ScenarioEntityCountElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_logOnce)
            {
                state.Enabled = false;
                return;
            }

            _logOnce = true;
            var info = SystemAPI.GetSingleton<ScenarioInfo>();
            var counts = SystemAPI.GetSingletonBuffer<ScenarioEntityCountElement>();

            Debug.Log($"[Space4x ScenarioRunner] Scenario={info.ScenarioId} seed={info.Seed} ticks={info.RunTicks} entries={counts.Length}. TODO: wire spawns to registries (see Docs/PureDOTS_ScenarioRunner_Wiring.md).");

            for (int i = 0; i < counts.Length; i++)
            {
                var entry = counts[i];
                Debug.Log($"[Space4x ScenarioRunner] RegistryId={entry.RegistryId} Count={entry.Count}");
            }

            state.Enabled = false;
        }
    }
}
