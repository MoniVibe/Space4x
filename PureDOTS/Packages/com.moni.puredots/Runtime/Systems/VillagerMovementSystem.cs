using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Movement;
using PureDOTS.Runtime.Navigation;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Villager;
using PureDOTS.Systems.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Moves villagers toward their current target positions with simple steering.
    /// Integrates with flow field navigation when available.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HotPathSystemGroup))]
    // Removed invalid UpdateAfter: VillagerTargetingSystem runs in VillagerSystemGroup.
    [UpdateAfter(typeof(FlowFieldFollowSystem))]
    public partial struct VillagerMovementSystem : ISystem
    {
        private ComponentLookup<TimeBubbleMembership> _bubbleMembershipLookup;
        private ComponentLookup<MovementSuppressed> _movementSuppressedLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            _bubbleMembershipLookup = state.GetComponentLookup<TimeBubbleMembership>(isReadOnly: true);
            _movementSuppressedLookup = state.GetComponentLookup<MovementSuppressed>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }
            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            
            // Use TimeHelpers to check if we should update (handles pause, rewind, stasis)
            var defaultMembership = default(TimeBubbleMembership);
            if (!TimeHelpers.ShouldUpdate(timeState, rewindState, defaultMembership))
            {
                return;
            }

            // Get villager behavior config or use defaults
            var config = SystemAPI.HasSingleton<VillagerBehaviorConfig>()
                ? SystemAPI.GetSingleton<VillagerBehaviorConfig>()
                : VillagerBehaviorConfig.CreateDefaults();

            _bubbleMembershipLookup.Update(ref state);
            _movementSuppressedLookup.Update(ref state);
            
            var job = new UpdateVillagerMovementJob
            {
                DeltaTime = timeState.FixedDeltaTime,
                CurrentTick = timeState.Tick,
                ArrivalDistance = config.ArrivalDistance,
                FleeSpeedMultiplier = config.FleeSpeedMultiplier,
                LowEnergySpeedMultiplier = config.LowEnergySpeedMultiplier,
                LowEnergyThreshold = config.LowEnergyThreshold,
                VelocityThreshold = config.VelocityThreshold,
                RotationSpeed = config.RotationSpeed,
                AccelerationMultiplier = config.AccelerationMultiplier,
                DecelerationMultiplier = config.DecelerationMultiplier,
                TurnBlendSpeed = config.TurnBlendSpeed,
                TickTimeState = tickTimeState,
                TimeState = timeState,
                RewindState = rewindState,
                BubbleMembershipLookup = _bubbleMembershipLookup,
                MovementSuppressedLookup = _movementSuppressedLookup
            };
            
            state.Dependency = job.ScheduleParallel(state.Dependency);
            
            // Count frozen entities for debug log (outside Burst)
#if UNITY_EDITOR
            LogFrozenEntities(ref state);
