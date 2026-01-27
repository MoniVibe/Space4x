using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Operations
{
    /// <summary>
    /// Executes blockade operations by clamping logistics routes (risk/delay/deny) based on operation severity and commander persona.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OperationInitSystem))]
    public partial struct BlockadeExecutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<OperationTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = SystemAPI.GetSingleton<TimeState>().Tick;

            // Process all active blockade operations
            foreach (var (operation, rules, progress, blockadeParams, participants, entity) in 
                SystemAPI.Query<RefRW<Operation>, RefRO<OperationRules>, RefRW<OperationProgress>, 
                    RefRO<BlockadeParams>, DynamicBuffer<OperationParticipant>>()
                .WithAll<OperationTag>()
                .WithEntityAccess())
            {
                if (operation.ValueRO.Kind != OperationKind.Blockade)
                    continue;

                if (operation.ValueRO.State != OperationState.Active)
                    continue;

                // Update progress
                progress.ValueRW.ElapsedTicks = currentTick - operation.ValueRO.StartedTick;
                operation.ValueRW.LastUpdateTick = currentTick;

                // Apply blockade effects to logistics routes
                ApplyBlockadeToLogistics(ref state, operation.ValueRO, blockadeParams.ValueRO, currentTick);

                // Update success metric based on blockade effectiveness
                UpdateBlockadeSuccessMetric(ref progress.ValueRW, blockadeParams.ValueRO, participants);

                // Check if blockade should resolve
                if (OperationHelpers.ShouldResolve(progress.ValueRO, rules.ValueRO))
                {
                    operation.ValueRW.State = OperationState.Resolving;
                }
            }
        }

        [BurstCompile]
        private void ApplyBlockadeToLogistics(
            ref SystemState state,
            Operation operation,
            BlockadeParams blockadeParams,
            uint currentTick)
        {
            // Find all logistics jobs that pass through the blockaded area
            // Simplified: check if job origin or destination is the target location
            foreach (var (job, jobEntity) in SystemAPI.Query<RefRW<LogisticsJob>>()
                .WithEntityAccess())
            {
                // Check if job passes through blockaded area
                bool passesThrough = job.ValueRO.Origin == operation.TargetLocation ||
                                     job.ValueRO.Destination == operation.TargetLocation;

                if (!passesThrough)
                    continue;

                // Check if job is blocked by blockade filters
                if (IsJobBlocked(job.ValueRO, operation, blockadeParams))
                {
                    // Hard deny - cancel or fail the job
                    if (job.ValueRO.Status == LogisticsJobStatus.InTransit ||
                        job.ValueRO.Status == LogisticsJobStatus.Assigned)
                    {
                        job.ValueRW.Status = LogisticsJobStatus.Failed;
                    }
                    continue;
                }

                // Apply risk and delay multipliers
                // Note: LogisticsJob doesn't have explicit risk/delay fields,
                // so we'd need to add a modifier component or modify the job priority
                // For now, we'll modify priority to simulate delay (lower priority = delayed)
                if (job.ValueRO.Priority < 255)
                {
                    // Increase priority (lower number = higher priority, so we increase the number)
                    int priorityIncrease = (int)(blockadeParams.DelayMultiplier * 10f);
                    job.ValueRW.Priority = (byte)math.min(255, job.ValueRO.Priority + priorityIncrease);
                }
            }
        }

        [BurstCompile]
        private bool IsJobBlocked(LogisticsJob job, Operation operation, BlockadeParams blockadeParams)
        {
            // Check hard deny threshold
            // Simplified: use job priority as proxy for risk (lower priority = higher risk)
            float jobRisk = 1f - (job.Priority / 255f);
            
            if (jobRisk > blockadeParams.HardDenyThreshold)
                return true;

            // Check blocked targets flags
            // Simplified: check if job origin/destination org matches blocked orgs
            // In production, check actual org affiliations
            if ((blockadeParams.BlockedTargets & 0xFFFF) == 0xFFFF)
                return false; // Everyone blocked, but we already checked threshold

            // Additional filtering logic would go here
            return false;
        }

        [BurstCompile]
        private void UpdateBlockadeSuccessMetric(
            ref OperationProgress progress,
            BlockadeParams blockadeParams,
            DynamicBuffer<OperationParticipant> participants)
        {
            // Success metric increases with:
            // - Number of participants (more coverage)
            // - Severity of blockade (harsher = more effective)
            // - Time elapsed (longer blockade = more disruption)

            float participantScore = math.clamp(participants.Length / 10f, 0f, 1f);
            float severityScore = blockadeParams.RiskMultiplier / 3f; // Normalize to 0-1
            float timeScore = math.clamp(progress.ElapsedTicks / 216000f, 0f, 1f); // 1 hour = full score

            progress.SuccessMetric = (participantScore * 0.3f + severityScore * 0.4f + timeScore * 0.3f);
        }
    }
}





