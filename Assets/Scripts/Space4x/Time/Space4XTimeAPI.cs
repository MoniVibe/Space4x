using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;

namespace Space4X.Temporal
{
    /// <summary>
    /// High-level API for time control in Space4X.
    /// Wraps PureDOTS time systems for game-specific use.
    /// 
    /// SINGLE-PLAYER ONLY: This API is designed for single-player mode.
    /// In multiplayer, these methods will route through per-player time authority.
    /// All commands use PlayerId = TimePlayerIds.SinglePlayer (0) and Scope = Global or LocalBubble.
    /// </summary>
    public static class Space4XTimeAPI
    {
        /// <summary>
        /// Requests a global rewind to the specified number of ticks in the past.
        /// 
        /// SINGLE-PLAYER ONLY: Convenience wrapper that calls the player-aware overload with playerId = 0.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="ticksBack">Number of ticks to rewind.</param>
        /// <param name="sourceTechId">ID of the technology/module that triggered the rewind (0 for player input).</param>
        /// <returns>True if the request was accepted.</returns>
        public static bool RequestGlobalRewind(World world, uint ticksBack, uint sourceTechId = 0)
        {
            return RequestGlobalRewind(world, ticksBack, sourceTechId, TimePlayerIds.SinglePlayer);
        }

        /// <summary>
        /// Requests a global rewind to the specified number of ticks in the past.
        /// 
        /// MULTIPLAYER-AWARE: Takes explicit playerId parameter. In SP, use TimePlayerIds.SinglePlayer (0).
        /// In MP, this will route through per-player time authority. Uses Scope = Global.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="ticksBack">Number of ticks to rewind.</param>
        /// <param name="sourceTechId">ID of the technology/module that triggered the rewind (0 for player input).</param>
        /// <param name="playerId">Player ID making the request (0 for single-player).</param>
        /// <returns>True if the request was accepted.</returns>
        public static bool RequestGlobalRewind(World world, uint ticksBack, uint sourceTechId, byte playerId)
        {
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var entityManager = world.EntityManager;

            // Get current tick
            using var timeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
            if (timeQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var timeState = timeQuery.GetSingleton<TimeState>();
            uint targetTick = timeState.Tick > ticksBack ? timeState.Tick - ticksBack : 0;

            // Find the command buffer entity
            using var rewindQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>());
            if (rewindQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var rewindEntity = rewindQuery.GetSingletonEntity();
            if (!entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                return false;
            }

            var commandBuffer = entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            commandBuffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.StartRewind,
                UintParam = targetTick,
                Scope = TimeControlScope.Global,
                Source = sourceTechId > 0 ? TimeControlSource.Technology : TimeControlSource.Player,
                SourceId = sourceTechId,
                PlayerId = playerId,
                Priority = 0
            });

            return true;
        }

        /// <summary>
        /// Spawns a local time field at the specified position.
        /// 
        /// SINGLE-PLAYER ONLY: Convenience wrapper that calls the player-aware overload with ownerPlayerId = 0.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="center">Center position of the field.</param>
        /// <param name="radius">Radius of the field.</param>
        /// <param name="mode">Time mode for the field.</param>
        /// <param name="scale">Time scale (for Scale/FastForward modes).</param>
        /// <param name="durationTicks">Duration in ticks (0 = permanent until removed).</param>
        /// <param name="priority">Priority for overlap resolution.</param>
        /// <param name="sourceModuleEntity">Entity of the module that created this field.</param>
        /// <returns>The created field entity, or Entity.Null on failure.</returns>
        public static Entity SpawnLocalTimeField(World world, float3 center, float radius, 
            TimeBubbleMode mode = TimeBubbleMode.Scale, float scale = 0.5f, 
            uint durationTicks = 0, byte priority = 100, Entity sourceModuleEntity = default)
        {
            return SpawnLocalTimeField(world, center, radius, mode, scale, durationTicks, priority, sourceModuleEntity, TimePlayerIds.SinglePlayer);
        }

