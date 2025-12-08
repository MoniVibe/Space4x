using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Shared.Demo;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Physics;
using Space4X.Runtime.Interaction;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Spatial;

// Resolve type ambiguity: Space4X mining systems use Space4X.Registry versions
using ResourceTypeId = Space4X.Registry.ResourceTypeId;
using ResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;

namespace Space4X.Demo
{
    public enum Space4XSpawnKind : byte
    {
        Miner = 0,
        Carrier = 1,
        Asteroid = 2
    }

    /// <summary>
    /// Runtime spawn request for the Space4X demo.
    /// </summary>
    public struct Space4XSpawnRequest : IBufferElementData
    {
        public Space4XSpawnKind Kind;
        public int Count;
        public float3 Center;
        public float Radius;
        public uint Seed;
    }

    internal static class Space4XDemoSpawnIds
    {
        public static readonly FixedString64Bytes AsteroidPrefix = "runtime-asteroid-";
        public static readonly FixedString64Bytes CarrierPrefix = "runtime-carrier-";
        public static readonly FixedString64Bytes MinerPrefix = "runtime-miner-";
        public static readonly FixedString64Bytes ResourceMinerals = "space4x.resource.minerals";
        public const uint DefaultSeed = 0x9E3779B9u;

        public static FixedString64Bytes BuildId(FixedString64Bytes prefix, int index)
        {
            var id = prefix;
            id.Append(index);
            return id;
        }

        public static uint EnsureNonZero(uint seed) => seed == 0 ? 1u : seed;
    }

    /// <summary>
    /// Ensures a spawn queue exists.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XDemoSpawnQueueSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!em.CreateEntityQuery(typeof(Space4XSpawnRequest)).IsEmptyIgnoreFilter)
                return;

            var e = em.CreateEntity();
            em.AddBuffer<Space4XSpawnRequest>(e);
        }
    }

    /// <summary>
    /// Consumes spawn requests and instantiates demo entities deterministically.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XDemoRuntimeSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XSpawnRequest>();
            state.RequireForUpdate<DemoRenderReady>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode != RewindMode.Record)
                return;

            var em = state.EntityManager;
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<Space4XSpawnRequest>());
            if (query.IsEmptyIgnoreFilter)
                return;

            var queue = query.GetSingletonEntity();
            var buffer = em.GetBuffer<Space4XSpawnRequest>(queue);
            if (buffer.Length == 0)
                return;

            // Current tick for broadcast stamps
            uint currentTick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;

            for (int i = 0; i < buffer.Length; i++)
            {
                var req = buffer[i];
                var seed = req.Seed != 0 ? req.Seed : Space4XDemoSpawnIds.DefaultSeed + (uint)i;
                var random = new Unity.Mathematics.Random(Space4XDemoSpawnIds.EnsureNonZero(seed));
                int count = math.max(1, req.Count);
                float radius = req.Radius <= 0f ? 12f : req.Radius;

                for (int n = 0; n < count; n++)
                {
                    var pos = RandomOnRing(ref random, req.Center, radius);

                    switch (req.Kind)
                    {
                        case Space4XSpawnKind.Asteroid:
                            CreateAsteroid(em, pos, n);
                            break;
                        case Space4XSpawnKind.Carrier:
                            var carrier = CreateCarrier(em, pos, currentTick, n);
                            // Optional: spawn a miner attached to the carrier for convenience
                            var minerPos = pos + new float3(4f, 0f, 0f);
                            CreateMiner(em, minerPos, carrier, currentTick, n);
                            break;
                        case Space4XSpawnKind.Miner:
                            CreateMiner(em, pos, Entity.Null, currentTick, n);
                            break;
                    }
                }
            }

            buffer.Clear();
        }

        private static float3 RandomOnRing(ref Unity.Mathematics.Random random, float3 center, float radius)
        {
            var angle = random.NextFloat(0f, math.PI * 2f);
            var r = radius * random.NextFloat(0.6f, 1f);
            return center + new float3(math.cos(angle) * r, 0f, math.sin(angle) * r);
        }

        private static Entity CreateAsteroid(EntityManager em, float3 position, int index)
        {
            var asteroidEntity = em.CreateEntity();
            DemoRenderUtil.MakeRenderable(
                em,
                asteroidEntity,
                position,
                new float3(2f, 2f, 2f),
                new float4(0.5f, 0.5f, 0.5f, 1f));

            em.AddComponent<RockTag>(asteroidEntity);
            em.AddComponent<ThrowableTag>(asteroidEntity);
            em.AddComponent<ResourceNodeTag>(asteroidEntity);

            em.AddComponentData(asteroidEntity, new ResourceDeposit
            {
                ResourceTypeId = 0,
                CurrentAmount = 1000f,
                MaxAmount = 1000f,
                RegenPerSecond = 0f
            });

            em.AddComponentData(asteroidEntity, new MaterialStats
            {
                Hardness = 2.5f,
                Fragility = 0.5f,
                Density = 3.0f
            });

            em.AddComponentData(asteroidEntity, new Destructible
            {
                HitPoints = 100f,
                MaxHitPoints = 100f
            });

            em.AddComponentData(asteroidEntity, new ImpactDamage
            {
                DamagePerImpulse = 10f,
                MinImpulse = 1f
            });

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

            em.AddBuffer<SpaceCollisionEvent>(asteroidEntity);
            em.AddBuffer<PhysicsCollisionEventElement>(asteroidEntity);
            em.AddComponent<NeedsPhysicsSetup>(asteroidEntity);

            em.AddComponentData(asteroidEntity, new Asteroid
            {
                AsteroidId = Space4XDemoSpawnIds.BuildId(Space4XDemoSpawnIds.AsteroidPrefix, index),
                ResourceType = ResourceType.Minerals,
                ResourceAmount = 1000f,
                MaxResourceAmount = 1000f,
                MiningRate = 10f
            });

            em.AddComponentData(asteroidEntity, new ResourceTypeId
            {
                Value = Space4XDemoSpawnIds.ResourceMinerals
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
                UnitsRemaining = 1000f
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

        private static Entity CreateCarrier(EntityManager em, float3 position, uint currentTick, int index)
        {
            var carrierEntity = em.CreateEntity();
            DemoRenderUtil.MakeRenderable(
                em,
                carrierEntity,
                position,
                new float3(1f, 1f, 1f),
                new float4(0.2f, 0.4f, 1f, 1f));

            em.AddComponentData(carrierEntity, new Carrier
            {
                CarrierId = Space4XDemoSpawnIds.BuildId(Space4XDemoSpawnIds.CarrierPrefix, index),
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
                FleetId = new FixedString64Bytes($"runtime-fleet-{index:D2}"),
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

        private static Entity CreateMiner(EntityManager em, float3 position, Entity carrierEntity, uint currentTick, int index)
        {
            var minerEntity = em.CreateEntity();
            DemoRenderUtil.MakeRenderable(
                em,
                minerEntity,
                position,
                new float3(1f, 1f, 1f),
                new float4(1f, 1f, 0f, 1f));

            em.AddComponentData(minerEntity, new MiningVessel
            {
                VesselId = Space4XDemoSpawnIds.BuildId(Space4XDemoSpawnIds.MinerPrefix, index),
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
                ResourceId = Space4XDemoSpawnIds.ResourceMinerals,
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
                ResourceId = Space4XDemoSpawnIds.ResourceMinerals,
                PendingAmount = 0f,
                SpawnThreshold = 20f,
                SpawnReady = 0
            });

            em.AddComponent<SpatialIndexedTag>(minerEntity);

            return minerEntity;
        }
    }
}

