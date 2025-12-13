using Unity.Burst;
using Unity.Entities;

namespace Space4X.Rendering.Systems
{
    /// <summary>
    /// Ensures the render catalog blob allocated with Allocator.Persistent is released when the world shuts down.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct Space4XRenderCatalogLifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            if (state.WorldUnmanaged.Name != "Game World")
            {
                state.Enabled = false;
                return;
            }
            state.RequireForUpdate<Space4XRenderCatalogSingleton>();
        }

        public void OnDestroy(ref SystemState state)
        {
            // Do NOT dispose Space4XRenderCatalogSingleton.Catalog here.
            // Baked/deserialized blobs are auto-released on scene unload.
            // Runtime-created blobs are already disposed by RenderCatalogAuthoring.CleanupRuntimeCatalog().
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
