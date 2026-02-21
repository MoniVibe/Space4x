using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Hand;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems.Physics;
using Space4X.Runtime.Interaction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Pickable = PureDOTS.Runtime.Interaction.Pickable;

namespace Space4X.Systems.Interaction
{
    /// <summary>
    /// Handles pickup interaction using RMB input and Unity.Physics raycasts.
    /// Implements state machine: Empty → AboutToPick → Holding
    /// </summary>
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    // Removed invalid UpdateAfter: PhysicsPostEventSystemGroup lives in FixedStep; cross-group ordering handled by event adapters.
    public partial struct Space4XPickupSystem : ISystem
    {
        private ComponentLookup<Pickable> _pickableLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<HeldByPlayer> _heldLookup;
        private EntityQuery _godHandQuery;
        private uint _lastInputSampleId;
        private NativeParallelHashSet<Entity> _loggedFallbackEntities;

        // Cursor movement threshold in world space units (3 pixels converted)
        private const float CursorMovementThreshold = 0.1f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<PhysicsConfig>();
            state.RequireForUpdate<HandInputFrame>();
            state.RequireForUpdate<HandHover>();
            state.RequireForUpdate<Space4XControlModeRuntimeState>();

            _pickableLookup = state.GetComponentLookup<Pickable>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _heldLookup = state.GetComponentLookup<HeldByPlayer>(true);

            _godHandQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XGodHandTag, PickupState>()
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
            var modeState = SystemAPI.GetSingleton<Space4XControlModeRuntimeState>();
            if (modeState.IsDivineHandEnabled == 0)
            {
                return;
            }

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
            var handHover = SystemAPI.GetSingleton<HandHover>();
            var interactionPolicy = InteractionPolicy.CreateDefault();
            if (SystemAPI.TryGetSingleton(out InteractionPolicy policyValue))
            {
                interactionPolicy = policyValue;
            }

            // Update lookups
            _pickableLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _heldLookup.Update(ref state);

            // Check if pointer is over UI
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            bool isNewSample = inputFrame.SampleId != _lastInputSampleId;
            bool rmbDown = inputFrame.RmbHeld;
            bool rmbWasPressed = isNewSample && inputFrame.RmbPressed;
            bool rmbWasReleased = isNewSample && inputFrame.RmbReleased;

            var rayOrigin = inputFrame.RayOrigin;
            var rayDirection = math.normalizesafe(inputFrame.RayDirection, new float3(0f, 0f, 1f));
            Entity hitEntity = handHover.TargetEntity;
            float3 hitPosition = handHover.HitPosition;
            float hoverDistance = handHover.Distance;

            // Update state machine
            ref var pickupState = ref pickupStateRef.ValueRW;
            var deltaTime = timeState.FixedDeltaTime;

            switch (pickupState.State)
            {
                case PickupStateType.Empty:
                    HandleEmptyState(ref state, ref pickupState, rmbWasPressed, rayOrigin, rayDirection, hitEntity, hitPosition, hoverDistance, godHandEntity);
                    break;

                case PickupStateType.AboutToPick:
                    HandleAboutToPickState(ref state, ref pickupState, rmbDown, rmbWasReleased, rayOrigin, rayDirection, deltaTime, godHandEntity, interactionPolicy);
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

            if (isNewSample)
            {
                _lastInputSampleId = inputFrame.SampleId;
            }
        }

        [BurstDiscard]
        private void HandleEmptyState(
            ref SystemState state,
            ref PickupState pickupState,
            bool rmbWasPressed,
            float3 rayOrigin,
            float3 rayDirection,
            Entity hitEntity,
            float3 hitPosition,
            float hoverDistance,
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
            if (_heldLookup.HasComponent(hitEntity) && _heldLookup.IsComponentEnabled(hitEntity))
            {
                return;
            }

            // Transition to AboutToPick
            pickupState.State = PickupStateType.AboutToPick;
            pickupState.TargetEntity = hitEntity;
            pickupState.HoldDistance = hoverDistance;
            pickupState.LastRaycastPosition = rayOrigin + rayDirection * pickupState.HoldDistance;
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
            float3 rayOrigin,
            float3 rayDirection,
            float deltaTime,
            Entity godHandEntity,
            InteractionPolicy interactionPolicy)
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
            var rayPoint = rayOrigin + rayDirection * math.max(0f, pickupState.HoldDistance);
            float movementDistance = math.distance(rayPoint, pickupState.LastRaycastPosition);
            pickupState.CursorMovementAccumulator += movementDistance;
            pickupState.LastRaycastPosition = rayPoint;

