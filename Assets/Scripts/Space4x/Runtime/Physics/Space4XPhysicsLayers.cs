using Unity.Physics;

namespace Space4X.Physics
{
    /// <summary>
    /// Physics layer enumeration for Space4X entities.
    /// Used for collision filtering between different entity types.
    /// </summary>
    public enum Space4XPhysicsLayer : byte
    {
        /// <summary>
        /// Player and NPC ships (carriers, frigates, etc.).
        /// Collides with: Asteroid, Projectile, Debris, Station
        /// </summary>
        Ship = 0,

        /// <summary>
        /// Asteroids and mineable objects.
        /// Collides with: Ship, Projectile, Miner
        /// </summary>
        Asteroid = 1,

        /// <summary>
        /// Projectiles (missiles, lasers, etc.).
        /// Collides with: Ship, Asteroid, Station, Debris
        /// </summary>
        Projectile = 2,

        /// <summary>
        /// Debris and wreckage (cosmetic, non-authoritative).
        /// Collides with: Ship, Projectile
        /// </summary>
        Debris = 3,

        /// <summary>
        /// Sensor-only colliders (triggers for detection, no physical response).
        /// Collides with: Ship, Asteroid, Projectile (as triggers)
        /// </summary>
        SensorOnly = 4,

        /// <summary>
        /// Mining vessels and drones.
        /// Collides with: Asteroid, Ship, Station
        /// </summary>
        Miner = 5,

        /// <summary>
        /// Space stations and large structures.
        /// Collides with: Ship, Projectile, Miner
        /// </summary>
        Station = 6,

        /// <summary>
        /// Docking zones and capture areas (triggers).
        /// Collides with: Ship, Miner (as triggers)
        /// </summary>
        DockingZone = 7
    }

    /// <summary>
    /// Static helper class for Space4X collision layer configuration.
    /// </summary>
    public static class Space4XPhysicsLayers
    {
        // Layer indices (matching enum values)
        public const int Ship = 0;
        public const int Asteroid = 1;
        public const int Projectile = 2;
        public const int Debris = 3;
        public const int SensorOnly = 4;
        public const int Miner = 5;
        public const int Station = 6;
        public const int DockingZone = 7;

        /// <summary>
        /// Creates a collision filter for the specified layer.
        /// </summary>
        public static CollisionFilter CreateFilter(Space4XPhysicsLayer layer)
        {
            return new CollisionFilter
            {
                BelongsTo = GetBelongsToMask(layer),
                CollidesWith = GetCollidesWithMask(layer),
                GroupIndex = 0
            };
        }

        /// <summary>
        /// Gets the "belongs to" bitmask for a layer.
        /// </summary>
        public static uint GetBelongsToMask(Space4XPhysicsLayer layer)
        {
            return 1u << (int)layer;
        }

        /// <summary>
        /// Gets the "collides with" bitmask for a layer.
        /// Defines which layers this layer can collide with.
        /// </summary>
        public static uint GetCollidesWithMask(Space4XPhysicsLayer layer)
        {
            return layer switch
            {
                // Ship collides with: Asteroid, Projectile, Debris, Station, DockingZone (trigger)
                Space4XPhysicsLayer.Ship => (1u << Asteroid) | (1u << Projectile) | (1u << Debris) | (1u << Station) | (1u << DockingZone),

                // Asteroid collides with: Ship, Projectile, Miner
                Space4XPhysicsLayer.Asteroid => (1u << Ship) | (1u << Projectile) | (1u << Miner),

                // Projectile collides with: Ship, Asteroid, Station, Debris
                Space4XPhysicsLayer.Projectile => (1u << Ship) | (1u << Asteroid) | (1u << Station) | (1u << Debris),

                // Debris collides with: Ship, Projectile (but no gameplay response, just visual)
                Space4XPhysicsLayer.Debris => (1u << Ship) | (1u << Projectile),

                // Sensor collides with: Ship, Asteroid, Projectile (as triggers)
                Space4XPhysicsLayer.SensorOnly => (1u << Ship) | (1u << Asteroid) | (1u << Projectile),

                // Miner collides with: Asteroid, Ship, Station
                Space4XPhysicsLayer.Miner => (1u << Asteroid) | (1u << Ship) | (1u << Station),

                // Station collides with: Ship, Projectile, Miner
                Space4XPhysicsLayer.Station => (1u << Ship) | (1u << Projectile) | (1u << Miner),

                // DockingZone collides with: Ship, Miner (as triggers)
                Space4XPhysicsLayer.DockingZone => (1u << Ship) | (1u << Miner),

                _ => 0u
            };
        }

        /// <summary>
        /// Checks if two layers should collide.
        /// </summary>
        public static bool ShouldCollide(Space4XPhysicsLayer layerA, Space4XPhysicsLayer layerB)
        {
            var maskA = GetCollidesWithMask(layerA);
            var maskB = GetBelongsToMask(layerB);
            return (maskA & maskB) != 0;
        }

        /// <summary>
        /// Gets the default priority for a layer.
        /// Higher priority entities are processed first when physics budget is limited.
        /// </summary>
        public static byte GetDefaultPriority(Space4XPhysicsLayer layer)
        {
            return layer switch
            {
                Space4XPhysicsLayer.Ship => 200,       // High priority (player-visible)
                Space4XPhysicsLayer.Projectile => 255, // Highest priority (fast-moving)
                Space4XPhysicsLayer.Asteroid => 100,   // Medium priority
                Space4XPhysicsLayer.Miner => 150,      // Above medium (player units)
                Space4XPhysicsLayer.Station => 100,    // Medium priority
                Space4XPhysicsLayer.Debris => 50,      // Low priority (cosmetic)
                Space4XPhysicsLayer.SensorOnly => 75,  // Low-medium (detection)
                Space4XPhysicsLayer.DockingZone => 75, // Low-medium (triggers)
                _ => 100
            };
        }
    }
}

