using Space4X.Physics;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Demo
{
    /// <summary>
    /// Simple movement system for physics test entities.
    /// Reads SpaceVelocity and updates LocalTransform to move entities.
    /// This is a minimal system for testing physics collisions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.Physics.PhysicsSyncSystem))]
    public partial struct Space4XPhysicsTestMovementSystem : ISystem
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
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var deltaTime = timeState.FixedDeltaTime;

            var job = new UpdatePhysicsTestMovementJob
            {
                DeltaTime = deltaTime
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct UpdatePhysicsTestMovementJob : IJobEntity
        {
            public float DeltaTime;

            public void Execute(ref LocalTransform transform, in SpaceVelocity velocity)
            {
                // Simple integration: position += velocity * deltaTime
                transform.Position += velocity.Linear * DeltaTime;
            }
        }
    }
}


