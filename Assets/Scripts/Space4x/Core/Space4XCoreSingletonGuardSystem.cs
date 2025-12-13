using Unity.Entities;
using UnityEngine;
using Space4X.Rendering.Systems;

namespace Space4X.Core
{
    using Debug = UnityEngine.Debug;

    
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XCoreSingletonGuardSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (state.WorldUnmanaged.Name != "Game World")
            {
                state.Enabled = false;
                return;
            }
            // Force creation of ApplyRenderCatalogSystem if it doesn't exist
            if (!state.WorldUnmanaged.GetExistingSystemState<ApplyRenderCatalogSystem>().Enabled)
            {
                // Note: This doesn't actually create it if it's not in the world, 
                // but we can log if it's missing.
                // To force add, we'd need to use World.GetOrCreateSystem, but that's managed.
                // For unmanaged systems, we rely on bootstrap.
                
                // Let's just log for now.
                Debug.Log("[Space4XCoreSingletonGuardSystem] Checking for ApplyRenderCatalogSystem...");
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false;
        }
    }
}
