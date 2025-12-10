using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Physics;
using Space4X.Runtime.Interaction;
using Space4X.Mining;
using Space4X.Presentation;
using Shared.Demo;
using UnityEngine;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Platform;
using Unity.Collections;

// Resolve type ambiguity: Space4X mining systems use Space4X.Registry versions
using ResourceTypeId = Space4X.Registry.ResourceTypeId;
using ResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;
using MiningJobComponent = Space4X.Registry.MiningJob;
using MiningPhaseState = Space4X.Registry.MiningPhase;

namespace Space4X.Demo
{
    /// <summary>
    /// Spawns a minimal mining scene: one miner vessel + one resource node,
    /// wired into existing mining systems for a complete gameplay loop.
    /// Runs once at startup, then disables itself.
    /// Relies on Space4XMiningDebugRenderSetupSystem to assign simple Lit debug visuals.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Shared.Demo.SharedDemoRenderBootstrap))]
    public partial struct Space4XMiningDemoBootstrapSystem : ISystem
    {
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            Debug.Log("[Space4XMiningDemoBootstrapSystem] OnCreate");
            state.RequireForUpdate<DemoRenderReady>(); // Same requirement as debug cube spawner
            state.RequireForUpdate<DemoScenarioState>(); // Require scenario state
        }

        // NOTE: Not Burst compiled because DemoRenderUtil.MakeRenderable uses managed code
        public void OnUpdate(ref SystemState state)
        {
            // Check scenario - only run in AllSystemsShowcase or Space4XPhysicsOnly
            var scenario = SystemAPI.GetSingleton<DemoScenarioState>().Current;
            if (scenario != DemoScenario.AllSystemsShowcase && scenario != DemoScenario.Space4XPhysicsOnly)
            {
                return;
            }

            if (_initialized)
            {
                Debug.Log("[Space4XMiningDemoBootstrapSystem] Already initialized, disabling.");
                state.Enabled = false;
                return;
            }

            Debug.Log($"[Space4XMiningDemoBootstrapSystem] OnUpdate enter (frame {UnityEngine.Time.frameCount})");

            var em = state.EntityManager;
            
            // Get current tick for fleet broadcast
            uint currentTick = 0;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                currentTick = timeState.Tick;
            }

            // --- ROCK/ASTEROID at (10,0,0) ---
            // Using rock authoring pattern: RockTag, ThrowableTag, ResourceNodeTag, ResourceDeposit, MaterialStats, Physics
            var asteroidEntity = em.CreateEntity();
            var asteroidPosition = new float3(10f, 0f, 0f);
            em.AddComponentData(asteroidEntity, LocalTransform.FromPositionRotationScale(
                asteroidPosition,
                quaternion.identity,
                2f)); // larger scale for rock

            // Add rock tags
            em.AddComponent<RockTag>(asteroidEntity);
            em.AddComponent<ThrowableTag>(asteroidEntity);
            em.AddComponent<ResourceNodeTag>(asteroidEntity);
            
            // Add ResourceDeposit (rock resource component)
            em.AddComponentData(asteroidEntity, new ResourceDeposit
            {
                ResourceTypeId = 0, // Minerals (would need proper mapping)
                CurrentAmount = 1000f,
                MaxAmount = 1000f,
                RegenPerSecond = 0f
            });
            
            // Add MaterialStats (rock defaults: Hardness 2.5, Density 3.0)
            em.AddComponentData(asteroidEntity, new MaterialStats
            {
                Hardness = 2.5f,
                Fragility = 0.5f,
                Density = 3.0f
            });
            
            // Add Destructible (optional, for collision damage)
            em.AddComponentData(asteroidEntity, new Destructible
            {
                HitPoints = 100f,
                MaxHitPoints = 100f
            });
            
            // Add ImpactDamage (rocks deal damage on collision)
            em.AddComponentData(asteroidEntity, new ImpactDamage
            {
                DamagePerImpulse = 10f,
                MinImpulse = 1f
            });
            
            // Add Space4X physics components (similar to Space4XVesselPhysicsAuthoring)
            em.AddComponentData(asteroidEntity, new SpacePhysicsBody
            {
                Layer = Space4XPhysicsLayer.Asteroid,
                Priority = 100,
                Flags = SpacePhysicsFlags.IsActive | SpacePhysicsFlags.RaisesCollisionEvents
            });
            
