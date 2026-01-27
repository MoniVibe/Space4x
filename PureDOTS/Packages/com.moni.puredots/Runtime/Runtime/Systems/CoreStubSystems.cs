using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Stub system for climate state updates. Provides an anchor point for systems that need to run after climate state is updated.
    /// NOTE: UpdateInGroup disabled to avoid Runtime→Systems circular dependency. Systems assembly has the real version.
    /// </summary>
    [BurstCompile]
    // [UpdateInGroup(typeof(EnvironmentSystemGroup))] // Disabled: Runtime can't reference Systems
    public partial struct ClimateStateUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnDestroy(ref SystemState state) { }

        public void OnUpdate(ref SystemState state) { }
    }

    /// <summary>
    /// Stub system for biome derivation. Provides an anchor point for systems that need to run after biome classification.
    /// NOTE: UpdateInGroup disabled to avoid Runtime→Systems circular dependency. Systems assembly has the real version.
    /// </summary>
    [BurstCompile]
    // [UpdateInGroup(typeof(EnvironmentSystemGroup))] // Disabled: Runtime can't reference Systems
    public partial struct BiomeDerivationSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnDestroy(ref SystemState state) { }

        public void OnUpdate(ref SystemState state) { }
    }
}

