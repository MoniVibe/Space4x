using PureDOTS.Runtime.Core;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Manages application of valve profiles to the simulation valve singleton.
    /// Profiles can be loaded from ScriptableObjects or JSON and applied at runtime.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    // Removed invalid UpdateAfter: CoreSingletonBootstrapSystem runs in TimeSystemGroup; cross-group order must be set at group composition.
    public partial struct ValveProfileManagerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationFeatureFlags>();
            state.RequireForUpdate<SimulationScalars>();
            state.RequireForUpdate<SimulationOverrides>();
            state.RequireForUpdate<SimulationSandboxFlags>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Profile application is command-driven (via commands/buffers in future)
            // For now, this system exists as a skeleton for future profile loading/application
            // Systems can directly modify the singleton components, or send commands here
        }

        /// <summary>
        /// Applies a profile to the simulation valve singleton.
        /// Called by external systems or commands.
        /// </summary>
        public void ApplyProfile(ref SystemState state, in SimulationValveProfile profile)
        {
            var valveEntity = SystemAPI.GetSingletonEntity<SimulationFeatureFlags>();
            
            SystemAPI.SetComponent<SimulationFeatureFlags>(valveEntity, profile.FeatureFlags);
            SystemAPI.SetComponent<SimulationScalars>(valveEntity, profile.Scalars);
            SystemAPI.SetComponent<SimulationOverrides>(valveEntity, profile.Overrides);
            SystemAPI.SetComponent<SimulationSandboxFlags>(valveEntity, profile.SandboxFlags);
        }

        /// <summary>
        /// Gets the current valve state as a profile.
        /// Useful for saving current configuration.
        /// </summary>
        public SimulationValveProfile GetCurrentProfile(ref SystemState state)
        {
            var features = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            var scalars = SystemAPI.GetSingleton<SimulationScalars>();
            var overrides = SystemAPI.GetSingleton<SimulationOverrides>();
            var sandbox = SystemAPI.GetSingleton<SimulationSandboxFlags>();

            return new SimulationValveProfile
            {
                Name = new Unity.Collections.FixedString64Bytes("Current"),
                FeatureFlags = features,
                Scalars = scalars,
                Overrides = overrides,
                SandboxFlags = sandbox
            };
        }
    }
}