            em.AddComponentData(asteroidEntity, new SpaceColliderData
            {
                Type = ColliderType.Sphere,
                Radius = 2.5f,
                Size = float3.zero,
                Height = 0f,
                CenterOffset = float3.zero
            });
            
            em.AddComponentData(asteroidEntity, new SpaceVelocity
            {
                Linear = float3.zero,
                Angular = float3.zero
            });
            
            em.AddComponentData(asteroidEntity, new RequiresPhysics
            {
                Priority = 100,
                Flags = PhysicsInteractionFlags.Collidable
            });
            
            em.AddComponentData(asteroidEntity, new PhysicsInteractionConfig
            {
                Mass = 1f,
                CollisionRadius = 2.5f,
                Restitution = 0f,
                Friction = 0f,
                LinearDamping = 0f,
                AngularDamping = 0f
            });
            
            // Add collision event buffers
            em.AddBuffer<SpaceCollisionEvent>(asteroidEntity);
            em.AddBuffer<PhysicsCollisionEventElement>(asteroidEntity);
            em.AddComponent<NeedsPhysicsSetup>(asteroidEntity);
            
            // Legacy asteroid component (for backward compatibility with mining systems)
            em.AddComponentData(asteroidEntity, new Asteroid
            {
                AsteroidId = new FixedString64Bytes("test-asteroid-01"),
                ResourceType = ResourceType.Minerals,
                ResourceAmount = 1000f,
                MaxResourceAmount = 1000f,
                MiningRate = 10f
            });
            
            // Add ResourceTypeId (Space4X.Registry namespace)
            em.AddComponentData(asteroidEntity, new ResourceTypeId 
            { 
                Value = new FixedString64Bytes("space4x.resource.minerals") 
            });
            
            // Add ResourceSourceConfig
            var maxWorkers = math.clamp((int)math.ceil(10f / 5f), 1, 16);
            em.AddComponentData(asteroidEntity, new ResourceSourceConfig
            {
                GatherRatePerWorker = 10f,
                MaxSimultaneousWorkers = (ushort)maxWorkers,
                RespawnSeconds = 0f,
                Flags = 0
            });
            
            // Add ResourceSourceState (PureDOTS.Runtime.Components namespace)
            em.AddComponentData(asteroidEntity, new PureDOTS.Runtime.Components.ResourceSourceState
            {
                UnitsRemaining = 1000f
            });
            
            // Add SpatialIndexedTag for registry bridge
            em.AddComponent<SpatialIndexedTag>(asteroidEntity);
            
            // Add rewind/history components
            em.AddComponentData(asteroidEntity, new LastRecordedTick { Tick = 0 });
            em.AddComponent<RewindableTag>(asteroidEntity);
            em.AddComponentData(asteroidEntity, new HistoryTier
            {
                Tier = HistoryTier.TierType.LowVisibility,
                OverrideStrideSeconds = 0f
            });
            
            // Add ResourceHistorySample buffer
            em.AddBuffer<ResourceHistorySample>(asteroidEntity);

            // Add VisualProfileIdComponent for visual assignment
            em.AddComponentData(asteroidEntity, new VisualProfileIdComponent
            {
                ProfileId = VisualProfileId.DebugAsteroid
            });

            // --- CARRIER at (-10,0,0) ---
            var carrierEntity = em.CreateEntity();
            var carrierPosition = new float3(-10f, 0f, 0f);
            em.AddComponentData(carrierEntity, LocalTransform.FromPositionRotationScale(
                carrierPosition,
                quaternion.identity,
                1f));
            em.AddComponent<PlatformTag>(carrierEntity);

            em.AddComponentData(carrierEntity, new Carrier
            {
                CarrierId = new FixedString64Bytes("test-carrier-01"),
                AffiliationEntity = Entity.Null,
                Speed = 5f,
                PatrolCenter = float3.zero,
                PatrolRadius = 50f
            });
            
            // Add PatrolBehavior component
            em.AddComponentData(carrierEntity, new PatrolBehavior
            {
                CurrentWaypoint = float3.zero, // Will be initialized by CarrierPatrolSystem
                WaitTime = 2f,
                WaitTimer = 0f
            });
            
