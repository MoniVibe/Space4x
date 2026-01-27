using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace PureDOTS.Systems.Physics
{
    /// <summary>
    /// Processes physics collision and trigger events from Unity Physics.
    /// Translates them into ECS-friendly event buffers for game systems to consume.
    /// </summary>
    /// <remarks>
    /// Philosophy:
    /// - Physics events are translated to ECS gameplay events
    /// - Game-specific systems (Space4X, Godgame) consume these events
    /// - Events are skipped during rewind playback and post-rewind settle frames
    /// - This is a base system; game-specific event processing is in game projects
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsPostEventSystemGroup))]
    public partial struct PhysicsEventSystem : ISystem
    {
        private ComponentLookup<RequiresPhysics> _requiresPhysicsLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private BufferLookup<PhysicsCollisionEventElement> _collisionEventLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<PhysicsConfig>();
            state.RequireForUpdate<SimulationSingleton>();

            _requiresPhysicsLookup = state.GetComponentLookup<RequiresPhysics>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _collisionEventLookup = state.GetBufferLookup<PhysicsCollisionEventElement>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }
            var config = SystemAPI.GetSingleton<PhysicsConfig>();

            // Skip during rewind playback
            if (rewindState.Mode == RewindMode.Playback)
            {
                return;
            }

            // Skip if provider is None
            if (config.ProviderId == PhysicsProviderIds.None)
            {
                return;
            }

            // Skip if physics is disabled
            if (!config.IsSpace4XPhysicsEnabled && !config.IsGodgamePhysicsEnabled)
            {
                return;
            }

            // Skip during post-rewind settle frames
            if (PhysicsConfigHelpers.IsPostRewindSettleFrame(in config, timeState.Tick))
            {
                return;
            }

            // Only process if using Entities provider (Unity Physics)
            // Other providers (Havok) would need their own event processing systems
            if (config.ProviderId != PhysicsProviderIds.Entities)
            {
                return;
            }

            // Update lookups
            _requiresPhysicsLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _collisionEventLookup.Update(ref state);

            // Get simulation singleton for collision events
            var simulation = SystemAPI.GetSingleton<SimulationSingleton>();
            
            // Get physics world singleton for CalculateDetails
            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

            // Process collision events
            var collisionJob = new ProcessCollisionEventsJob
            {
                RequiresPhysicsLookup = _requiresPhysicsLookup,
                TransformLookup = _transformLookup,
                CollisionEventLookup = _collisionEventLookup,
                PhysicsWorld = physicsWorldSingleton.PhysicsWorld,
                CurrentTick = timeState.Tick,
                LogCollisions = config.LogCollisions != 0
            };

            state.Dependency = collisionJob.Schedule(simulation, state.Dependency);

            // Process trigger events
            var triggerJob = new ProcessTriggerEventsJob
            {
                RequiresPhysicsLookup = _requiresPhysicsLookup,
                CollisionEventLookup = _collisionEventLookup,
                CurrentTick = timeState.Tick,
                LogCollisions = config.LogCollisions != 0
            };

            state.Dependency = triggerJob.Schedule(simulation, state.Dependency);
        }

        /// <summary>
        /// Job that processes collision events from Unity Physics.
        /// </summary>
        [BurstCompile]
        public struct ProcessCollisionEventsJob : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<RequiresPhysics> RequiresPhysicsLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            public BufferLookup<PhysicsCollisionEventElement> CollisionEventLookup;
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            public uint CurrentTick;
            public bool LogCollisions;

            public void Execute(CollisionEvent collisionEvent)
            {
                var entityA = collisionEvent.EntityA;
                var entityB = collisionEvent.EntityB;

                // Only process if at least one entity has RequiresPhysics
                bool aHasPhysics = RequiresPhysicsLookup.HasComponent(entityA);
                bool bHasPhysics = RequiresPhysicsLookup.HasComponent(entityB);

                if (!aHasPhysics && !bHasPhysics)
                {
                    return;
                }

                // Calculate collision details for better impulse and contact point data
                // Create local copy to pass as ref (PhysicsWorld is a struct containing NativeArrays)
                var physicsWorldLocal = PhysicsWorld;
                var details = collisionEvent.CalculateDetails(ref physicsWorldLocal);
                var normal = collisionEvent.Normal;
                var impulse = details.EstimatedImpulse;
                var contactPoint = details.EstimatedContactPointPositions.Length > 0 
                    ? details.AverageContactPointPosition 
                    : EstimateContactPoint(this, entityA, entityB);

                // Add event to entity A's buffer if it has one
                if (aHasPhysics && CollisionEventLookup.HasBuffer(entityA))
                {
                    var buffer = CollisionEventLookup[entityA];
                    buffer.Add(new PhysicsCollisionEventElement
                    {
                        OtherEntity = entityB,
                        ContactPoint = contactPoint,
                        ContactNormal = normal,
                        Impulse = impulse,
                        Tick = CurrentTick,
                        EventType = PhysicsCollisionEventType.Collision
                    });
                }

                // Add event to entity B's buffer if it has one
                if (bHasPhysics && CollisionEventLookup.HasBuffer(entityB))
                {
                    var buffer = CollisionEventLookup[entityB];
                    buffer.Add(new PhysicsCollisionEventElement
                    {
                        OtherEntity = entityA,
                        ContactPoint = contactPoint,
                        ContactNormal = -normal, // Flip normal for entity B
                        Impulse = impulse,
                        Tick = CurrentTick,
                        EventType = PhysicsCollisionEventType.Collision
                    });
                }

                // Dispose the NativeArray from details to avoid memory leaks
                if (details.EstimatedContactPointPositions.IsCreated)
                {
                    details.EstimatedContactPointPositions.Dispose();
                }

            }

            private static float3 EstimateContactPoint(in ProcessCollisionEventsJob job, Entity a, Entity b)
            {
                bool hasA = job.TransformLookup.HasComponent(a);
                bool hasB = job.TransformLookup.HasComponent(b);

                if (hasA && hasB)
                {
                    var posA = job.TransformLookup[a].Position;
                    var posB = job.TransformLookup[b].Position;
                    return (posA + posB) * 0.5f;
                }

                if (hasA)
                {
                    return job.TransformLookup[a].Position;
                }

                if (hasB)
                {
                    return job.TransformLookup[b].Position;
                }

                return float3.zero;
            }
        }

        /// <summary>
        /// Job that processes trigger events from Unity Physics.
        /// </summary>
        [BurstCompile]
        public struct ProcessTriggerEventsJob : ITriggerEventsJob
        {
            [ReadOnly] public ComponentLookup<RequiresPhysics> RequiresPhysicsLookup;
            public BufferLookup<PhysicsCollisionEventElement> CollisionEventLookup;
            public uint CurrentTick;
            public bool LogCollisions;

            public void Execute(TriggerEvent triggerEvent)
            {
                var entityA = triggerEvent.EntityA;
                var entityB = triggerEvent.EntityB;

                // Only process if at least one entity has RequiresPhysics
                bool aHasPhysics = RequiresPhysicsLookup.HasComponent(entityA);
                bool bHasPhysics = RequiresPhysicsLookup.HasComponent(entityB);

                if (!aHasPhysics && !bHasPhysics)
                {
                    return;
                }

                // Add trigger event to entity A's buffer if it has one
                if (aHasPhysics && CollisionEventLookup.HasBuffer(entityA))
                {
                    var buffer = CollisionEventLookup[entityA];
                    buffer.Add(new PhysicsCollisionEventElement
                    {
                        OtherEntity = entityB,
                        ContactPoint = float3.zero, // Triggers don't have contact points
                        ContactNormal = float3.zero,
                        Tick = CurrentTick,
                        EventType = PhysicsCollisionEventType.TriggerEnter
                    });
                }

                // Add trigger event to entity B's buffer if it has one
                if (bHasPhysics && CollisionEventLookup.HasBuffer(entityB))
                {
                    var buffer = CollisionEventLookup[entityB];
                    buffer.Add(new PhysicsCollisionEventElement
                    {
                        OtherEntity = entityA,
                        ContactPoint = float3.zero,
                        ContactNormal = float3.zero,
                        Tick = CurrentTick,
                        EventType = PhysicsCollisionEventType.TriggerEnter
                    });
                }
            }
        }
    }

    /// <summary>
    /// System that clears collision event buffers at the start of each frame.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsPreSyncSystemGroup), OrderFirst = true)]
    public partial struct PhysicsEventClearSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Clear all collision event buffers
            // Note: Clear() is safe in foreach - the mutation pattern applies to direct element assignment, not method calls
            foreach (var buffer in SystemAPI.Query<DynamicBuffer<PhysicsCollisionEventElement>>())
            {
                buffer.Clear();
            }
        }
    }
}

