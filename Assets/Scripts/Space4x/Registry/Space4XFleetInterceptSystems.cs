using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct Space4XFleetInterceptBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XFleetInterceptQueue>(out _))
            {
                state.Enabled = false;
                return;
            }

            var entity = state.EntityManager.CreateEntity(
                typeof(Space4XFleetInterceptQueue),
                typeof(Space4XFleetInterceptTelemetry));

            state.EntityManager.AddBuffer<InterceptRequest>(entity);
            state.EntityManager.AddBuffer<FleetInterceptCommandLogEntry>(entity);
            state.EntityManager.SetComponentData(entity, new Space4XFleetInterceptTelemetry
            {
                LastAttemptTick = 0,
                InterceptAttempts = 0,
                RendezvousAttempts = 0
            });

            state.Enabled = false;
        }
    }

    /// <summary>
    /// Pushes fleet position and velocity into the broadcast component for spatial queries.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GameplayFixedStepSyncSystem))]
    public partial struct FleetBroadcastSystem : ISystem
    {
        private ComponentLookup<FleetKinematics> _kinematicsLookup;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;

        public void OnCreate(ref SystemState state)
        {
            _kinematicsLookup = state.GetComponentLookup<FleetKinematics>(true);
            _residencyLookup = state.GetComponentLookup<SpatialGridResidency>(false);
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode == RewindMode.Playback)
            {
                return;
            }

            _kinematicsLookup.Update(ref state);
            _residencyLookup.Update(ref state);
            var tick = time.Tick;

            foreach (var (broadcast, transform, entity) in SystemAPI.Query<RefRW<FleetMovementBroadcast>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                var velocity = broadcast.ValueRO.Velocity;
                if (_kinematicsLookup.HasComponent(entity))
                {
                    velocity = _kinematicsLookup[entity].Velocity;
                }

                broadcast.ValueRW.Position = transform.ValueRO.Position;
                broadcast.ValueRW.Velocity = velocity;
                broadcast.ValueRW.LastUpdateTick = tick;

                if (_residencyLookup.HasComponent(entity))
                {
                    var residency = _residencyLookup.GetRefRW(entity);
                    residency.ValueRW.LastPosition = transform.ValueRO.Position;
                }
            }
        }
    }

    /// <summary>
    /// Generates intercept requests by selecting nearby fleets for haulers/support vessels.
    /// Uses nearest neighbor selection with deterministic tie-breaking and routes through the shared queue.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FleetBroadcastSystem))]
    public partial struct FleetInterceptRequestSystem : ISystem
    {
        private ComponentLookup<FleetMovementBroadcast> _broadcastLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        private const float DefaultDetectionRadius = 250f;
        private const float DefaultDetectionRadiusSq = DefaultDetectionRadius * DefaultDetectionRadius;

        public void OnCreate(ref SystemState state)
        {
            _broadcastLookup = state.GetComponentLookup<FleetMovementBroadcast>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);

            state.RequireForUpdate<Space4XFleetInterceptQueue>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode == RewindMode.Playback)
            {
                return;
            }

            _broadcastLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _statsLookup.Update(ref state);

            var queueEntity = SystemAPI.GetSingletonEntity<Space4XFleetInterceptQueue>();
            var requests = state.EntityManager.GetBuffer<InterceptRequest>(queueEntity);

            foreach (var (capability, requesterEntity) in SystemAPI.Query<RefRO<InterceptCapability>>().WithEntityAccess())
            {
                if (!_transformLookup.HasComponent(requesterEntity))
                {
                    continue;
                }

                var requesterPos = _transformLookup[requesterEntity].Position;
                var bestTarget = Entity.Null;
                var bestDistSq = float.MaxValue;

                foreach (var (broadcast, targetEntity) in SystemAPI.Query<RefRO<FleetMovementBroadcast>>().WithEntityAccess())
                {
                    if (targetEntity == requesterEntity)
                    {
                        continue;
                    }

                    if (!_transformLookup.HasComponent(targetEntity))
                    {
                        continue;
                    }

                    var targetPos = _transformLookup[targetEntity].Position;
                    var distanceSq = math.lengthsq(targetPos - requesterPos);
                    if (distanceSq > DefaultDetectionRadiusSq)
                    {
                        continue;
                    }

                    if (distanceSq < bestDistSq || (math.abs(distanceSq - bestDistSq) < 1e-4f && targetEntity.Index < bestTarget.Index))
                    {
                        bestDistSq = distanceSq;
                        bestTarget = targetEntity;
                    }
                }

                if (bestTarget == Entity.Null)
                {
                    continue;
                }

                // Diplomacy stat influences interception decisions
                // Higher diplomacy = more likely to attempt diplomatic rendezvous vs aggressive intercept
                byte requireRendezvous = capability.ValueRO.AllowIntercept == 0 ? (byte)1 : (byte)0;
                if (_statsLookup.HasComponent(requesterEntity))
                {
                    var stats = _statsLookup[requesterEntity];
                    var diplomacyModifier = stats.Diplomacy / 100f; // 0-1 normalized
                    // High diplomacy increases chance of peaceful rendezvous
                    if (diplomacyModifier > 0.6f && capability.ValueRO.AllowIntercept != 0)
                    {
                        requireRendezvous = 1; // Prefer diplomatic approach
                    }
                }

                requests.Add(new InterceptRequest
                {
                    Requester = requesterEntity,
                    Target = bestTarget,
                    Priority = capability.ValueRO.TechTier,
                    RequestTick = time.Tick,
                    RequireRendezvous = requireRendezvous
                });
            }
        }
    }

    /// <summary>
    /// Computes intercept courses for haulers, falling back to rendezvous when tech/flags disallow interception.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FleetInterceptRequestSystem))]
    public partial struct InterceptPathfindingSystem : ISystem
    {
        private ComponentLookup<InterceptCapability> _capabilityLookup;
        private ComponentLookup<FleetMovementBroadcast> _broadcastLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        public void OnCreate(ref SystemState state)
        {
            _capabilityLookup = state.GetComponentLookup<InterceptCapability>(true);
            _broadcastLookup = state.GetComponentLookup<FleetMovementBroadcast>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);

            state.RequireForUpdate<Space4XFleetInterceptQueue>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode == RewindMode.Playback)
            {
                return;
            }

            var queueEntity = SystemAPI.GetSingletonEntity<Space4XFleetInterceptQueue>();
            var requests = state.EntityManager.GetBuffer<InterceptRequest>(queueEntity);
            if (requests.Length == 0)
            {
                return;
            }

            var commandLog = state.EntityManager.GetBuffer<FleetInterceptCommandLogEntry>(queueEntity);
            var telemetry = state.EntityManager.GetComponentData<Space4XFleetInterceptTelemetry>(queueEntity);

            _capabilityLookup.Update(ref state);
            _broadcastLookup.Update(ref state);
            _transformLookup.Update(ref state);

            SortRequests(ref requests);

            for (var i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                if (!_capabilityLookup.HasComponent(request.Requester) || !_transformLookup.HasComponent(request.Requester))
                {
                    continue;
                }

                if (!_broadcastLookup.HasComponent(request.Target))
                {
                    continue;
                }

                var capability = _capabilityLookup[request.Requester];
                var targetBroadcast = _broadcastLookup[request.Target];
                var requesterPos = _transformLookup[request.Requester].Position;
                var mode = InterceptMode.Rendezvous;
                var interceptPoint = targetBroadcast.Position;
                var estimatedTick = time.Tick;

                var allowIntercept = request.RequireRendezvous == 0 &&
                                     capability.AllowIntercept != 0 &&
                                     targetBroadcast.AllowsInterception != 0 &&
                                     capability.TechTier >= targetBroadcast.TechTier;

                if (allowIntercept && TryComputeIntercept(requesterPos, capability.MaxSpeed, targetBroadcast.Position, targetBroadcast.Velocity, time.FixedDeltaTime, out var intercept, out var offsetTicks))
                {
                    mode = InterceptMode.Intercept;
                    interceptPoint = intercept;
                    estimatedTick = time.Tick + offsetTicks;
                }

                ApplyCourse(ref state, request.Requester, request.Target, interceptPoint, estimatedTick, mode);

                telemetry.LastAttemptTick = time.Tick;
                if (mode == InterceptMode.Intercept)
                {
                    telemetry.InterceptAttempts += 1;
                }
                else
                {
                    telemetry.RendezvousAttempts += 1;
                }

                commandLog.Add(new FleetInterceptCommandLogEntry
                {
                    Tick = time.Tick,
                    Requester = request.Requester,
                    Target = request.Target,
                    InterceptPoint = interceptPoint,
                    EstimatedInterceptTick = estimatedTick,
                    Mode = mode
                });
            }

            requests.Clear();
            state.EntityManager.SetComponentData(queueEntity, telemetry);
        }

        private static void SortRequests(ref DynamicBuffer<InterceptRequest> requests)
        {
            for (var i = 0; i < requests.Length - 1; i++)
            {
                var min = i;
                for (var j = i + 1; j < requests.Length; j++)
                {
                    if (Compare(requests[j], requests[min]) < 0)
                    {
                        min = j;
                    }
                }

                if (min != i)
                {
                    (requests[i], requests[min]) = (requests[min], requests[i]);
                }
            }
        }

        private static int Compare(in InterceptRequest a, in InterceptRequest b)
        {
            var priority = a.Priority.CompareTo(b.Priority);
            if (priority != 0)
            {
                return priority;
            }

            var tick = a.RequestTick.CompareTo(b.RequestTick);
            if (tick != 0)
            {
                return tick;
            }

            return a.Requester.Index.CompareTo(b.Requester.Index);
        }

        private static bool TryComputeIntercept(in float3 chaserPos, float chaserSpeed, in float3 targetPos, in float3 targetVelocity, float fixedDeltaTime, out float3 interceptPoint, out uint offsetTicks)
        {
            interceptPoint = targetPos;
            offsetTicks = 0;

            if (chaserSpeed <= 0.001f)
            {
                return false;
            }

            var relPos = targetPos - chaserPos;
            var relVel = targetVelocity;
            var a = math.lengthsq(relVel) - chaserSpeed * chaserSpeed;
            var b = 2f * math.dot(relPos, relVel);
            var c = math.lengthsq(relPos);
            float time;

            if (math.abs(a) < 1e-6f)
            {
                var distance = math.sqrt(c);
                time = distance / math.max(0.001f, chaserSpeed);
            }
            else
            {
                var discriminant = b * b - 4f * a * c;
                if (discriminant < 0f)
                {
                    return false;
                }

                var sqrtDisc = math.sqrt(discriminant);
                var t1 = (-b + sqrtDisc) / (2f * a);
                var t2 = (-b - sqrtDisc) / (2f * a);
                time = float.MaxValue;

                if (t1 > 0f)
                {
                    time = math.min(time, t1);
                }

                if (t2 > 0f)
                {
                    time = math.min(time, t2);
                }

                if (time == float.MaxValue)
                {
                    return false;
                }
            }

            interceptPoint = targetPos + targetVelocity * time;
            var tickOffset = math.ceil(time / math.max(0.0001f, fixedDeltaTime));
            offsetTicks = (uint)math.max(1, (int)tickOffset);
            return true;
        }

        private static void ApplyCourse(ref SystemState state, Entity requester, Entity target, in float3 interceptPoint, uint estimatedTick, InterceptMode mode)
        {
            var course = new InterceptCourse
            {
                TargetFleet = target,
                InterceptPoint = interceptPoint,
                EstimatedInterceptTick = estimatedTick,
                UsesInterception = mode == InterceptMode.Intercept ? (byte)1 : (byte)0
            };

            if (state.EntityManager.HasComponent<InterceptCourse>(requester))
            {
                state.EntityManager.SetComponentData(requester, course);
            }
            else
            {
                state.EntityManager.AddComponentData(requester, course);
            }
        }
    }

    /// <summary>
    /// Maintains rendezvous courses by retargeting to the latest broadcast position.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(InterceptPathfindingSystem))]
    public partial struct RendezvousCoordinationSystem : ISystem
    {
        private ComponentLookup<FleetMovementBroadcast> _broadcastLookup;

        public void OnCreate(ref SystemState state)
        {
            _broadcastLookup = state.GetComponentLookup<FleetMovementBroadcast>(true);
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode == RewindMode.Playback)
            {
                return;
            }

            _broadcastLookup.Update(ref state);

            foreach (var course in SystemAPI.Query<RefRW<InterceptCourse>>())
            {
                if (course.ValueRO.UsesInterception != 0 || course.ValueRO.TargetFleet == Entity.Null)
                {
                    continue;
                }

                if (!_broadcastLookup.HasComponent(course.ValueRO.TargetFleet))
                {
                    continue;
                }

                var broadcast = _broadcastLookup[course.ValueRO.TargetFleet];
                course.ValueRW.InterceptPoint = broadcast.Position;
                course.ValueRW.EstimatedInterceptTick = time.Tick;
            }
        }
    }

    /// <summary>
    /// Publishes basic intercept telemetry for debug HUD bindings.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XCrewSkillTelemetrySystem))]
    public partial struct Space4XFleetInterceptTelemetrySystem : ISystem
    {
        private EntityQuery _telemetryQuery;
        private EntityQuery _queueQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<Space4XFleetInterceptQueue>();

            _telemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();

            _queueQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XFleetInterceptQueue>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var queueEntity = _queueQuery.GetSingletonEntity();
            var metrics = state.EntityManager.GetComponentData<Space4XFleetInterceptTelemetry>(queueEntity);

            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            buffer.AddMetric("space4x.intercept.attempts", metrics.InterceptAttempts);
            buffer.AddMetric("space4x.intercept.rendezvous", metrics.RendezvousAttempts);
            buffer.AddMetric("space4x.intercept.lastTick", metrics.LastAttemptTick);
        }
    }
}
