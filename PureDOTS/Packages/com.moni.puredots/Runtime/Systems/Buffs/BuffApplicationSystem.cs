using PureDOTS.Runtime.Buffs;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Buffs
{
    /// <summary>
    /// Processes buff application requests and handles stacking behavior.
    /// Runs in GameplaySystemGroup before buff tick/aggregation systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial struct BuffApplicationSystem : ISystem
    {
        private ComponentLookup<BuffStatCache> _buffStatCacheLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _buffStatCacheLookup = state.GetComponentLookup<BuffStatCache>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            // Get buff catalog
            if (!SystemAPI.TryGetSingleton<BuffCatalogRef>(out var catalogRef) ||
                !catalogRef.Blob.IsCreated)
            {
                return; // No buff catalog configured
            }

            ref var catalog = ref catalogRef.Blob.Value;

            // Process all entities with buff application requests
            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            _buffStatCacheLookup.Update(ref state);

            var buffHandle = new ProcessBuffRequestsJob
            {
                Catalog = catalogRef.Blob,
                CurrentTick = currentTick,
                Ecb = ecb,
                BuffStatCacheLookup = _buffStatCacheLookup
            }.ScheduleParallel(state.Dependency);

            // Process dispel requests
            var dispelHandle = new ProcessDispelRequestsJob
            {
                CurrentTick = currentTick,
                Ecb = ecb
            }.ScheduleParallel(buffHandle);

            state.Dependency = dispelHandle;
        }

        [BurstCompile]
        public partial struct ProcessBuffRequestsJob : IJobEntity
        {
            [ReadOnly]
            public BlobAssetReference<BuffDefinitionBlob> Catalog;

            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<BuffStatCache> BuffStatCacheLookup;

            void Execute(
                Entity entity,
                [EntityIndexInQuery] int entityInQueryIndex,
                DynamicBuffer<BuffApplicationRequest> requests,
                ref DynamicBuffer<ActiveBuff> activeBuffs)
            {
                ref var catalog = ref Catalog.Value;

                for (int i = 0; i < requests.Length; i++)
                {
                    var request = requests[i];
                    if (request.BuffId.Length == 0)
                        continue;

                    // Find buff definition
                    int buffIndex = -1;
                    for (int j = 0; j < catalog.Buffs.Length; j++)
                    {
                        if (catalog.Buffs[j].BuffId.Equals(request.BuffId))
                        {
                            buffIndex = j;
                            break;
                        }
                    }

                    if (buffIndex < 0)
                        continue; // Buff not found in catalog

                    ref var buffDef = ref catalog.Buffs[buffIndex];

                    // Find existing buff instance
                    int existingIndex = -1;
                    for (int j = 0; j < activeBuffs.Length; j++)
                    {
                        if (activeBuffs[j].BuffId.Equals(request.BuffId))
                        {
                            existingIndex = j;
                            break;
                        }
                    }

                    byte stacksToApply = (byte)math.max(1, request.StacksToApply);
                    float duration = request.DurationOverride > 0f
                        ? request.DurationOverride
                        : buffDef.BaseDuration;

                    if (existingIndex >= 0)
                    {
                        // Handle stacking behavior
                        var existing = activeBuffs[existingIndex];
                        switch (buffDef.Stacking)
                        {
                            case StackBehavior.Additive:
                                // Add stacks up to max
                                byte newStacks = (byte)math.min(
                                    buffDef.MaxStacks,
                                    existing.CurrentStacks + stacksToApply);
                                existing.CurrentStacks = newStacks;
                                activeBuffs[existingIndex] = existing;
                                break;

                            case StackBehavior.Multiplicative:
                                // Add stacks up to max (multiplication happens in aggregation)
                                newStacks = (byte)math.min(
                                    buffDef.MaxStacks,
                                    existing.CurrentStacks + stacksToApply);
                                existing.CurrentStacks = newStacks;
                                activeBuffs[existingIndex] = existing;
                                break;

                            case StackBehavior.Refresh:
                                // Refresh duration, keep stacks
                                existing.RemainingDuration = duration;
                                existing.TimeSinceLastTick = 0f;
                                activeBuffs[existingIndex] = existing;
                                break;

                            case StackBehavior.Replace:
                                // Replace with new instance
                                existing.RemainingDuration = duration;
                                existing.TimeSinceLastTick = 0f;
                                existing.CurrentStacks = stacksToApply;
                                existing.SourceEntity = request.SourceEntity;
                                existing.AppliedTick = CurrentTick;
                                activeBuffs[existingIndex] = existing;
                                break;
                        }

                        // Emit applied event (events buffer will be added by game-specific systems if needed)
                        // For now, we skip event emission in the job - games can add event buffers and process them separately
                    }
                    else
                    {
                        // Add new buff instance
                        var newBuff = new ActiveBuff
                        {
                            BuffId = request.BuffId,
                            SourceEntity = request.SourceEntity,
                            RemainingDuration = duration,
                            TimeSinceLastTick = 0f,
                            CurrentStacks = stacksToApply,
                            AppliedTick = CurrentTick
                        };
                        activeBuffs.Add(newBuff);

                        // Ensure BuffStatCache exists (will be initialized by aggregation system)
                        if (!BuffStatCacheLookup.HasComponent(entity))
                        {
                            Ecb.AddComponent(entityInQueryIndex, entity, new BuffStatCache
                            {
                                LastUpdateTick = CurrentTick
                            });
                        }

                        // Emit applied event (events buffer will be added by game-specific systems if needed)
                        // For now, we skip event emission in the job - games can add event buffers and process them separately
                    }
                }

                // Clear processed requests
                requests.Clear();
            }
        }

        [BurstCompile]
        public partial struct ProcessDispelRequestsJob : IJobEntity
        {
            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;

            void Execute(
                Entity entity,
                [EntityIndexInQuery] int entityInQueryIndex,
                DynamicBuffer<BuffDispelRequest> dispelRequests,
                ref DynamicBuffer<ActiveBuff> activeBuffs)
            {
                for (int i = 0; i < dispelRequests.Length; i++)
                {
                    var request = dispelRequests[i];

                    // Remove matching buffs
                    for (int j = activeBuffs.Length - 1; j >= 0; j--)
                    {
                        var buff = activeBuffs[j];
                        bool shouldRemove = false;

                        if (request.BuffId.Length == 0)
                        {
                            // Remove all buffs (or all debuffs if DebuffsOnly)
                            if (!request.DebuffsOnly)
                            {
                                shouldRemove = true;
                            }
                            else
                            {
                                // Need catalog to check category - skip for now, remove all debuffs
                                // TODO: Access catalog to check BuffCategory
                                shouldRemove = true; // Simplified: remove all if debuffs only
                            }
                        }
                        else if (buff.BuffId.Equals(request.BuffId))
                        {
                            shouldRemove = true;
                        }

                        if (shouldRemove)
                        {
                            // Emit removed event (events buffer will be added by game-specific systems if needed)
                            // For now, we skip event emission in the job - games can add event buffers and process them separately
                            activeBuffs.RemoveAtSwapBack(j);
                        }
                    }
                }

                // Clear processed requests
                dispelRequests.Clear();
            }
        }
    }
}