            // Check if cursor moved enough to transition to Holding
            if (pickupState.CursorMovementAccumulator > CursorMovementThreshold)
            {
                // Transition to Holding
                var targetEntity = pickupState.TargetEntity;
                if (targetEntity != Entity.Null && state.EntityManager.Exists(targetEntity))
                {
                    bool hasHeldByPlayer = _heldLookup.HasComponent(targetEntity);
                    bool hasMovementSuppressed = state.EntityManager.HasComponent<MovementSuppressed>(targetEntity);
                    if ((!hasHeldByPlayer || !hasMovementSuppressed) && interactionPolicy.AllowStructuralFallback == 0)
                    {
                        if (interactionPolicy.LogStructuralFallback != 0)
                        {
                            var missing = hasHeldByPlayer
                                ? "MovementSuppressed"
                                : hasMovementSuppressed
                                    ? "HeldByPlayer"
                                    : "HeldByPlayer, MovementSuppressed";
                            LogFallbackOnce(targetEntity, missing, skipped: true);
                        }
                        pickupState.State = PickupStateType.Empty;
                        pickupState.TargetEntity = Entity.Null;
                        return;
                    }

                    // Get holder transform (god hand entity)
                    var holderTransform = _transformLookup.HasComponent(godHandEntity)
                        ? _transformLookup[godHandEntity]
                        : new LocalTransform { Position = float3.zero, Rotation = quaternion.identity, Scale = 1f };

                    var targetTransform = _transformLookup[targetEntity];
                    var worldPos = rayPoint;
                    var localOffset = math.mul(math.inverse(targetTransform.Rotation), worldPos - targetTransform.Position);

                    // Set HeldByPlayer component
                    var ecb = new EntityCommandBuffer(Allocator.TempJob);
                    var heldByPlayer = new HeldByPlayer
                    {
                        Holder = godHandEntity,
                        LocalOffset = localOffset,
                        HoldStartPosition = worldPos,
                        HoldStartTime = pickupState.HoldTime
                    };
                    if (hasHeldByPlayer)
                    {
                        ecb.SetComponent(targetEntity, heldByPlayer);
                        ecb.SetComponentEnabled<HeldByPlayer>(targetEntity, true);
                    }
                    else
                    {
                        ecb.AddComponent(targetEntity, heldByPlayer);
                        if (interactionPolicy.LogStructuralFallback != 0)
                        {
                            LogFallbackOnce(targetEntity, "HeldByPlayer", skipped: false);
                        }
                    }

                    // Enable MovementSuppressed
                    if (hasMovementSuppressed)
                    {
                        ecb.SetComponentEnabled<MovementSuppressed>(targetEntity, true);
                    }
                    else
                    {
                        ecb.AddComponent<MovementSuppressed>(targetEntity);
                        if (interactionPolicy.LogStructuralFallback != 0)
                        {
                            LogFallbackOnce(targetEntity, "MovementSuppressed", skipped: false);
                        }
                    }

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

        [BurstDiscard]
        private void LogFallbackOnce(Entity target, string missingComponents, bool skipped)
        {
            if (!_loggedFallbackEntities.IsCreated || !_loggedFallbackEntities.Add(target))
            {
                return;
            }

            var action = skipped ? "skipping pick (strict policy)" : "using structural fallback";
            UnityEngine.Debug.LogWarning($"[Space4XPickupSystem] Missing {missingComponents} on entity {target.Index}:{target.Version}; {action}.");
        }
    }
}
