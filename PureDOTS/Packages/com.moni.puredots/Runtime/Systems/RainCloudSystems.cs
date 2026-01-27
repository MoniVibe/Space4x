using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
#if GODGAME
using Godgame.Runtime;
#endif
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct RainCloudMovementSystem : ISystem
    {
        private TimeAwareController _controller;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RainCloudTag>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _controller = new TimeAwareController(
                TimeAwareExecutionPhase.Record | TimeAwareExecutionPhase.CatchUp,
                TimeAwareExecutionOptions.SkipWhenPaused);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (!_controller.TryBegin(timeState, rewindState, out var context))
            {
                return;
            }

            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (cloudState, cloudConfig, transform, entity) in
                     SystemAPI.Query<RefRW<RainCloudState>, RefRO<RainCloudConfig>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
#if GODGAME
                // Skip held entities (game-specific: Divine Hand)
                if (SystemAPI.HasComponent<HandHeldTag>(entity))
                {
                    cloudState.ValueRW.Velocity = float3.zero;
                    continue;
                }
#endif

                var stateRef = cloudState.ValueRO;
                var config = cloudConfig.ValueRO;
                var localTransform = transform.ValueRO;

                stateRef.AgeSeconds += deltaTime;

                float3 noiseDir = float3.zero;
                if (config.DriftNoiseStrength > 0f && config.DriftNoiseFrequency > 0f)
                {
                    float time = stateRef.AgeSeconds * config.DriftNoiseFrequency;
                    noiseDir = new float3(
                        noise.cnoise(new float2(time, 1.123f)),
                        0f,
                        noise.cnoise(new float2(time, 3.456f)));
                    noiseDir = math.normalizesafe(noiseDir, config.DefaultVelocity);
                    noiseDir *= config.DriftNoiseStrength;
                }

                float3 desiredVelocity = config.DefaultVelocity + noiseDir;
                stateRef.Velocity = math.lerp(stateRef.Velocity, desiredVelocity, math.clamp(config.FollowLerp, 0.01f, 1f));

                localTransform.Position += stateRef.Velocity * deltaTime;

                stateRef.ActiveRadius = math.max(config.MinRadius,
                    config.BaseRadius + localTransform.Position.y * config.RadiusPerHeight);

                cloudState.ValueRW = stateRef;
                transform.ValueRW = localTransform;

                if (SystemAPI.HasComponent<RainCloudLifetime>(entity))
                {
                    var lifetime = SystemAPI.GetComponent<RainCloudLifetime>(entity);
                    lifetime.SecondsRemaining -= deltaTime;
                    if (lifetime.SecondsRemaining <= 0f)
                    {
                        state.EntityManager.DestroyEntity(entity);
                    }
                    else
                    {
                        state.EntityManager.SetComponentData(entity, lifetime);
                    }
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(VegetationSystemGroup))]
    public partial struct RainCloudMoistureSystem : ISystem
    {
        private EntityQuery _vegetationQuery;
        private TimeAwareController _controller;
        private ComponentLookup<RainCloudState> _rainCloudStateLookup;
        private BufferLookup<RainCloudMoistureHistory> _rainHistoryLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RainCloudTag>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _vegetationQuery = SystemAPI.QueryBuilder()
                .WithAllRW<VegetationHealth, LocalTransform>()
                .Build();
            _controller = new TimeAwareController(
                TimeAwareExecutionPhase.Record | TimeAwareExecutionPhase.CatchUp,
                TimeAwareExecutionOptions.SkipWhenPaused);
            _rainCloudStateLookup = state.GetComponentLookup<RainCloudState>(false);
            _rainHistoryLookup = state.GetBufferLookup<RainCloudMoistureHistory>(false);
        }

        private struct RainCloudCache
        {
            public Entity Entity;
            public float3 Position;
            public float Radius;
            public float MoisturePerSecond;
            public float Falloff;
        }

       [BurstCompile]
       public void OnUpdate(ref SystemState state)
       {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (!_controller.TryBegin(timeState, rewindState, out var context))
            {
                return;
            }

            if (_vegetationQuery.IsEmpty)
            {
                return;
            }

            var cloudCache = new NativeList<RainCloudCache>(Allocator.Temp);
            var entityManager = state.EntityManager;
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (cloudState, cloudConfig, transform, entity) in
                     SystemAPI.Query<RefRW<RainCloudState>, RefRO<RainCloudConfig>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
#if GODGAME
                // Skip held entities (game-specific: Divine Hand)
                if (SystemAPI.HasComponent<HandHeldTag>(entity))
                {
                    continue;
                }
#endif
                var stateRef = cloudState.ValueRO;
                var config = cloudConfig.ValueRO;
                var position = transform.ValueRO.Position;

                float radius = math.max(config.MinRadius,
                    config.BaseRadius + position.y * config.RadiusPerHeight);

                stateRef.ActiveRadius = radius;

                if (config.MoistureCapacity > 0f)
                {
                    stateRef.MoistureRemaining = math.max(0f, stateRef.MoistureRemaining);
                }

                cloudState.ValueRW = stateRef;

                if (radius <= 0.01f)
                {
                    continue;
                }

                cloudCache.Add(new RainCloudCache
                {
                    Entity = entity,
                    Position = position,
                    Radius = radius,
                    MoisturePerSecond = config.MoisturePerSecond,
                    Falloff = math.max(0.01f, config.MoistureFalloff)
                });
            }

            if (cloudCache.Length == 0)
            {
                cloudCache.Dispose();
                return;
            }

            var usage = new NativeArray<float>(cloudCache.Length, Allocator.Temp);

            foreach (var (health, transform, entity) in SystemAPI.Query<RefRW<VegetationHealth>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                float3 vegetationPos = transform.ValueRO.Position;
                float deltaMoisture = 0f;

                for (int i = 0; i < cloudCache.Length; i++)
                {
                    var cloud = cloudCache[i];
                    float2 horizontal = new float2(
                        vegetationPos.x - cloud.Position.x,
                        vegetationPos.z - cloud.Position.z);
                    float distance = math.length(horizontal);
                    if (distance > cloud.Radius)
                    {
                        continue;
                    }

                    float t = distance / math.max(0.001f, cloud.Radius);
                    float weight = math.pow(1f - t, cloud.Falloff);
                    float contribution = cloud.MoisturePerSecond * deltaTime * weight;
                    deltaMoisture += contribution;
                    usage[i] += contribution;
                }

                if (deltaMoisture <= 0f)
                {
                    continue;
                }

                var healthRef = health.ValueRO;
                healthRef.WaterLevel = math.clamp(healthRef.WaterLevel + deltaMoisture, 0f, 100f);
                health.ValueRW = healthRef;
            }

            _rainCloudStateLookup.Update(ref state);
            _rainHistoryLookup.Update(ref state);
            uint tick = context.Time.Tick;

            for (int i = 0; i < cloudCache.Length; i++)
            {
                float used = usage[i];
                if (!_rainCloudStateLookup.HasComponent(cloudCache[i].Entity))
                {
                    continue;
                }

                var stateRef = _rainCloudStateLookup[cloudCache[i].Entity];
                if (stateRef.MoistureRemaining > 0f)
                {
                    stateRef.MoistureRemaining = math.max(0f, stateRef.MoistureRemaining - used);
                }

                _rainCloudStateLookup[cloudCache[i].Entity] = stateRef;

                if (_rainHistoryLookup.HasBuffer(cloudCache[i].Entity))
                {
                    var history = _rainHistoryLookup[cloudCache[i].Entity];
                    history.Add(new RainCloudMoistureHistory
                    {
                        Tick = tick,
                        MoistureApplied = used,
                        RadiusAtTick = cloudCache[i].Radius
                    });
                }
            }

            usage.Dispose();
            cloudCache.Dispose();
        }
    }
}
