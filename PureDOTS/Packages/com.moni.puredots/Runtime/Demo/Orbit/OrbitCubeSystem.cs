#if PUREDOTS_SCENARIO && PUREDOTS_LEGACY_SCENARIO_ASM
using Unity.Burst;
using Unity.Entities;
using UnityEngine.Scripting.APIUpdating;

namespace PureDOTS.LegacyScenario.Orbit
{
    /// <summary>
    /// Legacy scenario orbit system.
    /// Disabled for Space4X because Space4X has its own
    /// debug spawner + orbit systems that use the correct
    /// RenderMeshArray / Entities Graphics APIs.
    /// </summary>
    [BurstCompile]
    [DisableAutoCreation] // make sure it never runs
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [MovedFrom(true, "PureDOTS.Demo.Orbit", null, "OrbitCubeSystem")]
    public partial struct OrbitCubeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            if (!LegacyScenarioGate.IsEnabled)
            {
                state.Enabled = false;
                return;
            }

            // Intentionally empty.
            // If you ever want to revive this legacy scenario, implement spawning/orbit here
            // using the current Entities Graphics APIs.
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Intentionally empty – no work done.
        }
    }
}
#endif
