using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Logistics manager system.
    /// Operates only on KnownFacts (not ground truth) from comms/knowledge system.
    /// Creates and updates logistics jobs based on stale information.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct LogisticsManagerSystem : ISystem
    {
        private ComponentLookup<LogisticsJob> _jobLookup;
        private ComponentLookup<MovementPlan> _movementPlanLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _jobLookup = state.GetComponentLookup<LogisticsJob>(false);
            _movementPlanLookup = state.GetComponentLookup<MovementPlan>(false);
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

            _jobLookup.Update(ref state);
            _movementPlanLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Process jobs based on KnownFacts (stale information)
            // This enables scenarios like:
            // - Wagons resupplying dead armies (no destruction fact arrived)
            // - Freighters arriving at dead colonies
            // - Fleets defending supply lines for fallen empires

            foreach (var (job, jobEntity) in SystemAPI.Query<RefRW<LogisticsJob>>()
                .WithEntityAccess())
            {
                var jobValue = job.ValueRO;

                // Check if destination entity still exists (ground truth check)
                // In a full implementation, we'd check KnownFacts instead
                if (!state.EntityManager.Exists(jobValue.Destination))
                {
                    // Destination doesn't exist - cancel job if we have that fact
                    // For now, we'll cancel immediately (in practice, would check KnownFacts)
                    if (jobValue.Status == LogisticsJobStatus.Requested || 
                        jobValue.Status == LogisticsJobStatus.Assigned ||
                        jobValue.Status == LogisticsJobStatus.InTransit)
                    {
                        job.ValueRW.Status = LogisticsJobStatus.Cancelled;
                    }
                    continue;
                }

                // Check if we have stale movement plan information
                // In practice, this would come from KnownFacts, not direct component access
                if (_movementPlanLookup.TryGetComponent(jobValue.Destination, out var movementPlan))
                {
                    // Update job based on movement plan if needed
                    // This is where stale information would be used
                    // For now, this is a placeholder
                }

                // TODO: When comms/knowledge system exists:
                // 1. Query KnownFacts for entity status (alive/destroyed)
                // 2. Query KnownFacts for MovementPlan at time-of-issue
                // 3. Only cancel/redirect jobs when facts indicate destruction
                // 4. Use stale MovementPlan facts for rendezvous planning
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

