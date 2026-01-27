using Unity.Collections;
using Unity.Physics;

namespace PureDOTS.Runtime.Physics
{
    /// <summary>
    /// Placeholder physics provider for Havok Physics integration.
    /// Not implemented yet - this is a stub for future use.
    /// </summary>
    public struct HavokPhysicsProvider : IPhysicsProvider
    {
        public void Step(float deltaTime, ref PhysicsWorld world)
        {
            // TODO: Implement Havok Physics stepping
            // This is a placeholder - not implemented yet
        }

        public NativeArray<CollisionEvent> GetCollisionEvents(Allocator allocator)
        {
            // TODO: Implement Havok collision event collection
            // This is a placeholder - not implemented yet
            return new NativeArray<CollisionEvent>(0, allocator);
        }

        public NativeArray<TriggerEvent> GetTriggerEvents(Allocator allocator)
        {
            // TODO: Implement Havok trigger event collection
            // This is a placeholder - not implemented yet
            return new NativeArray<TriggerEvent>(0, allocator);
        }
    }
}

