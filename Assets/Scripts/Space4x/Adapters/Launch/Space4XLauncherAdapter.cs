using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Launch;
using PureDOTS.Runtime.Physics;
using Space4X.Authoring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Adapters.Launch
{
    /// <summary>
    /// Space4X-specific adapter for launcher mechanics.
    /// Reads Space4X input/AI commands and writes LaunchRequest entries.
    /// Also processes collision events for launched objects.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.Launch.LaunchRequestIntakeSystem))]
    public partial struct Space4XLauncherInputAdapter : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            // Only process in Record mode
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();

            // Process launchers with pending launch commands
            // In a real implementation, this would read from an AI command buffer or player input
            // For now, this is a placeholder showing the adapter pattern

            foreach (var (config, launcherConfig, requestBuffer, transform, entity) in
                SystemAPI.Query<RefRO<LauncherConfig>, RefRO<Space4XLauncherConfig>, DynamicBuffer<LaunchRequest>, RefRO<LocalTransform>>()
                    .WithAll<Space4XLauncherTag>()
                    .WithEntityAccess())
            {
                // Example: Check for pending launch commands (would come from AI or input system)
                // ProcessLaunchCommands(ref requestBuffer, config, launcherConfig, transform, timeState.Tick);
            }
        }

        /// <summary>
        /// Helper to queue a launch from a Space4X launcher.
        /// Called by AI systems or player commands when triggering a launch.
        /// </summary>
        public static void QueueLaunch(
            ref DynamicBuffer<LaunchRequest> requestBuffer,
            Entity sourceEntity,
            Entity payloadEntity,
            float3 targetPosition,
            float3 launcherPosition,
            float speed)
        {
            // Calculate launch velocity (straight line in space, no gravity arc)
            var direction = math.normalize(targetPosition - launcherPosition);
            var velocity = direction * speed;

            requestBuffer.Add(new LaunchRequest
            {
                SourceEntity = sourceEntity,
                PayloadEntity = payloadEntity,
                LaunchTick = 0, // Immediate
                InitialVelocity = velocity,
                Flags = 0
            });
        }

        /// <summary>
        /// Helper to queue a delayed launch.
        /// Useful for coordinated fleet actions or timed releases.
        /// </summary>
        public static void QueueDelayedLaunch(
            ref DynamicBuffer<LaunchRequest> requestBuffer,
            Entity sourceEntity,
            Entity payloadEntity,
            float3 velocity,
            uint launchTick)
        {
            requestBuffer.Add(new LaunchRequest
            {
                SourceEntity = sourceEntity,
                PayloadEntity = payloadEntity,
                LaunchTick = launchTick,
                InitialVelocity = velocity,
                Flags = 0
            });
        }
    }

    /// <summary>
    /// Processes collision events for launched objects in Space4X.
    /// Translates generic collision events to Space4X-specific effects.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Launch.LaunchExecutionSystem))]
    public partial struct Space4XLauncherCollisionAdapter : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            // Only process in Record mode
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Process collision events for launched objects
            foreach (var (projectileTag, collisionBuffer, entity) in
                SystemAPI.Query<RefRO<LaunchedProjectileTag>, DynamicBuffer<PhysicsCollisionEventElement>>()
                    .WithEntityAccess())
            {
                for (int i = 0; i < collisionBuffer.Length; i++)
                {
                    var collision = collisionBuffer[i];

                    // Process Space4X-specific collision effects
                    // Examples:
                    // - Cargo pod delivery (transfer resources to target)
                    // - Torpedo impact (apply damage based on impulse)
                    // - Probe arrival (activate scanning at location)
                    // - Drone deployment (convert to active drone entity)

                    // This is where game-specific logic goes
                    // The adapter translates generic collision events to Space4X behavior
                }
            }
        }
    }
}