namespace PureDOTS.Runtime.Physics
{
    /// <summary>
    /// Buffer element for physics collision events.
    /// Added to entities with RequiresPhysics that need collision event processing.
    /// </summary>
    public struct PhysicsCollisionEventElement : IBufferElementData
    {
        /// <summary>
        /// The other entity involved in the collision.
        /// </summary>
        public Entity OtherEntity;

        /// <summary>
        /// Contact point in world space.
        /// </summary>
        public float3 ContactPoint;

        /// <summary>
        /// Contact normal (pointing away from this entity).
        /// </summary>
        public float3 ContactNormal;

        /// <summary>
        /// Estimated impulse applied during the collision.
        /// Useful for damage calculations and collision response.
        /// </summary>
        public float Impulse;

        /// <summary>
        /// Tick when the event occurred.
        /// </summary>
        public uint Tick;

        /// <summary>
        /// Type of collision event.
        /// </summary>
        public PhysicsCollisionEventType EventType;
    }

    /// <summary>
    /// Type of physics collision event.
    /// </summary>
    public enum PhysicsCollisionEventType : byte
    {
        /// <summary>
        /// Standard collision (contact).
        /// </summary>
        Collision = 0,

        /// <summary>
        /// Trigger enter (overlap start).
        /// </summary>
        TriggerEnter = 1,

        /// <summary>
        /// Trigger exit (overlap end).
        /// </summary>
        TriggerExit = 2
    }
}

