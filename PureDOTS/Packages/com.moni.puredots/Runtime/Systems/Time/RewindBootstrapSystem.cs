using Unity.Burst;
using Unity.Entities;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Systems.Time
{
    /// <summary>
    /// Ensures a default RewindState singleton exists so systems relying on it can run without exceptions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct RewindBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // No runtime work; creation is handled in OnCreate.
        }
    }
}
