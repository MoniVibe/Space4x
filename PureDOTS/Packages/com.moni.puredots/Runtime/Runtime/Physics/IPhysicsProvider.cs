using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace PureDOTS.Runtime.Physics
{
    /// <summary>
    /// Known provider identifiers for physics backends.
    /// </summary>
    public static class PhysicsProviderIds
    {
        public const byte None = 0;
        public const byte Entities = 1;
        public const byte Havok = 2;
    }

    /// <summary>
    /// Contract implemented by physics providers to support interchangeable physics backends.
    /// Minimal v1 interface - only what's needed now.
    /// </summary>
    public interface IPhysicsProvider
    {
        /// <summary>
        /// Steps the physics simulation forward by deltaTime.
        /// </summary>
        /// <param name="deltaTime">Time step in seconds</param>
        /// <param name="world">Physics world to step</param>
        void Step(float deltaTime, ref PhysicsWorld world);

        /// <summary>
        /// Gets collision events from the physics simulation.
        /// </summary>
        /// <param name="allocator">Allocator for the returned array</param>
        /// <returns>Array of collision events</returns>
        NativeArray<CollisionEvent> GetCollisionEvents(Allocator allocator);

        /// <summary>
        /// Gets trigger events from the physics simulation.
        /// </summary>
        /// <param name="allocator">Allocator for the returned array</param>
        /// <returns>Array of trigger events</returns>
        NativeArray<TriggerEvent> GetTriggerEvents(Allocator allocator);
    }
}

