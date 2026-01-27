using PureDOTS.Config;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Villagers
{
    /// <summary>
    /// Updates villager job state machine to handle WorkClaim → Navigate → Act → Deliver phases.
    /// Integrates with existing VillagerJobExecutionSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(WorkAssignmentSystem))]
    public partial struct WorkClaimExecutionSystem : ISystem
    {
        private ComponentLookup<WorkOffer> _offerLookup;
        private ComponentLookup<WorkClaim> _claimLookup;
        private ComponentLookup<ResourceSourceState> _resourceStateLookup;
        private ComponentLookup<StorehouseInventory> _storehouseLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _offerLookup = state.GetComponentLookup<WorkOffer>(false);
            _claimLookup = state.GetComponentLookup<WorkClaim>(true);
            _resourceStateLookup = state.GetComponentLookup<ResourceSourceState>(true);
            _storehouseLookup = state.GetComponentLookup<StorehouseInventory>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<JobDefinitionCatalogComponent>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }
            
            if (!SystemAPI.TryGetSingleton<JobDefinitionCatalogComponent>(out var jobCatalog))
            {
                return;
            }
            
            _offerLookup.Update(ref state);
            _claimLookup.Update(ref state);
            _resourceStateLookup.Update(ref state);
            _storehouseLookup.Update(ref state);
            _transformLookup.Update(ref state);
            
            var currentTick = timeState.Tick;
            var deltaTime = timeState.FixedDeltaTime;
            var jobBlob = jobCatalog.Catalog;
            
            var job = new ExecuteClaimsJob
            {
                OfferLookup = _offerLookup,
                ResourceStateLookup = _resourceStateLookup,
                StorehouseLookup = _storehouseLookup,
                TransformLookup = _transformLookup,
                JobCatalog = jobBlob,
                CurrentTick = currentTick,
                DeltaTime = deltaTime
            };
            
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
        
        [BurstCompile]
        public partial struct ExecuteClaimsJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public ComponentLookup<WorkOffer> OfferLookup;
            [ReadOnly] public ComponentLookup<ResourceSourceState> ResourceStateLookup;
            [ReadOnly] public ComponentLookup<StorehouseInventory> StorehouseLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public BlobAssetReference<JobDefinitionCatalogBlob> JobCatalog;
            public uint CurrentTick;
            public float DeltaTime;
            
            public void Execute(
                Entity villagerEntity,
                ref WorkClaim claim,
                ref VillagerJob job,
                ref VillagerAIState aiState,
                in LocalTransform transform)
            {
                if (claim.Offer == Entity.Null || !OfferLookup.HasComponent(claim.Offer))
                {
                    // No claim or offer removed
                    if (job.Phase != VillagerJob.JobPhase.Idle)
                    {
                        job.Phase = VillagerJob.JobPhase.Idle;
                        job.Type = VillagerJob.JobType.None;
                        aiState.CurrentState = VillagerAIState.State.Idle;
                        aiState.CurrentGoal = VillagerAIState.Goal.None;
                    }
                    return;
                }
                
                var offer = OfferLookup[claim.Offer];
                
                // Check if target still exists
                if (offer.Target == Entity.Null || !TransformLookup.HasComponent(offer.Target))
                {
                    ReleaseOffer(ref claim, ref job, ref aiState);
                    return;
                }
                
                var targetPos = TransformLookup[offer.Target].Position;
                var distanceSq = math.distancesq(transform.Position, targetPos);
                var actDistanceSq = 9f; // 3 units for gathering/acting
                
                // Get job definition
                if (!JobCatalog.IsCreated || offer.JobId < 0 || offer.JobId >= JobCatalog.Value.Jobs.Length)
                {
                    return;
                }
                
                ref var jobDef = ref JobCatalog.Value.GetJob(offer.JobId);
                
                // State machine: Assigned → Gathering/Acting → Delivering → Completed
                switch (job.Phase)
                {
                    case VillagerJob.JobPhase.Assigned:
                        // Check if reached target
                        if (distanceSq <= actDistanceSq)
                        {
                            // Transition to Gathering/Acting
                            job.Phase = VillagerJob.JobPhase.Gathering;
                            job.LastStateChangeTick = CurrentTick;
                            aiState.CurrentState = VillagerAIState.State.Working;
                            aiState.StateTimer = 0f;
                            aiState.StateStartTick = CurrentTick;
                        }
                        else
                        {
                            // Still navigating
                            aiState.CurrentState = VillagerAIState.State.Travelling;
                            aiState.TargetPosition = targetPos;
                        }
                        break;
                        
                    case VillagerJob.JobPhase.Gathering:
                        // Act on the job
                        if (distanceSq > actDistanceSq)
                        {
                            // Moved away, go back to navigating
                            job.Phase = VillagerJob.JobPhase.Assigned;
                            aiState.CurrentState = VillagerAIState.State.Travelling;
                            break;
                        }
                        
                        // Progress the job
                        aiState.StateTimer += DeltaTime;
                        
                        // Check if job is complete (simplified: based on duration)
                        if (aiState.StateTimer >= jobDef.BaseDurationSeconds)
                        {
                            // Transition to Delivering
                            job.Phase = VillagerJob.JobPhase.Delivering;
                            job.LastStateChangeTick = CurrentTick;
                            aiState.StateTimer = 0f;
                            
                            // Find nearest storehouse for delivery (simplified)
                            // In full implementation, this would use StorehouseRegistry
                            aiState.CurrentState = VillagerAIState.State.Travelling;
                        }
                        break;
                        
                    case VillagerJob.JobPhase.Delivering:
                        // Find storehouse and deliver
                        // Simplified: mark as completed after delivery
                        job.Phase = VillagerJob.JobPhase.Completed;
                        job.LastStateChangeTick = CurrentTick;
                        aiState.CurrentState = VillagerAIState.State.Idle;
                        aiState.CurrentGoal = VillagerAIState.Goal.None;
                        
                        // Release claim + expire offer so we don't loop the same task
                        CompleteOffer(ref claim);
                        break;
                        
                    case VillagerJob.JobPhase.Completed:
                    case VillagerJob.JobPhase.Idle:
                        // Reset for next job
                        job.Phase = VillagerJob.JobPhase.Idle;
                        job.Type = VillagerJob.JobType.None;
                        CompleteOffer(ref claim);
                        break;
                }
            }

            private void ReleaseOffer(ref WorkClaim claim, ref VillagerJob job, ref VillagerAIState aiState)
            {
                if (claim.Offer != Entity.Null && OfferLookup.HasComponent(claim.Offer))
                {
                    var offer = OfferLookup[claim.Offer];
                    if (offer.Taken > 0)
                    {
                        offer.Taken--;
                    }
                    OfferLookup[claim.Offer] = offer;
                }

                claim = default;
                job.Phase = VillagerJob.JobPhase.Idle;
                job.Type = VillagerJob.JobType.None;
                aiState.CurrentState = VillagerAIState.State.Idle;
                aiState.CurrentGoal = VillagerAIState.Goal.None;
            }

            private void CompleteOffer(ref WorkClaim claim)
            {
                if (claim.Offer != Entity.Null && OfferLookup.HasComponent(claim.Offer))
                {
                    var offer = OfferLookup[claim.Offer];
                    if (offer.Taken > 0)
                    {
                        offer.Taken--;
                    }
                    offer.ExpiresAtTick = math.max(offer.ExpiresAtTick, CurrentTick + 30u);
                    OfferLookup[claim.Offer] = offer;
                }

                claim = default;
            }
        }
    }
}
