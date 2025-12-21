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
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Systems.AI
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Moves vessels toward their current target positions with simple steering.
    /// Similar to VillagerMovementSystem but designed for vessels.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    public partial struct VesselMovementSystem : ISystem
    {
        private ComponentLookup<ThreatProfile> _threatLookup;
        private ComponentLookup<VesselStanceComponent> _stanceLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VesselMovement>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _threatLookup = state.GetComponentLookup<ThreatProfile>(true);
            _stanceLookup = state.GetComponentLookup<VesselStanceComponent>(true);
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
                UnityDebug.Log($"[VesselMovementSystem] Found {vesselCount} vessels, DeltaTime={deltaTime}, Tick={currentTick}");
            }
#endif

            _threatLookup.Update(ref state);
            _stanceLookup.Update(ref state);

            var job = new UpdateVesselMovementJob
            {
                DeltaTime = deltaTime,
                CurrentTick = currentTick,
                ArrivalDistance = 2f, // Vessels stop 2 units away from target
                BaseRotationSpeed = 2f, // Base rotate speed in radians per second
                ThreatLookup = _threatLookup,
                StanceLookup = _stanceLookup
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
                
                // TargetPosition should be resolved by VesselTargetingSystem (runs earlier in Space4XTransportAISystemGroup).
                var targetPosition = aiState.TargetPosition;
                var toTarget = targetPosition - transform.Position;
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
