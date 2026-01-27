using PureDOTS.Runtime;
using PureDOTS.Runtime.Miracles;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Bootstrap system that initializes the ScenarioState singleton.
    /// Runs in InitializationSystemGroup to ensure scenario state is available before other systems.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ScenarioBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<ScenarioState>())
            {
                var entity = state.EntityManager.CreateEntity(typeof(ScenarioState));
                state.EntityManager.SetComponentData(entity, new ScenarioState
                {
                    Current = ScenarioKind.AllSystemsShowcase,
                    IsInitialized = false,
                    BootPhase = ScenarioBootPhase.None,
                    EnableGodgame = true,
                    EnableSpace4x = true
                });
            }

            // Bootstrap GodPool singleton
            if (!SystemAPI.HasSingleton<GodPool>())
            {
                var poolEntity = state.EntityManager.CreateEntity(typeof(GodPool));
                state.EntityManager.SetComponentData(poolEntity, new GodPool
                {
                    Essence = 0f
                });
            }

            // Bootstrap MiracleSelection singleton
            if (!SystemAPI.HasSingleton<MiracleSelection>())
            {
                var selectionEntity = state.EntityManager.CreateEntity(typeof(MiracleSelection));
                state.EntityManager.SetComponentData(selectionEntity, new MiracleSelection
                {
                    SelectedMiracleId = (int)MiracleId.None
                });
            }
        }

        public void OnUpdate(ref SystemState state) { }
    }
}



