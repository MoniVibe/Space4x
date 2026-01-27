using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Transport.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Systems
{
    /// <summary>
    /// Integrates with comms/info system.
    /// Broadcasts node status changes as InfoPackets.
    /// Listens for status updates to enable stale knowledge scenarios.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TransportInfoIntegrationSystem : ISystem
    {
        private ComponentLookup<WarpRelayNode> _nodeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _nodeLookup = state.GetComponentLookup<WarpRelayNode>(false);
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

            _nodeLookup.Update(ref state);

            // Track node status changes and broadcast as InfoPackets
            // TODO: When comms/info system exists:
            // 1. Detect node status changes (Online → Destroyed, Online → Captured, etc.)
            // 2. Generate InfoPacket with node status fact
            // 3. Route planning systems use KnownFacts (not ground truth)
            // 4. Enables scenarios:
            //    - Ships waiting at destroyed nodes (no destruction fact arrived)
            //    - Remote factions only see "went silent" (no last message)
            //    - Re-routing after news arrives (InfoPacket with Destroyed fact)

            // For now, this is a placeholder that can be extended when the comms system is implemented
            foreach (var (node, nodeEntity) in SystemAPI.Query<RefRO<WarpRelayNode>>()
                .WithAll<WarpRelayNodeTag>()
                .WithEntityAccess())
            {
                var nodeValue = node.ValueRO;

                // Check for status changes that should be broadcast
                // In practice, would compare current status with last known status from KnownFacts
                // For now, just track that status changes should generate InfoPackets

                if (nodeValue.Status == WarpRelayNodeStatus.Destroyed ||
                    nodeValue.Status == WarpRelayNodeStatus.Captured)
                {
                    // These status changes should generate InfoPackets
                    // TODO: Generate InfoPacket with fact: NodeStatus(NodeId, Status, Tick)
                }
            }
        }
    }
}

