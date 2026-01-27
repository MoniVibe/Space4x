using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Cleans up dead vegetation after decay period has elapsed.
    /// Destroys entities marked as dead and decayable after the configured respawn delay.
    /// Runs late in the vegetation system group to ensure all lifecycle transitions are complete.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VegetationSystemGroup))]
    [UpdateAfter(typeof(VegetationHealthSystem))]
    public partial struct VegetationDecaySystem : ISystem
    {
        private EntityQuery _decayableQuery;
        private ComponentLookup<VegetationProduction> _productionLookup;
        private ComponentLookup<VegetationParent> _parentLookup;
        private ComponentLookup<VegetationReproduction> _reproductionLookup;
        private ComponentLookup<TimeBubbleMembership> _bubbleMembershipLookup;
        private static readonly ProfilerMarker s_UpdateVegetationDecayMarker = 
            new ProfilerMarker("VegetationDecaySystem.Update");

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _decayableQuery = SystemAPI.QueryBuilder()
                .WithAll<VegetationId, VegetationLifecycle, VegetationDecayableTag, VegetationSpeciesIndex, LocalTransform>()
                .WithAny<VegetationDeadTag>()
                .WithNone<PlaybackGuardTag>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<VegetationSpeciesLookup>();
            state.RequireForUpdate<VegetationSpawnCommandQueue>();
            state.RequireForUpdate(_decayableQuery);
            _productionLookup = state.GetComponentLookup<VegetationProduction>(true);
            _parentLookup = state.GetComponentLookup<VegetationParent>(true);
            _reproductionLookup = state.GetComponentLookup<VegetationReproduction>(false);
            _bubbleMembershipLookup = state.GetComponentLookup<TimeBubbleMembership>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (s_UpdateVegetationDecayMarker.Auto())
            {
                var timeState = SystemAPI.GetSingleton<TimeState>();
                if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
                {
                    return;
                }
                var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
                
                // Use TimeHelpers to check if we should update (handles pause, rewind, stasis)
                var defaultMembership = default(TimeBubbleMembership);
                if (!TimeHelpers.ShouldUpdate(timeState, rewindState, defaultMembership))
                {
                    return;
                }

                // Safety check: ensure species catalog exists
                if (!SystemAPI.HasSingleton<VegetationSpeciesLookup>())
                {
#if UNITY_EDITOR
                    LogMissingSpeciesLookup();
#endif
                    return;
                }

                var speciesLookup = SystemAPI.GetSingleton<VegetationSpeciesLookup>();

                if (!speciesLookup.CatalogBlob.IsCreated)
                {
#if UNITY_EDITOR
                    LogCatalogNotCreated();
#endif
                    return;
                }

                var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
                var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

                var spawnQueueEntity = SystemAPI.GetSingletonEntity<VegetationSpawnCommandQueue>();
                var spawnBuffer = state.EntityManager.GetBuffer<VegetationSpawnCommand>(spawnQueueEntity);

                var spawnCommands = new NativeQueue<VegetationSpawnCommand>(Allocator.TempJob);

                _productionLookup.Update(ref state);
                _parentLookup.Update(ref state);
                _reproductionLookup.Update(ref state);
                _bubbleMembershipLookup.Update(ref state);

                var job = new UpdateVegetationDecayJob
                {
                    DeltaTime = TimeHelpers.GetGlobalDelta(tickTimeState, timeState),
                    CurrentTick = timeState.Tick,
                    Ecb = ecb.AsParallelWriter(),
                    SpeciesCatalogBlob = speciesLookup.CatalogBlob,
                    SpawnCommands = spawnCommands.AsParallelWriter(),
                    ParentLookup = _parentLookup,
                    ReproductionLookup = _reproductionLookup,
                    ProductionLookup = _productionLookup,
                    TickTimeState = tickTimeState,
                    TimeState = timeState,
                    BubbleMembershipLookup = _bubbleMembershipLookup
                };

                state.Dependency = job.ScheduleParallel(state.Dependency);
                state.Dependency.Complete();

                while (spawnCommands.TryDequeue(out var command))
                {
                    spawnBuffer.Add(command);
                }

                spawnCommands.Dispose();

#if UNITY_EDITOR
                LogUpdateSummary(_decayableQuery, timeState.Tick);
#endif
            }
        }

        [BurstCompile]
        public partial struct UpdateVegetationDecayJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;
            public BlobAssetReference<VegetationSpeciesCatalogBlob> SpeciesCatalogBlob;
            public NativeQueue<VegetationSpawnCommand>.ParallelWriter SpawnCommands;
            [ReadOnly] public ComponentLookup<VegetationParent> ParentLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<VegetationReproduction> ReproductionLookup;
            [ReadOnly] public ComponentLookup<VegetationProduction> ProductionLookup;
            public TickTimeState TickTimeState;
            public TimeState TimeState;
            [ReadOnly] public ComponentLookup<TimeBubbleMembership> BubbleMembershipLookup;

            public void Execute(
                ref VegetationLifecycle lifecycle,
                DynamicBuffer<VegetationHistoryEvent> historyEvents,
                in VegetationSpeciesIndex speciesIndex,
                in Entity entity,
                in LocalTransform transform,
                [ChunkIndexInQuery] int chunkIndex)
            {
                // Get species data from blob
                if (!SpeciesCatalogBlob.IsCreated || speciesIndex.Value >= SpeciesCatalogBlob.Value.Species.Length)
                {
                    return; // Invalid species index
                }

                // Get bubble membership for this entity (if any)
                var membership = BubbleMembershipLookup.HasComponent(entity)
                    ? BubbleMembershipLookup[entity]
                    : default(TimeBubbleMembership);
                
                // Use TimeHelpers to get effective delta time (handles bubbles, pause, etc.)
                var effectiveDelta = TimeHelpers.GetEffectiveDelta(TickTimeState, TimeState, membership);
                
                // Skip if entity is in stasis or paused
                if (effectiveDelta <= 0f)
                {
                    return;
                }

                ref var speciesData = ref SpeciesCatalogBlob.Value.Species[speciesIndex.Value];

                // Ensure vegetation is in dead stage
                if (lifecycle.CurrentStage != VegetationLifecycle.LifecycleStage.Dead)
                {
                    return;
                }

                // Update stage timer (time since death) using effective delta
                lifecycle.StageTimer += effectiveDelta;

                var decayThreshold = math.max(0f, speciesData.RespawnDelay);

                // Check if decay period has elapsed
                if (lifecycle.StageTimer >= decayThreshold)
                {
                    // Record death event for deterministic replay
                    historyEvents.Add(new VegetationHistoryEvent
                    {
                        Type = VegetationHistoryEvent.EventType.Died,
                        EventTick = CurrentTick,
                        Value = lifecycle.TotalAge
                    });

                    if (ParentLookup.HasComponent(entity))
                    {
                        var parent = ParentLookup[entity].Value;
                        if (parent != Entity.Null && ReproductionLookup.HasComponent(parent))
                        {
                            var reproduction = ReproductionLookup[parent];
                            if (reproduction.ActiveOffspring > 0)
                            {
                                reproduction.ActiveOffspring--;
                                ReproductionLookup[parent] = reproduction;
                            }
                        }
                    }

                    // Queue a natural respawn when no parent is tracking this vegetation
                    var hasParent = ParentLookup.HasComponent(entity) && ParentLookup[entity].Value != Entity.Null;
                    if (!hasParent && speciesData.RespawnDelay > 0f)
                    {
                        var resourceType = default(FixedString64Bytes);
                        if (ProductionLookup.HasComponent(entity))
                        {
                            resourceType = ProductionLookup[entity].ResourceTypeId;
                        }

                        SpawnCommands.Enqueue(new VegetationSpawnCommand
                        {
                            SpeciesIndex = speciesIndex.Value,
                            Position = transform.Position,
                            Parent = Entity.Null,
                            ParentId = 0,
                            IssuedTick = CurrentTick,
                            SequenceId = 0,
                            ResourceTypeId = resourceType
                        });
                    }

                    // Schedule entity destruction
                    Ecb.DestroyEntity(chunkIndex, entity);
                }
            }
        }

#if UNITY_EDITOR
        [BurstDiscard]
        private static void LogMissingSpeciesLookup()
        {
            UnityEngine.Debug.LogWarning("[VegetationDecaySystem] VegetationSpeciesLookup singleton not found. Skipping update.");
        }

        [BurstDiscard]
        private static void LogCatalogNotCreated()
        {
            UnityEngine.Debug.LogWarning("[VegetationDecaySystem] Species catalog blob not created. Skipping update.");
        }

        [BurstDiscard]
        private static void LogUpdateSummary(EntityQuery query, uint tick)
        {
            UnityEngine.Debug.Log($"[VegetationDecaySystem] Processed {query.CalculateEntityCount()} decayable entities at tick {tick}");
        }
#endif
    }
}

