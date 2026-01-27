using PureDOTS.Runtime.Logistics.Components;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics
{
    public static class RouteGraphService
    {
        public static RouteGraphConfig GetDefaultConfig()
        {
            return new RouteGraphConfig
            {
                Mode = RouteGraphMode.Direct,
                DirectCost = 1f
            };
        }

        public static bool TryGetRoute(
            in RouteGraphConfig config,
            Entity sourceNode,
            Entity destinationNode,
            out RouteGraphResult result)
        {
            result = default;

            if (sourceNode == Entity.Null || destinationNode == Entity.Null)
            {
                return false;
            }

            if (config.Mode == RouteGraphMode.None)
            {
                return false;
            }

            result.HasRoute = 1;
            result.Cost = config.DirectCost > 0f ? config.DirectCost : 1f;
            return true;
        }
    }
}
