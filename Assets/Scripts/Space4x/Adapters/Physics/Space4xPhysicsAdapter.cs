using PureDOTS.Runtime.Physics;
using PureDOTS.Systems.Physics;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics.Systems;

namespace Space4X.Adapters.Physics
{
    /// <summary>
    /// Space4X-specific physics adapter.
    /// Selects physics provider via config and subscribes to collision events.
    /// This adapter does NOT fork PureDOTS systems - it only configures and consumes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsPostEventSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Physics.PhysicsEventSystem))]
    public partial struct Space4XPhysicsAdapter : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<PhysicsConfig>();

            // Select provider based on config
            // Currently only Entities (Unity Physics) is supported
            if (config.ProviderId == PhysicsProviderIds.None)
            {
                // Physics disabled - adapter does nothing
                return;
            }

            if (config.ProviderId == PhysicsProviderIds.Entities)
            {
                // Using Unity Physics - events are already processed by PhysicsEventSystem
                // This adapter can subscribe to PhysicsCollisionEventElement buffers here
                // and translate them to Space4X-specific behavior (ship collisions, asteroid impacts, etc.)
                ProcessSpace4XCollisionEvents(ref state);
            }
            else if (config.ProviderId == PhysicsProviderIds.Havok)
            {
                // Havok provider not implemented yet
                // When implemented, this adapter would process Havok-specific events
            }
        }

        private void ProcessSpace4XCollisionEvents(ref SystemState state)
        {
            // Example: Subscribe to collision events and translate to Space4X behavior
            // This is where you'd add game-specific collision handling logic
            // For now, this is a placeholder showing the adapter pattern

            // Example query for entities with collision events:
            // foreach (var (events, entity) in SystemAPI.Query<DynamicBuffer<PhysicsCollisionEventElement>>())
            // {
            //     // Process Space4X-specific collision logic (ship-to-ship, ship-to-asteroid, etc.)
            // }
        }
    }
}

