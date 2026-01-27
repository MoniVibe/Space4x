using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Movement;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Movement
{
    /// <summary>
    /// Composes MovementState.Desired = GoalSteer + AvoidanceSteer within CommandPolicy budgets.
    /// Runs early in FixedStepSimulationSystemGroup.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct CommandComposeSystem : ISystem
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
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var deltaTime = timeState.DeltaTime;

            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            transformLookup.Update(ref state);

            var job = new CommandComposeJob
            {
                DeltaTime = deltaTime,
                TransformLookup = transformLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct CommandComposeJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

            void Execute(
                Entity entity,
                ref MovementState movementState,
                in CommandPolicy commandPolicy,
                in AvoidanceProfile avoidanceProfile,
                in HazardAvoidanceState avoidanceState,
                in LocalTransform transform)
            {
                // Get goal steering (from AI/movement target - placeholder for now)
                float3 goalSteer = float3.zero;
                // TODO: Query goal/target position from AI system or movement command
                // For now, use forward direction as placeholder
                goalSteer = transform.Forward();

                // Get avoidance steering from HazardAvoidanceState
                float3 avoidanceSteer = avoidanceState.CurrentAdjustment;
                float avoidanceWeight = avoidanceState.AvoidanceUrgency;

                // Compose: blend goal with avoidance
                float3 desired = goalSteer + avoidanceSteer * avoidanceWeight;

                // Normalize desired direction
                float desiredLength = math.length(desired);
                if (desiredLength > 1e-6f)
                {
                    desired = math.normalize(desired);
                }
                else
                {
                    desired = goalSteer; // Fallback to goal if no avoidance
                }

                // Clamp magnitude by MaxEvasionAccel budget
                // This is a simplified version - full implementation would integrate acceleration
                float maxSpeed = 1f; // TODO: Get from MovementModelSpec
                float maxEvasionSpeed = commandPolicy.MaxEvasionAccel * DeltaTime;
                float clampedSpeed = math.min(maxSpeed, maxEvasionSpeed);

                movementState.Desired = desired * clampedSpeed;
            }
        }
    }
}

