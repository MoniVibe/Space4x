using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Integration hooks for comms/knowledge system.
    /// Broadcasts MovementPlan and LogisticsJob as InfoPackets.
    /// This is a stub - actual integration will happen when comms system exists.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct LogisticsInfoIntegrationSystem : ISystem
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

            // TODO: When comms/knowledge system exists:
            // 1. Broadcast MovementPlan facts as InfoPackets
            // 2. Broadcast LogisticsJob creation/updates as InfoPackets
            // 3. Listen for InfoPackets about entity status (alive/destroyed)
            // 4. Update jobs based on received facts

            // For now, this is a placeholder that can be extended when the comms system is implemented
        }
    }
}

