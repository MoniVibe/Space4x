using PureDOTS.Runtime.Transport;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Transport
{
    /// <summary>
    /// Ensures a default hauling policy exists so opportunistic hauling can run headless.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct HaulingPolicyBootstrapSystem : ISystem
    {
        [BurstDiscard]
        public void OnCreate(ref SystemState state)
        {
            var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<HaulingPolicyConfig>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = state.EntityManager.CreateEntity(typeof(HaulingPolicyConfig));
                state.EntityManager.SetComponentData(entity, HaulingPolicyConfig.Default);
            }
            query.Dispose();
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state) { }
    }
}





