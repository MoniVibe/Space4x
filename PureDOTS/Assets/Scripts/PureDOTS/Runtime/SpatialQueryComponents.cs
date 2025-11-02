using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Settings for proximity queries against resource sources. Allows swapping implementations without changing consumers.
    /// </summary>
    public struct ResourceProximitySettings : IComponentData
    {
        public float SearchRadius;
        public byte MaxResults; // reserved for future multi-result queries

        public static ResourceProximitySettings CreateDefault()
        {
            return new ResourceProximitySettings
            {
                SearchRadius = 50f,
                MaxResults = 1
            };
        }
    }

    /// <summary>
    /// Result of a proximity query against resource sources for a given consumer entity.
    /// </summary>
    public struct ResourceProximityResult : IComponentData
    {
        public Entity NearestResource;
        public float3 NearestPosition;
        public float NearestDistanceSq;
    }
}




