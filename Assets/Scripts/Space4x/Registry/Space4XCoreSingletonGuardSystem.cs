using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures core PureDOTS singletons (TimeState, RewindState, registries, etc.) exist in the world before other systems run.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct Space4XCoreSingletonGuardSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            UnityEngine.Debug.Log("[Space4XCoreSingletonGuardSystem] OnCreate called. Checking singletons...");
            // Idempotent call that seeds time/rewind/registry singletons if the scene forgot to bake PureDotsConfigAuthoring.
            CoreSingletonBootstrapSystem.EnsureSingletons(state.EntityManager);
#if UNITY_EDITOR
            UnityEngine.Debug.Log("[Space4XCoreSingletonGuardSystem] Core singletons ensured.");
#endif
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