#endif
        }

        [BurstCompile]
        public partial struct UpdateVillagerMovementJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;
            public float ArrivalDistance;
            public float FleeSpeedMultiplier;
            public float LowEnergySpeedMultiplier;
            public float LowEnergyThreshold;
            public float VelocityThreshold;
            public float RotationSpeed;
            public float AccelerationMultiplier;
            public float DecelerationMultiplier;
            public float TurnBlendSpeed;
            public TickTimeState TickTimeState;
            public TimeState TimeState;
            public RewindState RewindState;
            [ReadOnly] public ComponentLookup<TimeBubbleMembership> BubbleMembershipLookup;
            [ReadOnly] public ComponentLookup<MovementSuppressed> MovementSuppressedLookup;

            [Unity.Burst.CompilerServices.SkipLocalsInit]
            public void Execute(
                ref VillagerMovement movement,
                ref LocalTransform transform,
                in VillagerAIState aiState,
                in VillagerNeeds needs,
                in Entity entity,
                [ChunkIndexInQuery] int chunkIndex)
            {
                // Skip if movement is suppressed (e.g., being held by player)
                if (MovementSuppressedLookup.HasComponent(entity) &&
                    MovementSuppressedLookup.IsComponentEnabled(entity))
                {
                    movement.Velocity = float3.zero;
                    movement.DesiredVelocity = float3.zero;
                    movement.IsMoving = 0;
                    return;
                }
                
                // Get bubble membership for this entity (if any)
                var membership = BubbleMembershipLookup.HasComponent(entity)
                    ? BubbleMembershipLookup[entity]
                    : default(TimeBubbleMembership);
                
                // Gate movement by stasis - if entity is in stasis, don't move
                if (TimeHelpers.IsInStasis(membership))
                {
                    movement.Velocity = float3.zero;
                    movement.DesiredVelocity = float3.zero;
                    movement.IsMoving = 0;
                    return;
                }
                
                // Use TimeHelpers to check if we should update
                if (!TimeHelpers.ShouldUpdate(TimeState, RewindState, membership))
                {
                    movement.Velocity = float3.zero;
                    movement.DesiredVelocity = float3.zero;
                    movement.IsMoving = 0;
                    return;
                }
                
                // Get effective delta time (handles bubbles, pause, etc.)
                var effectiveDelta = TimeHelpers.GetEffectiveDelta(TickTimeState, TimeState, membership);
                if (effectiveDelta <= 0f)
                {
                    movement.Velocity = float3.zero;
                    movement.DesiredVelocity = float3.zero;
                    movement.IsMoving = 0;
                    return;
                }
                
                // Check if flow field navigation is available
                float3 desiredVelocity = float3.zero;

                // Try to use flow field if available (checked via optional component)
                // FlowFieldFollowSystem will have already set movement.Velocity if agent has FlowFieldAgentTag
                // Otherwise fall back to direct targeting

                if (aiState.TargetPosition.Equals(float3.zero) || aiState.TargetEntity == Entity.Null)
                {
                    desiredVelocity = movement.DesiredVelocity;
                    if (math.lengthsq(desiredVelocity) < VelocityThreshold * VelocityThreshold)
                    {
                        movement.Velocity = float3.zero;
                        movement.DesiredVelocity = float3.zero;
                        movement.IsMoving = 0;
                        return;
                    }
                    // Otherwise continue with flow field direction
                }
                else
                {
                    var toTarget = aiState.TargetPosition - transform.Position;
                    var distance = math.length(toTarget);

                    if (distance <= ArrivalDistance)
                    {
                        movement.Velocity = float3.zero;
                        movement.DesiredVelocity = float3.zero;
                        movement.IsMoving = 0;
                        return;
                    }

                    var direction = math.normalize(toTarget);
                    desiredVelocity = direction * movement.BaseSpeed;
                }

                // Apply speed multipliers
                var speedMultiplier = 1f;
                if (aiState.CurrentState == VillagerAIState.State.Fleeing)
                {
                    speedMultiplier = FleeSpeedMultiplier;
                }
                else if (needs.EnergyFloat < LowEnergyThreshold)
                {
                    speedMultiplier = LowEnergySpeedMultiplier;
                }

                // If not using flow field, compute velocity from direction
                if (math.lengthsq(desiredVelocity) > VelocityThreshold * VelocityThreshold)
                {
                    var desiredSpeed = math.length(desiredVelocity) * speedMultiplier;
                    var desiredDir = math.normalizesafe(desiredVelocity);
                    desiredVelocity = desiredDir * desiredSpeed;
                }

                var currentVelocity = movement.Velocity;
                if (math.lengthsq(currentVelocity) > VelocityThreshold * VelocityThreshold &&
                    math.lengthsq(desiredVelocity) > VelocityThreshold * VelocityThreshold)
                {
                    var currentDir = math.normalizesafe(currentVelocity);
                    var desiredDir = math.normalizesafe(desiredVelocity);
                    var turnLerp = math.saturate(effectiveDelta * math.max(0.1f, TurnBlendSpeed));
                    var blendedDir = math.normalizesafe(math.lerp(currentDir, desiredDir, turnLerp), desiredDir);
                    desiredVelocity = blendedDir * math.length(desiredVelocity);
                }

                var targetSpeed = math.length(desiredVelocity);
                var currentSpeed = math.length(currentVelocity);
                var acceleration = math.max(0.1f, movement.BaseSpeed * math.max(0.1f, AccelerationMultiplier));
                var deceleration = math.max(0.1f, movement.BaseSpeed * math.max(0.1f, DecelerationMultiplier));
                var accelLimit = targetSpeed > currentSpeed ? acceleration : deceleration;
                var maxDelta = accelLimit * effectiveDelta;
                var deltaV = desiredVelocity - currentVelocity;
                var deltaSq = math.lengthsq(deltaV);
                if (maxDelta > 0f && deltaSq > maxDelta * maxDelta)
                {
                    deltaV = math.normalizesafe(deltaV) * maxDelta;
                }

                currentVelocity += deltaV;
                movement.Velocity = currentVelocity;
                movement.DesiredVelocity = desiredVelocity;
                movement.CurrentSpeed = math.length(currentVelocity);

                // Apply movement using effective delta time
                if (movement.CurrentSpeed > VelocityThreshold)
                {
                    transform.Position += movement.Velocity * effectiveDelta;

                    var moveDirection = math.normalize(movement.Velocity);
                    // Use current rotation's up vector for 3D-aware rotation
                    // For ground units, this preserves upright orientation
                    // For flying/space units, this maintains consistent roll
                    OrientationHelpers.DeriveUpFromRotation(transform.Rotation, OrientationHelpers.WorldUp, out var currentUp);
                    OrientationHelpers.LookRotationSafe3D(moveDirection, currentUp, out movement.DesiredRotation);
                    transform.Rotation = math.slerp(transform.Rotation, movement.DesiredRotation, effectiveDelta * RotationSpeed);
                    movement.IsMoving = 1;
                }
                else
                {
                    movement.Velocity = float3.zero;
                    movement.DesiredVelocity = float3.zero;
                    movement.CurrentSpeed = 0f;
                    movement.IsMoving = 0;
                }

                movement.LastMoveTick = CurrentTick;
            }
        }
        
#if UNITY_EDITOR
        [BurstDiscard]
        private void LogFrozenEntities(ref SystemState state)
        {
            var frozenCount = 0;
            foreach (var (membership, _) in SystemAPI.Query<RefRO<TimeBubbleMembership>>().WithEntityAccess())
            {
                if (TimeHelpers.IsInStasis(membership.ValueRO))
                {
                    frozenCount++;
                }
            }
            if (frozenCount > 0)
            {
                UnityEngine.Debug.Log($"[Stasis] {frozenCount} entities frozen");
            }
        }
#endif
    }
}
