using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Executes route plans by driving haulers along waypoints.
    /// Uses existing movement/pathing systems - logistics provides waypoints, movement executes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct RouteExecutionSystem : ISystem
    {
        private ComponentLookup<RoutePlan> _routePlanLookup;
        private BufferLookup<WaypointElement> _waypointBufferLookup;
        private ComponentLookup<LogisticsJob> _jobLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ScenarioState>();
            _routePlanLookup = state.GetComponentLookup<RoutePlan>(false);
            _waypointBufferLookup = state.GetBufferLookup<WaypointElement>(false);
            _jobLookup = state.GetComponentLookup<LogisticsJob>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario)
                || !scenario.IsInitialized
                || !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _routePlanLookup.Update(ref state);
            _waypointBufferLookup.Update(ref state);
            _jobLookup.Update(ref state);

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var tick = tickTimeState.Tick;

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            foreach (var (assignment, entity) in SystemAPI.Query<RefRW<HaulAssignment>>()
                .WithAll<HaulerTag>()
                .WithEntityAccess())
            {
                var assign = assignment.ValueRO;

                // Check if job still exists and is valid
                if (assign.JobId == 0)
                {
                    continue;
                }

                // Find the job entity (this is a simplified lookup - in practice, jobs might be stored differently)
                // For now, we'll assume jobs are entities with LogisticsJob component
                bool jobFound = false;
                Entity jobEntity = Entity.Null;

                foreach (var (jobRef, jobEnt) in SystemAPI.Query<RefRO<LogisticsJob>>().WithEntityAccess())
                {
                    if (jobRef.ValueRO.JobId == assign.JobId)
                    {
                        jobEntity = jobEnt;
                        jobFound = true;
                        break;
                    }
                }

                if (!jobFound)
                {
                    // Job no longer exists, clear assignment
                    assignment.ValueRW = default;
                    continue;
                }

                var job = _jobLookup[jobEntity];
                if (job.Status == LogisticsJobStatus.Completed || 
                    job.Status == LogisticsJobStatus.Failed || 
                    job.Status == LogisticsJobStatus.Cancelled)
                {
                    // Job is done, clear assignment
                    assignment.ValueRW = default;
                    continue;
                }

                // Get route plan and waypoints
                if (!_routePlanLookup.TryGetComponent(entity, out var routePlan) ||
                    !_waypointBufferLookup.HasBuffer(entity))
                {
                    // No route plan, stay idle
                    if (assign.State != HaulAssignmentState.Idle)
                    {
                        assignment.ValueRW.State = HaulAssignmentState.Idle;
                    }
                    continue;
                }

                var waypoints = _waypointBufferLookup[entity];
                if (waypoints.Length == 0)
                {
                    // No waypoints, stay idle
                    if (assign.State != HaulAssignmentState.Idle)
                    {
                        assignment.ValueRW.State = HaulAssignmentState.Idle;
                    }
                    continue;
                }

                // Check if we've reached the current waypoint
                // This is a stub - actual position checking would be done by movement systems
                // For now, we'll just advance waypoints based on time/distance estimates
                // In practice, movement systems would set a flag when waypoint is reached

                // Update job status to InTransit if we have a route
                if (job.Status == LogisticsJobStatus.Assigned && assign.State == HaulAssignmentState.EnRoute)
                {
                    var jobRef = _jobLookup.GetRefRW(jobEntity);
                    jobRef.ValueRW.Status = LogisticsJobStatus.InTransit;
                }

                // Check if we've completed all waypoints
                if (assign.CurrentWaypointIndex >= waypoints.Length)
                {
                    // Route complete - mark job as completed
                    var jobRef = _jobLookup.GetRefRW(jobEntity);
                    jobRef.ValueRW.Status = LogisticsJobStatus.Completed;
                    assignment.ValueRW = default;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

