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
using UnityEngine;
using UDebug = UnityEngine.Debug;
using HandStateData = PureDOTS.Runtime.Hand.HandState;
using InteractionPickable = PureDOTS.Runtime.Interaction.Pickable;

namespace PureDOTS.Systems.Hand
{
    /// <summary>
    /// Handles pickup commands: attaches HandHeldTag, suppresses movement,
    /// zeros physics velocity, and updates HandState.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(HandCommandEmitterSystem))]
    [UpdateBefore(typeof(PhysicsInitializeGroup))]
    public partial struct HandPickupSystem : ISystem
    {
        private ComponentLookup<PickableTag> _pickableTagLookup;
        private ComponentLookup<InteractionPickable> _pickableLookup;
        private ComponentLookup<HandHeldTag> _heldLookup;
        private ComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<WorldManipulableTag> _worldManipulableLookup;
        private ComponentLookup<NeverPickableTag> _neverPickableLookup;
        private ComponentLookup<MovementSuppressed> _movementSuppressedLookup;
        private NativeParallelHashSet<Entity> _loggedFallbackEntities;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<HandInputFrame>();
            _pickableTagLookup = state.GetComponentLookup<PickableTag>(true);
            _pickableLookup = state.GetComponentLookup<InteractionPickable>(true);
            _heldLookup = state.GetComponentLookup<HandHeldTag>(false);
            _velocityLookup = state.GetComponentLookup<PhysicsVelocity>(false);
            _worldManipulableLookup = state.GetComponentLookup<WorldManipulableTag>(true);
            _neverPickableLookup = state.GetComponentLookup<NeverPickableTag>(true);
            _movementSuppressedLookup = state.GetComponentLookup<MovementSuppressed>(true);
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
            var input = SystemAPI.GetSingleton<HandInputFrame>();
            var interactionPolicy = InteractionPolicy.CreateDefault();
            if (SystemAPI.TryGetSingleton(out InteractionPolicy interactionPolicyValue))
            {
                interactionPolicy = interactionPolicyValue;
            }
            var policy = new HandPickupPolicy
            {
                AutoPickDynamicPhysics = 0,
                EnableWorldGrab = 0,
                DebugWorldGrabAny = 0,
                WorldGrabRequiresTag = 1
            };
            if (SystemAPI.TryGetSingleton(out HandPickupPolicy pickupPolicyValue))
            {
                policy = pickupPolicyValue;
            }
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            _pickableTagLookup.Update(ref state);
            _pickableLookup.Update(ref state);
            _heldLookup.Update(ref state);
            _velocityLookup.Update(ref state);
            _worldManipulableLookup.Update(ref state);
            _neverPickableLookup.Update(ref state);
            _movementSuppressedLookup.Update(ref state);

            foreach (var (handStateRef, commandBuffer, handEntity) in SystemAPI.Query<RefRW<HandStateData>, DynamicBuffer<HandCommand>>().WithEntityAccess())
            {
                var handState = handStateRef.ValueRW;
                var buffer = commandBuffer;

                for (int i = buffer.Length - 1; i >= 0; i--)
                {
                    var cmd = buffer[i];
                    if (cmd.Tick != currentTick || cmd.Type != HandCommandType.Pick)
                    {
                        continue;
                    }

                    if (ProcessPickCommand(ref state, handEntity, ref handState, cmd, input, policy, interactionPolicy, ref ecb))
                    {
                        buffer.RemoveAt(i);
                    }
                }

                handStateRef.ValueRW = handState;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private bool ProcessPickCommand(ref SystemState state, Entity handEntity, ref HandStateData handState, HandCommand cmd, in HandInputFrame input, HandPickupPolicy policy, InteractionPolicy interactionPolicy, ref EntityCommandBuffer ecb)
        {
            var target = cmd.TargetEntity;
            if (target == Entity.Null || !state.EntityManager.Exists(target))
            {
                return false;
            }

            // Already held by someone else? skip
            if (_heldLookup.HasComponent(target))
            {
                return false;
            }

            bool neverPickable = _neverPickableLookup.HasComponent(target);
            if (neverPickable)
            {
                return false;
            }

            // Require pickable tag/component if present
            bool hasPickable = _pickableTagLookup.HasComponent(target) || _pickableLookup.HasComponent(target);
            bool hasPhysicsVelocity = _velocityLookup.HasComponent(target);
            bool autoPickDynamic = policy.AutoPickDynamicPhysics != 0 && hasPhysicsVelocity;
            bool hasWorldTag = _worldManipulableLookup.HasComponent(target);
            bool worldGrabActive = policy.EnableWorldGrab != 0 && input.CtrlHeld && input.ShiftHeld;
            bool worldGrabAllowed = worldGrabActive &&
                (policy.DebugWorldGrabAny != 0 || policy.WorldGrabRequiresTag == 0 || hasWorldTag);

            if (!hasPickable && !autoPickDynamic && !worldGrabAllowed)
            {
                return false;
            }

            bool hasMovementSuppressed = _movementSuppressedLookup.HasComponent(target);
            if (!hasMovementSuppressed && interactionPolicy.AllowStructuralFallback == 0)
            {
                if (interactionPolicy.LogStructuralFallback != 0)
                {
                    LogFallbackOnce(target, "MovementSuppressed", skipped: true);
                }
                return false;
            }

            ecb.AddComponent(target, new HandHeldTag { Holder = handEntity });
            if (hasMovementSuppressed)
            {
                ecb.SetComponentEnabled<MovementSuppressed>(target, true);
            }
            else
            {
                ecb.AddComponent<MovementSuppressed>(target);
                if (interactionPolicy.LogStructuralFallback != 0)
                {
                    LogFallbackOnce(target, "MovementSuppressed", skipped: false);
                }
            }

            if (hasPhysicsVelocity)
            {
                var velocity = _velocityLookup[target];
                velocity.Linear = float3.zero;
                velocity.Angular = float3.zero;
                ecb.SetComponent(target, velocity);
            }

            handState.HeldEntity = target;
            handState.CurrentState = HandStateType.Holding;
            handState.StateTimer = 0;
            handState.HoldPoint = cmd.TargetPosition;
            handState.HoldDistance = math.max(handState.HoldDistance, 0f);

            return true;
        }

        [BurstDiscard]
        private void LogFallbackOnce(Entity target, string componentName, bool skipped)
        {
            if (!_loggedFallbackEntities.IsCreated || !_loggedFallbackEntities.Add(target))
            {
                return;
            }

            var action = skipped ? "skipping pick (strict policy)" : "using structural fallback";
            UDebug.LogWarning($"[HandPickupSystem] Missing {componentName} on entity {target.Index}:{target.Version}; {action}.");
        }
    }
}
