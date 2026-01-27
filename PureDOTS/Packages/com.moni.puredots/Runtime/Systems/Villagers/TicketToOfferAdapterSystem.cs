using PureDOTS.Config;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villager;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Villagers
{
    /// <summary>
    /// Temporary adapter system that converts legacy VillagerJobTicket components to WorkOffer entities.
    /// This allows the new WorkOffer system to coexist with the legacy ticket system during transition.
    /// 
    /// Cutover rule: Once all producers use WorkOffer directly, this adapter can be deleted.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateBefore(typeof(WorkOfferBuildSystem))]
    public partial struct TicketToOfferAdapterSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VillagerJobTicket> _ticketLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _ticketLookup = state.GetComponentLookup<VillagerJobTicket>(true);
            
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
            
            _transformLookup.Update(ref state);
            _ticketLookup.Update(ref state);
            
            var currentTick = timeState.Tick;
            var jobBlob = jobCatalog.Catalog;
            
            // Remove existing offers created from tickets (to ensure idempotency)
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var ecbWriter = ecb.AsParallelWriter();
            var removeOldOffersJob = new RemoveOldTicketOffersJob
            {
                Ecb = ecbWriter
            };
            state.Dependency = removeOldOffersJob.ScheduleParallel(state.Dependency);
            
            // Convert tickets to offers
            var convertJob = new ConvertTicketsToOffersJob
            {
                TransformLookup = _transformLookup,
                TicketLookup = _ticketLookup,
                JobCatalog = jobBlob,
                CurrentTick = currentTick,
                Ecb = ecbWriter
            };
            state.Dependency = convertJob.ScheduleParallel(state.Dependency);
            
            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
        
        /// <summary>
        /// Removes WorkOffer entities that were created from tickets in previous frames.
        /// Uses a tag component to identify ticket-originated offers.
        /// </summary>
        [BurstCompile]
        public partial struct RemoveOldTicketOffersJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            
            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, in TicketOriginatedOfferTag tag)
            {
                Ecb.DestroyEntity(chunkIndex, entity);
            }
        }
        
        /// <summary>
        /// Converts VillagerJobTicket components to WorkOffer entities.
        /// Creates a 1:1 mapping: each ticket becomes one offer.
        /// </summary>
        [BurstCompile]
        public partial struct ConvertTicketsToOffersJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<VillagerJobTicket> TicketLookup;
            [ReadOnly] public BlobAssetReference<JobDefinitionCatalogBlob> JobCatalog;
            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;
            
            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity villagerEntity, in VillagerJob job)
            {
                // Only convert tickets for villagers with active jobs
                if (job.Type == VillagerJob.JobType.None || job.Phase == VillagerJob.JobPhase.Idle)
                {
                    return;
                }
                
                if (!TicketLookup.HasComponent(villagerEntity))
                {
                    return;
                }
                
                var ticket = TicketLookup[villagerEntity];
                
                // Skip if ticket doesn't have a valid resource entity
                if (ticket.ResourceEntity == Entity.Null)
                {
                    return;
                }
                
                // Skip if resource entity no longer exists
                if (!TransformLookup.HasComponent(ticket.ResourceEntity))
                {
                    return;
                }
                
                // Find job ID from ticket's JobType
                var jobTypeIndex = (byte)ticket.JobType;
                if (!JobCatalog.Value.TryGetJobIndex(jobTypeIndex, out var jobId))
                {
                    return; // Job type not found in catalog
                }
                
                // Generate deterministic seed from ticket ID and assigned tick
                var seed = (uint)(ticket.TicketId ^ ticket.AssignedTick ^ CurrentTick);
                
                // Create WorkOffer entity from ticket
                var offerEntity = Ecb.CreateEntity(chunkIndex);
                Ecb.AddComponent(chunkIndex, offerEntity, new WorkOffer
                {
                    JobId = jobId,
                    Target = ticket.ResourceEntity,
                    Slots = 1, // Legacy tickets assume single worker
                    Taken = 0,
                    Priority = ticket.Priority,
                    Seed = seed,
                    RequiredLayerMask = 1, // Default ground layer
                    EtaSlope = 0.5f // Default ETA penalty
                });
                
                // Tag as ticket-originated for cleanup
                Ecb.AddComponent(chunkIndex, offerEntity, new TicketOriginatedOfferTag());
            }
        }
    }
    
    /// <summary>
    /// Tag component marking WorkOffer entities created from legacy tickets.
    /// Used to identify and clean up ticket-originated offers.
    /// </summary>
    public struct TicketOriginatedOfferTag : IComponentData
    {
    }
}
