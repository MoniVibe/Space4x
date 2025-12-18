using PureDOTS.Runtime.Components;
using PureDOTS.Runtime;
using PureDOTS.Systems;
using Space4X.Physics;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Runtime.Interaction;
using Space4X.Presentation.Camera;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

using ScenarioKind = PureDOTS.Runtime.ScenarioKind;
using ScenarioState = PureDOTS.Runtime.ScenarioState;
using Space4XHandState = Space4X.Runtime.Interaction.HandState;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Systems.Interaction
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Debug-only hand interaction system for grabbing and throwing entities.
    /// Allows grabbing entities via mouse raycast and throwing them.
    /// </summary>
    /// <remarks>
    /// This is a debug tool, not canonical gameplay. Uses non-deterministic input.
    /// </remarks>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XCameraSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.GameplaySystemGroup))]
    public partial struct Space4XHandInteractionSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VesselMovement> _vesselMovementLookup;
        private ComponentLookup<SpaceVelocity> _spaceVelocityLookup;
        private ComponentLookup<SpacePhysicsBody> _physicsBodyLookup;

        [BurstDiscard]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XHandState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<PhysicsWorldSingleton>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _vesselMovementLookup = state.GetComponentLookup<VesselMovement>(false);
            _spaceVelocityLookup = state.GetComponentLookup<SpaceVelocity>(false);
            _physicsBodyLookup = state.GetComponentLookup<SpacePhysicsBody>(true);
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            // Check scenario - only run in AllSystemsShowcase, Space4XPhysicsOnly, or HandThrowSandbox
            var scenario = SystemAPI.GetSingleton<ScenarioState>().Current;
            if (scenario != ScenarioKind.AllSystemsShowcase && 
                scenario != ScenarioKind.Space4XPhysicsOnly && 
                scenario != ScenarioKind.HandThrowSandbox)
            {
                return;
            }

            // Skip during rewind playback (debug-only tool)
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Skip if paused
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            // Get camera state (required for raycast)
            if (!SystemAPI.TryGetSingleton<Space4XCameraState>(out var cameraState))
            {
                return; // Camera not ready yet
            }

            // Get physics world for raycasting
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

            // Update lookups
            _transformLookup.Update(ref state);
            _vesselMovementLookup.Update(ref state);
            _spaceVelocityLookup.Update(ref state);
            _physicsBodyLookup.Update(ref state);

            // Get hand state
            var handStateEntity = SystemAPI.GetSingletonEntity<Space4XHandState>();
            var handState = SystemAPI.GetComponent<Space4XHandState>(handStateEntity);

            var deltaTime = timeState.FixedDeltaTime > 0f ? timeState.FixedDeltaTime : Time.deltaTime;

            // Read input (debug-only, non-deterministic)
            bool mouseDown = GetMouseButtonDown(0);
            bool mouseHeld = GetMouseButton(0);
            bool mouseUp = GetMouseButtonUp(0);
            bool rightMouseHeld = GetMouseButton(1); // Right mouse for charging
            bool modifierHeld = GetModifierKey(); // Shift or Ctrl
            float scrollDelta = GetScrollDelta();

            // Handle scroll wheel distance adjustment
            if (math.abs(scrollDelta) > 0.001f)
            {
                handState.GrabDistance = math.clamp(
                    handState.GrabDistance + scrollDelta * 5f, // 5 units per scroll tick
                    5f, // min distance
                    100f); // max distance
            }

            // Compute camera forward direction
            var camPos = cameraState.Position;
            var camForward = math.mul(cameraState.Rotation, math.float3(0f, 0f, 1f));

            // Handle grab attempt (mouse down)
            if (mouseDown && !handState.IsGrabbing)
            {
                TryGrabEntity(ref state, physicsWorld, camPos, camForward, ref handState);
            }

            // Handle dragging (mouse held)
            if (handState.IsGrabbing && mouseHeld && handState.Grabbed != Entity.Null)
            {
                // Verify entity still exists
                if (!state.EntityManager.Exists(handState.Grabbed) || !_transformLookup.HasComponent(handState.Grabbed))
                {
                    // Entity was destroyed, release grab
                    ReleaseGrab(ref state, ref handState);
                }
                else
                {
                    // Update charge state
                    handState.IsCharging = rightMouseHeld || modifierHeld;
                    if (handState.IsCharging)
                    {
                        handState.ChargeTime += deltaTime;
                    }
                    else
                    {
                        handState.ChargeTime = 0f;
                    }

                    DragEntity(ref state, camPos, camForward, deltaTime, ref handState);
                }
            }

            // Handle throw (mouse up)
            if (mouseUp && handState.IsGrabbing && handState.Grabbed != Entity.Null)
            {
                ThrowEntity(ref state, camPos, camForward, timeState.Tick, ref handState);
            }

            // Update hand state singleton
            SystemAPI.SetComponent(handStateEntity, handState);
        }

        [BurstDiscard]
        private void TryGrabEntity(ref SystemState state, PhysicsWorldSingleton physicsWorld, float3 camPos, float3 camForward, ref Space4XHandState handState)
        {
            // Build layer mask for grabbable entities
            uint layerMask = Space4XPhysicsLayers.GetBelongsToMask(Space4XPhysicsLayer.Ship)
                          | Space4XPhysicsLayers.GetBelongsToMask(Space4XPhysicsLayer.Asteroid)
                          | Space4XPhysicsLayers.GetBelongsToMask(Space4XPhysicsLayer.Miner);

            // Build raycast input
            var maxDistance = 200f;
            var rayEnd = camPos + camForward * maxDistance;

            var raycastInput = new RaycastInput
            {
                Start = camPos,
                End = rayEnd,
                Filter = new CollisionFilter
                {
                    BelongsTo = layerMask,
                    CollidesWith = ~0u, // Collide with all layers
                    GroupIndex = 0
                }
            };

            // Perform raycast
            if (physicsWorld.CastRay(raycastInput, out var hit))
            {
                var hitEntity = hit.Entity;
                
                // Verify entity has required components
                if (!_transformLookup.HasComponent(hitEntity))
                {
                    return;
                }

                // Prefer entities with ThrowableTag (rocks, etc.)
                // Fall back to layer-based logic for backward compatibility
                bool isThrowable = state.EntityManager.HasComponent<ThrowableTag>(hitEntity);
                bool shouldGrab = isThrowable || layerMask != 0; // Always grab if hit (layer mask already filtered)

                if (!shouldGrab)
                {
                    return;
                }

                var hitTransform = _transformLookup[hitEntity];
                var hitPoint = hit.Position;

                // Store grab state
                handState.Grabbed = hitEntity;
                handState.GrabDistance = math.distance(camPos, hitPoint);
                handState.LocalOffset = hitPoint - hitTransform.Position;
                handState.LastFramePos = camPos + camForward * handState.GrabDistance;
                handState.IsGrabbing = true;

                // Add GrabbedTag to disable AI
                var handStateEntity = SystemAPI.GetSingletonEntity<Space4XHandState>();
                if (!state.EntityManager.HasComponent<GrabbedTag>(hitEntity))
                {
                    state.EntityManager.AddComponent<GrabbedTag>(hitEntity);
                }
                // Set the holder
                var grabbedTag = new GrabbedTag { Holder = handStateEntity };
                state.EntityManager.SetComponentData(hitEntity, grabbedTag);

                var entityType = isThrowable ? "throwable" : "entity";
                UnityDebug.Log($"[Space4XHandInteractionSystem] Grabbed {entityType} {hitEntity.Index}");
            }
        }

        [BurstDiscard]
        private void DragEntity(ref SystemState state, float3 camPos, float3 camForward, float deltaTime, ref Space4XHandState handState)
        {
            var grabbedEntity = handState.Grabbed;
            if (grabbedEntity == Entity.Null || !_transformLookup.HasComponent(grabbedEntity))
            {
                return;
            }

            // Compute desired hand position
            var handPos = camPos + camForward * handState.GrabDistance;

            // Compute target entity position
            var targetPos = handPos - handState.LocalOffset;

            // Update entity transform directly (ECS-authoritative)
            var transform = _transformLookup[grabbedEntity];
            transform.Position = targetPos;
            _transformLookup[grabbedEntity] = transform;

            // Always compute hand velocity (for throw calculation and slingshot)
            if (deltaTime > 0.001f)
            {
                var handVelocity = (handPos - handState.LastFramePos) / deltaTime;
                handState.CurrentHandVel = handVelocity;
                handState.LastFramePos = handPos;
            }
            else
            {
                handState.CurrentHandVel = float3.zero;
                handState.LastFramePos = handPos;
            }

            // Compute pull direction (from grab point to current hand)
            var grabPoint = transform.Position + handState.LocalOffset;
            handState.PullDirection = handPos - grabPoint;

            // Clear velocity while dragging (hand controls position)
            if (_vesselMovementLookup.HasComponent(grabbedEntity))
            {
                var movement = _vesselMovementLookup[grabbedEntity];
                movement.Velocity = float3.zero;
                movement.IsMoving = 0;
                _vesselMovementLookup[grabbedEntity] = movement;
            }

            if (_spaceVelocityLookup.HasComponent(grabbedEntity))
            {
                var velocity = _spaceVelocityLookup[grabbedEntity];
                velocity.Linear = float3.zero;
                _spaceVelocityLookup[grabbedEntity] = velocity;
            }
        }

        [BurstDiscard]
        private void ThrowEntity(ref SystemState state, float3 camPos, float3 camForward, uint tick, ref Space4XHandState handState)
        {
            var grabbedEntity = handState.Grabbed;
            if (grabbedEntity == Entity.Null)
            {
                return;
            }

            // Build throw request using helper
            var throwRequest = BuildThrowRequest(handState, camForward, tick);

            // Get throw request buffer and add request
            var handStateEntity = SystemAPI.GetSingletonEntity<Space4XHandState>();
            if (state.EntityManager.HasBuffer<ThrowRequest>(handStateEntity))
            {
                var throwRequestBuffer = state.EntityManager.GetBuffer<ThrowRequest>(handStateEntity);
                throwRequestBuffer.Add(throwRequest);
                UnityDebug.Log($"[Space4XHandInteractionSystem] Queued throw request for entity {grabbedEntity.Index} (strength: {throwRequest.Strength}, charge: {handState.ChargeTime:F2}s)");
            }

            // Remove GrabbedTag
            if (state.EntityManager.HasComponent<GrabbedTag>(grabbedEntity))
            {
                state.EntityManager.RemoveComponent<GrabbedTag>(grabbedEntity);
            }

            // Clear grab state
            handState.Grabbed = Entity.Null;
            handState.IsGrabbing = false;
            handState.IsCharging = false;
            handState.ChargeTime = 0f;
            handState.LocalOffset = float3.zero;
            handState.CurrentHandVel = float3.zero;
            handState.PullDirection = float3.zero;
        }

        /// <summary>
        /// Builds a throw request from hand state. Used for queuing throws and enabling slingshot mechanics.
        /// </summary>
        private static ThrowRequest BuildThrowRequest(Space4XHandState hand, float3 camForward, uint tick)
        {
            var dir = math.normalizesafe(camForward + 0.3f * math.normalizesafe(hand.CurrentHandVel));
            var baseStrength = math.length(hand.CurrentHandVel);

            var chargeFactor = hand.IsCharging
                ? math.saturate(hand.ChargeTime / 1.0f) // 0–1 sec charge
                : 1f;

            return new ThrowRequest
            {
                Target = hand.Grabbed,
                Direction = dir,
                Strength = baseStrength * chargeFactor,
                Origin = hand.LastFramePos,
                Tick = tick,
            };
        }

        [BurstDiscard]
        private bool GetMouseButtonDown(int button)
        {
            if (Mouse.current == null) return false;
            return button == 0 ? Mouse.current.leftButton.wasPressedThisFrame : 
                   button == 1 ? Mouse.current.rightButton.wasPressedThisFrame : 
                   Mouse.current.middleButton.wasPressedThisFrame;
        }

        [BurstDiscard]
        private bool GetMouseButton(int button)
        {
            if (Mouse.current == null) return false;
            return button == 0 ? Mouse.current.leftButton.isPressed : 
                   button == 1 ? Mouse.current.rightButton.isPressed : 
                   Mouse.current.middleButton.isPressed;
        }

        [BurstDiscard]
        private bool GetMouseButtonUp(int button)
        {
            if (Mouse.current == null) return false;
            return button == 0 ? Mouse.current.leftButton.wasReleasedThisFrame : 
                   button == 1 ? Mouse.current.rightButton.wasReleasedThisFrame : 
                   Mouse.current.middleButton.wasReleasedThisFrame;
        }

        [BurstDiscard]
        private float GetScrollDelta()
        {
            if (Mouse.current == null) return 0f;
            return Mouse.current.scroll.ReadValue().y / 120f; // Normalize scroll
        }

        [BurstDiscard]
        private bool GetModifierKey()
        {
            if (Keyboard.current == null) return false;
            return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.leftCtrlKey.isPressed;
        }

        [BurstDiscard]
        private void ReleaseGrab(ref SystemState state, ref Space4XHandState handState)
        {
            if (handState.Grabbed != Entity.Null && state.EntityManager.Exists(handState.Grabbed))
            {
                // Remove GrabbedTag
                if (state.EntityManager.HasComponent<GrabbedTag>(handState.Grabbed))
                {
                    state.EntityManager.RemoveComponent<GrabbedTag>(handState.Grabbed);
                }
            }

            // Clear grab state
            handState.Grabbed = Entity.Null;
            handState.IsGrabbing = false;
            handState.LocalOffset = float3.zero;
        }
    }
}
