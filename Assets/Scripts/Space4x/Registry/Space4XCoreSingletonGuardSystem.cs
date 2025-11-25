using PureDOTS.Systems;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures core PureDOTS singletons (TimeState, RewindState, registries, etc.) exist in the world before other systems run.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct Space4XCoreSingletonGuardSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Idempotent call that seeds time/rewind/registry singletons if the scene forgot to bake PureDotsConfigAuthoring.
            CoreSingletonBootstrapSystem.EnsureSingletons(state.EntityManager);
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
