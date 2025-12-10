using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Time;
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
    /// Handles throw/drop mechanics: RMB release, 3s settle timer, movement detection, velocity accumulation.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XHeldFollowSystem))]
    public partial struct Space4XThrowSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Unity.Physics.PhysicsVelocity> _physicsVelocityLookup;
        private ComponentLookup<PickupState> _pickupStateLookup;
        private EntityQuery _godHandQuery;
        private EntityQuery _heldEntitiesQuery;

        // Settle timer threshold (3 seconds)
        private const float SettleHoldTime = 3f;
        // Base throw force
        private const float BaseThrowForce = 10f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _physicsVelocityLookup = state.GetComponentLookup<Unity.Physics.PhysicsVelocity>(false);
            _pickupStateLookup = state.GetComponentLookup<PickupState>(false);

            _godHandQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XGodHandTag, PickupState>()
                .Build();

            _heldEntitiesQuery = SystemAPI.QueryBuilder()
                .WithAll<HeldByPlayer>()
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

            _transformLookup.Update(ref state);
            _physicsVelocityLookup.Update(ref state);
            _pickupStateLookup.Update(ref state);

            // Read input
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null)
            {
                return;
            }

            bool rmbDown = mouse.rightButton.isPressed;
            bool rmbWasReleased = mouse.rightButton.wasReleasedThisFrame;
            bool shiftHeld = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

            // Get camera for direction calculation
            var camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                return;
            }

            ref var pickupState = ref pickupStateRef.ValueRW;
            var deltaTime = timeState.FixedDeltaTime;

            // Update hold time
            if (pickupState.State == PickupStateType.Holding || pickupState.State == PickupStateType.PrimedToThrow)
            {
                pickupState.HoldTime += deltaTime;

                // Check for 3s settle
                if (pickupState.HoldTime >= SettleHoldTime && rmbDown)
                {
                    HandleSettleToTerrain(ref state, pickupState.TargetEntity, camera);
                    return;
                }

                // Check for movement to prime throw
                if (pickupState.IsMoving && pickupState.State == PickupStateType.Holding)
                {
                    pickupState.State = PickupStateType.PrimedToThrow;
                }
            }

            // Handle RMB release
            if (rmbWasReleased)
            {
                if (pickupState.State == PickupStateType.Holding)
                {
                    // Drop (gentle release)
                    HandleDrop(ref state, pickupState.TargetEntity);
                    ResetPickupState(ref pickupState);
                }
                else if (pickupState.State == PickupStateType.PrimedToThrow)
                {
                    if (shiftHeld)
                    {
                        // Queue throw
                        HandleQueueThrow(ref state, godHandEntity, pickupState.TargetEntity, camera, pickupState);
                        ResetPickupState(ref pickupState);
                    }
                    else
                    {
                        // Immediate throw
                        HandleThrow(ref state, pickupState.TargetEntity, camera, pickupState);
                        ResetPickupState(ref pickupState);
                    }
                }
            }
        }

        [BurstDiscard]
        private void HandleDrop(ref SystemState state, Entity targetEntity)
        {
            if (targetEntity == Entity.Null || !state.EntityManager.Exists(targetEntity))
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Remove HeldByPlayer
            if (state.EntityManager.HasComponent<HeldByPlayer>(targetEntity))
            {
                ecb.RemoveComponent<HeldByPlayer>(targetEntity);
            }

            // Remove MovementSuppressed
            if (state.EntityManager.HasComponent<MovementSuppressed>(targetEntity))
            {
                ecb.RemoveComponent<MovementSuppressed>(targetEntity);
            }

            // Set velocity to zero (gentle drop)
            if (_physicsVelocityLookup.HasComponent(targetEntity))
            {
                var velocity = _physicsVelocityLookup[targetEntity];
                velocity.Linear = float3.zero;
                velocity.Angular = float3.zero;
                ecb.SetComponent(targetEntity, velocity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstDiscard]
        private void HandleThrow(
            ref SystemState state,
            Entity targetEntity,
            UnityEngine.Camera camera,
            PickupState pickupState)
        {
            if (targetEntity == Entity.Null || !state.EntityManager.Exists(targetEntity))
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Calculate throw direction
            float3 throwDirection;
            if (math.lengthsq(pickupState.AccumulatedVelocity) > 0.01f)
            {
                // Use accumulated movement velocity
                throwDirection = math.normalize(pickupState.AccumulatedVelocity);
            }
            else
            {
                // Fallback to camera forward
                throwDirection = camera.transform.forward;
            }

            // Calculate throw force
            float throwForce = BaseThrowForce + math.length(pickupState.AccumulatedVelocity) * 0.5f;
            float3 throwVelocity = throwDirection * throwForce;

            // Remove HeldByPlayer
            if (state.EntityManager.HasComponent<HeldByPlayer>(targetEntity))
            {
                ecb.RemoveComponent<HeldByPlayer>(targetEntity);
            }

            // Remove MovementSuppressed
            if (state.EntityManager.HasComponent<MovementSuppressed>(targetEntity))
            {
                ecb.RemoveComponent<MovementSuppressed>(targetEntity);
            }

            // Set physics velocity
            if (_physicsVelocityLookup.HasComponent(targetEntity))
            {
                var velocity = _physicsVelocityLookup[targetEntity];
                velocity.Linear = throwVelocity;
                velocity.Angular = float3.zero;
                ecb.SetComponent(targetEntity, velocity);
            }

            // Add BeingThrown component
            ecb.AddComponent(targetEntity, new BeingThrown
            {
                InitialVelocity = throwVelocity,
                TimeSinceThrow = 0f
            });

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstDiscard]
        private void HandleQueueThrow(
            ref SystemState state,
            Entity godHandEntity,
            Entity targetEntity,
            UnityEngine.Camera camera,
            PickupState pickupState)
        {
            if (targetEntity == Entity.Null || !state.EntityManager.Exists(targetEntity))
            {
                return;
            }

            // Calculate throw direction
            float3 throwDirection;
            if (math.lengthsq(pickupState.AccumulatedVelocity) > 0.01f)
            {
                throwDirection = math.normalize(pickupState.AccumulatedVelocity);
            }
            else
            {
                throwDirection = camera.transform.forward;
            }

            // Calculate throw force
            float throwForce = BaseThrowForce + math.length(pickupState.AccumulatedVelocity) * 0.5f;

            // Add to throw queue
            if (state.EntityManager.HasBuffer<ThrowQueue>(godHandEntity))
            {
                var queue = state.EntityManager.GetBuffer<ThrowQueue>(godHandEntity);
                queue.Add(new ThrowQueue
                {
                    Value = new ThrowQueueEntry
                    {
                        Target = targetEntity,
                        Direction = throwDirection,
                        Force = throwForce
                    }
                });
            }

            // Remove HeldByPlayer and MovementSuppressed
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            if (state.EntityManager.HasComponent<HeldByPlayer>(targetEntity))
            {
                ecb.RemoveComponent<HeldByPlayer>(targetEntity);
            }
            if (state.EntityManager.HasComponent<MovementSuppressed>(targetEntity))
            {
                ecb.RemoveComponent<MovementSuppressed>(targetEntity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstDiscard]
        private void HandleSettleToTerrain(ref SystemState state, Entity targetEntity, UnityEngine.Camera camera)
        {
            if (targetEntity == Entity.Null || !state.EntityManager.Exists(targetEntity))
            {
                return;
            }

            // Raycast to terrain from camera
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var mousePosition = mouse.position.ReadValue();
            var ray = camera.ScreenPointToRay(mousePosition);

            // Simple terrain raycast (assuming Y=0 is ground, or use physics raycast)
            if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorldSingleton))
            {
                return;
            }

            var collisionWorld = physicsWorldSingleton.CollisionWorld;
            var raycastInput = new RaycastInput
            {
                Start = ray.origin,
                End = ray.origin + ray.direction * 1000f,
                Filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = ~0u,
                    GroupIndex = 0
                }
            };

            if (collisionWorld.CastRay(raycastInput, out var hit))
            {
                var ecb = new EntityCommandBuffer(Allocator.TempJob);

                // Move entity to hit position
                if (_transformLookup.HasComponent(targetEntity))
                {
                    var transform = _transformLookup[targetEntity];
                    transform.Position = hit.Position;
                    ecb.SetComponent(targetEntity, transform);
                }

                // Remove HeldByPlayer and MovementSuppressed
                if (state.EntityManager.HasComponent<HeldByPlayer>(targetEntity))
                {
                    ecb.RemoveComponent<HeldByPlayer>(targetEntity);
                }
                if (state.EntityManager.HasComponent<MovementSuppressed>(targetEntity))
                {
                    ecb.RemoveComponent<MovementSuppressed>(targetEntity);
                }

                // Zero velocity
                if (_physicsVelocityLookup.HasComponent(targetEntity))
                {
                    var velocity = _physicsVelocityLookup[targetEntity];
                    velocity.Linear = float3.zero;
                    velocity.Angular = float3.zero;
                    ecb.SetComponent(targetEntity, velocity);
                }

                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }
        }

        private void ResetPickupState(ref PickupState pickupState)
        {
            pickupState.State = PickupStateType.Empty;
            pickupState.TargetEntity = Entity.Null;
            pickupState.LastRaycastPosition = float3.zero;
            pickupState.CursorMovementAccumulator = 0f;
            pickupState.HoldTime = 0f;
            pickupState.AccumulatedVelocity = float3.zero;
            pickupState.IsMoving = false;
        }
    }
}
