using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Processes PathRequest components by priority and respects performance budgets.
    /// Queues excess requests for future ticks when budget is exceeded.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    // Removed invalid UpdateAfter: NavPerformanceBudgetSystem runs in Spatial group (OrderFirst).
    [UpdateBefore(typeof(PathfindingSystem))]
    public partial struct PathRequestSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<NavPerformanceBudget>();
            state.RequireForUpdate<NavPerformanceCounters>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var budget = SystemAPI.GetSingleton<NavPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<NavPerformanceCounters>();

            // Get or create queue entity
            Entity queueEntity;
            DynamicBuffer<NavRequestQueue> queueBuffer;
            if (!SystemAPI.TryGetSingletonEntity<NavRequestQueue>(out queueEntity))
            {
                queueEntity = state.EntityManager.CreateEntity();
                queueBuffer = state.EntityManager.AddBuffer<NavRequestQueue>(queueEntity);
            }
            else
            {
                queueBuffer = SystemAPI.GetBuffer<NavRequestQueue>(queueEntity);
            }

            // Collect all active requests and sort by priority
            using var requests = new NativeList<(Entity entity, PathRequest request)>(Allocator.Temp);
            foreach (var (request, entity) in
                SystemAPI.Query<RefRO<PathRequest>>()
                .WithEntityAccess())
            {
                if (request.ValueRO.IsActive != 0)
                {
                    requests.Add((entity, request.ValueRO));
                }
            }

            // Sort by priority (0=Critical first, 3=Low last)
            requests.Sort(new RequestPriorityComparer());

            // Process requests by priority, respecting budgets
            foreach (var (entity, request) in requests)
            {
                // Check budget based on heat tier
                bool canProcess = false;
                if (request.HeatTier == NavHeatTier.Warm)
                {
                    if (counters.ValueRO.LocalPathQueriesThisTick < budget.MaxLocalPathQueriesPerTick)
                    {
                        canProcess = true;
                        counters.ValueRW.LocalPathQueriesThisTick++;
                    }
                }
                else if (request.HeatTier == NavHeatTier.Cold)
                {
                    if (counters.ValueRO.StrategicRouteQueriesThisTick < budget.MaxStrategicRoutePlansPerTick)
                    {
                        canProcess = true;
                        counters.ValueRW.StrategicRouteQueriesThisTick++;
                    }
                }
                else // Hot path - should not go through this system
                {
                    // Hot path requests are handled directly in movement systems
                    continue;
                }

                if (canProcess)
                {
                    // Ensure entity has PathState and PathResult
                    if (!SystemAPI.HasComponent<PathState>(entity))
                    {
                        state.EntityManager.AddComponent<PathState>(entity);
                    }

                    if (!SystemAPI.HasBuffer<PathResult>(entity))
                    {
                        state.EntityManager.AddBuffer<PathResult>(entity);
                    }

                    // PathfindingSystem or MultiModalRoutePlannerSystem will process the request
                }
                else
                {
                    // Budget exceeded - queue request for next tick
                    // Drop low priority requests if queue is too large
                    if (request.Priority == NavRequestPriority.Low && queueBuffer.Length > budget.QueueSizeWarningThreshold)
                    {
                        counters.ValueRW.RequestsDroppedThisTick++;
                        continue;
                    }

                    queueBuffer.Add(new NavRequestQueue
                    {
                        RequestingEntity = entity,
                        StartPosition = request.StartPosition,
                        GoalPosition = request.GoalPosition,
                        Priority = (byte)request.Priority,
                        HeatTier = request.HeatTier,
                        EnqueueTick = timeState.Tick,
                        LocomotionMode = request.LocomotionMode
                    });
                }
            }

            // Process queued requests (up to budget)
            for (int i = queueBuffer.Length - 1; i >= 0; i--)
            {
                var queuedRequest = queueBuffer[i];
                bool canProcessQueued = false;

                if (queuedRequest.HeatTier == NavHeatTier.Warm)
                {
                    if (counters.ValueRO.LocalPathQueriesThisTick < budget.MaxLocalPathQueriesPerTick)
                    {
                        canProcessQueued = true;
                        counters.ValueRW.LocalPathQueriesThisTick++;
                    }
                }
                else if (queuedRequest.HeatTier == NavHeatTier.Cold)
                {
                    if (counters.ValueRO.StrategicRouteQueriesThisTick < budget.MaxStrategicRoutePlansPerTick)
                    {
                        canProcessQueued = true;
                        counters.ValueRW.StrategicRouteQueriesThisTick++;
                    }
                }

                if (canProcessQueued)
                {
                    // Restore PathRequest from queue
                    var requestEntity = queuedRequest.RequestingEntity;
                    if (state.EntityManager.Exists(requestEntity))
                    {
                        if (!SystemAPI.HasComponent<PathRequest>(requestEntity))
                        {
                            state.EntityManager.AddComponentData(requestEntity, new PathRequest
                            {
                                RequestingEntity = requestEntity,
                                StartPosition = queuedRequest.StartPosition,
                                GoalPosition = queuedRequest.GoalPosition,
                                LocomotionMode = queuedRequest.LocomotionMode,
                                Priority = (NavRequestPriority)queuedRequest.Priority,
                                HeatTier = queuedRequest.HeatTier,
                                RequestTick = timeState.Tick,
                                IsActive = 1
                            });
                        }

                        if (!SystemAPI.HasComponent<PathState>(requestEntity))
                        {
                            state.EntityManager.AddComponent<PathState>(requestEntity);
                        }

                        if (!SystemAPI.HasBuffer<PathResult>(requestEntity))
                        {
                            state.EntityManager.AddBuffer<PathResult>(requestEntity);
                        }
                    }

                    queueBuffer.RemoveAtSwapBack(i);
                }
            }
        }

        private struct RequestPriorityComparer : System.Collections.Generic.IComparer<(Entity entity, PathRequest request)>
        {
            public int Compare((Entity entity, PathRequest request) x, (Entity entity, PathRequest request) y)
            {
                // Compare enum values directly to avoid boxing
                int xPriority = (int)x.request.Priority;
                int yPriority = (int)y.request.Priority;
                if (xPriority < yPriority) return -1;
                if (xPriority > yPriority) return 1;
                return 0;
            }
        }
    }
}

