using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Hand;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Time;
using Space4X.Runtime.Interaction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Space4X.Systems.Interaction
{
    /// <summary>
    /// Handles throw/drop mechanics: RMB release, 3s settle timer, movement detection, velocity accumulation.
    /// </summary>
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct Space4XThrowSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Unity.Physics.PhysicsVelocity> _physicsVelocityLookup;
        private ComponentLookup<PickupState> _pickupStateLookup;
        private EntityQuery _godHandQuery;
        private EntityQuery _heldEntitiesQuery;
        private uint _lastInputSampleId;
        private NativeParallelHashSet<Entity> _loggedFallbackEntities;

        // Settle timer threshold (3 seconds)
        private const float SettleHoldTime = 3f;
        // Base throw force
        private const float BaseThrowForce = 10f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<HandInputFrame>();
            state.RequireForUpdate<HandHover>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _physicsVelocityLookup = state.GetComponentLookup<Unity.Physics.PhysicsVelocity>(false);
            _pickupStateLookup = state.GetComponentLookup<PickupState>(false);

            _godHandQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XGodHandTag, PickupState>()
                .Build();

            _heldEntitiesQuery = SystemAPI.QueryBuilder()
                .WithAll<HeldByPlayer>()
                .Build();
            _loggedFallbackEntities = new NativeParallelHashSet<Entity>(128, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_loggedFallbackEntities.IsCreated)
            {
                _loggedFallbackEntities.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            // Only mutate during record mode (play)
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (_godHandQuery.IsEmpty)
            {
                return;
            }

            var godHandEntity = _godHandQuery.GetSingletonEntity();
            var pickupStateRef = SystemAPI.GetComponentRW<PickupState>(godHandEntity);
            var inputFrame = SystemAPI.GetSingleton<HandInputFrame>();
            var hover = SystemAPI.GetSingleton<HandHover>();
            var interactionPolicy = InteractionPolicy.CreateDefault();
            if (SystemAPI.TryGetSingleton(out InteractionPolicy policyValue))
            {
                interactionPolicy = policyValue;
            }

            _transformLookup.Update(ref state);
            _physicsVelocityLookup.Update(ref state);
            _pickupStateLookup.Update(ref state);

            bool isNewSample = inputFrame.SampleId != _lastInputSampleId;
            bool rmbDown = inputFrame.RmbHeld;
            bool rmbWasReleased = isNewSample && inputFrame.RmbReleased;
            bool shiftHeld = inputFrame.ShiftHeld;
            var aimDirection = math.normalizesafe(inputFrame.RayDirection, new float3(0f, 0f, 1f));

            ref var pickupState = ref pickupStateRef.ValueRW;
            var deltaTime = timeState.FixedDeltaTime;

            // Update hold time
            if (pickupState.State == PickupStateType.Holding || pickupState.State == PickupStateType.PrimedToThrow)
            {
                pickupState.HoldTime += deltaTime;

                // Check for 3s settle
                if (pickupState.HoldTime >= SettleHoldTime && rmbDown)
                {
                    HandleSettleToTerrain(ref state, pickupState.TargetEntity, hover);
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
                        HandleQueueThrow(ref state, godHandEntity, pickupState.TargetEntity, aimDirection, pickupState);
                        ResetPickupState(ref pickupState);
                    }
                    else
                    {
                        // Immediate throw
                        HandleThrow(ref state, pickupState.TargetEntity, aimDirection, pickupState, interactionPolicy);
                        ResetPickupState(ref pickupState);
                    }
                }
            }

            if (isNewSample)
            {
                _lastInputSampleId = inputFrame.SampleId;
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

            // Disable HeldByPlayer
            if (state.EntityManager.HasComponent<HeldByPlayer>(targetEntity))
            {
                ecb.SetComponentEnabled<HeldByPlayer>(targetEntity, false);
            }

            // Disable MovementSuppressed
            if (state.EntityManager.HasComponent<MovementSuppressed>(targetEntity))
            {
                ecb.SetComponentEnabled<MovementSuppressed>(targetEntity, false);
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
            float3 aimDirection,
            PickupState pickupState,
            InteractionPolicy interactionPolicy)
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
                // Fallback to input ray direction
                throwDirection = aimDirection;
            }

            // Calculate throw force
            float throwForce = BaseThrowForce + math.length(pickupState.AccumulatedVelocity) * 0.5f;
            float3 throwVelocity = throwDirection * throwForce;

            // Disable HeldByPlayer
            if (state.EntityManager.HasComponent<HeldByPlayer>(targetEntity))
            {
                ecb.SetComponentEnabled<HeldByPlayer>(targetEntity, false);
            }

            // Disable MovementSuppressed
            if (state.EntityManager.HasComponent<MovementSuppressed>(targetEntity))
            {
                ecb.SetComponentEnabled<MovementSuppressed>(targetEntity, false);
            }

            // Set physics velocity
            if (_physicsVelocityLookup.HasComponent(targetEntity))
            {
                var velocity = _physicsVelocityLookup[targetEntity];
                velocity.Linear = throwVelocity;
                velocity.Angular = float3.zero;
                ecb.SetComponent(targetEntity, velocity);
            }

            var prevPosition = float3.zero;
            var prevRotation = quaternion.identity;
            if (_transformLookup.HasComponent(targetEntity))
            {
                var transform = _transformLookup[targetEntity];
                prevPosition = transform.Position;
                prevRotation = transform.Rotation;
            }

            bool hasBeingThrown = state.EntityManager.HasComponent<BeingThrown>(targetEntity);
            if (!hasBeingThrown && interactionPolicy.AllowStructuralFallback == 0)
            {
                if (interactionPolicy.LogStructuralFallback != 0)
                {
                    LogFallbackOnce(targetEntity, "BeingThrown", skipped: true);
                }
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
                return;
            }

            // Enable BeingThrown component
            var thrown = new BeingThrown
            {
                InitialVelocity = throwVelocity,
                TimeSinceThrow = 0f,
                PrevPosition = prevPosition,
                PrevRotation = prevRotation
            };
            if (hasBeingThrown)
            {
                ecb.SetComponent(targetEntity, thrown);
                ecb.SetComponentEnabled<BeingThrown>(targetEntity, true);
            }
            else
            {
                ecb.AddComponent(targetEntity, thrown);
                if (interactionPolicy.LogStructuralFallback != 0)
                {
                    LogFallbackOnce(targetEntity, "BeingThrown", skipped: false);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstDiscard]
        private void LogFallbackOnce(Entity target, string missingComponents, bool skipped)
        {
            if (!_loggedFallbackEntities.IsCreated || !_loggedFallbackEntities.Add(target))
            {
                return;
            }

            var action = skipped ? "skipping throw tag (strict policy)" : "using structural fallback";
            UnityEngine.Debug.LogWarning($"[Space4XThrowSystem] Missing {missingComponents} on entity {target.Index}:{target.Version}; {action}.");
        }

        [BurstDiscard]
        private void HandleQueueThrow(
            ref SystemState state,
            Entity godHandEntity,
            Entity targetEntity,
            float3 aimDirection,
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
                throwDirection = aimDirection;
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

            // Disable HeldByPlayer and MovementSuppressed
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            if (state.EntityManager.HasComponent<HeldByPlayer>(targetEntity))
            {
                ecb.SetComponentEnabled<HeldByPlayer>(targetEntity, false);
            }
            if (state.EntityManager.HasComponent<MovementSuppressed>(targetEntity))
            {
                ecb.SetComponentEnabled<MovementSuppressed>(targetEntity, false);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstDiscard]
        private void HandleSettleToTerrain(ref SystemState state, Entity targetEntity, HandHover hover)
        {
            if (targetEntity == Entity.Null || !state.EntityManager.Exists(targetEntity))
            {
                return;
            }

            if (hover.TargetEntity != Entity.Null)
            {
                var ecb = new EntityCommandBuffer(Allocator.TempJob);

                // Move entity to hit position
                if (_transformLookup.HasComponent(targetEntity))
                {
                    var transform = _transformLookup[targetEntity];
                    transform.Position = hover.HitPosition;
                    ecb.SetComponent(targetEntity, transform);
                }

                // Disable HeldByPlayer and MovementSuppressed
                if (state.EntityManager.HasComponent<HeldByPlayer>(targetEntity))
                {
                    ecb.SetComponentEnabled<HeldByPlayer>(targetEntity, false);
                }
                if (state.EntityManager.HasComponent<MovementSuppressed>(targetEntity))
                {
                    ecb.SetComponentEnabled<MovementSuppressed>(targetEntity, false);
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
            pickupState.HoldDistance = 0f;
            pickupState.AccumulatedVelocity = float3.zero;
            pickupState.IsMoving = false;
        }
    }
}
