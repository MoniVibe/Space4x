using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Physics;
using Space4X.Runtime.Interaction;
using Shared.Demo;
using UnityEngine;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;

// Resolve type ambiguity: Space4X mining systems use Space4X.Registry versions
using ResourceTypeId = Space4X.Registry.ResourceTypeId;
using ResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;

namespace Space4X.Demo
{
    /// <summary>
    /// Spawns a scaling test scenario: 20-50 miners and 10 asteroids for performance testing.
    /// Spreads asteroids in a grid pattern and assigns miners to carriers.
    /// Runs once at startup, then disables itself.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Shared.Demo.SharedDemoRenderBootstrap))]
    public partial struct Space4XMiningScalingBootstrapSystem : ISystem
    {
        private bool _initialized;

        // Configuration constants
        private const int AsteroidCount = 10;
        private const int MinerCount = 30;
        private const int CarrierCount = 2;
        private const float GridSpacing = 20f; // Distance between asteroids in grid
        private const float AsteroidResourceAmount = 2000f; // More resources for scaling test

        public void OnCreate(ref SystemState state)
        {
            Debug.Log("[Space4XMiningScalingBootstrapSystem] OnCreate");
            state.RequireForUpdate<DemoRenderReady>();
            state.RequireForUpdate<DemoScenarioState>();
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
                Debug.Log("[Space4XMiningScalingBootstrapSystem] Already initialized, disabling.");
                state.Enabled = false;
                return;
            }

            Debug.Log($"[Space4XMiningScalingBootstrapSystem] Spawning scaling test: {AsteroidCount} asteroids, {MinerCount} miners, {CarrierCount} carriers");

            var em = state.EntityManager;
            
            // Get current tick for fleet broadcast
            uint currentTick = 0;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                currentTick = timeState.Tick;
            }

            // Spawn carriers first (they'll be assigned to miners)
            var carriers = new NativeList<Entity>(CarrierCount, Allocator.Temp);
            for (int i = 0; i < CarrierCount; i++)
            {
                var carrierEntity = CreateCarrier(em, new float3(-30f + i * 20f, 0f, 0f), currentTick, i);
                carriers.Add(carrierEntity);
            }

            // Spawn asteroids in a grid pattern (5x2 grid)
            var asteroids = new NativeList<Entity>(AsteroidCount, Allocator.Temp);
            int gridCols = 5;
            int gridRows = 2;
            for (int row = 0; row < gridRows; row++)
            {
                for (int col = 0; col < gridCols && asteroids.Length < AsteroidCount; col++)
                {
                    var pos = new float3(
                        col * GridSpacing - (gridCols - 1) * GridSpacing * 0.5f,
                        0f,
                        row * GridSpacing - (gridRows - 1) * GridSpacing * 0.5f
                    );
                    var asteroidEntity = CreateAsteroid(em, pos, asteroids.Length);
                    asteroids.Add(asteroidEntity);
                }
            }

            // Spawn miners around carriers
            for (int i = 0; i < MinerCount; i++)
            {
                var carrierIndex = i % carriers.Length;
                var carrierEntity = carriers[carrierIndex];
                var carrierPos = em.GetComponentData<LocalTransform>(carrierEntity).Position;
                
                // Spread miners in a circle around carrier
                var angle = (float)i / MinerCount * math.PI * 2f;
                var radius = 5f + (i % 3) * 2f; // Vary radius slightly
                var minerPos = carrierPos + new float3(
                    math.cos(angle) * radius,
                    0f,
                    math.sin(angle) * radius
                );
                
                CreateMiner(em, minerPos, carrierEntity, currentTick, i);
            }

            carriers.Dispose();
            asteroids.Dispose();

            Debug.Log($"[Space4XMiningScalingBootstrapSystem] Spawned {AsteroidCount} asteroids, {MinerCount} miners, {CarrierCount} carriers for scaling test.");