        /// <summary>
        /// Spawns a local time field at the specified position.
        /// 
        /// MULTIPLAYER-AWARE: Takes explicit ownerPlayerId parameter. In SP, use TimePlayerIds.SinglePlayer (0).
        /// In MP, this determines which player owns the field. Uses Scope = LocalBubble.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="center">Center position of the field.</param>
        /// <param name="radius">Radius of the field.</param>
        /// <param name="mode">Time mode for the field.</param>
        /// <param name="scale">Time scale (for Scale/FastForward modes).</param>
        /// <param name="durationTicks">Duration in ticks (0 = permanent until removed).</param>
        /// <param name="priority">Priority for overlap resolution.</param>
        /// <param name="sourceModuleEntity">Entity of the module that created this field.</param>
        /// <param name="ownerPlayerId">Owner player ID (0 for single-player).</param>
        /// <returns>The created field entity, or Entity.Null on failure.</returns>
        public static Entity SpawnLocalTimeField(World world, float3 center, float radius, 
            TimeBubbleMode mode, float scale, 
            uint durationTicks, byte priority, Entity sourceModuleEntity, byte ownerPlayerId)
        {
            if (world == null || !world.IsCreated)
            {
                return Entity.Null;
            }

            var entityManager = world.EntityManager;

            // Get next bubble ID
            using var systemStateQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeBubbleSystemState>());
            uint bubbleId;
            if (systemStateQuery.IsEmptyIgnoreFilter)
            {
                // Create system state if it doesn't exist
                var stateEntity = entityManager.CreateEntity(typeof(TimeBubbleSystemState));
                entityManager.SetComponentData(stateEntity, new TimeBubbleSystemState
                {
                    NextBubbleId = 2,
                    ActiveBubbleCount = 0,
                    AffectedEntityCount = 0,
                    LastUpdateTick = 0
                });
                bubbleId = 1;
            }
            else
            {
                var stateEntity = systemStateQuery.GetSingletonEntity();
                var state = entityManager.GetComponentData<TimeBubbleSystemState>(stateEntity);
                bubbleId = state.NextBubbleId;
                state.NextBubbleId++;
                state.ActiveBubbleCount++;
                entityManager.SetComponentData(stateEntity, state);
            }

            // Get current tick for creation timestamp
            uint currentTick = 0;
            using var timeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
            if (!timeQuery.IsEmptyIgnoreFilter)
            {
                currentTick = timeQuery.GetSingleton<TimeState>().Tick;
            }

            // Create the field entity
            var fieldEntity = entityManager.CreateEntity();
            
            entityManager.AddComponentData(fieldEntity, TimeBubbleId.Create(bubbleId, new FixedString32Bytes("TimeField")));
            
            entityManager.AddComponentData(fieldEntity, new TimeBubbleParams
            {
                BubbleId = bubbleId,
                Mode = mode,
                Scale = scale,
                RewindOffsetTicks = 0,
                PlaybackTick = 0,
                Priority = priority,
                OwnerPlayerId = ownerPlayerId,
                AffectsOwnedEntitiesOnly = false,
                SourceEntity = sourceModuleEntity,
                DurationTicks = durationTicks,
                CreatedAtTick = currentTick,
                IsActive = true,
                AllowMembershipChanges = mode != TimeBubbleMode.Stasis
            });

            entityManager.AddComponentData(fieldEntity, TimeBubbleVolume.CreateSphere(center, radius));