            // Add MovementCommand component
            em.AddComponentData(carrierEntity, new MovementCommand
            {
                TargetPosition = float3.zero, // Will be set by CarrierPatrolSystem
                ArrivalThreshold = 1f
            });
            
            // Add ResourceStorage buffer
            var storageBuffer = em.AddBuffer<ResourceStorage>(carrierEntity);
            storageBuffer.Add(ResourceStorage.Create(ResourceType.Minerals, 10000f));
            
            // Add SpatialIndexedTag for registry bridge
            em.AddComponent<SpatialIndexedTag>(carrierEntity);
            
            // Add Space4XFleet component (manually, since CarrierFleetBootstrapSystem is disabled)
            em.AddComponentData(carrierEntity, new Space4XFleet
            {
                FleetId = new FixedString64Bytes("test-carrier-01"),
                ShipCount = 1,
                Posture = Space4XFleetPosture.Patrol,
                TaskForce = 0
            });
            
            // Add FleetMovementBroadcast component
            em.AddComponentData(carrierEntity, new FleetMovementBroadcast
            {
                Position = carrierPosition,
                Velocity = float3.zero,
                LastUpdateTick = currentTick,
                AllowsInterception = 1,
                TechTier = 0
            });

            // Add VisualProfileIdComponent for visual assignment
            em.AddComponentData(carrierEntity, new VisualProfileIdComponent
            {
                ProfileId = VisualProfileId.DebugCarrier
            });

            // --- MINER VESSEL at (-5,0,0) ---
            var minerEntity = em.CreateEntity();
            var minerPosition = new float3(-5f, 0f, 0f);
            em.AddComponentData(minerEntity, LocalTransform.FromPositionRotationScale(
                minerPosition,
                quaternion.identity,
                1f));
            em.AddComponent<MiningVesselTag>(minerEntity);

            em.AddComponentData(minerEntity, new MiningVessel
            {
                VesselId = new FixedString64Bytes("test-miner-01"),
                CarrierEntity = carrierEntity, // Link to carrier
                MiningEfficiency = 1f,
                Speed = 15f,
                CargoCapacity = 100f,
                CurrentCargo = 0f,
                CargoResourceType = ResourceType.Minerals
            });
            
            // Add MiningJob component
            em.AddComponentData(minerEntity, new MiningJobComponent
            {
                State = MiningJobState.None,
                TargetAsteroid = Entity.Null,
                MiningProgress = 0f
            });
            
            // Add VesselAIState component
            em.AddComponentData(minerEntity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.None,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });
            
            // Add VesselMovement component
            em.AddComponentData(minerEntity, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = 15f,
                CurrentSpeed = 0f,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });
            
            // Add SpawnResourceRequest buffer
            em.AddBuffer<SpawnResourceRequest>(minerEntity);
            
            // Add MiningOrder component (enables AI systems to pick it up)
            em.AddComponentData(minerEntity, new MiningOrder
            {
                ResourceId = new FixedString64Bytes("space4x.resource.minerals"),
                Source = MiningOrderSource.Scripted,
                Status = MiningOrderStatus.Pending,
                PreferredTarget = Entity.Null,
                TargetEntity = Entity.Null,
                IssuedTick = currentTick
            });
            
            // Add MiningState component
            em.AddComponentData(minerEntity, new MiningState
            {
                Phase = MiningPhaseState.Idle,
                ActiveTarget = Entity.Null,
                MiningTimer = 0f,
                TickInterval = 0.5f
            });
            
            // Add MiningYield component
            em.AddComponentData(minerEntity, new MiningYield
            {
                ResourceId = new FixedString64Bytes("space4x.resource.minerals"),
                PendingAmount = 0f,
                SpawnThreshold = 20f,
                SpawnReady = 0
            });
            
            // Add SpatialIndexedTag for registry bridge
            em.AddComponent<SpatialIndexedTag>(minerEntity);

            // Add VisualProfileIdComponent for visual assignment
            em.AddComponentData(minerEntity, new VisualProfileIdComponent
            {
                ProfileId = VisualProfileId.DebugMiner
            });

            Debug.Log("[Space4XMiningDemoBootstrapSystem] Spawned asteroid (gray), carrier (blue), and miner (yellow) with full Space4X gameplay components.");

            _initialized = true;
            state.Enabled = false;
        }
    }
}
