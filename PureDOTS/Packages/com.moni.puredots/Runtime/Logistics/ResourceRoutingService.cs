using PureDOTS.Runtime.Logistics.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Logistics
{
    /// <summary>
    /// Static service class for resource routing operations.
    /// Provides methods for route calculation, cost computation, and rerouting.
    /// </summary>
    public static class ResourceRoutingService
    {
        /// <summary>
        /// Calculates a route between source and destination nodes.
        /// Simplified implementation - full pathfinding would integrate with spatial grid.
        /// </summary>
        public static Route CalculateRoute(
            float3 sourcePosition,
            float3 destinationPosition,
            RouteProfile profile,
            uint currentTick,
            int routeId,
            float transportSpeedMetersPerSecond = 10f,
            float baseCostPerMeter = 0.1f)
        {
            // Calculate direct distance
            float distance = math.distance(sourcePosition, destinationPosition);

            // Calculate transit time (distance / speed)
            float transitTime = distance / math.max(0.1f, transportSpeedMetersPerSecond);

            // Calculate base cost (distance * cost per meter)
            float baseCost = distance * baseCostPerMeter;

            // Apply risk cost based on tolerance (simplified: no edge data yet)
            float riskCost = 0f; // Would be calculated from edge risk data

            // Total cost
            float totalCost = baseCost + riskCost * (1f - profile.RiskTolerance);

            return new Route
            {
                RouteId = routeId,
                SourceNode = Entity.Null, // Set by caller
                DestinationNode = Entity.Null, // Set by caller
                Status = RouteStatus.Valid,
                TotalDistance = distance,
                TotalCost = totalCost,
                EstimatedTransitTime = transitTime,
                CalculatedTick = currentTick,
                CacheVersion = 1,
                CacheKey = new RouteCacheKey
                {
                    SourceNode = Entity.Null, // Set by caller
                    DestinationNode = Entity.Null, // Set by caller
                    BehaviorProfileId = 0,
                    LegalityMask = profile.LegalityFlags,
                    KnowledgeVersionId = 0,
                    TopologyVersionId = 0
                }
            };
        }

        /// <summary>
        /// Gets total route cost.
        /// </summary>
        public static float GetRouteCost(Route route)
        {
            return route.TotalCost;
        }

        /// <summary>
        /// Reroutes a shipment due to route disruption.
        /// </summary>
        public static Route RerouteShipment(
            Route currentRoute,
            RouteRerouteReason reason,
            RouteProfile profile,
            uint currentTick)
        {
            // Simplified: returns same route marked as expired
            // Full implementation would find alternate route
            var rerouted = currentRoute;
            rerouted.Status = RouteStatus.Expired;
            rerouted.CalculatedTick = currentTick;
            rerouted.CacheVersion++;
            return rerouted;
        }

        /// <summary>
        /// Updates route cache (invalidates expired routes).
        /// </summary>
        public static void UpdateRouteCache(ref Route route, uint currentTick, uint cacheTTL)
        {
            if (route.CalculatedTick + cacheTTL < currentTick)
            {
                route.Status = RouteStatus.Expired;
            }
        }

        /// <summary>
        /// Gets estimated time to arrival for a route.
        /// </summary>
        public static float GetRouteETA(Route route, uint currentTick)
        {
            if (route.Status != RouteStatus.Valid)
            {
                return float.MaxValue;
            }

            if (route.EstimatedTransitTime > 0f)
            {
                return route.EstimatedTransitTime;
            }

            // Fallback: estimate from distance (would need speed parameter)
            return route.TotalDistance * 0.1f; // Placeholder
        }

        /// <summary>
        /// Finds alternate route with constraints.
        /// </summary>
        public static Route FindAlternateRoute(
            Entity sourceNode,
            Entity destinationNode,
            RouteConstraints constraints,
            RouteProfile profile,
            uint currentTick,
            int routeId)
        {
            // Simplified: same as CalculateRoute
            // Full implementation would try multiple paths and select best match
            return CalculateRoute(float3.zero, float3.zero, profile, currentTick, routeId);
        }

        /// <summary>
        /// Calculates route cost terms from edge states.
        /// </summary>
        public static float CalculateRouteCost(
            NativeList<RouteEdge> edges,
            RouteProfile profile)
        {
            float totalCost = 0f;

            for (int i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                float edgeCost = edge.BaseCost;

                // Apply risk cost based on tolerance
                if (edge.RiskCost > 0f && profile.RiskTolerance < 1f)
                {
                    edgeCost += edge.RiskCost * (1f - profile.RiskTolerance);
                }

                // Apply congestion cost
                edgeCost += edge.CongestionCost;

                totalCost += edgeCost;
            }

            return totalCost;
        }
    }
}