            return fieldEntity;
        }

        /// <summary>
        /// Removes a local time field.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="fieldEntity">The field entity to remove.</param>
        /// <returns>True if the field was found and removed.</returns>
        public static bool RemoveLocalTimeField(World world, Entity fieldEntity)
        {
            if (world == null || !world.IsCreated || fieldEntity == Entity.Null)
            {
                return false;
            }

            var entityManager = world.EntityManager;

            if (!entityManager.Exists(fieldEntity))
            {
                return false;
            }

            // Get field ID to clean up memberships
            uint fieldId = 0;
            if (entityManager.HasComponent<TimeBubbleId>(fieldEntity))
            {
                fieldId = entityManager.GetComponentData<TimeBubbleId>(fieldEntity).Id;
            }

            // Remove membership from affected entities
            if (fieldId > 0)
            {
                using var membershipQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeBubbleMembership>());
                var entities = membershipQuery.ToEntityArray(Allocator.Temp);
                
                foreach (var entity in entities)
                {
                    var membership = entityManager.GetComponentData<TimeBubbleMembership>(entity);
                    if (membership.BubbleId == fieldId)
                    {
                        entityManager.RemoveComponent<TimeBubbleMembership>(entity);
                        if (entityManager.HasComponent<StasisTag>(entity))
                        {
                            entityManager.RemoveComponent<StasisTag>(entity);
                        }
                    }
                }
                
                entities.Dispose();
            }

            // Update system state
            using var systemStateQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeBubbleSystemState>());
            if (!systemStateQuery.IsEmptyIgnoreFilter)
            {
                var stateEntity = systemStateQuery.GetSingletonEntity();
                var state = entityManager.GetComponentData<TimeBubbleSystemState>(stateEntity);
                state.ActiveBubbleCount = math.max(0, state.ActiveBubbleCount - 1);
                entityManager.SetComponentData(stateEntity, state);
            }

            // Destroy the field entity
            entityManager.DestroyEntity(fieldEntity);
            return true;
        }

        /// <summary>
        /// Gets the time field entity for a module.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="moduleEntity">The module entity that created the field.</param>
        /// <returns>The field entity if found, otherwise Entity.Null.</returns>
        public static Entity GetTimeFieldForModule(World world, Entity moduleEntity)
        {
            if (world == null || !world.IsCreated || moduleEntity == Entity.Null)
            {
                return Entity.Null;
            }

            var entityManager = world.EntityManager;
            using var fieldQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<TimeBubbleParams>(),
                ComponentType.ReadOnly<TimeBubbleId>());

            var entities = fieldQuery.ToEntityArray(Allocator.Temp);
            var paramsArray = fieldQuery.ToComponentDataArray<TimeBubbleParams>(Allocator.Temp);

            Entity result = Entity.Null;
            for (int i = 0; i < entities.Length; i++)
            {
                if (paramsArray[i].SourceEntity == moduleEntity && paramsArray[i].IsActive)
                {
                    result = entities[i];
                    break;
                }
            }

            entities.Dispose();
            paramsArray.Dispose();
            return result;
        }

        /// <summary>
        /// Sets the global time speed.
        /// 
        /// SINGLE-PLAYER ONLY: Convenience wrapper that calls the player-aware overload with playerId = 0.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="speedMultiplier">Speed multiplier (0.01 to 16.0).</param>
        /// <returns>True if the command was accepted.</returns>
        public static bool SetGlobalTimeSpeed(World world, float speedMultiplier)
        {
            return SetGlobalTimeSpeed(world, speedMultiplier, TimePlayerIds.SinglePlayer);
        }

        /// <summary>
        /// Sets the global time speed.
        /// 
        /// MULTIPLAYER-AWARE: Takes explicit playerId parameter. In SP, use TimePlayerIds.SinglePlayer (0).
        /// In MP, this will route through per-player time authority. Uses Scope = Global.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="speedMultiplier">Speed multiplier (0.01 to 16.0).</param>
        /// <param name="playerId">Player ID making the request (0 for single-player).</param>
        /// <returns>True if the command was accepted.</returns>
        public static bool SetGlobalTimeSpeed(World world, float speedMultiplier, byte playerId)
        {
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var entityManager = world.EntityManager;
            using var rewindQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>());
            if (rewindQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var rewindEntity = rewindQuery.GetSingletonEntity();
            if (!entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                return false;
            }

            var commandBuffer = entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            commandBuffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.SetSpeed,
                FloatParam = TimeHelpers.ClampSpeed(speedMultiplier),
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = playerId,
                Priority = 0
            });

            return true;
        }

        /// <summary>
        /// Toggles global pause state.
        /// 
        /// SINGLE-PLAYER ONLY: Convenience wrapper that calls the player-aware overload with playerId = 0.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <returns>True if the command was accepted.</returns>
        public static bool TogglePause(World world)
        {
            return TogglePause(world, TimePlayerIds.SinglePlayer);
        }

        /// <summary>
        /// Toggles global pause state.
        /// 
        /// MULTIPLAYER-AWARE: Takes explicit playerId parameter. In SP, use TimePlayerIds.SinglePlayer (0).
        /// In MP, this will route through per-player time authority. Uses Scope = Global.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="playerId">Player ID making the request (0 for single-player).</param>
        /// <returns>True if the command was accepted.</returns>
        public static bool TogglePause(World world, byte playerId)
        {
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var entityManager = world.EntityManager;
            
            // Check current pause state
            using var timeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
            if (timeQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var timeState = timeQuery.GetSingleton<TimeState>();
            
            using var rewindQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>());
            if (rewindQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var rewindEntity = rewindQuery.GetSingletonEntity();
            if (!entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                return false;
            }

            var commandBuffer = entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            commandBuffer.Add(new TimeControlCommand
            {
                Type = timeState.IsPaused ? TimeControlCommandType.Resume : TimeControlCommandType.Pause,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = playerId,
                Priority = 0
            });

            return true;
        }

        /// <summary>
        /// Steps the simulation forward by the specified number of ticks.
        /// 
        /// SINGLE-PLAYER ONLY: Convenience wrapper that calls the player-aware overload with playerId = 0.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="ticks">Number of ticks to advance.</param>
        /// <returns>True if the command was accepted.</returns>
        public static bool StepForward(World world, uint ticks = 1)
        {
            return StepForward(world, ticks, TimePlayerIds.SinglePlayer);
        }

        /// <summary>
        /// Steps the simulation forward by the specified number of ticks.
        /// 
        /// MULTIPLAYER-AWARE: Takes explicit playerId parameter. In SP, use TimePlayerIds.SinglePlayer (0).
        /// In MP, this will route through per-player time authority. Uses Scope = Global.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="ticks">Number of ticks to advance.</param>
        /// <param name="playerId">Player ID making the request (0 for single-player).</param>
        /// <returns>True if the command was accepted.</returns>
        public static bool StepForward(World world, uint ticks, byte playerId)
        {
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var entityManager = world.EntityManager;
            using var rewindQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>());
            if (rewindQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var rewindEntity = rewindQuery.GetSingletonEntity();
            if (!entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                return false;
            }

            var commandBuffer = entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            commandBuffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.StepTicks,
                UintParam = ticks,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = playerId,
                Priority = 0
            });

            return true;
        }

        /// <summary>
        /// Creates a temporal shield field (slow time zone around carriers/stations).
        /// SINGLE-PLAYER ONLY: Convenience wrapper with ownerPlayerId = 0.
        /// </summary>
        public static Entity CreateTemporalShieldField(World world, float3 center, float radius, 
            uint durationTicks, Entity moduleEntity)
        {
            return CreateTemporalShieldField(world, center, radius, durationTicks, moduleEntity, TimePlayerIds.SinglePlayer);
        }

        /// <summary>
        /// Creates a temporal shield field (slow time zone around carriers/stations).
        /// MULTIPLAYER-AWARE: Takes explicit ownerPlayerId parameter.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="center">Center position of the field.</param>
        /// <param name="radius">Radius of the field.</param>
        /// <param name="durationTicks">Duration in ticks.</param>
        /// <param name="moduleEntity">Entity of the module that created this field.</param>
        /// <param name="ownerPlayerId">Owner player ID (0 for single-player).</param>
        public static Entity CreateTemporalShieldField(World world, float3 center, float radius, 
            uint durationTicks, Entity moduleEntity, byte ownerPlayerId)
        {
            return SpawnLocalTimeField(world, center, radius, TimeBubbleMode.Scale, 0.1f, 
                durationTicks, 175, moduleEntity, ownerPlayerId);
        }

        /// <summary>
        /// Creates a warp bubble field (accelerated time for travel).
        /// SINGLE-PLAYER ONLY: Convenience wrapper with ownerPlayerId = 0.
        /// </summary>
        public static Entity CreateWarpBubbleField(World world, float3 center, float radius, 
            float speedMultiplier, uint durationTicks, Entity moduleEntity)
        {
            return CreateWarpBubbleField(world, center, radius, speedMultiplier, durationTicks, moduleEntity, TimePlayerIds.SinglePlayer);
        }

        /// <summary>
        /// Creates a warp bubble field (accelerated time for travel).
        /// MULTIPLAYER-AWARE: Takes explicit ownerPlayerId parameter.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="center">Center position of the field.</param>
        /// <param name="radius">Radius of the field.</param>
        /// <param name="speedMultiplier">Speed multiplier for the field.</param>
        /// <param name="durationTicks">Duration in ticks.</param>
        /// <param name="moduleEntity">Entity of the module that created this field.</param>
        /// <param name="ownerPlayerId">Owner player ID (0 for single-player).</param>
        public static Entity CreateWarpBubbleField(World world, float3 center, float radius, 
            float speedMultiplier, uint durationTicks, Entity moduleEntity, byte ownerPlayerId)
        {
            return SpawnLocalTimeField(world, center, radius, TimeBubbleMode.FastForward, speedMultiplier, 
                durationTicks, 150, moduleEntity, ownerPlayerId);
        }

        /// <summary>
        /// Creates a mining acceleration field (faster resource extraction).
        /// SINGLE-PLAYER ONLY: Convenience wrapper with ownerPlayerId = 0.
        /// </summary>
        public static Entity CreateMiningAccelerationField(World world, float3 center, float radius, 
            float speedMultiplier, uint durationTicks, Entity moduleEntity)
        {
            return CreateMiningAccelerationField(world, center, radius, speedMultiplier, durationTicks, moduleEntity, TimePlayerIds.SinglePlayer);
        }

        /// <summary>
        /// Creates a mining acceleration field (faster resource extraction).
        /// MULTIPLAYER-AWARE: Takes explicit ownerPlayerId parameter.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="center">Center position of the field.</param>
        /// <param name="radius">Radius of the field.</param>
        /// <param name="speedMultiplier">Speed multiplier for the field.</param>
        /// <param name="durationTicks">Duration in ticks.</param>
        /// <param name="moduleEntity">Entity of the module that created this field.</param>
        /// <param name="ownerPlayerId">Owner player ID (0 for single-player).</param>
        public static Entity CreateMiningAccelerationField(World world, float3 center, float radius, 
            float speedMultiplier, uint durationTicks, Entity moduleEntity, byte ownerPlayerId)
        {
            return SpawnLocalTimeField(world, center, radius, TimeBubbleMode.FastForward, speedMultiplier, 
                durationTicks, 125, moduleEntity, ownerPlayerId);
        }

        /// <summary>
        /// Creates a stasis field (freeze enemies).
        /// SINGLE-PLAYER ONLY: Convenience wrapper with ownerPlayerId = 0.
        /// </summary>
        public static Entity CreateStasisField(World world, float3 center, float radius, 
            uint durationTicks, Entity moduleEntity)
        {
            return CreateStasisField(world, center, radius, durationTicks, moduleEntity, TimePlayerIds.SinglePlayer);
        }

        /// <summary>
        /// Creates a stasis field (freeze enemies).
        /// MULTIPLAYER-AWARE: Takes explicit ownerPlayerId parameter.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="center">Center position of the field.</param>
        /// <param name="radius">Radius of the field.</param>
        /// <param name="durationTicks">Duration in ticks.</param>
        /// <param name="moduleEntity">Entity of the module that created this field.</param>
        /// <param name="ownerPlayerId">Owner player ID (0 for single-player).</param>
        public static Entity CreateStasisField(World world, float3 center, float radius, 
            uint durationTicks, Entity moduleEntity, byte ownerPlayerId)
        {
            return SpawnLocalTimeField(world, center, radius, TimeBubbleMode.Stasis, 0f, 
                durationTicks, 200, moduleEntity, ownerPlayerId);
        }

        /// <summary>
        /// Begins a rewind preview/scrub. Placeholder - implement when preview flow is ready.
        /// </summary>
        public static void BeginRewindPreview(float scrubSpeed = 1f)
        {
            if (!TryGetRewindCommandBuffer(out var commandBuffer))
            {
                return;
            }

            var clampedSpeed = math.clamp(scrubSpeed, 1f, 4f);
            commandBuffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.BeginPreviewRewind,
                FloatParam = clampedSpeed,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = TimePlayerIds.SinglePlayer,
                Priority = 200
            });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log($"[Space4XTimeAPI] BeginRewindPreview speed={clampedSpeed:F2}");
