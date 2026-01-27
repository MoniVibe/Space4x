using Unity.Collections;
using Unity.Physics;

namespace PureDOTS.Runtime.Physics
{
    /// <summary>
    /// No-op physics provider for tools/tests where physics is not needed.
    /// Safe to use when physics is disabled.
    /// </summary>
    public struct NoPhysicsProvider : IPhysicsProvider
    {
        public void Step(float deltaTime, ref PhysicsWorld world)
        {
            // No-op: does nothing
        }

        public NativeArray<CollisionEvent> GetCollisionEvents(Allocator allocator)
        {
            return new NativeArray<CollisionEvent>(0, allocator);
        }

        public NativeArray<TriggerEvent> GetTriggerEvents(Allocator allocator)
        {
            return new NativeArray<TriggerEvent>(0, allocator);
        }
    }
}

