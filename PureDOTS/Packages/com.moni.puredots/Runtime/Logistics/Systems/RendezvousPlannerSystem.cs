using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Plans rendezvous points for jobs with moving targets.
    /// Computes intercept points based on hauler and target movement.
    /// Only enabled when LogisticsTechProfile includes MovingTargetRendez.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct RendezvousPlannerSystem : ISystem
    {
        private ComponentLookup<LogisticsJob> _jobLookup;
        private ComponentLookup<MovementPlan> _movementPlanLookup;
        private ComponentLookup<LogisticsTechProfile> _techProfileLookup;
        private BufferLookup<WaypointElement> _waypointBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ScenarioState>();
            _jobLookup = state.GetComponentLookup<LogisticsJob>(false);
            _movementPlanLookup = state.GetComponentLookup<MovementPlan>(false);
            _techProfileLookup = state.GetComponentLookup<LogisticsTechProfile>(false);
            _waypointBufferLookup = state.GetBufferLookup<WaypointElement>(false);
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

            _jobLookup.Update(ref state);
            _movementPlanLookup.Update(ref state);
            _techProfileLookup.Update(ref state);
            _waypointBufferLookup.Update(ref state);

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var tick = tickTimeState.Tick;

            // Check if rendezvous is enabled (requires tech profile with MovingTargetRendez)
            bool rendezvousEnabled = false;
            foreach (var techProfile in SystemAPI.Query<RefRO<LogisticsTechProfile>>())
            {
                if ((techProfile.ValueRO.Features & LogisticsFeatures.MovingTargetRendez) != 0)
                {
                    rendezvousEnabled = true;
                    break;
                }
            }

            if (!rendezvousEnabled)
            {
                return;
            }

            // Process jobs that need rendezvous planning
            foreach (var (job, jobEntity) in SystemAPI.Query<RefRW<LogisticsJob>>()
                .WithEntityAccess())
            {
                var jobValue = job.ValueRO;

                // Only process jobs with Rendezvous or FollowEntity destination mode
                if (jobValue.DestMode != DestinationMode.Rendezvous && 
                    jobValue.DestMode != DestinationMode.FollowEntity)
                {
                    continue;
                }

                // Only process jobs that are Requested or Assigned (not yet in transit)
                if (jobValue.Status != LogisticsJobStatus.Requested && 
                    jobValue.Status != LogisticsJobStatus.Assigned)
                {
                    continue;
                }

                // Check if destination has a movement plan
                if (!_movementPlanLookup.TryGetComponent(jobValue.Destination, out var targetMovementPlan))
                {
                    // No movement plan, can't compute rendezvous
                    continue;
                }

                // Get origin position (simplified - in practice would get from entity position)
                // For now, assume origin is at (0,0,0) or get from entity if available
                float3 originPos = float3.zero;
                if (state.EntityManager.HasComponent<Unity.Transforms.LocalTransform>(jobValue.Origin))
                {
                    originPos = state.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(jobValue.Origin).Position;
                }

                // Get target current position
                float3 targetPos = float3.zero;
                if (state.EntityManager.HasComponent<Unity.Transforms.LocalTransform>(jobValue.Destination))
                {
                    targetPos = state.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(jobValue.Destination).Position;
                }

                // Compute rendezvous point using naive intercept math
                // Simplified: assume hauler travels at constant speed, target moves at constant speed
                // Solve for meeting point where hauler travel time ≈ target travel time
                ComputeRendezvousPoint(
                    in originPos,
                    in targetPos,
                    in targetMovementPlan.CurrentTarget,
                    targetMovementPlan.Speed,
                    5.0f, // Default hauler speed (should come from hauler stats)
                    out var rendezvousPoint);

                // Create or update waypoint buffer for the job
                // In practice, waypoints would be on the hauler entity, not the job
                // For now, this is a simplified approach - jobs would be assigned to haulers
                // and waypoints would be created on the hauler entity

                // Mark job as having rendezvous planned
                // In a full implementation, we'd create waypoints on the assigned hauler
            }
        }

        [BurstCompile]
        private static void ComputeRendezvousPoint(
            in float3 originPos,
            in float3 targetCurrentPos,
            in float3 targetDestination,
            float targetSpeed,
            float haulerSpeed,
            out float3 result)
        {
            // Naive intercept calculation
            // Vector from target current to destination
            float3 targetDirection = math.normalize(targetDestination - targetCurrentPos);
            float targetDistance = math.distance(targetCurrentPos, targetDestination);

            // Estimate time for target to reach destination
            float targetTime = targetDistance / math.max(targetSpeed, 0.1f);

            // Estimate hauler travel time to target destination
            float haulerDistance = math.distance(originPos, targetDestination);
            float haulerTime = haulerDistance / math.max(haulerSpeed, 0.1f);

            // If hauler is faster, intercept at target destination
            // Otherwise, intercept somewhere along target's path
            if (haulerTime <= targetTime)
            {
                result = targetDestination;
                return;
            }

            // Intercept along path
            // Simplified: intercept at midpoint of target's path
            float interceptRatio = 0.5f;
            result = targetCurrentPos + targetDirection * (targetDistance * interceptRatio);
        }
    }
}

