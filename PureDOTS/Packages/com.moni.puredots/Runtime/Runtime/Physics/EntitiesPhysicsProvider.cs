using Unity.Collections;
using Unity.Physics;

namespace PureDOTS.Runtime.Physics
{
    /// <summary>
    /// Physics provider implementation using Unity Entities Physics.
    /// Wraps Unity Physics simulation and event collection.
    /// </summary>
    /// <remarks>
    /// Note: Unity Physics processes collision/trigger events through job interfaces
    /// (ICollisionEventsJob, ITriggerEventsJob) rather than exposing them directly.
    /// The PhysicsEventSystem handles event processing. This provider returns empty arrays
    /// for events since Unity Physics handles them via the job system.
    /// </remarks>
    public struct EntitiesPhysicsProvider : IPhysicsProvider
    {
        public void Step(float deltaTime, ref PhysicsWorld world)
        {
            // Unity Physics steps are handled by PhysicsStepSystemGroup
            // This provider doesn't need to step - Unity's built-in physics systems handle it
        }

        public NativeArray<CollisionEvent> GetCollisionEvents(Allocator allocator)
        {
            // Unity Physics processes collision events through ICollisionEventsJob interfaces
            // (see PhysicsEventSystem.ProcessCollisionEventsJob), not through direct access.
            // Return empty array - events are handled by the job system.
            return new NativeArray<CollisionEvent>(0, allocator);
        }

        public NativeArray<TriggerEvent> GetTriggerEvents(Allocator allocator)
        {
            // Unity Physics processes trigger events through ITriggerEventsJob interfaces
            // (see PhysicsEventSystem.ProcessTriggerEventsJob), not through direct access.
            // Return empty array - events are handled by the job system.
            return new NativeArray<TriggerEvent>(0, allocator);
        }
    }
}

