using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Demo
{
    /// <summary>
    /// Ensures DemoOptions, TimeState, and RewindState exist with sensible defaults for the demo.
    /// Mirrors shared demo bootstrap behavior across projects.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct DemoBootstrapEnsureOptionsSystem : ISystem
    {
        [BurstDiscard]
        static FixedString64Bytes FS(string s)
        {
            var f = default(FixedString64Bytes);
            for (int i = 0; i < s.Length; i++) f.Append(s[i]);
            return f;
        }

        public void OnCreate(ref SystemState s)
        {
            var em = s.EntityManager;
            if (!SystemAPI.TryGetSingleton<DemoOptions>(out _))
            {
#if SPACE4X_DEMO
                var e = em.CreateEntity(typeof(DemoOptions));
                em.SetComponentData(e, new DemoOptions { ScenarioPath = FS("Scenarios/space4x/combat_duel_weapons.json"), BindingsSet = 0 });
#endif
            }
            // TimeState is handled by CoreSingletonBootstrapSystem.
            if (!SystemAPI.TryGetSingleton<RewindState>(out _))
            {
                var r = em.CreateEntity(typeof(RewindState));
                em.SetComponentData(r, new RewindState { Mode = RewindMode.Record });
            }
        }
    }
}
