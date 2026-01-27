using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Time
{
    /// <summary>
    /// Base static API for time control operations.
    /// Provides a convenient interface for MonoBehaviour/UI code to interact with the time system.
    /// Uses World.DefaultGameObjectInjectionWorld to access the ECS world.
    /// </summary>
    public static class TimeAPI
    {
        /// <summary>
        /// Gets the default world, or null if not available.
        /// </summary>
        private static World GetWorld()
        {
            return World.DefaultGameObjectInjectionWorld;
        }

        /// <summary>
        /// Pauses the simulation.
        /// </summary>
        public static void Pause()
        {
            var world = GetWorld();
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[TimeAPI] World not available for Pause()");
                return;
            }

            var entityManager = world.EntityManager;
            if (!entityManager.HasComponent<RewindState>(entityManager.CreateEntityQuery(typeof(RewindState)).GetSingletonEntity()))
            {
                Debug.LogWarning("[TimeAPI] RewindState singleton not found");
                return;
            }

            var rewindEntity = entityManager.CreateEntityQuery(typeof(RewindState)).GetSingletonEntity();
            if (!entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                entityManager.AddBuffer<TimeControlCommand>(rewindEntity);
            }

            var commands = entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            commands.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.Pause,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = 0,
                Priority = 100
            });
        }

        /// <summary>
        /// Resumes the simulation.
        /// </summary>
        public static void Resume()
        {
            var world = GetWorld();
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[TimeAPI] World not available for Resume()");
                return;
            }

            var entityManager = world.EntityManager;
            var rewindEntity = entityManager.CreateEntityQuery(typeof(RewindState)).GetSingletonEntity();
            if (!entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                entityManager.AddBuffer<TimeControlCommand>(rewindEntity);
            }

            var commands = entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            commands.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.Resume,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = 0,
                Priority = 100
            });
        }

        /// <summary>
        /// Sets the simulation speed multiplier.
        /// </summary>
        /// <param name="speed">Speed multiplier (0.01-16.0)</param>
        public static void SetSpeed(float speed)
        {
            var world = GetWorld();
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[TimeAPI] World not available for SetSpeed()");
                return;
            }

            var entityManager = world.EntityManager;
            var rewindEntity = entityManager.CreateEntityQuery(typeof(RewindState)).GetSingletonEntity();
            if (!entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                entityManager.AddBuffer<TimeControlCommand>(rewindEntity);
            }

            var commands = entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            commands.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.SetSpeed,
                FloatParam = math.clamp(speed, TimeControlLimits.DefaultMinSpeed, TimeControlLimits.DefaultMaxSpeed),
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = 0,
                Priority = 100
            });
        }

        /// <summary>
        /// Steps the simulation forward by one tick.
        /// </summary>
        public static void StepOneTick()
        {
            var world = GetWorld();
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[TimeAPI] World not available for StepOneTick()");
                return;
            }

            var entityManager = world.EntityManager;
            var rewindEntity = entityManager.CreateEntityQuery(typeof(RewindState)).GetSingletonEntity();
            if (!entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                entityManager.AddBuffer<TimeControlCommand>(rewindEntity);
            }

            var commands = entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            commands.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.StepTicks,
                UintParam = 1,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = 0,
                Priority = 100
            });
        }

        /// <summary>
        /// Gets the current simulation tick.
        /// </summary>
        public static uint GetCurrentTick()
        {
            var world = GetWorld();
            if (world == null || !world.IsCreated)
            {
                return 0;
            }

            var entityManager = world.EntityManager;
            if (!entityManager.CreateEntityQuery(typeof(TimeState)).IsEmptyIgnoreFilter)
            {
                var timeState = entityManager.CreateEntityQuery(typeof(TimeState)).GetSingleton<TimeState>();
                return timeState.Tick;
            }

            return 0;
        }

        /// <summary>
        /// Gets the current effective time scale.
        /// </summary>
        public static float GetCurrentScale()
        {
            var world = GetWorld();
            if (world == null || !world.IsCreated)
            {
                return 1.0f;
            }

            var entityManager = world.EntityManager;
            if (!entityManager.CreateEntityQuery(typeof(TimeState)).IsEmptyIgnoreFilter)
            {
                var timeState = entityManager.CreateEntityQuery(typeof(TimeState)).GetSingleton<TimeState>();
                return timeState.CurrentSpeedMultiplier;
            }

            return 1.0f;
        }

        /// <summary>
        /// Creates a stasis bubble at the specified position.
        /// </summary>
        /// <param name="position">Center position of the bubble</param>
        /// <param name="radius">Radius of the bubble</param>
        /// <param name="durationTicks">Duration in ticks (0 = permanent until removed)</param>
        /// <returns>Entity ID of the created bubble, or Entity.Null if creation failed</returns>
        public static Entity CreateStasisBubble(float3 position, float radius, uint durationTicks)
        {
            var world = GetWorld();
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[TimeAPI] World not available for CreateStasisBubble()");
                return Entity.Null;
            }

            var entityManager = world.EntityManager;
            
            // Get current tick for CreatedAtTick
            uint currentTick = 0;
            if (!entityManager.CreateEntityQuery(typeof(TimeState)).IsEmptyIgnoreFilter)
            {
                currentTick = entityManager.CreateEntityQuery(typeof(TimeState)).GetSingleton<TimeState>().Tick;
            }

            // Generate a unique bubble ID (simple incrementing counter - in production, use a proper ID generator)
            // For now, use a hash of position and tick
            uint bubbleId = (uint)(position.GetHashCode() ^ (int)currentTick);
            if (bubbleId == 0) bubbleId = 1; // Ensure non-zero

            // Create bubble entity
            var bubbleEntity = entityManager.CreateEntity();
            
            // Add bubble ID component
            entityManager.AddComponentData(bubbleEntity, new TimeBubbleId
            {
                Id = bubbleId,
                Name = new FixedString32Bytes("StasisBubble")
            });

            // Add bubble parameters
            var bubbleParams = TimeBubbleParams.CreateStasis(bubbleId, 200); // Priority 200
            bubbleParams.DurationTicks = durationTicks;
            bubbleParams.CreatedAtTick = currentTick;
            bubbleParams.AuthorityPolicy = TimeBubbleAuthorityPolicy.LocalPlayerOnly;
            entityManager.AddComponentData(bubbleEntity, bubbleParams);

            // Add bubble volume
            var bubbleVolume = TimeBubbleVolume.CreateSphere(position, radius);
            entityManager.AddComponentData(bubbleEntity, bubbleVolume);

            return bubbleEntity;
        }

        /// <summary>
        /// Requests a rewind of the local player region.
        /// Currently a stub - will be implemented in the rewind proof-of-concept phase.
        /// </summary>
        /// <param name="lastNSeconds">Number of seconds to rewind</param>
        public static void RewindLocalPlayerRegion(float lastNSeconds)
        {
            var world = GetWorld();
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[TimeAPI] World not available for RewindLocalPlayerRegion()");
                return;
            }

            var entityManager = world.EntityManager;
            
            // Get current tick and calculate target tick
            uint currentTick = 0;
            float fixedDeltaTime = 1f / 60f; // Default
            if (!entityManager.CreateEntityQuery(typeof(TimeState)).IsEmptyIgnoreFilter)
            {
                var timeState = entityManager.CreateEntityQuery(typeof(TimeState)).GetSingleton<TimeState>();
                currentTick = timeState.Tick;
                fixedDeltaTime = timeState.FixedDeltaTime;
            }

            uint ticksToRewind = (uint)(lastNSeconds / fixedDeltaTime);
            uint targetTick = currentTick > ticksToRewind ? currentTick - ticksToRewind : 0;

            var rewindEntity = entityManager.CreateEntityQuery(typeof(RewindState)).GetSingletonEntity();
            if (!entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                entityManager.AddBuffer<TimeControlCommand>(rewindEntity);
            }

            var commands = entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            commands.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.StartRewind,
                UintParam = targetTick,
                Scope = TimeControlScope.Global, // For now, use Global; will change to Player scope in MP
                Source = TimeControlSource.Player,
                PlayerId = 0,
                Priority = 100
            });
        }

        /// <summary>
        /// Begins preview rewind - freezes world and starts scrubbing ghosts backwards.
        /// </summary>
        /// <param name="scrubSpeed">Rewind speed multiplier (1-4x)</param>
        public static void BeginRewindPreview(float scrubSpeed)
        {
            var world = GetWorld();
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[TimeAPI] World not available for BeginRewindPreview()");
                return;
            }

            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            var buffer = entityManager.AddBuffer<TimeControlCommand>(entity);
            buffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.BeginPreviewRewind,
                FloatParam = math.max(1.0f, math.min(4.0f, scrubSpeed)),
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = 0,
                Priority = 200
            });
        }

        /// <summary>
        /// Updates the preview rewind scrub speed while scrubbing.
        /// </summary>
        /// <param name="scrubSpeed">New rewind speed multiplier (1-4x)</param>
        public static void UpdateRewindPreviewSpeed(float scrubSpeed)
        {
            var world = GetWorld();
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[TimeAPI] World not available for UpdateRewindPreviewSpeed()");
                return;
            }

