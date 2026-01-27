using PureDOTS.Runtime.Alignment;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Ships
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ComplianceBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<ComplianceThresholds>())
            {
                var entity = state.EntityManager.CreateEntity(typeof(ComplianceThresholds));
                state.EntityManager.SetComponentData(entity, ComplianceThresholds.CreateDefault());
            }
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
