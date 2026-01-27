using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace PureDOTS.Systems.Physics
{
    /// <summary>
    /// Syncs ECS LocalTransform and velocity components to Unity Physics bodies.
    /// Runs before the physics simulation step.
    /// </summary>
    /// <remarks>
    /// Philosophy:
    /// - ECS is authoritative for positions and velocities
    /// - This system copies ECS state to physics bodies each frame
    /// - Physics bodies are kinematic (driven by ECS, not by physics forces)
    /// - During rewind playback, this system skips to avoid overwriting rewound state
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsPreSyncSystemGroup))]
    public partial struct PhysicsSyncSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<PhysicsVelocity> _physicsVelocityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<PhysicsConfig>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _physicsVelocityLookup = state.GetComponentLookup<PhysicsVelocity>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            var config = SystemAPI.GetSingleton<PhysicsConfig>();

            // Skip during rewind playback (ECS state is authoritative)
            if (rewindState.Mode == RewindMode.Playback)
            {
                return;
            }

            // Skip if physics is disabled
            if (!config.IsSpace4XPhysicsEnabled && !config.IsGodgamePhysicsEnabled)
            {
                return;
            }

            // Update lookups
            _transformLookup.Update(ref state);
            _physicsVelocityLookup.Update(ref state);

            // Sync all physics-enabled entities
            var syncJob = new SyncPhysicsTransformsJob
            {
                DeltaTime = timeState.FixedDeltaTime
            };

            state.Dependency = syncJob.ScheduleParallel(state.Dependency);
        }

        /// <summary>
        /// Job that syncs ECS transforms to physics velocity for kinematic bodies.
        /// For kinematic bodies, we set velocity based on position changes.
        /// </summary>
        [BurstCompile]
        public partial struct SyncPhysicsTransformsJob : IJobEntity
        {
            public float DeltaTime;

            public void Execute(
                in LocalTransform transform,
                in RequiresPhysics requiresPhysics,
                ref PhysicsVelocity physicsVelocity)
            {
                // For kinematic bodies, we don't need to set velocity
                // The physics system will use LocalTransform directly
                // But we can optionally provide velocity hints for collision detection

                // If the entity has explicit velocity data, we could read it here
                // For now, we leave PhysicsVelocity at zero for kinematic bodies
                // This is correct because kinematic bodies are moved by ECS, not physics

                // The actual position sync happens through LocalToWorld -> PhysicsWorld
                // which Unity Physics handles automatically for entities with LocalTransform

                // If we need to compute velocity from position delta, we would need
                // to store previous position - but that's optional for kinematic bodies
            }
        }
    }

    /// <summary>
    /// System that syncs physics collision results back to ECS.
    /// Handles the physics -> ECS direction after simulation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsPostEventSystemGroup))]
    public partial struct PhysicsResultSyncSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<PhysicsConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            var config = SystemAPI.GetSingleton<PhysicsConfig>();

            // Skip during rewind playback
            if (rewindState.Mode == RewindMode.Playback)
            {
                return;
            }

            // Skip if physics is disabled
            if (!config.IsSpace4XPhysicsEnabled && !config.IsGodgamePhysicsEnabled)
            {
                return;
            }

            // For kinematic bodies driven by ECS, we don't need to sync
            // position back from physics. The ECS position is authoritative.
            
            // This system is a placeholder for future features like:
            // - Reading contact points for ground detection
            // - Extracting collision impulses for damage calculation
            // - Syncing dynamic debris/ragdoll positions (non-authoritative)
        }
    }

    /// <summary>
    /// Helper system for physics rewind coordination.
    /// Marks the tick when rewind completes so other systems can detect settle frames.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsPreSyncSystemGroup), OrderFirst = true)]
    public partial struct PhysicsRewindMarkerSystem : ISystem
    {
        private RewindMode _previousMode;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<PhysicsConfig>();
            _previousMode = RewindMode.Record;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            var configEntity = SystemAPI.GetSingletonEntity<PhysicsConfig>();
            var config = SystemAPI.GetComponent<PhysicsConfig>(configEntity);

            // Detect transition from Playback/CatchUp to Record
            if (_previousMode != RewindMode.Record && rewindState.Mode == RewindMode.Record)
            {
                // Rewind just completed - mark the tick
                var timeState = SystemAPI.GetSingleton<TimeState>();
                config.LastRewindCompleteTick = timeState.Tick;
                config.Version++;
                SystemAPI.SetComponent(configEntity, config);
            }

            _previousMode = rewindState.Mode;
        }
    }
}
