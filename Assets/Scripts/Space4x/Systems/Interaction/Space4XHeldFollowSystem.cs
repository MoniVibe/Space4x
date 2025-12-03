using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Time;
using Space4X.Runtime.Interaction;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Space4X.Systems.Interaction
{
    /// <summary>
    /// Updates held entity positions to follow the god hand/camera.
    /// Handles different states: Holding, AboutToPick, PrimedToThrow
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XPickupSystem))]
    public partial struct Space4XHeldFollowSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<PickupState> _pickupStateLookup;
        private ComponentLookup<Unity.Physics.PhysicsVelocity> _physicsVelocityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _pickupStateLookup = state.GetComponentLookup<PickupState>(true);
            _physicsVelocityLookup = state.GetComponentLookup<Unity.Physics.PhysicsVelocity>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            // Skip during rewind playback
            if (rewindState.Mode == RewindMode.Playback)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _pickupStateLookup.Update(ref state);
            _physicsVelocityLookup.Update(ref state);

            var deltaTime = timeState.FixedDeltaTime;

            // Process all held entities
            foreach (var (heldByPlayer, transformRef, entity) in SystemAPI.Query<RefRO<HeldByPlayer>, RefRW<LocalTransform>>()
                .WithEntityAccess())
            {
                var held = heldByPlayer.ValueRO;
                var holderEntity = held.Holder;

                if (!state.EntityManager.Exists(holderEntity))
                {
                    continue;
                }

                // Get holder transform
                if (!_transformLookup.HasComponent(holderEntity))
                {
                    continue;
                }

                var holderTransform = _transformLookup[holderEntity];
                var pickupState = _pickupStateLookup.HasComponent(holderEntity)
                    ? _pickupStateLookup[holderEntity]
                    : default(PickupState);

                // Calculate target world position
                float3 targetPosition;
                quaternion targetRotation;

                if (pickupState.State == PickupStateType.AboutToPick)
                {
                    // In AboutToPick, lock to raycast position (handled by pickup system)
                    // Just maintain current position
                    targetPosition = transformRef.ValueRO.Position;
                    targetRotation = transformRef.ValueRO.Rotation;
                }
                else
                {
                    // Normal holding: follow holder with local offset
                    var rotatedOffset = math.mul(holderTransform.Rotation, held.LocalOffset);
                    targetPosition = holderTransform.Position + rotatedOffset;
                    targetRotation = holderTransform.Rotation; // Match holder rotation, or keep stable
                }

                // Update transform
                var transform = transformRef.ValueRO;
                transform.Position = targetPosition;
                transform.Rotation = targetRotation;
                transformRef.ValueRW = transform;

                // Zero out physics velocity to prevent physics from interfering
                if (_physicsVelocityLookup.HasComponent(entity))
                {
                    var velocity = _physicsVelocityLookup[entity];
                    velocity.Linear = float3.zero;
                    velocity.Angular = float3.zero;
                    _physicsVelocityLookup[entity] = velocity;
                }

                // Track movement for throw priming
                if (pickupState.State == PickupStateType.Holding || pickupState.State == PickupStateType.PrimedToThrow)
                {
                    var movementDelta = math.distance(holderTransform.Position, pickupState.LastHolderPosition);
                    if (movementDelta > 0.01f) // Threshold for movement detection
                    {
                        // Update pickup state to track movement (this will be read by ThrowSystem)
                        if (_pickupStateLookup.HasComponent(holderEntity))
                        {
                            var stateRef = SystemAPI.GetComponentRW<PickupState>(holderEntity);
                            ref var pickupStateRW = ref stateRef.ValueRW;
                            pickupStateRW.IsMoving = true;
                            pickupStateRW.LastHolderPosition = holderTransform.Position;

                            // Accumulate velocity for throw
                            var velocityDelta = (holderTransform.Position - pickupStateRW.LastHolderPosition) / deltaTime;
                            pickupStateRW.AccumulatedVelocity += velocityDelta * deltaTime;
                        }
                    }
                }
            }
        }
    }
}

