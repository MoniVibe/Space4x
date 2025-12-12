using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems.Physics;
using Space4X.Runtime.Interaction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Systems.Interaction
{
    /// <summary>
    /// Handles pickup interaction using RMB input and Unity.Physics raycasts.
    /// Implements state machine: Empty → AboutToPick → Holding
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // Removed invalid UpdateAfter: PhysicsPostEventSystemGroup lives in FixedStep; cross-group ordering handled by event adapters.
    public partial struct Space4XPickupSystem : ISystem
    {
        private ComponentLookup<Pickable> _pickableLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<HeldByPlayer> _heldLookup;
        private EntityQuery _godHandQuery;

        // Cursor movement threshold in world space units (3 pixels converted)
        private const float CursorMovementThreshold = 0.1f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<PhysicsConfig>();

            _pickableLookup = state.GetComponentLookup<Pickable>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _heldLookup = state.GetComponentLookup<HeldByPlayer>(true);

            _godHandQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XGodHandTag, PickupState>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            // Skip during rewind playback
            if (rewindState.Mode == RewindMode.Playback)
            {
                return;
            }

            if (_godHandQuery.IsEmpty)
            {
                return;
            }

            var godHandEntity = _godHandQuery.GetSingletonEntity();
            var pickupStateRef = SystemAPI.GetComponentRW<PickupState>(godHandEntity);

            // Update lookups
            _pickableLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _heldLookup.Update(ref state);

            // Read input (non-Burst)
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            bool rmbDown = mouse.rightButton.isPressed;
            bool rmbWasPressed = mouse.rightButton.wasPressedThisFrame;
            bool rmbWasReleased = mouse.rightButton.wasReleasedThisFrame;

            // Get camera for raycast
            var camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                return;
            }

            // Get physics world for raycast
            if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorldSingleton))
            {
                return;
            }

            var collisionWorld = physicsWorldSingleton.CollisionWorld;

            // Perform raycast from camera
            var mousePosition = mouse.position.ReadValue();
            var ray = camera.ScreenPointToRay(mousePosition);
            var rayStart = ray.origin;
            var rayEnd = rayStart + ray.direction * 1000f; // Max distance

            var raycastInput = new RaycastInput
            {
                Start = rayStart,
                End = rayEnd,
                Filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = ~0u,
                    GroupIndex = 0
                }
            };

            bool hasHit = collisionWorld.CastRay(raycastInput, out var hit);
            Entity hitEntity = hasHit ? hit.Entity : Entity.Null;
            float3 hitPosition = hasHit ? hit.Position : float3.zero;

            // Update state machine
            ref var pickupState = ref pickupStateRef.ValueRW;
            var deltaTime = timeState.FixedDeltaTime;

            switch (pickupState.State)
            {
                case PickupStateType.Empty:
                    HandleEmptyState(ref state, ref pickupState, rmbWasPressed, hitEntity, hitPosition, godHandEntity);
                    break;

                case PickupStateType.AboutToPick:
                    HandleAboutToPickState(ref state, ref pickupState, rmbDown, rmbWasReleased, hitEntity, hitPosition, deltaTime, godHandEntity);
                    break;

                case PickupStateType.Holding:
                    // Handled by HeldFollowSystem and ThrowSystem
                    break;

                case PickupStateType.PrimedToThrow:
                    // Handled by ThrowSystem
                    break;

                case PickupStateType.Queued:
                    // Handled by ThrowQueueSystem
                    break;
            }
        }

        [BurstDiscard]
        private void HandleEmptyState(
            ref SystemState state,
            ref PickupState pickupState,
            bool rmbWasPressed,
            Entity hitEntity,
            float3 hitPosition,
            Entity godHandEntity)
        {
            if (!rmbWasPressed || hitEntity == Entity.Null)
            {
                return;
            }

            // Check if hit entity is pickable
            if (!_pickableLookup.HasComponent(hitEntity))
            {
                return;
            }

            // Check if entity is already held
            if (_heldLookup.HasComponent(hitEntity))
            {
                return;
            }

            // Transition to AboutToPick
            pickupState.State = PickupStateType.AboutToPick;
            pickupState.TargetEntity = hitEntity;
            pickupState.LastRaycastPosition = hitPosition;
            pickupState.CursorMovementAccumulator = 0f;
            pickupState.HoldTime = 0f;
            pickupState.AccumulatedVelocity = float3.zero;
            pickupState.IsMoving = false;
        }

        [BurstDiscard]
        private void HandleAboutToPickState(
            ref SystemState state,
            ref PickupState pickupState,
            bool rmbDown,
            bool rmbWasReleased,
            Entity hitEntity,
            float3 hitPosition,
            float deltaTime,
            Entity godHandEntity)
        {
            if (rmbWasReleased)
            {
                // Cancel pickup
                pickupState.State = PickupStateType.Empty;
                pickupState.TargetEntity = Entity.Null;
                return;
            }

            if (!rmbDown)
            {
                // RMB released, cancel
                pickupState.State = PickupStateType.Empty;
                pickupState.TargetEntity = Entity.Null;
                return;
            }

            // Update hold time
            pickupState.HoldTime += deltaTime;

            // Track cursor movement
            float movementDistance = math.distance(hitPosition, pickupState.LastRaycastPosition);
            pickupState.CursorMovementAccumulator += movementDistance;
            pickupState.LastRaycastPosition = hitPosition;

            // Check if cursor moved enough to transition to Holding
            if (pickupState.CursorMovementAccumulator > CursorMovementThreshold)
            {
                // Transition to Holding
                var targetEntity = pickupState.TargetEntity;
                if (targetEntity != Entity.Null && state.EntityManager.Exists(targetEntity))
                {
                    // Get holder transform (god hand entity)
                    var holderTransform = _transformLookup.HasComponent(godHandEntity)
                        ? _transformLookup[godHandEntity]
                        : new LocalTransform { Position = float3.zero, Rotation = quaternion.identity, Scale = 1f };

                    // Calculate local offset
                    var worldPos = hitPosition;
                    var localOffset = math.mul(math.inverse(holderTransform.Rotation), worldPos - holderTransform.Position);

                    // Add HeldByPlayer component
                    var ecb = new EntityCommandBuffer(Allocator.TempJob);
                    ecb.AddComponent(targetEntity, new HeldByPlayer
                    {
                        Holder = godHandEntity,
                        LocalOffset = localOffset,
                        HoldStartPosition = worldPos,
                        HoldStartTime = pickupState.HoldTime
                    });

                    // Add MovementSuppressed
                    ecb.AddComponent<MovementSuppressed>(targetEntity);

                    // Zero out physics velocity if entity has physics
                    if (state.EntityManager.HasComponent<Unity.Physics.PhysicsVelocity>(targetEntity))
                    {
                        var velocity = state.EntityManager.GetComponentData<Unity.Physics.PhysicsVelocity>(targetEntity);
                        velocity.Linear = float3.zero;
                        velocity.Angular = float3.zero;
                        ecb.SetComponent(targetEntity, velocity);
                    }

                    ecb.Playback(state.EntityManager);
                    ecb.Dispose();

                    // Update state
                    pickupState.State = PickupStateType.Holding;
                    pickupState.LastHolderPosition = holderTransform.Position;
                }
            }
        }
    }
}
