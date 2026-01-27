using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Manages time bubble membership by detecting entities within bubble volumes.
    /// Adds/updates TimeBubbleMembership on affected entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SpatialSystemGroup))]
    public partial struct TimeBubbleMembershipSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Get or create system state
            if (!SystemAPI.TryGetSingletonRW<TimeBubbleSystemState>(out var systemStateHandle))
            {
                var stateEntity = state.EntityManager.CreateEntity(typeof(TimeBubbleSystemState));
                state.EntityManager.SetComponentData(stateEntity, new TimeBubbleSystemState
                {
                    NextBubbleId = 1,
                    ActiveBubbleCount = 0,
                    AffectedEntityCount = 0,
                    LastUpdateTick = 0
                });
                return; // Wait for next frame
            }

            ref var systemState2 = ref systemStateHandle.ValueRW;

            // Process create requests
            ProcessCreateRequests(ref state, ref systemState2, currentTick);

            // Process remove requests  
            ProcessRemoveRequests(ref state);

            // Update bubble durations and deactivate expired bubbles
            UpdateBubbleDurations(ref state, currentTick);

            // Update entity memberships
            UpdateMemberships(ref state, currentTick, ref systemState2);

            // Update stasis tags
            UpdateStasisTags(ref state);

            systemState2.LastUpdateTick = currentTick;
        }

        private void ProcessCreateRequests(ref SystemState state, ref TimeBubbleSystemState systemState, uint currentTick)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, entity) in SystemAPI.Query<RefRW<TimeBubbleCreateRequest>>()
                .WithEntityAccess())
            {
                if (!request.ValueRO.IsPending)
                {
                    continue;
                }

                // Create bubble entity
                var bubbleEntity = ecb.CreateEntity();
                uint bubbleId = systemState.NextBubbleId++;

                ecb.AddComponent(bubbleEntity, TimeBubbleId.Create(bubbleId));
                
                ecb.AddComponent(bubbleEntity, new TimeBubbleParams
                {
                    BubbleId = bubbleId,
                    Mode = request.ValueRO.Mode,
                    Scale = request.ValueRO.Scale,
                    RewindOffsetTicks = 0,
                    PlaybackTick = 0,
                    Priority = request.ValueRO.Priority,
                    OwnerPlayerId = 0,
                    AffectsOwnedEntitiesOnly = false,
                    SourceEntity = request.ValueRO.SourceEntity,
                    DurationTicks = request.ValueRO.DurationTicks,
                    CreatedAtTick = currentTick,
                    IsActive = true,
                    AllowMembershipChanges = true,
                    AuthorityPolicy = TimeBubbleAuthorityPolicy.SinglePlayerOnly
                });

                ecb.AddComponent(bubbleEntity, TimeBubbleVolume.CreateSphere(
                    request.ValueRO.Center,
                    request.ValueRO.Radius
                ));

                request.ValueRW.IsPending = false;
                systemState.ActiveBubbleCount++;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void ProcessRemoveRequests(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, entity) in SystemAPI.Query<RefRW<TimeBubbleRemoveRequest>>()
                .WithEntityAccess())
            {
                if (!request.ValueRO.IsPending)
                {
                    continue;
                }

                uint targetBubbleId = request.ValueRO.BubbleId;

                // Find and destroy the bubble entity
                foreach (var (bubbleId, bubbleEntity) in SystemAPI.Query<RefRO<TimeBubbleId>>()
                    .WithEntityAccess())
                {
                    if (bubbleId.ValueRO.Id == targetBubbleId)
                    {
                        ecb.DestroyEntity(bubbleEntity);
                        break;
                    }
                }

                // Remove membership from all affected entities
                foreach (var (membership, affectedEntity) in SystemAPI.Query<RefRW<TimeBubbleMembership>>()
                    .WithEntityAccess())
                {
                    if (membership.ValueRO.BubbleId == targetBubbleId)
                    {
                        ecb.RemoveComponent<TimeBubbleMembership>(affectedEntity);
                        ecb.RemoveComponent<StasisTag>(affectedEntity);
                    }
                }

                request.ValueRW.IsPending = false;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void UpdateBubbleDurations(ref SystemState state, uint currentTick)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (bubbleParams, bubbleId, entity) in SystemAPI
                .Query<RefRW<TimeBubbleParams>, RefRO<TimeBubbleId>>()
                .WithEntityAccess())
            {
                if (!bubbleParams.ValueRO.IsActive)
                {
                    continue;
                }

                // Check if bubble has expired
                if (bubbleParams.ValueRO.DurationTicks > 0)
                {
                    uint elapsed = currentTick - bubbleParams.ValueRO.CreatedAtTick;
                    if (elapsed >= bubbleParams.ValueRO.DurationTicks)
                    {
                        bubbleParams.ValueRW.IsActive = false;
                        
                        // Remove membership from all affected entities
                        uint expiredBubbleId = bubbleId.ValueRO.Id;
                        foreach (var (membership, affectedEntity) in SystemAPI.Query<RefRO<TimeBubbleMembership>>()
                            .WithEntityAccess())
                        {
                            if (membership.ValueRO.BubbleId == expiredBubbleId)
                            {
                                ecb.RemoveComponent<TimeBubbleMembership>(affectedEntity);
                                ecb.RemoveComponent<StasisTag>(affectedEntity);
                            }
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void UpdateMemberships(ref SystemState state, uint currentTick, ref TimeBubbleSystemState systemState)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            int affectedCount = 0;

            // Collect active bubbles
            // NOTE: In multiplayer, OwnerPlayerId will determine which player's entities are affected by this bubble.
            // Currently all bubbles affect all affectable entities regardless of OwnerPlayerId (single-player behavior).
            var activeBubbles = new NativeList<BubbleInfo>(16, Allocator.Temp);
            foreach (var (bubbleParams, volume, bubbleId) in SystemAPI
                .Query<RefRO<TimeBubbleParams>, RefRO<TimeBubbleVolume>, RefRO<TimeBubbleId>>())
            {
                if (!bubbleParams.ValueRO.IsActive)
                {
                    continue;
                }

                // TODO: In multiplayer, filter bubbles by OwnerPlayerId matching entity's owner
                // For now, all bubbles affect all affectable entities

                activeBubbles.Add(new BubbleInfo
                {
                    BubbleId = bubbleId.ValueRO.Id,
                    Mode = bubbleParams.ValueRO.Mode,
                    Scale = bubbleParams.ValueRO.Scale,
                    Priority = bubbleParams.ValueRO.Priority,
                    Volume = volume.ValueRO,
                    AllowMembershipChanges = bubbleParams.ValueRO.AllowMembershipChanges
                });
            }

            // Check each affectable entity
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                .WithAll<TimeBubbleAffectableTag>()
                .WithEntityAccess())
            {
                float3 position = transform.ValueRO.Position;
                bool hasMembership = state.EntityManager.HasComponent<TimeBubbleMembership>(entity);
                var currentMembership = hasMembership 
                    ? state.EntityManager.GetComponentData<TimeBubbleMembership>(entity) 
                    : default;

                // Find highest priority bubble containing this entity
                uint bestBubbleId = 0;
                byte bestPriority = 0;
                TimeBubbleMode bestMode = TimeBubbleMode.Scale;
                float bestScale = 1f;
                bool foundBubble = false;

                for (int i = 0; i < activeBubbles.Length; i++)
                {
                    var bubble = activeBubbles[i];
                    if (bubble.Volume.Contains(position))
                    {
                        if (!foundBubble || bubble.Priority > bestPriority)
                        {
                            foundBubble = true;
                            bestBubbleId = bubble.BubbleId;
                            bestPriority = bubble.Priority;
                            bestMode = bubble.Mode;
                            bestScale = bubble.Scale;
                        }
                    }
                }

                if (foundBubble)
                {
                    // Entity is in a bubble
                    var newMembership = new TimeBubbleMembership
                    {
                        BubbleId = bestBubbleId,
                        LocalMode = bestMode,
                        LocalScale = bestScale,
                        LocalPlaybackTick = 0,
                        MemberSinceTick = hasMembership && currentMembership.BubbleId == bestBubbleId
                            ? currentMembership.MemberSinceTick
                            : currentTick,
                        WasInBubblePreviousFrame = hasMembership,
                        BubblePriority = bestPriority
                    };

                    if (hasMembership)
                    {
                        state.EntityManager.SetComponentData(entity, newMembership);
                    }
                    else
                    {
                        ecb.AddComponent(entity, newMembership);
                    }

                    affectedCount++;
                }
                else if (hasMembership)
                {
                    // Entity left all bubbles
                    ecb.RemoveComponent<TimeBubbleMembership>(entity);
                    ecb.RemoveComponent<StasisTag>(entity);
                }
            }

            activeBubbles.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            systemState.AffectedEntityCount = affectedCount;
        }

        private void UpdateStasisTags(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Add StasisTag to entities in Stasis mode
            foreach (var (membership, entity) in SystemAPI.Query<RefRO<TimeBubbleMembership>>()
                .WithNone<StasisTag>()
                .WithEntityAccess())
            {
                if (membership.ValueRO.LocalMode == TimeBubbleMode.Stasis)
                {
                    ecb.AddComponent<StasisTag>(entity);
                }
            }

            // Remove StasisTag from entities not in Stasis mode
            foreach (var (membership, entity) in SystemAPI.Query<RefRO<TimeBubbleMembership>>()
                .WithAll<StasisTag>()
                .WithEntityAccess())
            {
                if (membership.ValueRO.LocalMode != TimeBubbleMode.Stasis)
                {
                    ecb.RemoveComponent<StasisTag>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private struct BubbleInfo
        {
            public uint BubbleId;
            public TimeBubbleMode Mode;
            public float Scale;
            public byte Priority;
            public TimeBubbleVolume Volume;
            public bool AllowMembershipChanges;
        }
    }
}

