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
            state.RequireForUpdate<Space4XRenderCatalogSingleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<Space4XRenderCatalogSingleton>(out var catalog))
                return;

            if (catalog.Catalog.IsCreated)
            {
                catalog.Catalog.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
