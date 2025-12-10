using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that visualizes logistics routes as lines between colonies/stations.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XFactionOverlaySystem))]
    public partial struct Space4XLogisticsOverlaySystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Space4XColony> _colonyLookup;
        private int _routeCount;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XLogisticsRoute>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _colonyLookup = state.GetComponentLookup<Space4XColony>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            // Only update if overlay is enabled
            if (!SystemAPI.HasSingleton<DebugOverlayConfig>())
            {
                return;
            }

            var overlayConfig = SystemAPI.GetSingleton<DebugOverlayConfig>();
            if (!overlayConfig.ShowLogisticsOverlay)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _colonyLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Limit to max 20 routes for performance
            const int maxRoutes = 20;
            int routeIndex = 0;

            // Create or update route overlay entities
            foreach (var (route, entity) in SystemAPI.Query<RefRO<Space4XLogisticsRoute>>().WithEntityAccess())
            {
                if (routeIndex >= maxRoutes)
                {
                    break;
                }

                // Find origin and destination positions
                float3 originPos = float3.zero;
                float3 destPos = float3.zero;
                bool foundOrigin = false;
                bool foundDest = false;

                // Search for colonies with matching IDs
                foreach (var (colony, transform, colonyEntity) in SystemAPI
                             .Query<RefRO<Space4XColony>, RefRO<LocalTransform>>()
                             .WithEntityAccess())
                {
                    if (colony.ValueRO.ColonyId.Equals(route.ValueRO.OriginColonyId))
                    {
                        originPos = transform.ValueRO.Position;
                        foundOrigin = true;
                    }
                    if (colony.ValueRO.ColonyId.Equals(route.ValueRO.DestinationColonyId))
                    {
                        destPos = transform.ValueRO.Position;
                        foundDest = true;
                    }
                }

                if (foundOrigin && foundDest)
                {
                    // Create or update route overlay entity
                    Entity overlayEntity;
                    if (!SystemAPI.HasComponent<RouteOverlayTag>(entity))
                    {
                        overlayEntity = state.EntityManager.CreateEntity();
                        state.EntityManager.AddComponent<RouteOverlayTag>(overlayEntity);
                        state.EntityManager.AddComponent<LogisticsRouteOverlay>(overlayEntity);
                    }
                    else
                    {
                        overlayEntity = entity;
                    }

                    // Determine route color based on status
                    float4 routeColor = route.ValueRO.Status switch
                    {
                        Space4XLogisticsRouteStatus.Operational => new float4(0.2f, 1f, 0.2f, 1f),  // Green
                        Space4XLogisticsRouteStatus.Disrupted => new float4(1f, 0.2f, 0.2f, 1f),   // Red
                        Space4XLogisticsRouteStatus.Overloaded => new float4(1f, 1f, 0.2f, 1f),    // Yellow
                        _ => new float4(0.5f, 0.5f, 0.5f, 1f)                                      // Gray
                    };

                    // Line width based on throughput
                    float lineWidth = math.clamp(route.ValueRO.DailyThroughput / 100f, 0.1f, 2f);

                    state.EntityManager.SetComponentData(overlayEntity, new LogisticsRouteOverlay
                    {
                        RouteId = route.ValueRO.RouteId,
                        OriginPosition = originPos,
                        DestinationPosition = destPos,
                        RouteStatus = route.ValueRO.Status,
                        Throughput = route.ValueRO.DailyThroughput,
                        LineWidth = lineWidth,
                        LineColor = routeColor
                    });
                }

                routeIndex++;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

