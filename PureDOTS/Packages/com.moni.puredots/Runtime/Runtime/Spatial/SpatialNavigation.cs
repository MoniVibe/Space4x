using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Spatial
{
    public struct VelocitySample : IComponentData
    {
        public float3 Velocity;
        public float3 LastPosition;
    }

    public static class NavHelpers
    {
        public static float ComputeTravelTime(float3 from, float3 to, float speed)
        {
            if (speed <= 0f)
            {
                return 1f;
            }

            var distance = math.length(to - from);
            return math.max(0.1f, distance / speed);
        }
    }
}
