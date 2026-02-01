using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
#if UNITY_EDITOR
using Space4X.Debug;
#endif

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Ensures core PureDOTS singletons (TimeState, RewindState, registries, etc.) exist in the world before other systems run.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct Space4XCoreSingletonGuardSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            LogInfo("[Space4XCoreSingletonGuardSystem] OnCreate called. Checking singletons...");
            // Idempotent call that seeds time/rewind/registry singletons if the scene forgot to bake PureDotsConfigAuthoring.
            CoreSingletonBootstrapSystem.EnsureSingletons(state.EntityManager);
            LogInfo("[Space4XCoreSingletonGuardSystem] Core singletons ensured.");
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }

#if UNITY_EDITOR
        [BurstDiscard]
        private static void LogInfo(string message)
        {
            Space4XBurstDebug.Log(message);
        }
#else
        private static void LogInfo(string message) { }
#endif
    }
}
