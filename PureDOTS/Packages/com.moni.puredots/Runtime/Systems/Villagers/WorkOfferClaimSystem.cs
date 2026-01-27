using PureDOTS.Config;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Villagers
{
    /// <summary>
    /// Extends scheduler to read WorkOffer buffers, evaluate using NeedsHot, shift mask, ETA,
    /// and issue WorkClaim components. Deterministic tie-breaking using seeds.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(NeedDecaySystem))]
    [UpdateAfter(typeof(VillagerShiftSchedulingSystem))]
    [UpdateAfter(typeof(WorkOfferBuildSystem))]
    public partial struct WorkOfferClaimSystem : ISystem
    {
        private ComponentLookup<WorkOffer> _offerLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<SpatialLayerConfig> _layerConfigLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _offerLookup = state.GetComponentLookup<WorkOffer>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _layerConfigLookup = state.GetComponentLookup<SpatialLayerConfig>(true);
            
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
            _transformLookup.Update(ref state);
            _layerConfigLookup.Update(ref state);
            
            var currentTick = timeState.Tick;
            var jobBlob = jobCatalog.Catalog;
            
            // Collect all available offers into a native list for evaluation (parallelized)
            var offers = new NativeList<Entity>(Allocator.TempJob);
            var collectJob = new CollectOffersJob
            {
                Offers = offers
            };
            state.Dependency = collectJob.Schedule(state.Dependency);
            state.Dependency.Complete();
            
            if (offers.Length == 0)
            {
                offers.Dispose();
                return;
            }
            
            var job = new ClaimOffersJob
            {
                Offers = offers.AsArray(),
                OfferLookup = _offerLookup,
                TransformLookup = _transformLookup,
                LayerConfigLookup = _layerConfigLookup,
                JobCatalog = jobBlob,
                CurrentTick = currentTick,
                DeltaTime = timeState.FixedDeltaTime
            };
            
            state.Dependency = job.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();
            offers.Dispose();
        }
        
        [BurstCompile]
        public partial struct CollectOffersJob : IJobEntity
        {
            public NativeList<Entity> Offers;
            
            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, in WorkOffer offer)
            {
                if (offer.Taken < offer.Slots && offer.Target != Entity.Null)
                {
                    Offers.Add(entity);
                }
            }
        }
        
        [BurstCompile]
        public partial struct ClaimOffersJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity> Offers;
            [ReadOnly] public ComponentLookup<WorkOffer> OfferLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<SpatialLayerConfig> LayerConfigLookup;
            [ReadOnly] public BlobAssetReference<JobDefinitionCatalogBlob> JobCatalog;
            public uint CurrentTick;
            public float DeltaTime;
            
            public void Execute(
                Entity villagerEntity,
                ref WorkClaim claim,
                in VillagerNeedsHot needsHot,
                in VillagerShiftState shiftState,
                in VillagerJobPriorityState priorityState,
                in VillagerSeed seed,
                in SpatialLayerTag layerTag,
                in LocalTransform transform)
            {
                // If villager already has a claim, skip
                if (claim.Offer != Entity.Null && OfferLookup.HasComponent(claim.Offer))
                {
                    var existingOffer = OfferLookup[claim.Offer];
                    if (existingOffer.Taken < existingOffer.Slots)
                    {
                        return; // Keep existing claim
                    }
                }
                
                // Find best offer
                Entity bestOffer = Entity.Null;
                float bestUtility = float.MinValue;
                uint bestTieBreak = 0;
                
                var villagerLayerMask = (byte)(1 << layerTag.LayerId);
                
                for (int i = 0; i < Offers.Length; i++)
                {
                    var offerEntity = Offers[i];
                    if (!OfferLookup.HasComponent(offerEntity))
                    {
                        continue;
                    }
                    
                    var offer = OfferLookup[offerEntity];
                    
                    // Check if offer has expired
                    if (offer.ExpiresAtTick > 0 && CurrentTick >= offer.ExpiresAtTick)
                    {
                        continue;
                    }
                    
                    // Check layer mask compatibility
                    if ((villagerLayerMask & offer.RequiredLayerMask) == 0)
                    {
                        continue;
                    }
                    
                    // Check if offer has available slots
                    if (offer.Taken >= offer.Slots)
                    {
                        continue;
                    }
                    
                    // Check if target exists
                    if (offer.Target == Entity.Null || !TransformLookup.HasComponent(offer.Target))
                    {
                        continue;
                    }
                    
                    // Get job definition
                    if (!JobCatalog.IsCreated || offer.JobId < 0 || offer.JobId >= JobCatalog.Value.Jobs.Length)
                    {
                        continue;
                    }
                    
                    ref var jobDef = ref JobCatalog.Value.GetJob(offer.JobId);
                    
                    // Estimate ETA (simple distance-based for now)
                    var targetPos = TransformLookup[offer.Target].Position;
                    var distance = math.distance(transform.Position, targetPos);
                    var baseSpeed = 3f; // Default villager speed
                    var etaSeconds = distance / baseSpeed;
                    
                    // Apply layer cost multiplier if available
                    if (LayerConfigLookup.HasComponent(offer.Target))
                    {
                        var layerConfig = LayerConfigLookup[offer.Target];
                        etaSeconds *= math.max(1f, layerConfig.CostMultiplier);
                    }
                    
                    // Calculate utility
                    var utility = CalculateUtility(needsHot, ref jobDef, etaSeconds, shiftState.ShouldWork > 0, offer);
                    
                    // Tie-breaker for equal utilities
                    var tieBreak = VillagerSeed.TieBreak(offer.Seed, (uint)villagerEntity.Index, CurrentTick);
                    
                    if (utility > bestUtility || (math.abs(utility - bestUtility) < 0.001f && tieBreak > bestTieBreak))
                    {
                        bestUtility = utility;
                        bestOffer = offerEntity;
                        bestTieBreak = tieBreak;
                    }
                }
                
                // Claim best offer if utility is above threshold
                if (bestOffer != Entity.Null && bestUtility > 0.1f)
                {
                    claim.Offer = bestOffer;
                    claim.ClaimTick = CurrentTick;
                }
            }
            
            private static float CalculateUtility(
                VillagerNeedsHot needs,
                ref JobDefinitionData jobDef,
                float etaSeconds,
                bool shiftActive,
                WorkOffer offer)
            {
                // Base utility from needs
                var baseU = math.max(needs.UtilityWork * jobDef.BasePriority / 100f, 0f);
                
                // ETA penalty
                var etaPenalty = 1f / (1f + etaSeconds * offer.EtaSlope);
                
                // Shift multiplier
                var shiftMultiplier = shiftActive ? 1f : 0.3f; // Off-shift penalty
                
                // Priority boost
                var priorityBoost = offer.Priority / 100f;
                
                return baseU * etaPenalty * shiftMultiplier * (1f + priorityBoost * 0.5f);
            }
        }
    }
}

