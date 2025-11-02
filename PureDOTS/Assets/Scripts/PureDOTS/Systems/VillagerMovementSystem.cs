using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Moves villagers toward their current target positions with simple steering.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerTargetingSystem))]
    public partial struct VillagerMovementSystem : ISystem
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

            var job = new UpdateVillagerMovementJob
            {
                DeltaTime = timeState.FixedDeltaTime,
                CurrentTick = timeState.Tick,
                ArrivalDistance = 0.75f
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct UpdateVillagerMovementJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;
            public float ArrivalDistance;

            public void Execute(ref VillagerMovement movement, ref LocalTransform transform, in VillagerAIState aiState, in VillagerNeeds needs)
            {
                if (aiState.TargetPosition.Equals(float3.zero) || aiState.TargetEntity == Entity.Null)
                {
                    movement.Velocity = float3.zero;
                    movement.IsMoving = 0;
                    return;
                }

                var toTarget = aiState.TargetPosition - transform.Position;
                var distance = math.length(toTarget);

                if (distance <= ArrivalDistance)
                {
                    movement.Velocity = float3.zero;
                    movement.IsMoving = 0;
                    return;
                }

                var direction = math.normalize(toTarget);
                var speedMultiplier = 1f;

                if (aiState.CurrentState == VillagerAIState.State.Fleeing)
                {
                    speedMultiplier = 1.5f;
                }
                else if (needs.Energy < 20f)
                {
                    speedMultiplier = 0.5f;
                }

                movement.CurrentSpeed = movement.BaseSpeed * speedMultiplier;
                movement.Velocity = direction * movement.CurrentSpeed;
                transform.Position += movement.Velocity * DeltaTime;

                if (math.lengthsq(movement.Velocity) > 0.0001f)
                {
                    movement.DesiredRotation = quaternion.LookRotationSafe(direction, math.up());
                    transform.Rotation = math.slerp(transform.Rotation, movement.DesiredRotation, DeltaTime * 4f);
                }

                movement.IsMoving = 1;
                movement.LastMoveTick = CurrentTick;
            }
        }
    }
}
