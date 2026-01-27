using Unity.Entities;

namespace PureDOTS.Runtime.Physics
{
    /// <summary>
    /// Global configuration singleton for Unity Physics integration.
    /// Controls which game modes have physics enabled and debug settings.
    /// </summary>
    /// <remarks>
    /// Philosophy:
    /// - ECS is authoritative for all gameplay state
    /// - Unity Physics is a derived collision detection layer
    /// - Physics bodies are kinematic and driven by ECS transforms
    /// - Collision events are translated back into ECS gameplay events
    /// </remarks>
    public struct PhysicsConfig : IComponentData
    {
        /// <summary>
        /// Physics provider to use (None=0, Entities=1, Havok=2).
        /// Default: Entities (Unity Physics)
        /// </summary>
        public byte ProviderId;

        /// <summary>
        /// Enable physics for Space4X entities (ships, carriers, asteroids, projectiles).
        /// 0 = disabled, 1 = enabled
        /// </summary>
        public byte EnableSpace4XPhysics;

        /// <summary>
        /// Enable physics for Godgame entities (villagers, buildings, terrain).
        /// 0 = disabled, 1 = enabled
        /// </summary>
        public byte EnableGodgamePhysics;

        /// <summary>
        /// Log collision events to console for debugging.
        /// 0 = disabled, 1 = enabled
        /// </summary>
        public byte LogCollisions;

        /// <summary>
        /// Skip collision processing for N frames after rewind to allow settling.
        /// Default: 1 frame
        /// </summary>
        public byte PostRewindSettleFrames;

        /// <summary>
        /// Maximum physics bodies to process per frame (budget limiting).
        /// 0 = unlimited
        /// </summary>
        public ushort MaxPhysicsBodiesPerFrame;

        /// <summary>
        /// Physics LOD distance - entities beyond this distance from camera may skip physics.
        /// 0 = disabled (all entities use physics)
        /// </summary>
        public float PhysicsLODDistance;

        /// <summary>
        /// Tick when last rewind completed (used for settle frame detection).
        /// </summary>
        public uint LastRewindCompleteTick;

        /// <summary>
        /// Version number for config changes (incremented when config is modified).
        /// </summary>
        public uint Version;

        public bool IsSpace4XPhysicsEnabled => EnableSpace4XPhysics != 0;
        public bool IsGodgamePhysicsEnabled => EnableGodgamePhysics != 0;
        public bool IsLoggingEnabled => LogCollisions != 0;

        /// <summary>
        /// Creates default physics configuration.
        /// </summary>
        public static PhysicsConfig CreateDefault()
        {
            return new PhysicsConfig
            {
                ProviderId = PhysicsProviderIds.Entities, // Default to Unity Physics
                EnableSpace4XPhysics = 1,
                EnableGodgamePhysics = 1,
                LogCollisions = 0,
                PostRewindSettleFrames = 1,
                MaxPhysicsBodiesPerFrame = 0, // unlimited
                PhysicsLODDistance = 0f, // disabled
                LastRewindCompleteTick = 0,
                Version = 1
            };
        }

        /// <summary>
        /// Creates a debug configuration with logging enabled.
        /// </summary>
        public static PhysicsConfig CreateDebug()
        {
            var config = CreateDefault();
            config.LogCollisions = 1;
            return config;
        }
    }

    /// <summary>
    /// Tag component indicating the PhysicsConfig singleton entity.
    /// </summary>
    public struct PhysicsConfigTag : IComponentData { }

    /// <summary>
    /// Collider type enumeration shared across Space4X and Godgame.
    /// </summary>
    public enum ColliderType : byte
    {
        Sphere = 0,
        Box = 1,
        Capsule = 2,
        Mesh = 3,
        Compound = 4
    }

    /// <summary>
    /// Helper methods for physics configuration.
    /// </summary>
    public static class PhysicsConfigHelpers
    {
        /// <summary>
        /// Checks if we're in a post-rewind settle period where collision events should be skipped.
        /// </summary>
        public static bool IsPostRewindSettleFrame(in PhysicsConfig config, uint currentTick)
        {
            if (config.PostRewindSettleFrames == 0)
                return false;

            if (config.LastRewindCompleteTick == 0)
                return false;

            return currentTick <= config.LastRewindCompleteTick + config.PostRewindSettleFrames;
        }

        /// <summary>
        /// Checks if an entity should use physics based on LOD distance.
        /// </summary>
        public static bool ShouldUsePhysicsLOD(in PhysicsConfig config, float distanceFromCamera)
        {
            if (config.PhysicsLODDistance <= 0f)
                return true; // LOD disabled

            return distanceFromCamera <= config.PhysicsLODDistance;
        }
    }
}