            _initialized = true;
            state.Enabled = false;
        }

        private Entity CreateAsteroid(EntityManager em, float3 position, int index)
        {
            var asteroidEntity = em.CreateEntity();
            DemoRenderUtil.MakeRenderable(
                em,
                asteroidEntity,
                position,
                new float3(2f, 2f, 2f), // scale (larger for rock)
                new float4(0.5f, 0.5f, 0.5f, 1f)); // gray color

            // Add rock tags
            em.AddComponent<RockTag>(asteroidEntity);
            em.AddComponent<ThrowableTag>(asteroidEntity);
            em.AddComponent<ResourceNodeTag>(asteroidEntity);
            
            // Add ResourceDeposit
            em.AddComponentData(asteroidEntity, new ResourceDeposit
            {
                ResourceTypeId = 0, // Minerals
                CurrentAmount = AsteroidResourceAmount,
                MaxAmount = AsteroidResourceAmount,
                RegenPerSecond = 0f
            });
            
            // Add MaterialStats
            em.AddComponentData(asteroidEntity, new MaterialStats
            {
                Hardness = 2.5f,
                Fragility = 0.5f,
                Density = 3.0f
            });
            
            // Add Destructible
            em.AddComponentData(asteroidEntity, new Destructible
            {
                HitPoints = 100f,
                MaxHitPoints = 100f
            });
            
            // Add ImpactDamage
            em.AddComponentData(asteroidEntity, new ImpactDamage
            {
                DamagePerImpulse = 10f,
                MinImpulse = 1f
            });
            
            // Add Space4X physics components
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

            // Legacy asteroid component (for backward compatibility)
            em.AddComponentData(asteroidEntity, new Asteroid
            {
                AsteroidId = new FixedString64Bytes($"scaling-asteroid-{index:D2}"),
                ResourceType = ResourceType.Minerals,
                ResourceAmount = AsteroidResourceAmount,
                MaxResourceAmount = AsteroidResourceAmount,
                MiningRate = 10f
            });

            em.AddComponentData(asteroidEntity, new ResourceTypeId
            {
                Value = new FixedString64Bytes("space4x.resource.minerals")
            });

            var maxWorkers = math.clamp((int)math.ceil(10f / 5f), 1, 16);
            em.AddComponentData(asteroidEntity, new ResourceSourceConfig
            {
                GatherRatePerWorker = 10f,
                MaxSimultaneousWorkers = (ushort)maxWorkers,
                RespawnSeconds = 0f,
                Flags = 0
            });

            em.AddComponentData(asteroidEntity, new PureDOTS.Runtime.Components.ResourceSourceState
            {
                UnitsRemaining = AsteroidResourceAmount
            });

            em.AddComponent<SpatialIndexedTag>(asteroidEntity);
            em.AddComponentData(asteroidEntity, new LastRecordedTick { Tick = 0 });
            em.AddComponent<RewindableTag>(asteroidEntity);
            em.AddComponentData(asteroidEntity, new HistoryTier
            {
                Tier = HistoryTier.TierType.LowVisibility,
                OverrideStrideSeconds = 0f
            });
            em.AddBuffer<ResourceHistorySample>(asteroidEntity);

            return asteroidEntity;
        }

        private Entity CreateCarrier(EntityManager em, float3 position, uint currentTick, int index)
        {
            var carrierEntity = em.CreateEntity();
            DemoRenderUtil.MakeRenderable(
                em,
                carrierEntity,
                position,
                new float3(1f, 1f, 1f),
                new float4(0.2f, 0.4f, 1f, 1f)); // blue color

            em.AddComponentData(carrierEntity, new Carrier
            {
                CarrierId = new FixedString64Bytes($"scaling-carrier-{index:D2}"),
                AffiliationEntity = Entity.Null,
                Speed = 5f,
                PatrolCenter = position,
                PatrolRadius = 50f
            });

            em.AddComponentData(carrierEntity, new PatrolBehavior
            {
                CurrentWaypoint = float3.zero,
                WaitTime = 2f,
                WaitTimer = 0f
            });

            em.AddComponentData(carrierEntity, new MovementCommand
            {
                TargetPosition = float3.zero,
                ArrivalThreshold = 1f
            });

            var storageBuffer = em.AddBuffer<ResourceStorage>(carrierEntity);
            storageBuffer.Add(ResourceStorage.Create(ResourceType.Minerals, 10000f));

            em.AddComponent<SpatialIndexedTag>(carrierEntity);
            em.AddComponentData(carrierEntity, new Space4XFleet
            {
                FleetId = new FixedString64Bytes($"scaling-fleet-{index:D2}"),
                ShipCount = 1,
                Posture = Space4XFleetPosture.Patrol,
                TaskForce = 0
            });

            em.AddComponentData(carrierEntity, new FleetMovementBroadcast
            {
                Position = position,
                Velocity = float3.zero,
                LastUpdateTick = currentTick,
                AllowsInterception = 1,
                TechTier = 0
            });

            return carrierEntity;
        }

        private void CreateMiner(EntityManager em, float3 position, Entity carrierEntity, uint currentTick, int index)
        {
            var minerEntity = em.CreateEntity();
            DemoRenderUtil.MakeRenderable(
                em,
                minerEntity,
                position,
                new float3(1f, 1f, 1f),
                new float4(1f, 1f, 0f, 1f)); // yellow color

            em.AddComponentData(minerEntity, new MiningVessel
            {
                VesselId = new FixedString64Bytes($"scaling-miner-{index:D2}"),
                CarrierEntity = carrierEntity,
                MiningEfficiency = 1f,
                Speed = 15f,
                CargoCapacity = 100f,
                CurrentCargo = 0f,
                CargoResourceType = ResourceType.Minerals
            });

            em.AddComponentData(minerEntity, new MiningJob
            {
                State = MiningJobState.None,
                TargetAsteroid = Entity.Null,
                MiningProgress = 0f
            });

            em.AddComponentData(minerEntity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.None,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });

            em.AddComponentData(minerEntity, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = 15f,
                CurrentSpeed = 0f,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });

            em.AddBuffer<SpawnResourceRequest>(minerEntity);

            em.AddComponentData(minerEntity, new MiningOrder
            {
                ResourceId = new FixedString64Bytes("space4x.resource.minerals"),
                Source = MiningOrderSource.Scripted,
                Status = MiningOrderStatus.Pending,
                PreferredTarget = Entity.Null,
                TargetEntity = Entity.Null,
                IssuedTick = currentTick
            });

            em.AddComponentData(minerEntity, new MiningState
            {
                Phase = MiningPhase.Idle,
                ActiveTarget = Entity.Null,
                MiningTimer = 0f,
                TickInterval = 0.5f
            });

            em.AddComponentData(minerEntity, new MiningYield
            {
                ResourceId = new FixedString64Bytes("space4x.resource.minerals"),
                PendingAmount = 0f,
                SpawnThreshold = 20f,
                SpawnReady = 0
            });

            // Add target selection strategy (default to Nearest)
            em.AddComponentData(minerEntity, new MinerTargetStrategy
            {
                SelectionStrategy = MinerTargetStrategy.Strategy.Nearest
            });

            em.AddComponent<SpatialIndexedTag>(minerEntity);
        }
    }
}

