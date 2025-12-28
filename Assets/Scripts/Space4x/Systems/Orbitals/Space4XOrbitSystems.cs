using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.Orbitals
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4x.Scenario.Space4XMiningScenarioSystem))]
    public partial struct Space4XOrbitBootstrapSystem : ISystem
    {
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
            {
                state.Enabled = false;
                return;
            }

            if (SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) && !scenario.EnableSpace4x)
            {
                return;
            }

            if (SystemAPI.QueryBuilder().WithAll<Space4XOrbitStarTag>().Build().CalculateEntityCount() > 0)
            {
                _initialized = true;
                state.Enabled = false;
                return;
            }

            if (SystemAPI.QueryBuilder().WithAny<Carrier, Asteroid>().Build().CalculateEntityCount() == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var starPositions = new NativeList<float3>(Allocator.Temp);
            foreach (var (transform, _) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Carrier>().WithEntityAccess())
            {
                if (starPositions.Length >= 3)
                {
                    break;
                }

                starPositions.Add(transform.ValueRO.Position);
            }

            if (starPositions.Length == 0)
            {
                foreach (var (transform, _) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Asteroid>().WithEntityAccess())
                {
                    if (starPositions.Length >= 3)
                    {
                        break;
                    }

                    starPositions.Add(transform.ValueRO.Position);
                }
            }

            var centroid = float3.zero;
            for (var i = 0; i < starPositions.Length; i++)
            {
                centroid += starPositions[i];
            }
            centroid = starPositions.Length > 0 ? centroid / starPositions.Length : float3.zero;

            if (starPositions.Length < 3)
            {
                starPositions.Add(centroid + new float3(0f, 0f, 0f));
            }
            if (starPositions.Length < 3)
            {
                starPositions.Add(centroid + new float3(220f, 0f, -140f));
            }
            if (starPositions.Length < 3)
            {
                starPositions.Add(centroid + new float3(-240f, 0f, 180f));
            }

            var galacticCenter = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(galacticCenter, new Space4XOrbitCenterTag());
            state.EntityManager.AddComponentData(galacticCenter, LocalTransform.FromPosition(float3.zero));

            var stars = new NativeList<Entity>(Allocator.Temp);
            for (var i = 0; i < starPositions.Length; i++)
            {
                var position = starPositions[i];
                var star = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(star, new Space4XOrbitStarTag());
                state.EntityManager.AddComponentData(star, LocalTransform.FromPosition(position));

                var radius = math.max(10f, math.length(new float2(position.x, position.z)));
                var starPeriod = 24000f + radius * 40f;
                var phase = math.atan2(position.z, position.x);

                state.EntityManager.AddComponentData(star, new Space4XOrbitAnchor
                {
                    ParentStar = galacticCenter,
                    Radius = radius,
                    AngularSpeed = math.PI * 2f / math.max(1f, starPeriod),
                    Phase = phase,
                    Height = position.y,
                    EpochTick = timeState.Tick
                });
                state.EntityManager.AddComponentData(star, new Space4XOrbitAnchorState
                {
                    LastPosition = position,
                    Initialized = 1
                });

                stars.Add(star);
            }

            AssignOrbits(ref state, timeState.Tick, stars.AsArray());

            starPositions.Dispose();
            stars.Dispose();

            _initialized = true;
            state.Enabled = false;
        }

        private void AssignOrbits(ref SystemState state, uint currentTick, NativeArray<Entity> stars)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAny<Asteroid, VesselMovement>()
                         .WithEntityAccess())
            {
                if (SystemAPI.HasComponent<Space4XRogueOrbitTag>(entity))
                {
                    continue;
                }

                var position = transform.ValueRO.Position;
                var star = ResolveNearestStar(position, stars, ref state);
                var starPosition = GetStarPosition(star, ref state);
                var offset = position - starPosition;
                var radius = math.max(4f, math.length(new float2(offset.x, offset.z)));
                var period = 1200f + radius * 6f;
                var phase = math.atan2(offset.z, offset.x);

                if (!state.EntityManager.HasComponent<Space4XOrbitAnchor>(entity))
                {
                    ecb.AddComponent(entity, new Space4XOrbitAnchor
                    {
                        ParentStar = star,
                        Radius = radius,
                        AngularSpeed = math.PI * 2f / math.max(1f, period),
                        Phase = phase,
                        Height = offset.y,
                        EpochTick = currentTick
                    });
                }

                if (!state.EntityManager.HasComponent<Space4XOrbitAnchorState>(entity))
                {
                    ecb.AddComponent(entity, new Space4XOrbitAnchorState
                    {
                        LastPosition = position,
                        Initialized = 1
                    });
                }

                if (state.EntityManager.HasComponent<Asteroid>(entity) &&
                    !state.EntityManager.HasComponent<Space4XAsteroidCenter>(entity))
                {
                    ecb.AddComponent(entity, new Space4XAsteroidCenter
                    {
                        Position = position
                    });
                }

                if (!state.EntityManager.HasComponent<SpaceVelocity>(entity))
                {
                    ecb.AddComponent(entity, new SpaceVelocity
                    {
                        Linear = float3.zero,
                        Angular = float3.zero
                    });
                }

                if (!state.EntityManager.HasComponent<Space4XMicroImpulseTag>(entity))
                {
                    ecb.AddComponent<Space4XMicroImpulseTag>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private Entity ResolveNearestStar(float3 position, NativeArray<Entity> stars, ref SystemState state)
        {
            var bestStar = stars.Length > 0 ? stars[0] : Entity.Null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < stars.Length; i++)
            {
                var star = stars[i];
                var starPosition = GetStarPosition(star, ref state);
                var distance = math.distancesq(new float2(position.x, position.z), new float2(starPosition.x, starPosition.z));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestStar = star;
                }
            }

            return bestStar;
        }

        private float3 GetStarPosition(Entity star, ref SystemState state)
        {
            if (star == Entity.Null)
            {
                return float3.zero;
            }

            if (state.EntityManager.HasComponent<LocalTransform>(star))
            {
                return state.EntityManager.GetComponentData<LocalTransform>(star).Position;
            }

            return float3.zero;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.GameplaySystemGroup))]
    [UpdateBefore(typeof(Space4X.Systems.AI.Space4XTransportAISystemGroup))]
    public partial struct Space4XOrbitDriftSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var currentTick = timeState.Tick;
            var deltaTime = timeState.FixedDeltaTime > 0f ? timeState.FixedDeltaTime : timeState.DeltaTime;

            foreach (var (anchor, anchorState, transform, entity) in SystemAPI
                         .Query<RefRO<Space4XOrbitAnchor>, RefRW<Space4XOrbitAnchorState>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
                var parentPosition = ResolveParentPosition(anchor.ValueRO.ParentStar, ref state);
                var elapsedTicks = math.max(0u, currentTick - anchor.ValueRO.EpochTick);
                var phase = anchor.ValueRO.Phase + anchor.ValueRO.AngularSpeed * elapsedTicks * deltaTime;
                var orbitPosition = parentPosition + new float3(
                    math.cos(phase) * anchor.ValueRO.Radius,
                    anchor.ValueRO.Height,
                    math.sin(phase) * anchor.ValueRO.Radius);

                if (anchorState.ValueRO.Initialized == 0)
                {
                    anchorState.ValueRW.LastPosition = orbitPosition;
                    anchorState.ValueRW.Initialized = 1;
                    continue;
                }

                var delta = orbitPosition - anchorState.ValueRO.LastPosition;
                if (math.lengthsq(delta) > 0f)
                {
                    var current = transform.ValueRO;
                    current.Position += delta;
                    transform.ValueRW = current;

                    if (SystemAPI.HasComponent<Space4XAsteroidCenter>(entity))
                    {
                        var center = SystemAPI.GetComponentRW<Space4XAsteroidCenter>(entity);
                        center.ValueRW.Position += delta;
                    }
                }

                anchorState.ValueRW.LastPosition = orbitPosition;
            }
        }

        private static float3 ResolveParentPosition(Entity parent, ref SystemState state)
        {
            if (parent == Entity.Null)
            {
                return float3.zero;
            }

            if (state.EntityManager.HasComponent<LocalTransform>(parent))
            {
                return state.EntityManager.GetComponentData<LocalTransform>(parent).Position;
            }

            return float3.zero;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XOrbitDriftSystem))]
    public partial struct Space4XMicroImpulseSystem : ISystem
    {
        private const float Damping = 0.92f;
        private const float MinSpeed = 0.0005f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var deltaTime = timeState.FixedDeltaTime > 0f ? timeState.FixedDeltaTime : timeState.DeltaTime;
            var damp = math.pow(Damping, deltaTime * 60f);

            foreach (var (velocity, transform, entity) in SystemAPI
                         .Query<RefRW<SpaceVelocity>, RefRW<LocalTransform>>()
                         .WithAll<Space4XMicroImpulseTag>()
                         .WithNone<VesselMovement>()
                         .WithEntityAccess())
            {
                var delta = velocity.ValueRO.Linear * deltaTime;
                if (math.lengthsq(delta) > 0f)
                {
                    var current = transform.ValueRO;
                    current.Position += delta;
                    transform.ValueRW = current;

                    if (SystemAPI.HasComponent<Space4XAsteroidCenter>(entity))
                    {
                        var center = SystemAPI.GetComponentRW<Space4XAsteroidCenter>(entity);
                        center.ValueRW.Position += delta;
                    }
                }

                var linear = velocity.ValueRO.Linear * damp;
                if (math.lengthsq(linear) < MinSpeed * MinSpeed)
                {
                    linear = float3.zero;
                }

                velocity.ValueRW = new SpaceVelocity
                {
                    Linear = linear,
                    Angular = velocity.ValueRO.Angular * damp
                };
            }
        }
    }
}
