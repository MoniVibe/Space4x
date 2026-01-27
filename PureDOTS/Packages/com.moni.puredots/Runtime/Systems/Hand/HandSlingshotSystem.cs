using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Hand;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using HandStateData = PureDOTS.Runtime.Hand.HandState;
using Unity.Transforms;
using UnityEngine;
using UDebug = UnityEngine.Debug;

namespace PureDOTS.Systems.Hand
{
    /// <summary>
    /// Applies charge-scaled throws for slingshot releases.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(HandHoldFollowSystem))]
    [UpdateBefore(typeof(PhysicsInitializeGroup))]
    public partial struct HandSlingshotSystem : ISystem
    {
        private ComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<PhysicsGravityFactor> _gravityLookup;
        private ComponentLookup<HandHeldTag> _heldLookup;
        private ComponentLookup<MovementSuppressed> _movementLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<BeingThrown> _beingThrownLookup;
        private NativeParallelHashSet<Entity> _loggedFallbackEntities;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _velocityLookup = state.GetComponentLookup<PhysicsVelocity>(false);
            _gravityLookup = state.GetComponentLookup<PhysicsGravityFactor>(false);
            _heldLookup = state.GetComponentLookup<HandHeldTag>(false);
            _movementLookup = state.GetComponentLookup<MovementSuppressed>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _beingThrownLookup = state.GetComponentLookup<BeingThrown>(false);
            _loggedFallbackEntities = new NativeParallelHashSet<Entity>(128, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_loggedFallbackEntities.IsCreated)
            {
                _loggedFallbackEntities.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;
            var interactionPolicy = InteractionPolicy.CreateDefault();
            if (SystemAPI.TryGetSingleton(out InteractionPolicy policyValue))
            {
                interactionPolicy = policyValue;
            }
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            _velocityLookup.Update(ref state);
            _gravityLookup.Update(ref state);
            _heldLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _beingThrownLookup.Update(ref state);

            foreach (var (handStateRef, commandBuffer) in SystemAPI.Query<RefRW<HandStateData>, DynamicBuffer<HandCommand>>())
            {
                var handState = handStateRef.ValueRW;
                var buffer = commandBuffer;

                for (int i = buffer.Length - 1; i >= 0; i--)
                {
                    var cmd = buffer[i];
                    if (cmd.Tick != currentTick || cmd.Type != HandCommandType.SlingshotThrow)
                    {
                        continue;
                    }

                    if (ApplySlingshot(ref state, ref handState, cmd, interactionPolicy, ref ecb))
                    {
                        buffer.RemoveAt(i);
                    }
                }

                handStateRef.ValueRW = handState;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private bool ApplySlingshot(ref SystemState state, ref HandStateData handState, HandCommand command, InteractionPolicy interactionPolicy, ref EntityCommandBuffer ecb)
        {
            var target = command.TargetEntity;
            if (target == Entity.Null)
            {
                return false;
            }

            if (!_velocityLookup.HasComponent(target))
            {
                return ReleaseWithoutThrow(ref handState, target, ref ecb);
            }

            float speed = command.Speed > 0f ? command.Speed : math.lerp(10f, 35f, math.clamp(command.ChargeLevel, 0f, 1f));
            var velocity = _velocityLookup[target];
            velocity.Linear = math.normalizesafe(command.Direction, new float3(0f, 1f, 0f)) * speed;
            velocity.Angular = float3.zero;
            _velocityLookup[target] = velocity;

            if (_gravityLookup.HasComponent(target))
            {
                var gravity = _gravityLookup[target];
                gravity.Value = math.max(gravity.Value, 1f);
                _gravityLookup[target] = gravity;
            }

            if (_transformLookup.HasComponent(target))
            {
                var transform = _transformLookup[target];
                var thrown = new BeingThrown
                {
                    InitialVelocity = velocity.Linear,
                    TimeSinceThrow = 0f,
                    PrevPosition = transform.Position,
                    PrevRotation = transform.Rotation
                };

                if (_beingThrownLookup.HasComponent(target))
                {
                    ecb.SetComponent(target, thrown);
                    ecb.SetComponentEnabled<BeingThrown>(target, true);
                }
                else if (interactionPolicy.AllowStructuralFallback != 0)
                {
                    ecb.AddComponent(target, thrown);
                    if (interactionPolicy.LogStructuralFallback != 0)
                    {
                        LogFallbackOnce(target, "BeingThrown", skipped: false);
                    }
                }
                else if (interactionPolicy.LogStructuralFallback != 0)
                {
                    LogFallbackOnce(target, "BeingThrown", skipped: true);
                }
            }

            if (_heldLookup.HasComponent(target))
            {
                ecb.RemoveComponent<HandHeldTag>(target);
            }

            if (_movementLookup.HasComponent(target))
            {
                ecb.SetComponentEnabled<MovementSuppressed>(target, false);
            }

            if (handState.HeldEntity == target)
            {
                handState.HeldEntity = Entity.Null;
                handState.CurrentState = HandStateType.Cooldown;
                handState.StateTimer = 0;
            }

            return true;
        }

        [BurstDiscard]
        private void LogFallbackOnce(Entity target, string componentName, bool skipped)
        {
            if (!_loggedFallbackEntities.IsCreated || !_loggedFallbackEntities.Add(target))
            {
                return;
            }

            var action = skipped ? "skipping throw tag (strict policy)" : "using structural fallback";
            UDebug.LogWarning($"[HandSlingshotSystem] Missing {componentName} on entity {target.Index}:{target.Version}; {action}.");
        }

        private bool ReleaseWithoutThrow(ref HandStateData handState, Entity target, ref EntityCommandBuffer ecb)
        {
            if (_heldLookup.HasComponent(target))
            {
                ecb.RemoveComponent<HandHeldTag>(target);
            }

            if (_movementLookup.HasComponent(target))
            {
                ecb.SetComponentEnabled<MovementSuppressed>(target, false);
            }

            if (handState.HeldEntity == target)
            {
                handState.HeldEntity = Entity.Null;
                handState.CurrentState = HandStateType.Cooldown;
                handState.StateTimer = 0;
            }

            return true;
        }
    }
}



