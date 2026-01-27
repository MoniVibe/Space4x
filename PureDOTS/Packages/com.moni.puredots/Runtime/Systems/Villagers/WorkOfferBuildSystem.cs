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
    /// Builds WorkOffer entities from resource nodes, jobsites, and storehouses.
    /// Lives near the sources - creates offers deterministically based on available work.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerJobPrioritySchedulerSystem))]
    [UpdateAfter(typeof(TicketToOfferAdapterSystem))]
    public partial struct WorkOfferBuildSystem : ISystem
    {
        private ComponentLookup<ResourceSourceState> _resourceStateLookup;
        private ComponentLookup<StorehouseInventory> _storehouseLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<OfferNonce> _nonceLookup;
        private ComponentLookup<OfferSourceCap> _sourceCapLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _resourceStateLookup = state.GetComponentLookup<ResourceSourceState>(true);
            _storehouseLookup = state.GetComponentLookup<StorehouseInventory>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _nonceLookup = state.GetComponentLookup<OfferNonce>(false);
            _sourceCapLookup = state.GetComponentLookup<OfferSourceCap>(false);
            
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
            
            _resourceStateLookup.Update(ref state);
            _storehouseLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _nonceLookup.Update(ref state);
            _sourceCapLookup.Update(ref state);
            
            var currentTick = timeState.Tick;
            var jobBlob = jobCatalog.Catalog;
            
            // Find job IDs for Gather, Haul, Build, Rest
            var gatherJobId = FindJobId(jobBlob, VillagerJob.JobType.Gatherer);
            var haulJobId = FindJobId(jobBlob, VillagerJob.JobType.Merchant);
            var buildJobId = FindJobId(jobBlob, VillagerJob.JobType.Builder);
            var restJobId = FindJobId(jobBlob, VillagerJob.JobType.None); // Rest might map to None or a new type
            
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            // Remove expired offers first
            var removeExpiredJob = new RemoveExpiredOffersJob
            {
                CurrentTick = currentTick,
                SourceCapLookup = _sourceCapLookup,
                Ecb = ecb.AsParallelWriter()
            };
            state.Dependency = removeExpiredJob.ScheduleParallel(state.Dependency);
            
            // Create offers from resource nodes (Gather)
            var gatherJob = new BuildGatherOffersJob
            {
                JobId = gatherJobId,
                CurrentTick = currentTick,
                ResourceStateLookup = _resourceStateLookup,
                TransformLookup = _transformLookup,
                NonceLookup = _nonceLookup,
                SourceCapLookup = _sourceCapLookup,
                Ecb = ecb.AsParallelWriter()
            };
            state.Dependency = gatherJob.ScheduleParallel(state.Dependency);
            
            // Create offers from storehouses (Haul)
            var haulJob = new BuildHaulOffersJob
            {
                JobId = haulJobId,
                CurrentTick = currentTick,
                StorehouseLookup = _storehouseLookup,
                TransformLookup = _transformLookup,
                NonceLookup = _nonceLookup,
                SourceCapLookup = _sourceCapLookup,
                Ecb = ecb.AsParallelWriter()
            };
            state.Dependency = haulJob.ScheduleParallel(state.Dependency);
            
            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
        
        private static int FindJobId(BlobAssetReference<JobDefinitionCatalogBlob> catalog, VillagerJob.JobType jobType)
        {
            if (!catalog.IsCreated)
            {
                return -1;
            }
            
            var jobTypeIndex = (byte)jobType;
            return catalog.Value.FindJobIndex(jobTypeIndex);
        }
        
        [BurstCompile]
        public partial struct RemoveExpiredOffersJob : IJobEntity
        {
            public uint CurrentTick;
            [NativeDisableParallelForRestriction] public ComponentLookup<OfferSourceCap> SourceCapLookup;
            public EntityCommandBuffer.ParallelWriter Ecb;
            
            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, in WorkOffer offer)
            {
                // Remove offers that have expired (ExpiresAtTick > 0 and CurrentTick >= ExpiresAtTick)
                if (offer.ExpiresAtTick > 0 && CurrentTick >= offer.ExpiresAtTick)
                {
                    if (offer.Target != Entity.Null && SourceCapLookup.HasComponent(offer.Target))
                    {
                        var cap = SourceCapLookup[offer.Target];
                        if (cap.CurrentOpenOffers > 0)
                        {
                            cap.CurrentOpenOffers--;
                        }
                        SourceCapLookup[offer.Target] = cap;
                    }
                    Ecb.DestroyEntity(chunkIndex, entity);
                }
            }
        }
        
        [BurstCompile]
        public partial struct BuildGatherOffersJob : IJobEntity
        {
            public int JobId;
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<ResourceSourceState> ResourceStateLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            public ComponentLookup<OfferNonce> NonceLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<OfferSourceCap> SourceCapLookup;
            public EntityCommandBuffer.ParallelWriter Ecb;
            
            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, in ResourceSourceConfig config, in LocalTransform transform)
            {
                if (JobId < 0)
                {
                    return;
                }
                
                // Check if resource has available units
                if (!ResourceStateLookup.HasComponent(entity))
                {
                    return;
                }
                
                var resourceState = ResourceStateLookup[entity];
                if (resourceState.UnitsRemaining <= 0f)
                {
                    return;
                }
                
                // Check nonce cooldown
                if (!NonceLookup.HasComponent(entity))
                {
                    NonceLookup[entity] = new OfferNonce { LastIssuedTick = 0 };
                }
                
                var nonce = NonceLookup[entity];
                if (!OfferNonce.CanIssueOffer(CurrentTick, ref nonce, cooldownTicks: 10))
                {
                    return;
                }
                
                NonceLookup[entity] = nonce;
                
                // Check back-pressure cap
                if (!SourceCapLookup.HasComponent(entity))
                {
                    SourceCapLookup[entity] = new OfferSourceCap { MaxOpenOffers = 5, CurrentOpenOffers = 0 };
                }
                
                var cap = SourceCapLookup[entity];
                if (cap.CurrentOpenOffers >= cap.MaxOpenOffers)
                {
                    // Cap hit - skip creating offer (could log/telemetry here)
                    cap.LastCapHitTick = CurrentTick;
                    SourceCapLookup[entity] = cap;
                    return;
                }
                
                // Increment open offer count
                cap.CurrentOpenOffers++;
                SourceCapLookup[entity] = cap;
                
                // Calculate expiry (default: 60 ticks = ~1 second at 60 FPS)
                const uint defaultOfferLifetimeTicks = 60u;
                var expiresAtTick = CurrentTick + defaultOfferLifetimeTicks;
                
                // Create WorkOffer entity
                var offerEntity = Ecb.CreateEntity(chunkIndex);
                Ecb.AddComponent(chunkIndex, offerEntity, new WorkOffer
                {
                    JobId = JobId,
                    Target = entity,
                    Slots = (byte)math.max(1, (int)(resourceState.UnitsRemaining / 10f)), // More slots for larger resources
                    Taken = 0,
                    Priority = 50, // Base priority, will be adjusted by scheduler
                    Seed = (uint)(entity.Index ^ CurrentTick), // Deterministic seed
                    RequiredLayerMask = 1, // Default ground layer
                    EtaSlope = 0.5f, // Default ETA penalty
                    ExpiresAtTick = expiresAtTick
                });
            }
        }
        
        [BurstCompile]
        public partial struct BuildHaulOffersJob : IJobEntity
        {
            public int JobId;
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<StorehouseInventory> StorehouseLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            public ComponentLookup<OfferNonce> NonceLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<OfferSourceCap> SourceCapLookup;
            public EntityCommandBuffer.ParallelWriter Ecb;
            
            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, in StorehouseConfig config)
            {
                if (JobId < 0)
                {
                    return;
                }
                
                // Check if storehouse has capacity for hauling
                if (!StorehouseLookup.HasComponent(entity))
                {
                    return;
                }
                
                var inventory = StorehouseLookup[entity];
                if (inventory.TotalStored >= inventory.TotalCapacity * 0.95f)
                {
                    return; // Nearly full, no haul offers
                }
                
                // Check nonce cooldown
                if (!NonceLookup.HasComponent(entity))
                {
                    NonceLookup[entity] = new OfferNonce { LastIssuedTick = 0 };
                }
                
                var nonce = NonceLookup[entity];
                if (!OfferNonce.CanIssueOffer(CurrentTick, ref nonce, cooldownTicks: 10))
                {
                    return;
                }
                
                NonceLookup[entity] = nonce;
                
                // Check back-pressure cap
                if (!SourceCapLookup.HasComponent(entity))
                {
                    SourceCapLookup[entity] = new OfferSourceCap { MaxOpenOffers = 3, CurrentOpenOffers = 0 };
                }
                
                var cap = SourceCapLookup[entity];
                if (cap.CurrentOpenOffers >= cap.MaxOpenOffers)
                {
                    cap.LastCapHitTick = CurrentTick;
                    SourceCapLookup[entity] = cap;
                    return;
                }
                
                cap.CurrentOpenOffers++;
                SourceCapLookup[entity] = cap;
                
                const uint defaultOfferLifetimeTicks = 60u;
                var expiresAtTick = CurrentTick + defaultOfferLifetimeTicks;
                
                // Create WorkOffer entity for hauling TO this storehouse
                var offerEntity = Ecb.CreateEntity(chunkIndex);
                Ecb.AddComponent(chunkIndex, offerEntity, new WorkOffer
                {
                    JobId = JobId,
                    Target = entity,
                    Slots = 3, // Multiple haulers can work simultaneously
                    Taken = 0,
                    Priority = 40, // Lower than gather
                    Seed = (uint)(entity.Index ^ CurrentTick),
                    RequiredLayerMask = 1,
                    EtaSlope = 0.3f,
                    ExpiresAtTick = expiresAtTick
                });
            }
        }
    }
}
