using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Transport.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Systems
{
    /// <summary>
    /// Maintains registry of active warp relay nodes and validates network integrity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WarpRelayRegistrySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Validate that warp relay nodes have required components
            foreach (var (node, entity) in SystemAPI.Query<RefRO<WarpRelayNode>>()
                .WithAll<WarpRelayNodeTag>()
                .WithEntityAccess())
            {
                // Ensure platform has WarpRelay flag
                if (state.EntityManager.HasComponent<PlatformKind>(node.ValueRO.Platform))
                {
                    var platformKind = state.EntityManager.GetComponentData<PlatformKind>(node.ValueRO.Platform);
                    if ((platformKind.Flags & PlatformFlags.WarpRelay) == 0)
                    {
                        // Add flag if missing (or log warning in debug)
                        var platformKindRef = state.EntityManager.GetComponentData<PlatformKind>(node.ValueRO.Platform);
                        platformKindRef.Flags |= PlatformFlags.WarpRelay;
                        state.EntityManager.SetComponentData(node.ValueRO.Platform, platformKindRef);
                    }
                }

                // Validate node has drive bank
                if (!state.EntityManager.HasComponent<WarpRelayDriveBank>(entity))
                {
                    // Node without drive bank - could be invalid or still being constructed
                    // In practice, might want to log or mark as invalid
                }
            }
        }
    }
}

