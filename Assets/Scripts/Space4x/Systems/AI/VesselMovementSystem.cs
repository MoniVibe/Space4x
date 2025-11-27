using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Moves vessels toward their current target positions with simple steering.
    /// Similar to VillagerMovementSystem but designed for vessels.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateAfter(typeof(Space4XTransportAISystemGroup))]
    public partial struct VesselMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VesselMovement>();
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
            var currentTick = timeState.Tick;

            // Debug logging (only first frame)
#if UNITY_EDITOR
            if (currentTick == 1)
            {
                var vesselCount = SystemAPI.QueryBuilder().WithAll<VesselMovement>().Build().CalculateEntityCount();
                Debug.Log($"[VesselMovementSystem] Found {vesselCount} vessels, DeltaTime={deltaTime}, Tick={currentTick}");
            }
#endif

            var threatLookup = state.GetComponentLookup<ThreatProfile>(true);
            var transformLookup = state.GetComponentLookup<LocalTransform>(true);
            var stanceLookup = state.GetComponentLookup<VesselStanceComponent>(true);
            threatLookup.Update(ref state);
            transformLookup.Update(ref state);
            stanceLookup.Update(ref state);

            var job = new UpdateVesselMovementJob
            {
                DeltaTime = deltaTime,
                CurrentTick = currentTick,
                ArrivalDistance = 2f, // Vessels stop 2 units away from target
                BaseRotationSpeed = 2f, // Base rotate speed in radians per second
                ThreatLookup = threatLookup,
                TransformLookup = transformLookup,
                StanceLookup = stanceLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct UpdateVesselMovementJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;
            public float ArrivalDistance;
            public float BaseRotationSpeed;
            [ReadOnly] public ComponentLookup<ThreatProfile> ThreatLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<VesselStanceComponent> StanceLookup;

            public void Execute(Entity entity, ref VesselMovement movement, ref LocalTransform transform, in VesselAIState aiState)
            {
                // Don't move if mining - stay in place to gather resources
                if (aiState.CurrentState == VesselAIState.State.Mining)
                {
                    movement.Velocity = float3.zero;
                    movement.IsMoving = 0;
                    return;
                }

                // Only check TargetEntity - TargetPosition will be resolved by targeting system
                if (aiState.TargetEntity == Entity.Null)
                {
                    movement.Velocity = float3.zero;
                    movement.IsMoving = 0;
                    return;
                }
                
                // If TargetPosition is still zero, wait for targeting system to resolve it
                if (aiState.TargetPosition.Equals(float3.zero))
                {
                    return;
                }

                var toTarget = aiState.TargetPosition - transform.Position;
                var distance = math.length(toTarget);

                if (distance <= ArrivalDistance)
                {
                    movement.Velocity = float3.zero;
                    movement.IsMoving = 0;
                    // VesselGatheringSystem will transition to Mining state when close enough
                    return;
                }

                var direction = math.normalize(toTarget);
                
                // Get stance parameters (default to Balanced if no stance component)
                var stanceType = VesselStanceMode.Balanced;
                if (StanceLookup.HasComponent(entity))
                {
                    stanceType = StanceLookup[entity].CurrentStance;
                }
                
                var avoidanceRadius = StanceRouting.GetAvoidanceRadius(stanceType);
                var avoidanceStrength = StanceRouting.GetAvoidanceStrength(stanceType);
                var speedMultiplier = StanceRouting.GetSpeedMultiplier(stanceType);
                var rotationMultiplier = StanceRouting.GetRotationMultiplier(stanceType);
                
                // Apply stance-based threat avoidance
                direction = AvoidThreats(direction, transform.Position, avoidanceRadius, avoidanceStrength);

                movement.CurrentSpeed = movement.BaseSpeed * speedMultiplier;
                movement.Velocity = direction * movement.CurrentSpeed;
                transform.Position += movement.Velocity * DeltaTime;

                if (math.lengthsq(movement.Velocity) > 0.001f)
                {
                    movement.DesiredRotation = quaternion.LookRotationSafe(direction, math.up());
                    transform.Rotation = math.slerp(transform.Rotation, movement.DesiredRotation, DeltaTime * BaseRotationSpeed * rotationMultiplier);
                }

                movement.IsMoving = 1;
                movement.LastMoveTick = CurrentTick;
            }

            private float3 AvoidThreats(float3 desiredDirection, float3 position, float avoidanceRadius, float avoidanceStrength)
            {
                float avoidanceRadiusSq = avoidanceRadius * avoidanceRadius;
                float3 avoidanceVector = float3.zero;

                // Threat avoidance disabled here to keep job free of SystemAPI queries.

                // Combine desired direction with avoidance
                if (math.lengthsq(avoidanceVector) > 0.001f)
                {
                    var combinedDirection = math.normalize(desiredDirection + avoidanceVector);
                    return combinedDirection;
                }

                return desiredDirection;
            }
        }
    }
}

