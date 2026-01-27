using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Transport.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Systems
{
    /// <summary>
    /// Integrates hyperways as macro legs in logistics routes.
    /// When planning haul routes across systems, considers hyperway segments.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TransportLogisticsIntegrationSystem : ISystem
    {
        private ComponentLookup<LogisticsJob> _logisticsJobLookup;
        private ComponentLookup<WarpRelayNode> _nodeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _logisticsJobLookup = state.GetComponentLookup<LogisticsJob>(false);
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

            _logisticsJobLookup.Update(ref state);
            _nodeLookup.Update(ref state);

            // Process logistics jobs that might benefit from hyperway travel
            // Note: foreach not allowed in Burst, using SystemAPI.Query instead
            var jobQuery = SystemAPI.QueryBuilder().WithAll<LogisticsJob>().Build();
            var jobEntities = jobQuery.ToEntityArray(state.WorldUnmanaged.UpdateAllocator.ToAllocator);
            var jobComponents = jobQuery.ToComponentDataArray<LogisticsJob>(state.WorldUnmanaged.UpdateAllocator.ToAllocator);
            
            for (int i = 0; i < jobEntities.Length; i++)
            {
                var job = jobComponents[i];
                var jobEntity = jobEntities[i];
                
                // Only process jobs that are Requested or Assigned
                if (job.Status != LogisticsJobStatus.Requested && 
                    job.Status != LogisticsJobStatus.Assigned)
                {
                    continue;
                }

                // Check if origin and destination are in different systems
                // In practice, would check SystemId from entities
                // For now, simplified check

                // TODO: Find nodes based on entity positions/system IDs
                // For now, this is a placeholder that would:
                // 1. Get origin/destination system IDs
                // 2. Find nearest warp relay nodes in those systems
                // 3. Check if hyperway route exists and is better than free-flight
                // 4. If better, create WarpBooking for hauler instead of using own warp fuel

                // If hyperway route is better:
                // - Create WarpBooking for the hauler entity
                // - Modify logistics route to include hyperway segments
                // - Compare time/cost vs free-flight (fuel, time)
            }
            
            jobEntities.Dispose();
            jobComponents.Dispose();
        }
    }
}

