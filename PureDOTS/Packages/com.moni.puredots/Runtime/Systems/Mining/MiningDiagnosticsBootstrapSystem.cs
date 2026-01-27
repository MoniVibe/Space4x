using PureDOTS.Runtime.Mining;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Mining
{
    /// <summary>
    /// Bootstrap system that ensures MiningDiagnostics singleton exists in editor builds.
    /// </summary>
#if UNITY_EDITOR
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    // Removed invalid UpdateBefore: the target was an ECB singleton, not a system; default initialization order is sufficient.
    public partial struct MiningDiagnosticsBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Only create diagnostics in editor builds
            if (!SystemAPI.HasSingleton<MiningDiagnostics>())
            {
                var diagnosticsEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(diagnosticsEntity, new MiningDiagnostics
                {
                    ActiveSessionCount = 0,
                    MinedPerSecond = 0f,
                    InvalidSourceResets = 0,
                    InvalidCarrierResets = 0,
                    PhysicsDisruptionResets = 0,
                    TotalMined = 0f,
                    TimeAccumulator = 0f,
                    LastUpdateTick = 0
                });

                state.EntityManager.AddComponentData(diagnosticsEntity, new MiningDiagnosticsConfig
                {
                    Enabled = true,
                    UpdateIntervalSeconds = 1f
                });
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Bootstrap only runs once
            state.Enabled = false;
        }
    }
#endif
}

























