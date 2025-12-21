using Unity.Entities;

namespace Space4X.Runtime
{
    /// <summary>
    /// Creates the legacy scenario tag when explicitly enabled via environment flag.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct Space4XLegacyScenarioBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!Space4XLegacyScenarioGate.IsEnabled)
            {
                state.Enabled = false;
                return;
            }

            if (!SystemAPI.HasSingleton<LegacySpace4XScenarioTag>())
            {
                var entity = state.EntityManager.CreateEntity(typeof(LegacySpace4XScenarioTag));
                state.EntityManager.SetName(entity, "LegacySpace4XScenarioTag");
            }

            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state) { }
    }
}
