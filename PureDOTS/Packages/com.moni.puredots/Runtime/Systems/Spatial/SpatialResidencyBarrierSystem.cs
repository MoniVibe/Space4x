using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Spatial
{
    /// <summary>
    /// Ensures all jobs mutating <see cref="PureDOTS.Runtime.Spatial.SpatialGridResidency"/> complete
    /// before downstream systems read residency data via lookups.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(SpatialResidencyVersionSystem))]
    public partial struct SpatialResidencyBarrierSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
        }
    }
}
