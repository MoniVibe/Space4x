using PureDOTS.Runtime.Core;
using Unity.Entities;

namespace PureDOTS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct RuntimeModeBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            RuntimeMode.RefreshFromEnvironment();
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
