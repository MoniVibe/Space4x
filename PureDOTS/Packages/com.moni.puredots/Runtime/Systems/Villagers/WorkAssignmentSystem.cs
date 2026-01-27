using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Villagers
{
    /// <summary>
    /// Converts WorkClaim → VillagerJobState (Navigate→Act→Complete).
    /// Increments WorkOffer.Taken when claims are processed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(WorkOfferClaimSystem))]
    public partial struct WorkAssignmentSystem : ISystem
    {
        private ComponentLookup<WorkOffer> _offerLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _offerLookup = state.GetComponentLookup<WorkOffer>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
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
            
            _offerLookup.Update(ref state);
            _transformLookup.Update(ref state);
            
            var currentTick = timeState.Tick;
            
            var job = new ProcessClaimsJob
            {
                CurrentTick = currentTick,
                OfferLookup = _offerLookup,
                TransformLookup = _transformLookup
            };
            
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
        
        [BurstCompile]
        public partial struct ProcessClaimsJob : IJobEntity
        {
            public uint CurrentTick;
            public ComponentLookup<WorkOffer> OfferLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            
            public void Execute(
                Entity villagerEntity,
                ref WorkClaim claim,
                ref VillagerJob job,
                ref VillagerAIState aiState)
            {
                // Check if offer still exists and is valid
                if (claim.Offer == Entity.Null || !OfferLookup.HasComponent(claim.Offer))
                {
                    // Offer was removed, clear claim
                    claim = default;
                    job.Phase = VillagerJob.JobPhase.Idle;
                    return;
                }
                
                var offer = OfferLookup[claim.Offer];
                
                // Check if offer has expired
                if (offer.ExpiresAtTick > 0 && CurrentTick >= offer.ExpiresAtTick)
                {
                    claim = default;
                    job.Phase = VillagerJob.JobPhase.Idle;
                    return;
                }
                
                // Check if offer has available slots
                if (offer.Taken >= offer.Slots)
                {
                    // No slots available, clear claim
                    claim = default;
                    job.Phase = VillagerJob.JobPhase.Idle;
                    return;
                }
                
                // Check if target still exists
                if (offer.Target == Entity.Null || !TransformLookup.HasComponent(offer.Target))
                {
                    claim = default;
                    job.Phase = VillagerJob.JobPhase.Idle;
                    return;
                }
                
                // Increment offer taken count (only once per claim)
                // Note: This increment is not fully atomic in parallel execution, but the Idle check
                // prevents double-increment for the same villager. For true atomicity, consider
                // using ECB or a sequential processing step.
                if (job.Phase == VillagerJob.JobPhase.Idle)
                {
                    // Double-check slot availability before incrementing (best-effort atomicity)
                    if (offer.Taken < offer.Slots)
                    {
                        offer.Taken++;
                        OfferLookup[claim.Offer] = offer;
                    }
                    else
                    {
                        // Slot was taken by another villager, clear claim
                        claim = default;
                        job.Phase = VillagerJob.JobPhase.Idle;
                        return;
                    }
                    
                    // Set job state
                    job.Type = (VillagerJob.JobType)offer.JobId; // Map job ID to job type
                    job.Phase = VillagerJob.JobPhase.Assigned;
                    job.LastStateChangeTick = CurrentTick;
                    
                    // Set AI state to navigate
                    aiState.CurrentGoal = VillagerAIState.Goal.Work;
                    aiState.CurrentState = VillagerAIState.State.Travelling;
                    aiState.TargetEntity = offer.Target;
                    aiState.StateStartTick = CurrentTick;
                    
                    // Get target position
                    if (TransformLookup.HasComponent(offer.Target))
                    {
                        aiState.TargetPosition = TransformLookup[offer.Target].Position;
                    }
                }
            }
        }
    }
}