#endif
        }

        /// <summary>
        /// Updates rewind preview speed. Placeholder.
        /// </summary>
        public static void UpdateRewindPreviewSpeed(float scrubSpeed = 1f)
        {
            if (!TryGetRewindCommandBuffer(out var commandBuffer))
            {
                return;
            }

            commandBuffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.UpdatePreviewRewindSpeed,
                FloatParam = math.clamp(scrubSpeed, 1f, 4f),
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = TimePlayerIds.SinglePlayer,
                Priority = 150
            });
        }

        /// <summary>
        /// Ends the current rewind scrub without committing. Placeholder.
        /// </summary>
        public static void EndRewindScrub()
        {
            if (!TryGetRewindCommandBuffer(out var commandBuffer))
            {
                return;
            }

            commandBuffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.EndScrubPreview,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = TimePlayerIds.SinglePlayer,
                Priority = 200
            });
        }

        /// <summary>
        /// Commits the current rewind preview. Placeholder.
        /// </summary>
        public static void CommitRewindFromPreview()
        {
            if (!TryGetRewindCommandBuffer(out var commandBuffer))
            {
                return;
            }

            commandBuffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.CommitRewindFromPreview,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = TimePlayerIds.SinglePlayer,
                Priority = 250
            });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log("[Space4XTimeAPI] CommitRewindFromPreview");
#endif
        }

        /// <summary>
        /// Cancels the current rewind preview. Placeholder.
        /// </summary>
        public static void CancelRewindPreview()
        {
            if (!TryGetRewindCommandBuffer(out var commandBuffer))
            {
                return;
            }

            commandBuffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.CancelRewindPreview,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = TimePlayerIds.SinglePlayer,
                Priority = 200
            });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log("[Space4XTimeAPI] CancelRewindPreview");
#endif
        }

        private static bool TryGetRewindCommandBuffer(out DynamicBuffer<TimeControlCommand> commandBuffer)
        {
            commandBuffer = default;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var entityManager = world.EntityManager;
            using var rewindQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>());
            if (rewindQuery.IsEmptyIgnoreFilter || rewindQuery.CalculateEntityCount() != 1)
            {
                return false;
            }

            var rewindEntity = rewindQuery.GetSingletonEntity();
            if (!entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                entityManager.AddBuffer<TimeControlCommand>(rewindEntity);
            }

            commandBuffer = entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            return true;
        }
    }
}
