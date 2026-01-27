using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Movement
{
    /// <summary>
    /// Reference to a movement model specification blob.
    /// Entities use this to access their movement capabilities.
    /// </summary>
    public struct MovementModelRef : IComponentData
    {
        public BlobAssetReference<MovementModelSpec> Blob;
    }

    /// <summary>
    /// Runtime movement state for an entity.
    /// Updated each tick by movement systems.
    /// </summary>
    public struct MovementState : IComponentData
    {
        public float3 Vel; // Current velocity vector
        public float3 Desired; // Command vector (goal + avoidance steering)
        public byte Mode; // MovementMode enum (Cruise, Hover, Boost, Drift, etc.)
    }

    /// <summary>
    /// Movement mode enumeration.
    /// </summary>
    public enum MovementMode : byte
    {
        Cruise = 0,
        Hover = 1,
        Boost = 2,
        Drift = 3,
        Brake = 4
    }

    /// <summary>
    /// Request to switch movement mode.
    /// Processed by MovementModeSystem with validation (energy, heat, cooldown).
    /// </summary>
    public struct MovementModeRequest : IBufferElementData
    {
        public byte Mode; // MovementMode enum
        public uint RequestTick; // Tick when request was made
    }

    // ============================================================================
    // Movement Policy Tags
    // These tags define how movement systems constrain entity movement.
    // ============================================================================

    /// <summary>
    /// Tag for ground-based movement. Entities with this tag are constrained to:
    /// - Y position sampled from terrain height
    /// - Pitch/roll zeroed (or aligned to surface normal)
    /// - Velocity Y component constrained by terrain slope
    /// Used by: Godgame villagers, ground units, animals.
    /// </summary>
    public struct GroundMovementTag : IComponentData { }

    /// <summary>
    /// Tag for flying/hovering movement (2.5D). Entities with this tag:
    /// - Use XZ navmesh/pathfinding + separate altitude control
    /// - Maintain heading (yaw) but pitch only for visuals
    /// - Can change altitude but don't require full 6DoF
    /// Used by: Godgame flying creatures, drones, aircraft.
    /// </summary>
    public struct FlyingMovementTag : IComponentData { }

    /// <summary>
    /// Tag for full 6DoF space movement. Entities with this tag:
    /// - No axis constraints
    /// - Full 3D velocity and angular velocity
    /// - Orientation derived from velocity or explicit control
    /// Used by: Space4X ships, carriers, projectiles.
    /// </summary>
    public struct SpaceMovementTag : IComponentData { }

    /// <summary>
    /// Configuration for ground movement constraints.
    /// </summary>
    public struct GroundMovementConfig : IComponentData
    {
        /// <summary>
        /// Whether to align rotation to terrain surface normal (true) or keep upright (false).
        /// </summary>
        public bool AlignToSurface;
        
        /// <summary>
        /// Maximum slope angle (in radians) the entity can traverse.
        /// </summary>
        public float MaxSlopeAngle;
        
        /// <summary>
        /// Offset from terrain surface (for hovering slightly above ground).
        /// </summary>
        public float HeightOffset;
    }

    /// <summary>
    /// Configuration for flying movement constraints.
    /// </summary>
    public struct FlyingMovementConfig : IComponentData
    {
        /// <summary>
        /// Minimum altitude above terrain.
        /// </summary>
        public float MinAltitude;
        
        /// <summary>
        /// Maximum altitude above terrain.
        /// </summary>
        public float MaxAltitude;
        
        /// <summary>
        /// Preferred cruising altitude.
        /// </summary>
        public float PreferredAltitude;
        
        /// <summary>
        /// Rate of altitude change (units per second).
        /// </summary>
        public float AltitudeChangeRate;
    }
}

