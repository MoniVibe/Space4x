using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Time;
using Space4X.Mining;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Mining
{
    /// <summary>
    /// Simple mining job system for demo. Handles mining vessel state machine:
    /// Idle → FlyToAsteroid → Mine → ReturnToCarrier → Unload → Idle
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MiningJobSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ResourceDeposit> _resourceDepositLookup;
        private ComponentLookup<PlatformResources> _platformResourcesLookup;
        private ComponentLookup<MiningVesselFrameDef> _frameDefLookup;
        private EntityCommandBuffer.ParallelWriter _ecb;
        private EntityQuery _asteroidQuery;

        private const float MiningRange = 5f;
        private const float DeliveryRange = 10f;
        private const float MovementSpeed = 20f;
        private const float MiningRate = 10f; // Units per second

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _resourceDepositLookup = state.GetComponentLookup<ResourceDeposit>(true);
            _platformResourcesLookup = state.GetComponentLookup<PlatformResources>(false);
            _frameDefLookup = state.GetComponentLookup<MiningVesselFrameDef>(true);

            _asteroidQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceNodeTag, ResourceDeposit, LocalTransform>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Gate by DemoScenarioState.EnableSpace4x
            if (!SystemAPI.TryGetSingleton<DemoScenarioState>(out var scenario) || !scenario.EnableSpace4x)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _resourceDepositLookup.Update(ref state);
            _platformResourcesLookup.Update(ref state);
            _frameDefLookup.Update(ref state);

            var deltaTime = timeState.FixedDeltaTime;
            var currentTick = timeState.Tick;

            // Collect asteroids for nearest search
            var asteroids = new NativeList<AsteroidInfo>(Allocator.Temp);
            foreach (var (deposit, transform, entity) in SystemAPI.Query<RefRO<ResourceDeposit>, RefRO<LocalTransform>>()
                .WithAll<ResourceNodeTag>()
                .WithEntityAccess())
            {
                if (deposit.ValueRO.CurrentAmount > 0f)
                {
                    asteroids.Add(new AsteroidInfo
                    {
                        Entity = entity,
                        Position = transform.ValueRO.Position,
                        Richness = deposit.ValueRO.CurrentAmount
                    });
                }
            }

            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var job = new ProcessMiningJobsJob
            {
                DeltaTime = deltaTime,
                CurrentTick = currentTick,
                MiningRange = MiningRange,
                DeliveryRange = DeliveryRange,
                MovementSpeed = MovementSpeed,
                MiningRate = MiningRate,
                Asteroids = asteroids,
                TransformLookup = _transformLookup,
                ResourceDepositLookup = _resourceDepositLookup,
                PlatformResourcesLookup = _platformResourcesLookup,
                FrameDefLookup = _frameDefLookup,
                Ecb = ecb.AsParallelWriter()
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct ProcessMiningJobsJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;
            public float MiningRange;
            public float DeliveryRange;
            public float MovementSpeed;
            public float MiningRate;
            [ReadOnly] public NativeList<AsteroidInfo> Asteroids;
            public ComponentLookup<LocalTransform> TransformLookup;
            public ComponentLookup<ResourceDeposit> ResourceDepositLookup;
            public ComponentLookup<PlatformResources> PlatformResourcesLookup;
            [ReadOnly] public ComponentLookup<MiningVesselFrameDef> FrameDefLookup;
            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute([ChunkIndexInQuery] int chunkIndex, ref MiningJob job, ref LocalTransform transform, in MiningVesselTag tag, in Entity entity)
            {
                var frameDef = FrameDefLookup.HasComponent(entity)
                    ? FrameDefLookup[entity]
                    : new MiningVesselFrameDef { MaxCargo = 100f, MiningRate = MiningRate };

                switch (job.Phase)
                {
                    case MiningPhase.Idle:
                        // Find nearest asteroid
                        Entity nearestAsteroid = Entity.Null;
                        float nearestDist = float.MaxValue;
                        float3 nearestPos = float3.zero;

                        for (int i = 0; i < Asteroids.Length; i++)
                        {
                            var asteroid = Asteroids[i];
                            float dist = math.distance(transform.Position, asteroid.Position);
                            if (dist < nearestDist)
                            {
                                nearestDist = dist;
                                nearestAsteroid = asteroid.Entity;
                                nearestPos = asteroid.Position;
                            }
                        }

                        if (nearestAsteroid != Entity.Null)
                        {
                            job.Phase = MiningPhase.FlyToAsteroid;
                            job.TargetAsteroid = nearestAsteroid;
                            job.TargetPosition = nearestPos;
                            job.LastStateChangeTick = CurrentTick;
                        }
                        break;

                    case MiningPhase.FlyToAsteroid:
                        if (!TransformLookup.HasComponent(job.TargetAsteroid))
                        {
                            job.Phase = MiningPhase.Idle;
                            job.TargetAsteroid = Entity.Null;
                            break;
                        }

                        var asteroidTransform = TransformLookup[job.TargetAsteroid];
                        float distToAsteroid = math.distance(transform.Position, asteroidTransform.Position);

                        if (distToAsteroid <= MiningRange)
                        {
                            job.Phase = MiningPhase.Mining;
                            job.LastStateChangeTick = CurrentTick;
                        }
                        else
                        {
                            // Move toward asteroid
                            float3 direction = math.normalize(asteroidTransform.Position - transform.Position);
                            transform.Position += direction * MovementSpeed * DeltaTime;
                        }
                        break;

                    case MiningPhase.Mining:
                        if (!ResourceDepositLookup.HasComponent(job.TargetAsteroid))
                        {
                            job.Phase = MiningPhase.Idle;
                            job.TargetAsteroid = Entity.Null;
                            break;
                        }

                        var deposit = ResourceDepositLookup[job.TargetAsteroid];
                        float mined = math.min(MiningRate * DeltaTime, deposit.CurrentAmount);
                        mined = math.min(mined, frameDef.MaxCargo - job.CargoAmount);

                        if (mined > 0f && deposit.CurrentAmount > 0f)
                        {
                            job.CargoAmount += mined;
                            // Update ResourceDeposit via ECB
                            var updatedDeposit = deposit;
                            updatedDeposit.CurrentAmount -= mined;
                            Ecb.SetComponent(chunkIndex, job.TargetAsteroid, updatedDeposit);
                        }

                        if (job.CargoAmount >= frameDef.MaxCargo || deposit.CurrentAmount <= 0f)
                        {
                            job.Phase = MiningPhase.ReturnToCarrier;
                            job.LastStateChangeTick = CurrentTick;
                        }
                        break;

                    case MiningPhase.ReturnToCarrier:
                        if (job.CarrierEntity == Entity.Null || !TransformLookup.HasComponent(job.CarrierEntity))
                        {
                            job.Phase = MiningPhase.Idle;
                            job.CarrierEntity = Entity.Null;
                            break;
                        }

                        var carrierTransform = TransformLookup[job.CarrierEntity];
                        float distToCarrier = math.distance(transform.Position, carrierTransform.Position);

                        if (distToCarrier <= DeliveryRange)
                        {
                            job.Phase = MiningPhase.Unloading;
                            job.LastStateChangeTick = CurrentTick;
                        }
                        else
                        {
                            // Move toward carrier
                            float3 direction = math.normalize(carrierTransform.Position - transform.Position);
                            transform.Position += direction * MovementSpeed * DeltaTime;
                        }
                        break;

                    case MiningPhase.Unloading:
                        if (job.CarrierEntity == Entity.Null || !PlatformResourcesLookup.HasComponent(job.CarrierEntity))
                        {
                            job.Phase = MiningPhase.Idle;
                            job.CarrierEntity = Entity.Null;
                            job.CargoAmount = 0f;
                            break;
                        }

                        var platformResources = PlatformResourcesLookup[job.CarrierEntity];
                        platformResources.Ore += job.CargoAmount;
                        Ecb.SetComponent(chunkIndex, job.CarrierEntity, platformResources);

                        job.CargoAmount = 0f;
                        job.Phase = MiningPhase.Idle;
                        job.LastStateChangeTick = CurrentTick;
                        break;
                }
            }
        }

        private struct AsteroidInfo
        {
            public Entity Entity;
            public float3 Position;
            public float Richness;
        }
    }
}

