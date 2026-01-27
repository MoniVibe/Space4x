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
using Unity.Transforms;
using HandStateData = PureDOTS.Runtime.Hand.HandState;

namespace PureDOTS.Systems.Hand
{
    /// <summary>
    /// Applies critically damped spring forces so held entities follow the hand.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(HandPickupSystem))]
    [UpdateBefore(typeof(PhysicsInitializeGroup))]
    public partial struct HandHoldFollowSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<PhysicsMass> _massLookup;
        private ComponentLookup<HandPickable> _pickableLookup;
        private ComponentLookup<WorldManipulableTag> _worldManipulableLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<HandInputFrame>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _velocityLookup = state.GetComponentLookup<PhysicsVelocity>(false);
            _massLookup = state.GetComponentLookup<PhysicsMass>(true);
            _pickableLookup = state.GetComponentLookup<HandPickable>(true);
            _worldManipulableLookup = state.GetComponentLookup<WorldManipulableTag>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;
            float deltaTime = SystemAPI.Time.DeltaTime;
            var input = SystemAPI.GetSingleton<HandInputFrame>();
            var policy = new HandPickupPolicy
            {
                AutoPickDynamicPhysics = 0,
                EnableWorldGrab = 0,
                DebugWorldGrabAny = 0,
                WorldGrabRequiresTag = 1
            };
            if (SystemAPI.TryGetSingleton(out HandPickupPolicy policyValue))
            {
                policy = policyValue;
            }

            _transformLookup.Update(ref state);
            _velocityLookup.Update(ref state);
            _massLookup.Update(ref state);
            _pickableLookup.Update(ref state);
            _worldManipulableLookup.Update(ref state);

            foreach (var (handStateRef, commandBuffer) in SystemAPI.Query<RefRO<HandStateData>, DynamicBuffer<HandCommand>>())
            {
                var handState = handStateRef.ValueRO;
                var buffer = commandBuffer;

                for (int i = buffer.Length - 1; i >= 0; i--)
                {
                    var cmd = buffer[i];
                    if (cmd.Tick != currentTick || cmd.Type != HandCommandType.Hold)
                    {
                        continue;
                    }

                    if (ApplySpring(ref state, ref handState, cmd, deltaTime, input, policy))
                    {
                        buffer.RemoveAt(i);
                    }
                }
            }
        }

        private bool ApplySpring(ref SystemState state, ref HandStateData handState, HandCommand command, float deltaTime, in HandInputFrame input, HandPickupPolicy policy)
        {
            var target = command.TargetEntity;
            if (target == Entity.Null || !_transformLookup.HasComponent(target) || !_velocityLookup.HasComponent(target))
            {
                if (target == Entity.Null || !_transformLookup.HasComponent(target))
                {
                    return false;
                }

                bool worldGrabActive = policy.EnableWorldGrab != 0 && input.CtrlHeld && input.ShiftHeld;
                bool allowWorldDrag = policy.EnableWorldGrab != 0 &&
                    (worldGrabActive || policy.DebugWorldGrabAny != 0 || _worldManipulableLookup.HasComponent(target));
                if (!allowWorldDrag)
                {
                    return false;
                }

                var dragTransform = _transformLookup[target];
                float dragFollowFactor = 1f;
                if (_pickableLookup.HasComponent(target))
                {
                    dragFollowFactor = math.clamp(_pickableLookup[target].FollowLerp, 0.05f, 1f);
                }

                float3 dragTargetPosition = command.TargetPosition;
                dragTransform.Position = math.lerp(dragTransform.Position, dragTargetPosition, dragFollowFactor);
                _transformLookup[target] = dragTransform;
                return true;
            }

            var transform = _transformLookup[target];
            var velocity = _velocityLookup[target];

            float mass = 1f;
            if (_massLookup.HasComponent(target))
            {
                var physicsMass = _massLookup[target];
                if (physicsMass.InverseMass > 0f)
                {
                    mass = math.max(1f / physicsMass.InverseMass, 0.01f);
                }
            }
            else if (_pickableLookup.HasComponent(target))
            {
                mass = math.max(_pickableLookup[target].Mass, 0.01f);
            }

            float followFactor = 1f;
            if (_pickableLookup.HasComponent(target))
            {
                followFactor = math.clamp(_pickableLookup[target].FollowLerp, 0.05f, 1f);
            }

            float baseStiffness = 60f;
            float stiffness = baseStiffness * followFactor;
            float damping = 2f * math.sqrt(math.max(stiffness * mass, 0.0001f));

            float3 targetPosition = command.TargetPosition;
            float3 displacement = targetPosition - transform.Position;
            float3 springForce = displacement * stiffness;
            float3 dampingForce = velocity.Linear * damping;
            float3 acceleration = (springForce - dampingForce) / math.max(mass, 0.0001f);

            velocity.Linear += acceleration * deltaTime;
            _velocityLookup[target] = velocity;

            return true;
        }
    }
}
