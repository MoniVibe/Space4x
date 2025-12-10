using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Space4X.Physics;
using Space4X.Registry;
using Space4X.Runtime;
using Shared.Demo;
using UnityEngine;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Physics;
using Space4X.Runtime.Interaction;
using Space4XHandState = Space4X.Runtime.Interaction.HandState;

namespace Space4X.Demo
{
    /// <summary>
    /// Simple physics test bootstrap: spawns one miner and one asteroid on a collision course.
    /// Used to verify physics collision detection is working correctly.
    /// 
    /// Setup:
    /// - Asteroid at origin (0, 0, 0) with sphere collider, radius 2
    /// - Miner at (0, 0, -20) with sphere collider, radius 1
    /// - Miner has forward velocity (0, 0, 10) to move toward asteroid
    /// - Both have physics components so PhysicsBodyBootstrapSystem will create Havok bodies
    /// 
    /// Expected result:
    /// - Miner should collide with asteroid and generate collision events
    /// - Collision events should be logged by Space4XCollisionResponseSystem
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Shared.Demo.SharedDemoRenderBootstrap))]
    public partial struct Space4XPhysicsTestBootstrapSystem : ISystem
    {
        private bool _spawned;

        public void OnCreate(ref SystemState state)
        {
            Debug.Log("[Space4XPhysicsTestBootstrapSystem] OnCreate");
            state.RequireForUpdate<DemoRenderReady>();
            state.RequireForUpdate<PhysicsConfig>(); // Ensure physics world exists
        }

        // NOTE: Not Burst compiled because DemoRenderUtil.MakeRenderable uses managed code
        public void OnUpdate(ref SystemState state)
        {
            if (_spawned)
            {
                state.Enabled = false;
                return;
            }

            Debug.Log("[Space4XPhysicsTestBootstrapSystem] Spawning physics collision test: miner vs asteroid");

            var em = state.EntityManager;

            // --- ASTEROID at origin (0, 0, 0) ---
            var asteroidEntity = em.CreateEntity();
            var asteroidPosition = new float3(0f, 0f, 0f);
            
            // Setup rendering
            DemoRenderUtil.MakeRenderable(
                em,
                asteroidEntity,
                asteroidPosition,
                new float3(2f, 2f, 2f), // Scale to match collider radius
                new float4(0.5f, 0.5f, 0.5f, 1f)); // gray color

            // Add LocalTransform
            em.SetComponentData(asteroidEntity, LocalTransform.FromPositionRotationScale(
                asteroidPosition,
                quaternion.identity,
                1f));

            // Add physics components
            em.AddComponentData(asteroidEntity, new SpacePhysicsBody
            {
                Layer = Space4XPhysicsLayer.Asteroid,
                Priority = Space4XPhysicsLayers.GetDefaultPriority(Space4XPhysicsLayer.Asteroid),
                Flags = SpacePhysicsFlags.RaisesCollisionEvents | SpacePhysicsFlags.IsActive
            });

            em.AddComponentData(asteroidEntity, SpaceColliderData.CreateSphere(
                radius: 2f,
                centerOffset: float3.zero));

            // Add RequiresPhysics so PhysicsBodyBootstrapSystem picks it up
            em.AddComponentData(asteroidEntity, new RequiresPhysics
            {
                Priority = Space4XPhysicsLayers.GetDefaultPriority(Space4XPhysicsLayer.Asteroid),
                Flags = PhysicsInteractionFlags.Collidable
            });

            // Add PhysicsInteractionConfig to set collision radius
            em.AddComponentData(asteroidEntity, new PhysicsInteractionConfig
            {
                CollisionRadius = 2f,
                Mass = 1f,
                Restitution = 0f,
                Friction = 0f,
                LinearDamping = 0f,
                AngularDamping = 0f
            });

            // Add collision event buffer
            em.AddBuffer<PhysicsCollisionEventElement>(asteroidEntity);
            em.AddBuffer<SpaceCollisionEvent>(asteroidEntity);

            // Add minimal gameplay components for a valid asteroid
            em.AddComponentData(asteroidEntity, new Asteroid
            {
                AsteroidId = new Unity.Collections.FixedString64Bytes("physics-test-asteroid"),
                ResourceType = Space4X.Registry.ResourceType.Minerals,
                ResourceAmount = 1000f,
                MaxResourceAmount = 1000f,
                MiningRate = 10f
            });

            // --- MINER at (0, 0, -20) moving forward ---
            var minerEntity = em.CreateEntity();
            var minerPosition = new float3(0f, 0f, -20f);
            var minerVelocity = new float3(0f, 0f, 10f); // Forward velocity toward asteroid

            // Setup rendering
            DemoRenderUtil.MakeRenderable(
                em,
                minerEntity,
                minerPosition,
                new float3(1f, 1f, 1f), // Scale to match collider radius
                new float4(1f, 1f, 0f, 1f)); // yellow color

            // Add LocalTransform
            em.SetComponentData(minerEntity, LocalTransform.FromPositionRotationScale(
                minerPosition,
                quaternion.identity,
                1f));

            // Add physics components
            em.AddComponentData(minerEntity, new SpacePhysicsBody
            {
                Layer = Space4XPhysicsLayer.Miner,
                Priority = Space4XPhysicsLayers.GetDefaultPriority(Space4XPhysicsLayer.Miner),
                Flags = SpacePhysicsFlags.RaisesCollisionEvents | SpacePhysicsFlags.IsActive
            });

            em.AddComponentData(minerEntity, SpaceColliderData.CreateSphere(
                radius: 1f,
                centerOffset: float3.zero));

            // Add RequiresPhysics so PhysicsBodyBootstrapSystem picks it up
            em.AddComponentData(minerEntity, new RequiresPhysics
            {
                Priority = Space4XPhysicsLayers.GetDefaultPriority(Space4XPhysicsLayer.Miner),
                Flags = PhysicsInteractionFlags.Collidable
            });

            // Add PhysicsInteractionConfig to set collision radius
            em.AddComponentData(minerEntity, new PhysicsInteractionConfig
            {
                CollisionRadius = 1f,
                Mass = 1f,
                Restitution = 0f,
                Friction = 0f,
                LinearDamping = 0f,
                AngularDamping = 0f
            });

            // Add velocity component (synced to Havok by PhysicsSyncSystem)
            em.AddComponentData(minerEntity, new SpaceVelocity
            {
                Linear = minerVelocity,
                Angular = float3.zero
            });

            // Add collision event buffer
            em.AddBuffer<PhysicsCollisionEventElement>(minerEntity);
            em.AddBuffer<SpaceCollisionEvent>(minerEntity);

            // Add minimal gameplay components for a valid miner
            em.AddComponentData(minerEntity, new MiningVessel
            {
                VesselId = new Unity.Collections.FixedString64Bytes("physics-test-miner"),
                CarrierEntity = Entity.Null,
                MiningEfficiency = 1f,
                Speed = 15f,
                CargoCapacity = 100f,
                CurrentCargo = 0f,
                CargoResourceType = Space4X.Registry.ResourceType.Minerals
            });

            // Add VesselMovement component (used by movement systems)
            em.AddComponentData(minerEntity, new VesselMovement
            {
                Velocity = minerVelocity,
                BaseSpeed = 15f,
                CurrentSpeed = math.length(minerVelocity),
                DesiredRotation = quaternion.identity,
                IsMoving = 1,
                LastMoveTick = 0
            });

            // Create HandState singleton for debug hand tool
            if (!SystemAPI.HasSingleton<Space4XHandState>())
            {
                var handStateEntity = em.CreateEntity(typeof(Space4XHandState), typeof(ThrowRequest));
                em.SetComponentData(handStateEntity, new Space4XHandState
                {
                    Grabbed = Entity.Null,
                    GrabDistance = 20f, // Default grab distance
                    LocalOffset = float3.zero,
                    LastFramePos = float3.zero,
                    IsGrabbing = false,
                    CurrentHandVel = float3.zero,
                    IsCharging = false,
                    ChargeTime = 0f,
                    PullDirection = float3.zero
                });
                Debug.Log("[Space4XPhysicsTestBootstrapSystem] Created HandState singleton with ThrowRequest buffer for debug hand tool");
            }

            Debug.Log($"[Space4XPhysicsTestBootstrapSystem] Spawned collision test:");
            Debug.Log($"  - Asteroid at {asteroidPosition} (radius 2, layer: Asteroid)");
            Debug.Log($"  - Miner at {minerPosition} (radius 1, layer: Miner, velocity: {minerVelocity})");
            Debug.Log($"  - Expected collision in ~2 seconds at origin");

            _spawned = true;
            state.Enabled = false;
        }
    }
}