#if DEBUG_REWIND
            // Only log occasionally to reduce spam
            if (UnityEngine.Time.frameCount % 30 == 0)
            {
                Debug.Log($"[Space4XTimeAPI] UpdateRewindPreviewSpeed({scrubSpeed:F2})");
            }
#endif

            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            var buffer = entityManager.AddBuffer<TimeControlCommand>(entity);
            buffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.UpdatePreviewRewindSpeed,
                FloatParam = math.max(1.0f, math.min(4.0f, scrubSpeed)),
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = 0,
                Priority = 150
            });
        }

        /// <summary>
        /// Ends scrub preview - freezes ghosts at current preview position.
        /// </summary>
        public static void EndRewindScrub()
        {
            var world = GetWorld();
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[TimeAPI] World not available for EndRewindScrub()");
                return;
            }

            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            var buffer = entityManager.AddBuffer<TimeControlCommand>(entity);
            buffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.EndScrubPreview,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = 0,
                Priority = 200
            });
        }

        /// <summary>
        /// Commits rewind from preview - applies rewind to world state.
        /// </summary>
        public static void CommitRewindFromPreview()
        {
            var world = GetWorld();
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[TimeAPI] World not available for CommitRewindFromPreview()");
                return;
            }

            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            var buffer = entityManager.AddBuffer<TimeControlCommand>(entity);
            buffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.CommitRewindFromPreview,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = 0,
                Priority = 250
            });
        }

        /// <summary>
        /// Cancels rewind preview - aborts without changing world state.
        /// </summary>
        public static void CancelRewindPreview()
        {
            var world = GetWorld();
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[TimeAPI] World not available for CancelRewindPreview()");
                return;
            }

            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            var buffer = entityManager.AddBuffer<TimeControlCommand>(entity);
            buffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.CancelRewindPreview,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = 0,
                Priority = 200
            });
        }

        /// <summary>
        /// Gets the command entity (RewindState or RewindControlState singleton).
        /// </summary>
        private static Entity GetCommandEntity(EntityManager entityManager)
        {
            if (!entityManager.CreateEntityQuery(typeof(RewindState)).IsEmptyIgnoreFilter)
            {
                return entityManager.CreateEntityQuery(typeof(RewindState)).GetSingletonEntity();
            }
            if (!entityManager.CreateEntityQuery(typeof(RewindControlState)).IsEmptyIgnoreFilter)
            {
                return entityManager.CreateEntityQuery(typeof(RewindControlState)).GetSingletonEntity();
            }
            return Entity.Null;
        }

        /// <summary>
        /// Gets or creates the TimeControlCommand buffer on the given entity.
        /// </summary>
        private static DynamicBuffer<TimeControlCommand> GetOrCreateCommandBuffer(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasBuffer<TimeControlCommand>(entity))
            {
                entityManager.AddBuffer<TimeControlCommand>(entity);
            }
            return entityManager.GetBuffer<TimeControlCommand>(entity);
        }
    }
}

