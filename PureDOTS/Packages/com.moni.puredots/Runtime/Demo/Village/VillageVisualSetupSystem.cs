#if PUREDOTS_SCENARIO && PUREDOTS_LEGACY_SCENARIO_ASM
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Scripting.APIUpdating;

namespace PureDOTS.LegacyScenario.Village
{
    /// <summary>
    /// System that adds render components to legacy scenario village entities (homes, workplaces, villagers).
    /// Requires VillageWorldTag to be present in the world to run.
    /// Note: Visual assignment is handled by PureDOTS.LegacyScenario.Rendering.AssignVisualsSystem.
    /// This system is kept for compatibility but does not perform visual setup.
    /// </summary>
    [BurstCompile]
    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(VillageScenarioBootstrapSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [MovedFrom(true, "PureDOTS.Demo.Village", null, "VillageVisualSetupSystem")]
    public partial struct VillageVisualSetupSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!LegacyScenarioGate.IsEnabled)
            {
                state.Enabled = false;
                return;
            }

            // Visual assignment is handled by PureDOTS.LegacyScenario.Rendering.AssignVisualsSystem
            // which processes entities with VisualProfile component
            state.RequireForUpdate<VillageWorldTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Visual assignment is handled by PureDOTS.LegacyScenario.Rendering.AssignVisualsSystem
            // No work needed here
        }
    }
}
#endif
